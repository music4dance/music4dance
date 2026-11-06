#nullable enable

using System.Diagnostics;
using m4d.Services.ServiceHealth;

using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

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
/// On success it runs any migrations that startup deferred (see <see cref="DeferMigrations"/>),
/// marks the database healthy and re-runs the deferred FixupStats initialisation that was
/// skipped during startup.
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

    // Throttle: last attempt start time in UTC ticks (updated before Task.Run so
    // concurrent callers see the updated value immediately and don't queue extra tasks).
    private long _lastRetryUtcTicks = 0;

    // Set by startup when it couldn't run the migrations (the database was unavailable, or the
    // migration itself failed). Without it a reconnect would mark the database healthy on the old
    // schema, and every page that touches a new table would throw until the next restart.
    private volatile bool _migrationsDeferred;

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

    /// <summary>True while startup's migrations are still waiting to run.</summary>
    public bool MigrationsDeferred => _migrationsDeferred;

    /// <summary>
    /// Records that startup didn't run the migrations, so the next successful reconnection runs
    /// them before marking the database healthy. Startup doesn't call this when it skips
    /// migrations on purpose (PROD_DB or TEST_DB), so recovery never migrates those databases.
    /// </summary>
    public void DeferMigrations() => _migrationsDeferred = true;

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

        // Throttle check – update the timestamp here (before Task.Run) so that
        // concurrent requests racing through TriggerRecoveryIfNeeded all see the
        // updated value and don't each queue a separate Task.Run work item.
        var now = DateTime.UtcNow.Ticks;
        var elapsed = TimeSpan.FromTicks(now - Interlocked.Read(ref _lastRetryUtcTicks));
        if (elapsed < _retryInterval)
            return;
        Interlocked.Exchange(ref _lastRetryUtcTicks, now);

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

    internal async Task AttemptRecoveryAsync(string connectionString)
    {
        // Allow only one concurrent attempt; others return immediately
        if (!await _retryLock.WaitAsync(0))
        {
            _logger.LogDebug("Database recovery skipped – another attempt is already in progress");
            return;
        }

        try
        {
            // Refresh the throttle timestamp inside the lock so the interval
            // is measured from when the attempt actually ran, not when it was queued.
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

            bool canConnect;
            var sw = Stopwatch.StartNew();
            try
            {
                canConnect = await ProbeAsync(testConnStr);
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

            // --- Deferred migrations -----------------------------------------------
            // Run them before marking the database healthy: a healthy database on the old schema
            // fails on every query against a table the new release added.
            if (_migrationsDeferred)
            {
                try
                {
                    _logger.LogInformation("Database recovery: running the migrations deferred at startup...");
                    await MigrateAsync(testConnStr);
                    _migrationsDeferred = false;
                    _logger.LogInformation("Database recovery: deferred migrations completed");
                }
                catch (Exception ex)
                {
                    // Stay unavailable so the next request retries the migration.
                    _logger.LogError(ex, "Database recovery: deferred migrations failed – {Error}", ex.Message);
                    _serviceHealth.MarkUnavailable("Database", $"Migration failed: {ex.GetType().Name}: {ex.Message}");
                    return;
                }
            }

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

    /// <summary>Checks the connection. Virtual so tests can run recovery without SQL Server.</summary>
    internal virtual async Task<bool> ProbeAsync(string connectionString)
    {
        using var context = new DanceMusicContext(CreateOptions(connectionString));
        return await context.Database.CanConnectAsync();
    }

    /// <summary>Applies pending migrations. Virtual so tests can run recovery without SQL Server.</summary>
    internal virtual async Task MigrateAsync(string connectionString)
    {
        using var context = new DanceMusicContext(CreateOptions(connectionString));
        await context.Database.MigrateAsync();
    }

    // The same options as startup's migration context: no EnableRetryOnFailure (a single
    // fast-fail attempt; the next request retries) and PendingModelChangesWarning ignored.
    private static DbContextOptions<DanceMusicContext> CreateOptions(string connectionString) =>
        new DbContextOptionsBuilder<DanceMusicContext>()
            .UseSqlServer(connectionString, o => o.CommandTimeout(60))
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options;
}
