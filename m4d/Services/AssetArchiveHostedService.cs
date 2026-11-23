namespace m4d.Services;

/// <summary>
/// Archives this build's bundles at startup (see <see cref="AssetArchive"/>). It runs in the
/// background: the current build's bundles are served from wwwroot either way, and the archive
/// already holds the earlier builds', so nothing waits on the copy.
/// </summary>
public class AssetArchiveHostedService(AssetArchive archive, ILogger<AssetArchiveHostedService> logger)
    : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        Task.Run(() =>
        {
            try
            {
                archive.ArchiveCurrent();
            }
            catch (Exception ex)
            {
                // Pages for this build still work; only a later deploy loses its fallback
                logger.LogWarning(ex, "Couldn't archive this build's bundles to {Root}", archive.Root);
            }
        }, stoppingToken);
}
