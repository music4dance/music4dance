using System.Diagnostics;

using m4d.Configuration;
using m4d.Services.ServiceHealth;

using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace m4d.Services;

/// <summary>
/// Completes an App Configuration load that failed at startup, in place, without a restart.
///
/// AppConfigurationStartup adds the provider as optional, so a failed startup load leaves an
/// empty provider in the configuration chain. This service calls its refresher every
/// <see cref="RetryInterval"/>; the provider turns a refresh of a never-loaded store into a full
/// load (Key Vault references included), throttled internally to once per refresh interval. When
/// data arrives, configuration reloads, the secret-backed services pick up their credentials on
/// next use, and this service marks AppConfiguration and those services healthy.
///
/// The service exits at once when the startup load succeeded, and it never restarts the app, so
/// an intermittently slow App Configuration costs nothing worse than a delay.
/// </summary>
public class AppConfigurationRecoveryService(
    ILogger<AppConfigurationRecoveryService> logger,
    ServiceHealthManager serviceHealth,
    IConfiguration configuration,
    IConfigurationRefresherProvider refresherProvider) : BackgroundService
{
    public TimeSpan RetryInterval { get; init; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Whether App Configuration data has loaded; replaceable for tests.
    /// </summary>
    public Func<bool> HasLoaded { get; init; } = () => AppConfigurationStartup.HasLoaded(configuration);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (HasLoaded())
        {
            return;
        }

        logger.LogWarning(
            "[AppConfigRecovery] App Configuration didn't load at startup; retrying in place every {Interval}",
            RetryInterval);

        var timer = Stopwatch.StartNew();
        var refreshers = refresherProvider.Refreshers.ToList();
        var attempt = 0;
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(RetryInterval, stoppingToken);

                attempt += 1;
                foreach (var refresher in refreshers)
                {
                    // TryRefreshAsync logs and swallows failures; the provider throttles
                    // the reload attempts, so most calls return without a network request
                    _ = await refresher.TryRefreshAsync(stoppingToken);
                }

                if (HasLoaded())
                {
                    await OnRecovered(timer.Elapsed, attempt);
                    return;
                }

                if (attempt % 10 == 0)
                {
                    logger.LogWarning(
                        "[AppConfigRecovery] App Configuration still not loaded after {Elapsed:F0} minutes",
                        timer.Elapsed.TotalMinutes);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down
        }
    }

    private async Task OnRecovered(TimeSpan elapsed, int attempt)
    {
        serviceHealth.MarkHealthy("AppConfiguration");
        var stillMissing = SecretBackedServices.UpdateHealth(configuration, serviceHealth, markMissing: false);

        var message =
            $"App Configuration failed to load at startup and recovered in place after {elapsed.TotalMinutes:F1} minutes (check {attempt}).";
        if (stillMissing.Any())
        {
            message += $" Still not configured: {string.Join(", ", stillMissing)}.";
        }
        logger.LogWarning("[AppConfigRecovery] {Message}", message);

        try
        {
            // The startup failure email couldn't go out (the email settings live in App
            // Configuration), so this is the first the admins hear of it
            _ = await serviceHealth.SendNotificationAsync(
                HealthNotificationKind.Recovery, "AppConfiguration", message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[AppConfigRecovery] Failed to send recovery notification");
        }
    }
}
