using Microsoft.EntityFrameworkCore;

using System.Diagnostics;

namespace m4dModels;

/// <summary>
/// Songs linked by pending shared-id candidates (directly or through other songs), reviewed and
/// merged as a unit.
/// </summary>
public class SharedIdCluster
{
    public IReadOnlyList<SharedIdCandidate> Candidates { get; init; }
    public IReadOnlyList<Guid> SongIds { get; init; }

    // Filled in by SharedIdLog.LoadSongs, in SongIds order
    public IReadOnlyList<Song> Songs { get; set; } = [];

    // A rejected pair among these songs: they can't all be the same song
    public bool HasRejectedPair { get; set; }

    public IReadOnlyList<string> ServiceIds =>
        [.. Candidates.SelectMany(c => SharedIdLog.SplitIds(c.ServiceIds)).Distinct().Order()];

    public bool TitleArtistMatch => SharedIdLog.TitleArtistMatch(Songs);

    public SharedIdMatch Match => SharedIdLog.Match(Songs);

    public bool HasNoMergeSong => Songs.Any(s => s.SongProperties.Any(p => p.Name == Song.NoMergeCommand));
}

/// <summary>
/// What SharedIdLog.MergeMatching does with one cluster: merges it when SkipReason is null.
/// </summary>
public enum SharedIdAction
{
    Leave,
    Merge,
    Reject
}

/// <summary>How closely a cluster's titles and artists agree, closest first.</summary>
public enum SharedIdMatch
{
    // Same normal form (Song.CreateTitleHash)
    Exact,
    // The same once feat. clauses come out of the titles and artist credits
    WithoutFeaturing,
    // Every pair passes the fuzzy match used for service tracks (Song.TitleArtistMatch)
    Close,
    Different
}

/// <summary>
/// What to do with one cluster on the review page. For a merge, Groups (parallel to SongIds)
/// says which songs are the same: each group of two or more is merged, and the pairs between
/// groups are marked not the same. A song with an empty group is left out. Groups null puts every
/// song in one group. Names, when given, says where the merged songs' names come from; an edit by
/// the music4dance account sets any a merge doesn't produce on its own.
/// </summary>
public record SharedIdChoice(SharedIdAction Action, IReadOnlyList<Guid> SongIds,
    IReadOnlyCollection<int> CandidateIds, IReadOnlyList<string> Groups = null,
    SharedIdNamePicks Names = null)
{
    /// <summary>The songs of each group, in order of first appearance.</summary>
    public IReadOnlyList<IReadOnlyList<Guid>> MergeGroups =>
        [.. SongIds
            .Select((id, i) => (Id: id, Group: Groups == null ? "A" : i < Groups.Count ? Groups[i] : null))
            .Where(s => !string.IsNullOrWhiteSpace(s.Group))
            .GroupBy(s => s.Group.Trim())
            .Select(g => (IReadOnlyList<Guid>)[.. g.Select(s => s.Id).Distinct()])];
}

/// <summary>
/// Where a merged song's title, artist and individual artists come from: the index (in
/// SharedIdChoice.SongIds) of the song to take each from, or Proposed for the feat. cleanup of
/// the merge (FeaturedCleanup.Propose). A song that isn't in the group being merged counts as
/// Proposed.
/// </summary>
public record SharedIdNamePicks(int Title = SharedIdNamePicks.Proposed,
    int Artist = SharedIdNamePicks.Proposed, int Artists = SharedIdNamePicks.Proposed)
{
    public const int Proposed = -1;
}

public class SharedIdBatchResult
{
    public int Merged { get; set; }
    public int Rejected { get; set; }
    public int Left { get; set; }
    public int Stale { get; set; }

    // Each merge made: the songs merged and the song they became
    public List<(IReadOnlyList<Guid> From, Guid Into)> Merges { get; } = [];
}

public record SharedIdMergePlan(SharedIdCluster Cluster, string SkipReason)
{
    public const string Rejected = "A pair in it was marked not the same";
    public const string NoMerge = "A song is marked .NoMerge";
    public const string NoArtist = "A song has no artist";
    public const string WithoutFeaturing = "The titles or artists differ only by feat.";
    public const string Close = "The titles and artists are close but not the same";
    public const string Different = "The titles or artists differ";

    public bool WillMerge => SkipReason == null;

    // Worked out once, when the plan is made
    public SharedIdMatch Match { get; } = Cluster.Match;
}

/// <summary>
/// The review queue for songs that share a Spotify, iTunes or ISRC id. Enrichment of a catalog
/// song checks each id it's about to attach (CheckIds) and, when another song already holds it,
/// skips the id and records the pair here instead of creating a second holder. Seed adds the
/// pairs that already share an id. An admin then merges each cluster or marks it as different
/// songs (see architecture/songs/shared-id-review.md).
/// </summary>
public class SharedIdLog(DanceMusicCoreService dms)
{
    public const string SeedSource = "Seed";

    private const int MaxIdsLength = 1024;
    private const int MaxDetailLength = 1024;

    private DanceMusicContext Context => dms.Context;
    private SongIndex SongIndex => dms.SongIndex;

    #region Guard

    /// <summary>
    /// Of the ids an enrichment step is about to attach to <paramref name="song"/> (keys of
    /// <paramref name="claims"/>, spelled as the ServiceIds field spells them; each value
    /// describes the service track the id came from), the ones another catalog song already
    /// holds. Each conflict is recorded as a candidate. A song that isn't in the catalog yet is
    /// never blocked: the paths that add songs merge it into the holder instead.
    /// </summary>
    public async Task<HashSet<string>> CheckIds(Song song,
        IReadOnlyDictionary<string, string> claims, string source)
    {
        var owned = song.GetExtendedPurchaseIds().ToHashSet();
        var fresh = claims.Keys.Where(id => !string.IsNullOrWhiteSpace(id) && !owned.Contains(id))
            .ToHashSet();
        if (fresh.Count == 0)
        {
            return [];
        }

        var holders = (await SongIndex.FindSongsByServiceIds(fresh))
            .Where(s => s.SongId != song.SongId && !s.IsNull)
            .ToList();
        if (holders.Count == 0)
        {
            return [];
        }

        var catalogSong = await SongIndex.FindSong(song.SongId);
        if (catalogSong == null || catalogSong.IsNull)
        {
            return [];
        }

        var conflicts = holders
            .Select(h => (h.SongId, Ids: h.GetExtendedPurchaseIds().Where(fresh.Contains).Distinct().ToList()))
            .ToList();
        var held = conflicts.SelectMany(c => c.Ids).ToHashSet();
        try
        {
            foreach (var (holder, ids) in conflicts)
            {
                var detail = string.Join("; ", ids.Select(id => claims[id]).Distinct());
                await Record(holder, song.SongId, ids, source, detail);
            }

            _ = await Context.SaveChangesAsync();
        }
        catch (Exception e)
        {
            // The ids are still skipped; only the review entry is lost
            Trace.WriteLineIf(TraceLevels.General.TraceError,
                $"SharedIdLog: failed to record {song.SongId}: {e.Message}");
        }

        Trace.WriteLineIf(TraceLevels.General.TraceInfo,
            $"SharedIdLog: {source} skipped {string.Join(",", held)} on {song.SongId}");
        return held;
    }

    /// <summary>
    /// Adds the pair to the queue, or its ids to the pair's pending row. A pair already resolved
    /// stays resolved. The caller saves.
    /// </summary>
    public async Task Record(Guid a, Guid b, IEnumerable<string> ids, string source,
        string detail)
    {
        if (a == b)
        {
            return;
        }

        var (lo, hi) = Order(a, b);
        var row = Context.SharedIdCandidates.Local.FirstOrDefault(c => c.SongA == lo && c.SongB == hi)
            ?? await Context.SharedIdCandidates.FirstOrDefaultAsync(c => c.SongA == lo && c.SongB == hi);
        if (row == null)
        {
            _ = Context.SharedIdCandidates.Add(new SharedIdCandidate
            {
                SongA = lo,
                SongB = hi,
                ServiceIds = JoinIds(ids),
                Source = Truncate(source, 64),
                Detail = Truncate(detail, MaxDetailLength),
                Created = DateTimeOffset.UtcNow,
                Status = SharedIdStatus.Pending
            });
        }
        else if (row.Status == SharedIdStatus.Pending)
        {
            row.ServiceIds = JoinIds([.. SplitIds(row.ServiceIds), .. ids]);
            if (string.IsNullOrEmpty(row.Detail))
            {
                row.Detail = Truncate(detail, MaxDetailLength);
            }
        }
    }

    #endregion

    #region Seed

    /// <summary>
    /// Queues every pair of catalog songs that already share a Spotify, iTunes or ISRC id.
    /// Pairs already queued keep their status; pending ones pick up any new ids. Returns the
    /// number of pairs added.
    /// </summary>
    public async Task<int> Seed(Action<string, int> progress = null)
    {
        var holders = new Dictionary<string, List<Guid>>();
        var scanned = 0;
        await foreach (var (songId, ids) in SongIndex.StreamSongServiceIdsAsync())
        {
            foreach (var id in ids.Distinct())
            {
                if (!holders.TryGetValue(id, out var list))
                {
                    holders[id] = list = [];
                }
                list.Add(songId);
            }

            if (++scanned % 5000 == 0)
            {
                progress?.Invoke("Scanning", scanned);
            }
        }

        var pairs = new Dictionary<(Guid, Guid), SortedSet<string>>();
        foreach (var (id, songs) in holders.Where(h => h.Value.Count > 1))
        {
            var distinct = songs.Distinct().ToList();
            for (var i = 0; i < distinct.Count; i++)
            {
                for (var j = i + 1; j < distinct.Count; j++)
                {
                    var key = Order(distinct[i], distinct[j]);
                    if (!pairs.TryGetValue(key, out var shared))
                    {
                        pairs[key] = shared = [];
                    }
                    _ = shared.Add(id);
                }
            }
        }

        var existing = await Context.SharedIdCandidates.ToDictionaryAsync(c => (c.SongA, c.SongB));
        var added = 0;
        var now = DateTimeOffset.UtcNow;
        foreach (var ((lo, hi), ids) in pairs)
        {
            if (existing.TryGetValue((lo, hi), out var row))
            {
                if (row.Status == SharedIdStatus.Pending)
                {
                    row.ServiceIds = JoinIds([.. SplitIds(row.ServiceIds), .. ids]);
                }
                continue;
            }

            _ = Context.SharedIdCandidates.Add(new SharedIdCandidate
            {
                SongA = lo,
                SongB = hi,
                ServiceIds = JoinIds(ids),
                Source = SeedSource,
                Created = now,
                Status = SharedIdStatus.Pending
            });

            if (++added % 500 == 0)
            {
                _ = await Context.SaveChangesAsync();
                progress?.Invoke("Adding", added);
            }
        }

        _ = await Context.SaveChangesAsync();
        return added;
    }

    #endregion

    #region Review

    public async Task<int> PendingCount() =>
        await Context.SharedIdCandidates.CountAsync(c => c.Status == SharedIdStatus.Pending);

    /// <summary>
    /// The pending candidates grouped into clusters of connected songs, oldest first. Songs aren't
    /// loaded: call LoadSongs on the clusters you show.
    /// </summary>
    public async Task<IReadOnlyList<SharedIdCluster>> PendingClusters()
    {
        var rows = await Context.SharedIdCandidates
            .Where(c => c.Status == SharedIdStatus.Pending)
            .OrderBy(c => c.Id)
            .ToListAsync();

        var parent = new Dictionary<Guid, Guid>();
        Guid Find(Guid x)
        {
            while (parent.TryGetValue(x, out var p) && p != x)
            {
                parent[x] = parent.GetValueOrDefault(p, p);
                x = parent[x];
            }
            return x;
        }

        foreach (var row in rows)
        {
            _ = parent.TryAdd(row.SongA, row.SongA);
            _ = parent.TryAdd(row.SongB, row.SongB);
            var (ra, rb) = (Find(row.SongA), Find(row.SongB));
            if (ra != rb)
            {
                parent[rb] = ra;
            }
        }

        return [.. rows
            .GroupBy(r => Find(r.SongA))
            .Select(g => new SharedIdCluster
            {
                Candidates = [.. g],
                SongIds = [.. g.SelectMany(r => new[] { r.SongA, r.SongB }).Distinct()]
            })
            .OrderBy(c => c.Candidates.Min(r => r.Id))];
    }

    /// <summary>
    /// Loads the cluster's songs. A song merged or deleted since it was queued is followed to the
    /// song it was merged into, and its rows are re-pointed; returns false when that changed the
    /// cluster, in which case the caller should reload the clusters.
    /// </summary>
    public async Task<bool> LoadSongs(SharedIdCluster cluster)
    {
        var songs = new List<Song>();
        var moved = new Dictionary<Guid, Guid?>();
        foreach (var id in cluster.SongIds)
        {
            var song = await SongIndex.FindSong(id);
            if (song == null || song.IsNull)
            {
                moved[id] = (await SongIndex.FindMergedSong(id))?.SongId;
            }
            else
            {
                songs.Add(song);
            }
        }

        if (moved.Count > 0)
        {
            await Repoint(moved, null);
            return false;
        }

        cluster.Songs = songs;
        var ids = cluster.SongIds.ToList();
        cluster.HasRejectedPair = await Context.SharedIdCandidates.AnyAsync(
            c => c.Status == SharedIdStatus.Rejected && ids.Contains(c.SongA) && ids.Contains(c.SongB));
        return true;
    }

    /// <summary>
    /// Merges the songs (SimpleMergeSongs keeps all their history), marks the pairs among them
    /// merged, and moves their other pairs to the merged song. Returns the merged song, or null
    /// if fewer than two of the songs are still in the catalog. When <paramref name="names"/> is
    /// given, an edit by the music4dance account sets any of them the merge didn't produce.
    /// </summary>
    public Task<Song> Merge(ApplicationUser user, IReadOnlyCollection<Guid> songIds,
        SongNames names = null) =>
        Merge(user, songIds, names == null ? null : (_, _) => names);

    // As above, with the names worked out from the songs being merged and the merged song
    private async Task<Song> Merge(ApplicationUser user, IReadOnlyCollection<Guid> songIds,
        Func<IReadOnlyList<Song>, Song, SongNames> names)
    {
        var songs = new List<Song>();
        foreach (var id in songIds.Distinct())
        {
            var song = await SongIndex.FindSong(id);
            if (song != null && !song.IsNull)
            {
                songs.Add(song);
            }
        }

        if (songs.Count < 2)
        {
            return null;
        }

        // Not a pseudo user: a pseudo user's title or artist doesn't override one a real user
        // set (Song.LoadProperties), and nearly every title and artist was set by a real user
        var namesUser = names == null ? null : new ApplicationUser(Song.M4dUser);
        var merged = await SongIndex.SimpleMergeSongs(user, songs,
            names == null ? null : m => names(songs, m), namesUser);
        await Repoint(songs.ToDictionary(s => s.SongId, _ => (Guid?)merged.SongId), user.UserName);
        return merged;
    }

    /// <summary>
    /// The song Merge would make from the cluster's loaded songs, without saving anything; null
    /// if fewer than two songs would be merged.
    /// </summary>
    public async Task<Song> PreviewMerge(ApplicationUser user, SharedIdCluster cluster) =>
        cluster.Songs.Count < 2 ? null : await SongIndex.BuildSimpleMerge(user, cluster.Songs);

    /// <summary>
    /// Applies a page of review choices: merges, not-the-same, and clusters left for later.
    /// A merge whose songs changed since the page loaded (fewer than two are still in the
    /// catalog) is counted as stale and left alone.
    /// </summary>
    public async Task<SharedIdBatchResult> Apply(ApplicationUser user, IEnumerable<SharedIdChoice> choices)
    {
        var result = new SharedIdBatchResult();
        foreach (var choice in choices)
        {
            switch (choice.Action)
            {
                case SharedIdAction.Merge:
                    await ApplyMerge(user, choice, result);
                    break;
                case SharedIdAction.Reject:
                    await Reject(user, choice.CandidateIds);
                    result.Rejected += 1;
                    break;
                default:
                    result.Left += 1;
                    break;
            }
        }

        return result;
    }

    // Merges each group of the choice, then marks the pairs between groups not the same. When a
    // group's merge is stale, nothing is rejected: its songs changed elsewhere
    private async Task ApplyMerge(ApplicationUser user, SharedIdChoice choice, SharedIdBatchResult result)
    {
        var groups = choice.MergeGroups;
        var becameIds = new List<Guid>();
        var stale = false;
        foreach (var group in groups)
        {
            if (group.Count < 2)
            {
                becameIds.AddRange(group);
                continue;
            }

            var merged = await Merge(user, group,
                choice.Names == null ? null : (songs, m) => PickNames(choice, songs, m));
            if (merged == null)
            {
                stale = true;
                result.Stale += 1;
                continue;
            }

            result.Merged += 1;
            result.Merges.Add((group, merged.SongId));
            becameIds.Add(merged.SongId);
        }

        if (!stale && groups.Count > 1)
        {
            var rows = await Context.SharedIdCandidates
                .Where(c => c.Status == SharedIdStatus.Pending && becameIds.Contains(c.SongA) && becameIds.Contains(c.SongB))
                .ToListAsync();
            foreach (var row in rows)
            {
                Resolve(row, SharedIdStatus.Rejected, user.UserName);
            }

            _ = await Context.SaveChangesAsync();
            result.Rejected += rows.Count > 0 ? 1 : 0;
        }
    }

    // The names a group's merge lands on: each from the song picked for it when that song is in
    // the group, otherwise from the feat. cleanup of the merge
    private static SongNames PickNames(SharedIdChoice choice, IReadOnlyList<Song> songs, Song merged)
    {
        var proposal = FeaturedCleanup.Propose(merged, songs);
        Song From(int column) => column >= 0 && column < choice.SongIds.Count
            ? songs.FirstOrDefault(s => s.SongId == choice.SongIds[column])
            : null;

        var names = choice.Names;
        return new SongNames(
            From(names.Title)?.Title ?? proposal.Title,
            From(names.Artist)?.Artist ?? proposal.Artist,
            From(names.Artists)?.EffectiveArtists ?? proposal.Artists);
    }

    public async Task Reject(ApplicationUser user, IEnumerable<int> candidateIds)
    {
        var ids = candidateIds.ToList();
        var rows = await Context.SharedIdCandidates
            .Where(c => ids.Contains(c.Id) && c.Status == SharedIdStatus.Pending)
            .ToListAsync();
        foreach (var row in rows)
        {
            Resolve(row, SharedIdStatus.Rejected, user.UserName);
        }

        _ = await Context.SaveChangesAsync();
    }

    /// <summary>
    /// Merges every pending cluster whose songs all have the same title and the same artist
    /// (they share an id by construction), skipping clusters with a rejected pair or a song marked
    /// .NoMerge. Returns the number of clusters merged.
    /// </summary>
    public async Task<int> MergeMatching(ApplicationUser user, Action<string, int> progress = null)
    {
        var merged = 0;
        foreach (var plan in (await PlanMergeMatching(progress)).Where(p => p.WillMerge))
        {
            if (await Merge(user, plan.Cluster.SongIds) != null && ++merged % 100 == 0)
            {
                progress?.Invoke("Merged", merged);
            }
        }

        return merged;
    }

    /// <summary>
    /// What MergeMatching would do with each pending cluster: merge it, or skip it and why. The
    /// clusters' songs are loaded. Changes nothing but the bookkeeping LoadSongs does for songs
    /// merged or deleted since they were queued.
    /// </summary>
    public async Task<IReadOnlyList<SharedIdMergePlan>> PlanMergeMatching(
        Action<string, int> progress = null)
    {
        var plans = new List<SharedIdMergePlan>();
        var planned = new HashSet<string>();
        // A stale cluster re-points its rows, which changes the clusters; plan the new ones on
        // the next pass
        for (var pass = 0; pass < 3; pass++)
        {
            var stale = false;
            foreach (var cluster in await PendingClusters())
            {
                if (!planned.Add(string.Join(",", cluster.SongIds.Order())))
                {
                    continue;
                }

                if (!await LoadSongs(cluster))
                {
                    stale = true;
                    continue;
                }

                plans.Add(new SharedIdMergePlan(cluster, SkipReason(cluster)));
                if (plans.Count % 100 == 0)
                {
                    progress?.Invoke("Checked", plans.Count);
                }
            }

            if (!stale)
            {
                break;
            }
        }

        return plans;
    }

    private static string SkipReason(SharedIdCluster cluster) =>
        cluster.HasRejectedPair ? SharedIdMergePlan.Rejected
        : cluster.HasNoMergeSong ? SharedIdMergePlan.NoMerge
        : cluster.Songs.Any(s => string.IsNullOrWhiteSpace(s.Artist)) ? SharedIdMergePlan.NoArtist
        : cluster.Match switch
        {
            SharedIdMatch.Exact => null,
            SharedIdMatch.WithoutFeaturing => SharedIdMergePlan.WithoutFeaturing,
            SharedIdMatch.Close => SharedIdMergePlan.Close,
            _ => SharedIdMergePlan.Different
        };

    public static SharedIdMatch Match(IReadOnlyList<Song> songs) =>
        TitleArtistMatch(songs) ? SharedIdMatch.Exact
        : FeaturedCleanup.MatchWithoutFeaturing(songs) ? SharedIdMatch.WithoutFeaturing
        : CloseMatch(songs) ? SharedIdMatch.Close
        : SharedIdMatch.Different;

    /// <summary>
    /// Every pair of songs passes Song.TitleArtistMatch (the fuzzy match that filters service
    /// tracks), as given or with their feat. clauses removed.
    /// </summary>
    public static bool CloseMatch(IReadOnlyList<Song> songs)
    {
        if (songs.Count < 2 || songs.Any(s => s == null || s.IsNull || string.IsNullOrWhiteSpace(s.Artist)))
        {
            return false;
        }

        static Song WithoutFeaturing(Song s) => new()
        {
            Title = ArtistSplitter.RemoveTitleFeaturing(s.Title),
            Artist = ArtistSplitter.RemoveArtistFeaturing(s.Artist)
        };

        for (var i = 0; i < songs.Count; i++)
        {
            for (var j = i + 1; j < songs.Count; j++)
            {
                var (a, b) = (songs[i], songs[j]);
                var (sa, sb) = (WithoutFeaturing(a), WithoutFeaturing(b));
                if (!a.TitleArtistMatch(b.Title, b.Artist) && !sa.TitleArtistMatch(sb.Title, sb.Artist))
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Every song has the same title and the same (non-empty) artist, compared the way the title
    /// merge candidates are (Song.CreateTitleHash).
    /// </summary>
    public static bool TitleArtistMatch(IReadOnlyList<Song> songs)
    {
        if (songs.Count < 2 || songs.Any(s => s == null || s.IsNull || string.IsNullOrWhiteSpace(s.Artist)))
        {
            return false;
        }

        var title = Song.CreateTitleHash(songs[0].Title);
        var artist = Song.CreateTitleHash(songs[0].Artist);
        return songs.All(s => Song.CreateTitleHash(s.Title) == title && Song.CreateTitleHash(s.Artist) == artist);
    }

    // Moves every row that mentions a key of moved to its new song (null: deleted). A row whose
    // two songs end up the same is marked merged; one that now duplicates another row is folded
    // into it.
    private async Task Repoint(IReadOnlyDictionary<Guid, Guid?> moved, string userName)
    {
        var keys = moved.Keys.ToList();
        var rows = await Context.SharedIdCandidates
            .Where(c => keys.Contains(c.SongA) || keys.Contains(c.SongB))
            .ToListAsync();

        foreach (var row in rows.Where(
            r => r.Status is SharedIdStatus.Pending or SharedIdStatus.Rejected))
        {
            var a = moved.TryGetValue(row.SongA, out var na) ? na : row.SongA;
            var b = moved.TryGetValue(row.SongB, out var nb) ? nb : row.SongB;
            if (a == null || b == null)
            {
                if (row.Status == SharedIdStatus.Pending)
                {
                    Resolve(row, SharedIdStatus.Deleted, userName);
                }
                continue;
            }

            if (a == b)
            {
                if (row.Status == SharedIdStatus.Pending)
                {
                    Resolve(row, SharedIdStatus.Merged, userName);
                }
                continue;
            }

            var (lo, hi) = Order(a.Value, b.Value);
            if (lo == row.SongA && hi == row.SongB)
            {
                continue;
            }

            var other = Context.SharedIdCandidates.Local.FirstOrDefault(
                    c => c != row && c.SongA == lo && c.SongB == hi && Context.Entry(c).State != EntityState.Deleted)
                ?? await Context.SharedIdCandidates.FirstOrDefaultAsync(c => c.SongA == lo && c.SongB == hi);
            if (other == null || other.SongA != lo || other.SongB != hi)
            {
                row.SongA = lo;
                row.SongB = hi;
            }
            else
            {
                // A rejection wins over a pending row for the same pair
                if (other.Status == SharedIdStatus.Pending)
                {
                    other.ServiceIds = JoinIds([.. SplitIds(other.ServiceIds), .. SplitIds(row.ServiceIds)]);
                    if (row.Status == SharedIdStatus.Rejected)
                    {
                        Resolve(other, SharedIdStatus.Rejected, row.ResolvedBy);
                    }
                }
                _ = Context.SharedIdCandidates.Remove(row);
            }
        }

        _ = await Context.SaveChangesAsync();
    }

    private static void Resolve(SharedIdCandidate row, SharedIdStatus status, string userName)
    {
        row.Status = status;
        row.ResolvedBy = Truncate(userName, 256);
        row.ResolvedAt = DateTimeOffset.UtcNow;
    }

    #endregion

    #region Helpers

    internal static IEnumerable<string> SplitIds(string ids) =>
        (ids ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string JoinIds(IEnumerable<string> ids) =>
        Truncate(string.Join(",", ids.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().Order()),
            MaxIdsLength);

    private static (Guid, Guid) Order(Guid a, Guid b) => a.CompareTo(b) < 0 ? (a, b) : (b, a);

    private static string Truncate(string value, int length) =>
        value == null || value.Length <= length ? value : value[..length];

    #endregion
}
