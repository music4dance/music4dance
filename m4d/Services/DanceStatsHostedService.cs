using m4d.Services.ServiceHealth;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace m4d.Services;

public class DanceStatsHostedService(IServiceProvider serviceProvider, ILogger<DanceStatsHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // Create a new scope to retrieve scoped services
        using var scope = serviceProvider.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<DanceMusicContext>();
        var userManager =
            scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var searchService = scope.ServiceProvider.GetRequiredService<ISearchServiceManager>();
        var stats = scope.ServiceProvider.GetRequiredService<IDanceStatsManager>();
        var serviceHealth = scope.ServiceProvider.GetRequiredService<ServiceHealthManager>();

        var dms = new DanceMusicService(context, userManager, searchService, stats);

        // https://andrewlock.net/running-async-tasks-on-app-startup-in-asp-net-core-3/
        try
        {
            await stats.Initialize(dms, serviceHealth);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "DanceStats initialization failed - starting in degraded mode");
            serviceHealth.MarkUnavailable("Database", ex.Message);
            return;
        }

        // No runtime cache (a first start on a new data root, or it was deleted): the snapshot in
        // source control serves until a rebuild from the search index replaces it and writes the
        // cache. Without this, that wait would last until the next songstats job (every 6 hours).
        if (stats.Source == DanceStatsManager.FallbackSource)
        {
            _ = Task.Run(() => RebuildFromSearch(stats), CancellationToken.None);
        }
    }

    private async Task RebuildFromSearch(IDanceStatsManager stats)
    {
        try
        {
            logger.LogInformation("Dance stats loaded from the fallback snapshot; rebuilding from the search index");
            using var scope = serviceProvider.CreateScope();
            var dms = new DanceMusicService(
                scope.ServiceProvider.GetRequiredService<DanceMusicContext>(),
                scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(),
                scope.ServiceProvider.GetRequiredService<ISearchServiceManager>(),
                stats);
            await stats.ClearCache(dms, fromStore: true);
            logger.LogInformation("Dance stats rebuilt from the search index (source: {Source})", stats.Source);
        }
        catch (Exception ex)
        {
            // The fallback keeps serving; the songstats job tries again later
            logger.LogWarning(ex, "Rebuilding dance stats after a fallback load failed");
        }
    }

    // noop
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
