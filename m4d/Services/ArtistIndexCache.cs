namespace m4d.Services;

/// <summary>
/// Holds the <see cref="ArtistIndex"/> snapshot behind the artist index page. The first request
/// builds it (one streaming pass over the search index); after <see cref="Lifetime"/> requests are
/// served the stale snapshot while a background rebuild runs. See
/// architecture/artist-index-plan.md §11.2.
/// </summary>
public class ArtistIndexCache(ILogger<ArtistIndexCache> logger)
{
    public static TimeSpan Lifetime { get; set; } = TimeSpan.FromHours(6);

    private readonly SemaphoreSlim _buildLock = new(1, 1);
    private ArtistIndex _index;
    private int _refreshing;

    public async Task<ArtistIndex> GetAsync(DanceMusicCoreService dms, CancellationToken cancellationToken = default)
    {
        var current = _index;
        if (current != null)
        {
            if (DateTime.UtcNow - current.Built > Lifetime)
            {
                RefreshInBackground(dms);
            }
            return current;
        }

        await _buildLock.WaitAsync(cancellationToken);
        try
        {
            return _index ??= await Build(dms, cancellationToken);
        }
        finally
        {
            _ = _buildLock.Release();
        }
    }

    public void Invalidate() => _index = null;

    private void RefreshInBackground(DanceMusicCoreService dms)
    {
        if (Interlocked.CompareExchange(ref _refreshing, 1, 0) != 0)
        {
            return;
        }

        var transient = dms.GetTransientService();
        _ = Task.Run(async () =>
        {
            try
            {
                _index = await Build(transient, CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Artist index refresh failed; keeping the previous snapshot");
            }
            finally
            {
                transient.Dispose();
                _ = Interlocked.Exchange(ref _refreshing, 0);
            }
        });
    }

    private async Task<ArtistIndex> Build(DanceMusicCoreService dms, CancellationToken cancellationToken)
    {
        var started = DateTime.UtcNow;
        var index = await ArtistIndex.BuildAsync(
            dms.SongIndex.StreamSongArtistsAsync(CruftFilter.NoCruft, cancellationToken), cancellationToken);
        logger.LogInformation("Built artist index: {Count} artists in {Seconds:F1}s",
            index.Count, (DateTime.UtcNow - started).TotalSeconds);
        return index;
    }
}
