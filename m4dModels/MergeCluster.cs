namespace m4dModels;

internal class MergeCluster(int hash)
{
    private static List<Song> s_mergeCandidateCache;
    private static int s_mergeCandidateLevel;

    public int PropertyHash { get; set; } = hash;
    public List<Song> Songs { get; set; } = [];

    public static void ClearMergeCandidateCache()
    {
        s_mergeCandidateCache = null;
    }

    public static async Task<IReadOnlyCollection<Song>> GetMergeCandidates(
        DanceMusicCoreService dms, int n,
        int level)
    {
        if (level == s_mergeCandidateLevel && s_mergeCandidateCache != null)
        {
            return s_mergeCandidateCache;
        }

        var clusters = new Dictionary<int, MergeCluster>();

        // ReSharper disable once LoopCanBePartlyConvertedToQuery

        var songIndex = dms.SongIndex;
        foreach (var song in await songIndex.LoadLightSongs())
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

        // Consider improving this algorithm, but for now, just take the top n songs
        var ret = new List<Song>();

        foreach (var cluster in clusters.Values
                .TakeWhile(cluster => ret.Count + cluster.Songs.Count <= n)
                .Where(cluster => cluster.Songs.Count > 1))
        // Level 2 is all songs with a similar title
        {
            if (level == 2)
            {
                ret.AddRange(cluster.Songs);
            }
            // Level 0 is similar title + all other fields are the same or empty
            else if (level == 0)
            {
                var lumps = new List<MergeCluster>();

                foreach (var s in cluster.Songs)
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

                    if (added)
                    {
                        continue;
                    }

                    var mc = new MergeCluster(0);
                    mc.Songs.Add(s);
                    lumps.Add(mc);
                }

                foreach (var l in lumps)
                {
                    if (ret.Count + l.Songs.Count > n)
                    {
                        break;
                    }

                    if (l.Songs.Count > 1)
                    {
                        ret.AddRange(l.Songs);
                    }
                }
            }

            // Level 1 (default) is all songs that have a similar title and similar or empty artist
            else
            {
                var lumps = new Dictionary<int, MergeCluster>();

                var emptyArtist = false;
                foreach (var s in cluster.Songs)
                {
                    if (string.IsNullOrWhiteSpace(s.Artist))
                    {
                        emptyArtist = true;
                        break;
                    }

                    var hash = Song.CreateTitleHash(s.Artist);
                    if (!lumps.TryGetValue(hash, out var lump))
                    {
                        lump = new MergeCluster(hash);
                        lumps.Add(hash, lump);
                    }

                    lump.Songs.Add(s);
                }

                if (emptyArtist)
                // Add all of the songs in the cluster
                {
                    ret.AddRange(cluster.Songs);
                }
                else
                {
                    foreach (var l in lumps.Values)
                    {
                        // Level 3 == level but filter out lumps with lengths that are too divergents (epsilon > 20?)
                        if (level == 3)
                        {
                            l.Songs = FilterLength(l.Songs);
                        }

                        if (ret.Count + l.Songs.Count > n)
                        {
                            break;
                        }

                        if (l.Songs.Count > 1)
                        {
                            ret.AddRange(l.Songs);
                        }
                    }
                }
            }
        }

        s_mergeCandidateCache = ret;
        s_mergeCandidateLevel = level;

        return ret;
    }

    public static void RemoveMergeCandidate(Song song)
    {
        if (s_mergeCandidateCache == null)
        {
            return;
        }

        var idx = s_mergeCandidateCache.FindIndex(s => s.SongId == song.SongId);
        if (idx == -1)
        {
            return;
        }

        s_mergeCandidateCache.RemoveAt(idx);
    }

    private static List<Song> FilterLength(List<Song> lump)
    {
        if (lump.Count < 2)
        {
            return lump;
        }

        var total = 0;
        var count = 0;
        foreach (var song in lump.Where(song => song.Length.HasValue))
        {
            count += 1;
            // ReSharper disable once PossibleInvalidOperationException
            total += song.Length.Value;
        }

        // No songs have length, so this filter makes no sense
        if (count == 0)
        {
            return lump;
        }

        var avg = total / count;
        var ret = new List<Song>();
        foreach (var song in lump.Where(
            song =>
                !song.Length.HasValue || Math.Abs(song.Length.Value - avg) < 20))
        {
            ret.Add(song);
        }

        return ret;
    }
}
