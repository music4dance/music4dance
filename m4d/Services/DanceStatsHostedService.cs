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
        }
    }

    // noop
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
