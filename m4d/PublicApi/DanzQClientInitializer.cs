using OpenIddict.Abstractions;

namespace m4d.PublicApi;

internal sealed class DanzQClientInitializer(
    IServiceScopeFactory scopeFactory) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var client = await manager.FindByClientIdAsync(
            PublicApiDefaults.Clients.DanzQ, cancellationToken);
        var descriptor = DanzQClient.CreateDescriptor();

        if (client == null)
        {
            await manager.CreateAsync(descriptor, cancellationToken);
            return;
        }

        await manager.UpdateAsync(client, descriptor, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
