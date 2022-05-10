using System;
using System.Threading;
using System.Threading.Tasks;
using m4dModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace m4d.Services;

public class DanceStatsHostedService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;

    public DanceStatsHostedService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // Create a new scope to retrieve scoped services
        using var scope = _serviceProvider.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<DanceMusicContext>();
        var userManager =
            scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var searchService = scope.ServiceProvider.GetRequiredService<ISearchServiceManager>();
        var stats = scope.ServiceProvider.GetRequiredService<IDanceStatsManager>();

        var dms = new DanceMusicService(context, userManager, searchService, stats);

        // https://andrewlock.net/running-async-tasks-on-app-startup-in-asp-net-core-3/
        await stats.Initialize(dms);
    }

    // noop
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}