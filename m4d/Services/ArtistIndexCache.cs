namespace m4d.Services;

/// <summary>
/// Holds the <see cref="ArtistIndex"/> snapshot behind the artist index page. Building it takes
/// one streaming pass over the search index, which is too slow to do inside a request, so builds
/// always run in the background and callers are served whatever snapshot exists meanwhile: the
/// one in memory, or failing that the last one written to disk. See
/// architecture/individual-artists.md §9.2 and §9.6.
/// </summary>
public class ArtistIndexCache(ILogger<ArtistIndexCache> logger, IArtistIndexFileManager files)
{
    public static TimeSpan Lifetime { get; set; } = TimeSpan.FromHours(6);

    public static TimeSpan FirstBuildWait { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// How long to leave a failed build alone. Without it an unreachable search service is
    /// retried on every request, which is the worst moment to be starting streaming passes.
    /// </summary>
    public static TimeSpan RetryAfterFailure { get; set; } = TimeSpan.FromMinutes(5);

    private readonly object _lock = new();
    private ArtistIndex _index;
    private Task<ArtistIndex> _building;
    private Task<ArtistIndex> _loading;
    private DateTime _failedAt = DateTime.MinValue;
    private bool _fileIsStale;

    /// <summary>
    /// The best snapshot available, preferring memory, then disk. Null only when there is no
    /// persisted snapshot either and the first build hasn't finished within
    /// <see cref="FirstBuildWait"/>.
    /// </summary>
    public async Task<ArtistIndex> GetAsync(DanceMusicCoreService dms)
    {
        var current = _index ?? await LoadFromFile();
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

    /// <summary>
    /// The snapshot as it stands right now, without waiting for one - null until a build or a
    /// load finishes. Starts whichever is needed so a caller that never waits still gets one
    /// eventually. For type-ahead, where a slow answer is worse than no answer: blocking a
    /// keystroke for <see cref="FirstBuildWait"/> would be far worse than showing no suggestions.
    /// </summary>
    public ArtistIndex Current(DanceMusicCoreService dms)
    {
        var current = _index;
        if (current == null)
        {
            _ = LoadFromFile();
        }

        if (current == null || DateTime.UtcNow - current.Built > Lifetime)
        {
            _ = StartBuild(dms);
        }
        return current;
    }

    /// <summary>
    /// Drops the snapshot, on disk as well as in memory. Called after a backfill, which makes the
    /// persisted copy wrong rather than merely old - reloading it would put the pre-backfill
    /// artists straight back, and leave them there for the next instance to restart.
    /// </summary>
    public void Invalidate()
    {
        _index = null;
        _fileIsStale = true;
        try
        {
            files?.Delete();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not delete the persisted artist index");
        }
    }

    private Task<ArtistIndex> LoadFromFile()
    {
        if (files == null || _fileIsStale)
        {
            return Task.FromResult<ArtistIndex>(null);
        }

        lock (_lock)
        {
            return _loading ??= Task.Run(async () =>
            {
                try
                {
                    var index = ArtistIndex.LoadFromJson(await files.Read());
                    if (index == null)
                    {
                        return null;
                    }

                    // Only fills a gap: a build that finished while this was reading is newer by
                    // definition and shouldn't be replaced by a file written before it.
                    lock (_lock)
                    {
                        _index ??= index;
                    }

                    logger.LogInformation(
                        "Loaded artist index from disk: {Count} artists, built {Built:u}",
                        index.Count, index.Built);
                    return index;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Could not read the persisted artist index");
                    return null;
                }
            });
        }
    }

    private Task<ArtistIndex> StartBuild(DanceMusicCoreService dms)
    {
        lock (_lock)
        {
            if (_building != null)
            {
                return _building;
            }

            if (DateTime.UtcNow - _failedAt < RetryAfterFailure)
            {
                return Task.FromResult(_index);
            }

            _building = Task.Run(async () =>
            {
                // Acquired inside the try, not before it: this is the call that fails when the
                // database is unreachable, and a snapshot refresh failing is not a reason for the
                // request that triggered it to fail. The caller gets the snapshot it already had.
                DanceMusicCoreService transient = null;
                try
                {
                    transient = dms.GetTransientService();
                    var started = DateTime.UtcNow;
                    var index = await ArtistIndex.BuildAsync(
                        transient.SongIndex.StreamSongArtistsAsync(CruftFilter.NoCruft));
                    logger.LogInformation("Built artist index: {Count} artists in {Seconds:F1}s",
                        index.Count, (DateTime.UtcNow - started).TotalSeconds);

                    // An empty pass doesn't throw - an index mid-rebuild answers every page with
                    // no results - and publishing that would blank the page and then write the
                    // blank to disk, where the next restart would read it back. Treated as a
                    // failure so the existing snapshot stands and the retry is paced.
                    if (index.Count == 0 && _index != null && _index.Count > 0)
                    {
                        _failedAt = DateTime.UtcNow;
                        logger.LogWarning(
                            "Artist index build returned no artists; keeping the {Count} already held",
                            _index.Count);
                        return _index;
                    }

                    _index = index;
                    _fileIsStale = false;
                    await SaveToFile(index);
                    return index;
                }
                catch (Exception ex)
                {
                    // Leaves _index alone: a stale snapshot, or one read off disk, is a better
                    // answer than none while the search service is unreachable.
                    _failedAt = DateTime.UtcNow;
                    logger.LogWarning(ex, "Artist index build failed");
                    return _index;
                }
                finally
                {
                    transient?.Dispose();
                    lock (_lock)
                    {
                        _building = null;
                    }
                }
            });
            return _building;
        }
    }

    private async Task SaveToFile(ArtistIndex index)
    {
        if (files == null)
        {
            return;
        }

        try
        {
            await files.Write(index.SaveToJson());
        }
        catch (Exception ex)
        {
            // Persisting is an optimization. A read-only or full volume costs the next restart a
            // slow first request; it must not cost this build its result.
            logger.LogWarning(ex, "Could not write the persisted artist index");
        }
    }
}
