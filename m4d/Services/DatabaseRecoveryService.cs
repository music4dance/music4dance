#nullable enable

using System.Diagnostics;
using m4d.Services.ServiceHealth;

using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace m4d.Services;

/// <summary>
/// Handles reconnection attempts when the database becomes unavailable at startup.
///
/// Typical scenario: the Azure test instance (both app and DB are on-demand) receives a
/// request while the SQL server is still waking up. The startup health check fails and
/// marks the database unavailable. This service retries the connection on each subsequent
/// user request, throttled to at most once per <c>ServiceHealth:DatabaseRetryInterval</c>
/// (default 1 minute).
///
/// The reconnection attempt is fire-and-forget so it never delays the user's request.
/// On success it marks the database healthy and re-runs the deferred FixupStats
/// initialisation that was skipped during startup.
/// </summary>
public class DatabaseRecoveryService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ServiceHealthManager _serviceHealth;
    private readonly ILogger<DatabaseRecoveryService> _logger;
    private readonly IConfiguration _configuration;
    private readonly TimeSpan _retryInterval;

    // Semaphore ensures at most one recovery attempt runs at a time.
    private readonly SemaphoreSlim _retryLock = new(1, 1);

    // Best-effort throttle: set before the attempt starts so concurrent callers
    // see the updated time immediately; the semaphore protects actual execution.
    private volatile int _lastRetryTicks = 0; // Interlocked-safe DateTime.UtcNow.Ticks truncated to int32 range

    // Store full ticks as long for accuracy
    private long _lastRetryUtcTicks = 0;

    public DatabaseRecoveryService(
        IServiceProvider serviceProvider,
        ServiceHealthManager serviceHealth,
        IConfiguration configuration,
        ILogger<DatabaseRecoveryService> logger)
    {
        _serviceProvider = serviceProvider;
        _serviceHealth = serviceHealth;
        _logger = logger;
        _configuration = configuration;

        var intervalStr = configuration["ServiceHealth:DatabaseRetryInterval"] ?? "00:01:00";
        _retryInterval = TimeSpan.TryParse(intervalStr, out var parsed) ? parsed : TimeSpan.FromMinutes(1);

        _logger.LogInformation("DatabaseRecoveryService initialised (retry interval: {Interval})", _retryInterval);
    }

    /// <summary>
    /// Call on every user request (cheap when the database is healthy).
    /// If the database is unavailable and the retry throttle has elapsed, a
    /// background reconnection attempt is started without blocking the caller.
    /// </summary>
    public void TriggerRecoveryIfNeeded()
    {
        // Fast-path: nothing to do when database is healthy or unknown
        var status = _serviceHealth.GetServiceStatus("Database");
        if (status.Status != ServiceStatus.Unavailable)
            return;

        // No connection string configured → recovery is impossible (re-read each time so
        // appsettings changes mid-run are picked up, useful for local testing)
        var connectionString = _configuration["AZURE_SQL_CONNECTIONSTRING"]
            ?? _configuration.GetConnectionString("DanceMusicContextConnection");
        if (string.IsNullOrEmpty(connectionString))
            return;

        // Throttle check (non-atomic read is intentional – best-effort only)
        var elapsed = TimeSpan.FromTicks(DateTime.UtcNow.Ticks - Interlocked.Read(ref _lastRetryUtcTicks));
        if (elapsed < _retryInterval)
            return;

        // Fire-and-forget: never blocks the user request
        _ = Task.Run(async () =>
        {
            try
            {
                await AttemptRecoveryAsync(connectionString);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in database recovery background task");
            }
        });
    }

    // ---------------------------------------------------------------------------
    // Private helpers
    // ---------------------------------------------------------------------------

    private async Task AttemptRecoveryAsync(string connectionString)
    {
        // Allow only one concurrent attempt; others return immediately
        if (!await _retryLock.WaitAsync(0))
        {
            _logger.LogDebug("Database recovery skipped – another attempt is already in progress");
            return;
        }

        try
        {
            // Update the throttle timestamp now (inside the lock) so that the
            // next TriggerRecoveryIfNeeded call sees an up-to-date value whether
            // or not this attempt succeeds.
            Interlocked.Exchange(ref _lastRetryUtcTicks, DateTime.UtcNow.Ticks);

            _logger.LogInformation("Database recovery: attempting reconnection...");

            // --- Connectivity test -------------------------------------------------
            // Build a fresh connection string with an extended Connect Timeout so that
            // Azure SQL on-demand gets enough time to wake up (typically 20-45 s).
            string testConnStr;
            try
            {
                var csb = new SqlConnectionStringBuilder(connectionString)
                {
                    ConnectTimeout = 60   // seconds; overrides whatever is in the stored string
                };
                testConnStr = csb.ConnectionString;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Database recovery: could not parse connection string – {Error}", ex.Message);
                return;
            }

            var testOptions = new DbContextOptionsBuilder<DanceMusicContext>()
                .UseSqlServer(testConnStr, o =>
                {
                    o.CommandTimeout(60);
                    // No EnableRetryOnFailure here – we want a single fast-fail probe
                })
                .Options;

            bool canConnect;
            var sw = Stopwatch.StartNew();
            try
            {
                using var testContext = new DanceMusicContext(testOptions);
                canConnect = await testContext.Database.CanConnectAsync();
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogInformation(
                    "Database recovery: probe failed after {Elapsed}ms – {Error}",
                    sw.ElapsedMilliseconds, ex.Message);
                return;
            }

            sw.Stop();

            if (!canConnect)
            {
                _logger.LogInformation(
                    "Database recovery: probe returned false after {Elapsed}ms", sw.ElapsedMilliseconds);
                return;
            }

            _logger.LogInformation(
                "Database recovery: connection restored in {Elapsed}ms – running post-recovery initialisation",
                sw.ElapsedMilliseconds);

            // Mark healthy immediately so controllers start serving real content.
            // FixupStats below will re-confirm (and re-mark unavailable on failure).
            _serviceHealth.MarkHealthy("Database", sw.Elapsed);

            // --- Post-recovery FixupStats ------------------------------------------
            // Re-run the DB-dependent portion of DanceStatsInstance.FixupStats that
            // was skipped during startup when the DB was unavailable.
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var sp = scope.ServiceProvider;

                var context = sp.GetRequiredService<DanceMusicContext>();
                var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
                var searchService = sp.GetRequiredService<ISearchServiceManager>();
                var statsManager = sp.GetRequiredService<IDanceStatsManager>();

                if (statsManager.Instance != null)
                {
                    var dms = new DanceMusicService(context, userManager, searchService, statsManager);
                    await statsManager.Instance.FixupStats(dms, _serviceHealth);
                    _logger.LogInformation("Database recovery: post-recovery FixupStats completed");
                }
                else
                {
                    _logger.LogWarning(
                        "Database recovery: DanceStatsManager.Instance is null – skipping FixupStats; " +
                        "stats will be initialised when the next admin cache refresh runs");
                }
            }
            catch (Exception ex)
            {
                // A FixupStats failure does not undo the healthy mark – the DB itself
                // is connectable; individual operations may still succeed.
                _logger.LogWarning(ex,
                    "Database recovery: post-recovery FixupStats failed (database still healthy): {Error}",
                    ex.Message);
            }
        }
        finally
        {
            _retryLock.Release();
        }
    }
}
