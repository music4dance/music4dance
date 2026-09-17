namespace m4d.Services;

/// <summary>
/// Holds the <see cref="ArtistIndex"/> snapshot behind the artist index page. Building it takes
/// one streaming pass over the search index, which is too slow to do inside a request, so builds
/// always run in the background: callers wait briefly for a first build and otherwise get null
/// ("still building"), and once a snapshot is older than <see cref="Lifetime"/> they keep getting
/// it while a rebuild runs. See architecture/artist-index-plan.md §11.2.
/// </summary>
public class ArtistIndexCache(ILogger<ArtistIndexCache> logger)
{
    public static TimeSpan Lifetime { get; set; } = TimeSpan.FromHours(6);

    public static TimeSpan FirstBuildWait { get; set; } = TimeSpan.FromSeconds(10);

    private readonly object _lock = new();
    private ArtistIndex _index;
    private Task<ArtistIndex> _building;

    /// <summary>
    /// The current snapshot, or null when the first build hasn't finished within
    /// <see cref="FirstBuildWait"/>.
    /// </summary>
    public async Task<ArtistIndex> GetAsync(DanceMusicCoreService dms)
    {
        var current = _index;
        if (current != null)
        {
            if (DateTime.UtcNow - current.Built > Lifetime)
            {
                _ = StartBuild(dms);
            }
            return current;
        }

        var build = StartBuild(dms);
        var finished = await Task.WhenAny(build, Task.Delay(FirstBuildWait));
        return finished == build && build.IsCompletedSuccessfully ? build.Result : _index;
    }

    public void Invalidate() => _index = null;

    private Task<ArtistIndex> StartBuild(DanceMusicCoreService dms)
    {
        lock (_lock)
        {
            if (_building != null)
            {
                return _building;
            }

            var transient = dms.GetTransientService();
            _building = Task.Run(async () =>
            {
                try
                {
                    var started = DateTime.UtcNow;
                    var index = await ArtistIndex.BuildAsync(
                        transient.SongIndex.StreamSongArtistsAsync(CruftFilter.NoCruft));
                    logger.LogInformation("Built artist index: {Count} artists in {Seconds:F1}s",
                        index.Count, (DateTime.UtcNow - started).TotalSeconds);
                    _index = index;
                    return index;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Artist index build failed");
                    return _index;
                }
                finally
                {
                    transient.Dispose();
                    lock (_lock)
                    {
                        _building = null;
                    }
                }
            });
            return _building;
        }
    }
}
