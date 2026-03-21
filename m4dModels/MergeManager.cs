namespace m4dModels;

/// <summary>
/// Manages merge candidate selection and automatic merging operations.
/// Non-static design allows for testing with mock/test implementations.
/// </summary>
public class MergeManager
{
    private readonly SongIndex _songIndex;
    private readonly IDanceStatsManager _danceStats;

    // Cache for merge candidates (instance-based, not static)
    private List<Song> _mergeCandidateCache;
    private int _mergeCandidateLevel;

    public MergeManager(SongIndex songIndex, IDanceStatsManager danceStats)
    {
        _songIndex = songIndex ?? throw new ArgumentNullException(nameof(songIndex));
        _danceStats = danceStats ?? throw new ArgumentNullException(nameof(danceStats));
    }

    public void ClearMergeCandidateCache()
    {
        _mergeCandidateCache = null;
    }

    /// <summary>
    /// Gets merge candidates by clustering songs based on title/artist similarity.
    /// </summary>
    /// <param name="n">Maximum number of songs to return</param>
    /// <param name="level">
    /// 0 = Similar title + all other fields same/empty (Equivalent)
    /// 1 = Similar title + similar/empty artist (WeakEquivalent)  
    /// 2 = All songs with similar title only
    /// 3 = Similar title + artist + length within 20s (TitleArtistEquivalent)
    /// </param>
    public async Task<IReadOnlyCollection<Song>> GetMergeCandidates(int n, int level)
    {
        if (level == _mergeCandidateLevel && _mergeCandidateCache != null)
        {
            return _mergeCandidateCache;
        }

        var clusters = new Dictionary<int, MergeCluster>();

        // Use streaming to avoid 100K skip limit
        await foreach (var song in _songIndex.LoadLightSongsStreamingAsync())
        {
            var hash = song.TitleHash;
            if (level != 2)
            {
                hash = Song.CreateTitleHash(song.Title + song.Artist);
            }

            if (!clusters.TryGetValue(hash, out var mc))
            {
                mc = new MergeCluster(hash);
                clusters.Add(hash, mc);
            }

            mc.Songs.Add(song);
        }

        var ret = new List<Song>();

        foreach (var cluster in clusters.Values
                .TakeWhile(cluster => ret.Count + cluster.Songs.Count <= n)
                .Where(cluster => cluster.Songs.Count > 1))
        {
            if (level == 2)
            {
                ret.AddRange(cluster.Songs);
            }
            else if (level == 0)
            {
                ret.AddRange(GetEquivalentLumps(cluster.Songs, n - ret.Count));
            }
            else // level == 1 or 3
            {
                ret.AddRange(GetWeakEquivalentLumps(cluster.Songs, n - ret.Count, level));
            }
        }

        _mergeCandidateCache = ret;
        _mergeCandidateLevel = level;

        return ret;
    }

    private List<Song> GetEquivalentLumps(List<Song> clusterSongs, int remaining)
    {
        var ret = new List<Song>();
        var lumps = new List<MergeCluster>();

        foreach (var s in clusterSongs)
        {
            var added = false;
            foreach (var lump in lumps)
            {
                if (!s.Equivalent(lump.Songs[0]))
                {
                    continue;
                }

                lump.Songs.Add(s);
                added = true;
                break;
            }

            if (!added)
            {
                var mc = new MergeCluster(0);
                mc.Songs.Add(s);
                lumps.Add(mc);
            }
        }

        foreach (var l in lumps)
        {
            if (ret.Count + l.Songs.Count > remaining)
            {
                break;
            }

            if (l.Songs.Count > 1)
            {
                ret.AddRange(l.Songs);
            }
        }

        return ret;
    }

    private List<Song> GetWeakEquivalentLumps(List<Song> clusterSongs, int remaining, int level)
    {
        var ret = new List<Song>();
        var lumps = new Dictionary<int, MergeCluster>();

        var emptyArtist = clusterSongs.Any(s => string.IsNullOrWhiteSpace(s.Artist));

        if (emptyArtist)
        {
            // Add all songs in the cluster
            return clusterSongs;
        }

        foreach (var s in clusterSongs)
        {
            var hash = Song.CreateTitleHash(s.Artist);
            if (!lumps.TryGetValue(hash, out var lump))
            {
                lump = new MergeCluster(hash);
                lumps.Add(hash, lump);
            }

            lump.Songs.Add(s);
        }

        foreach (var l in lumps.Values)
        {
            // Level 3: filter out songs with divergent lengths (epsilon > 20s)
            if (level == 3)
            {
                l.Songs = FilterLength(l.Songs);
            }

            if (ret.Count + l.Songs.Count > remaining)
            {
                break;
            }

            if (l.Songs.Count > 1)
            {
                ret.AddRange(l.Songs);
            }
        }

        return ret;
    }

    /// <summary>
    /// Filters out songs whose length diverges significantly (>20s) from the cluster median.
    /// Uses median instead of mean for robustness to outliers.
    /// </summary>
    private static List<Song> FilterLength(List<Song> lump)
    {
        if (lump.Count < 2)
        {
            return lump;
        }

        // Get all songs with lengths
        var songsWithLength = lump.Where(song => song.Length.HasValue).ToList();

        // No songs have length, so this filter makes no sense
        if (songsWithLength.Count == 0)
        {
            return lump;
        }

        // Calculate median length (more robust to outliers than mean)
        var sortedLengths = songsWithLength.Select(s => s.Length.Value).OrderBy(l => l).ToList();
        int median;

        if (sortedLengths.Count % 2 == 0)
        {
            // Even count: average of two middle values
            median = (sortedLengths[sortedLengths.Count / 2 - 1] + sortedLengths[sortedLengths.Count / 2]) / 2;
        }
        else
        {
            // Odd count: middle value
            median = sortedLengths[sortedLengths.Count / 2];
        }

        // Filter: keep songs within 20s of median, or songs without length data
        return lump.Where(song =>
            !song.Length.HasValue || Math.Abs(song.Length.Value - median) < 20
        ).ToList();
    }

    public void RemoveMergeCandidate(Song song)
    {
        if (_mergeCandidateCache == null)
        {
            return;
        }

        var idx = _mergeCandidateCache.FindIndex(s => s.SongId == song.SongId);
        if (idx != -1)
        {
            _mergeCandidateCache.RemoveAt(idx);
        }
    }

    /// <summary>
    /// Automatically merges groups of equivalent songs.
    /// </summary>
    /// <param name="songs">Candidate songs (typically from GetMergeCandidates)</param>
    /// <param name="level">Equivalence level (0=Equivalent, 1=WeakEquivalent, 3=TitleArtistEquivalent)</param>
    /// <param name="user">User to attribute merges to (typically a system/bot user)</param>
    /// <returns>List of merged songs</returns>
    public async Task<IReadOnlyCollection<Song>> AutoMerge(
        IReadOnlyCollection<Song> songs,
        int level,
        ApplicationUser user)
    {
        var ret = new List<Song>();
        List<Song> cluster = null;

        try
        {
            foreach (var song in new List<Song>(songs))
            {
                if (cluster == null)
                {
                    cluster = [song];
                }
                else if (ShouldMerge(song, cluster[0], level))
                {
                    cluster.Add(song);
                }
                else
                {
                    if (cluster.Count > 1)
                    {
                        var s = await AutoMergeSingleCluster(cluster, user);
                        if (s != null)
                        {
                            ret.Add(s);
                        }
                    }
                    else if (cluster.Count == 1)
                    {
                        // Single song in cluster - can't merge
                    }

                    cluster = [song];
                }
            }

            // Handle final cluster after loop ends
            if (cluster != null)
            {
                if (cluster.Count > 1)
                {
                    var s = await AutoMergeSingleCluster(cluster, user);
                    if (s != null)
                    {
                        ret.Add(s);
                    }
                }
                else if (cluster.Count == 1)
                {
                    // Single song in cluster - can't merge  
                }
            }
        }
        finally
        {
            await _danceStats.ClearCache(null, false);
        }

        return ret;
    }

    private bool ShouldMerge(Song song, Song clusterFirst, int level)
    {
        return level switch
        {
            0 => song.Equivalent(clusterFirst),
            1 => song.WeakEquivalent(clusterFirst),
            3 => song.TitleArtistEquivalent(clusterFirst),
            _ => false
        };
    }

    /// <summary>
    /// Merges a single cluster of songs.
    /// Reloads full songs, filters .NoMerge, and executes SimpleMergeSongs.
    /// </summary>
    private async Task<Song> AutoMergeSingleCluster(List<Song> songs, ApplicationUser user)
    {
        // These songs are coming from "light loading", so need to reload the full songs before merging
        songs = [.. (await _songIndex.FindSongs(songs.Select(s => s.SongId)))];

        // Filter out songs with .NoMerge command (now that we have full properties)
        songs = songs.Where(s =>
            !s.SongProperties.Any(p => p.Name == Song.NoMergeCommand)
        ).ToList();

        // If filtering removed all songs or left only one, skip merge
        if (songs.Count < 2)
        {
            return null;
        }

        // Use simple merge: concatenates all properties, annotates with song GUIDs, sorts by date
        var song = await _songIndex.SimpleMergeSongs(user, songs);

        // Remove from cache
        foreach (var s in songs)
        {
            RemoveMergeCandidate(s);
        }

        return song;
    }

    /// <summary>
    /// Helper class for grouping songs by hash during merge candidate selection.
    /// Internal to MergeManager - not exposed publicly.
    /// </summary>
    private class MergeCluster
    {
        public int PropertyHash { get; set; }
        public List<Song> Songs { get; set; } = [];

        public MergeCluster(int hash)
        {
            PropertyHash = hash;
        }
    }
}
