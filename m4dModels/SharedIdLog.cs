using Microsoft.EntityFrameworkCore;

using System.Diagnostics;

namespace m4dModels;

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
        return SharedIdCluster.Group(rows);
    }

    /// <summary>
    /// A page of the queue, with each cluster's songs loaded and what merging all of them would
    /// make. When <paramref name="rank"/> is given, clusters are sorted by it (lowest first,
    /// oldest first within a rank) and the page counts how many have each rank. Clusters that
    /// <paramref name="deferred"/> picks out (the ones left for later) go after all the rest, in
    /// the same order, and aren't in the rank counts. Loading follows
    /// songs merged or deleted since they were queued, which changes the clusters, so they're
    /// worked out again (up to three times) until the page loads cleanly; a cluster still stale
    /// after that is shown with no songs.
    /// </summary>
    public async Task<SharedIdPage> LoadPage(ApplicationUser user, int page,
        Func<SharedIdCluster, int> rank = null, Func<SharedIdCluster, bool> deferred = null,
        int pageSize = SharedIdPage.DefaultPageSize)
    {
        page = Math.Max(1, page);
        IReadOnlyList<SharedIdCluster> clusters = [];
        List<SharedIdCluster> shown = [];
        for (var attempt = 0; attempt < 3; attempt++)
        {
            clusters = await PendingClusters();
            if (rank != null)
            {
                clusters = [.. clusters.OrderBy(rank)];
            }
            if (deferred != null)
            {
                clusters = [.. clusters.OrderBy(deferred)];
            }

            shown = [.. clusters.Skip((page - 1) * pageSize).Take(pageSize)];
            var current = true;
            foreach (var cluster in shown)
            {
                current &= await LoadSongs(cluster);
            }

            if (current)
            {
                break;
            }
        }

        var entries = new List<SharedIdPageCluster>();
        foreach (var cluster in shown)
        {
            Song preview = null;
            try
            {
                preview = await PreviewMerge(user, cluster);
            }
            catch (Exception e)
            {
                Trace.WriteLineIf(TraceLevels.General.TraceError,
                    $"SharedIdLog: couldn't preview the merge of {string.Join(",", cluster.SongIds)}: {e.Message}");
            }

            entries.Add(new SharedIdPageCluster(cluster, preview,
                preview == null ? null : FeaturedCleanup.Propose(preview, cluster.Songs)));
        }

        return new SharedIdPage
        {
            Page = page,
            PageSize = pageSize,
            PendingCandidates = await PendingCount(),
            ClusterCount = clusters.Count,
            Clusters = entries,
            DeferredCount = deferred == null ? 0 : clusters.Count(deferred),
            RankCounts = rank == null ? new Dictionary<int, int>()
                : clusters.Where(c => deferred == null || !deferred(c))
                    .GroupBy(rank).ToDictionary(g => g.Key, g => g.Count())
        };
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

        var ids = cluster.SongIds.ToList();
        cluster.Loaded(songs, await Context.SharedIdCandidates.AnyAsync(
            c => c.Status == SharedIdStatus.Rejected && ids.Contains(c.SongA) && ids.Contains(c.SongB)));
        return true;
    }

    /// <summary>
    /// Which of the songs each of their ids belongs to (SharedIdOwners.Propose), for the review
    /// page's Sort out ids. Songs merged or deleted since the page loaded are left out; null when
    /// fewer than two are left.
    /// </summary>
    public async Task<SharedIdOwnership> ProposeOwners(IEnumerable<Guid> songIds,
        Func<string, Task<ServiceTrack>> lookup)
    {
        var songs = await FindSongs(songIds);
        return songs.Count < 2 ? null : await SharedIdOwners.Propose(songs, lookup);
    }

    /// <summary>
    /// The songs' artists combined, checked against their Spotify and iTunes tracks unless
    /// <paramref name="force"/> (SharedIdArtists.Propose); null when fewer than two of the songs
    /// are still in the catalog.
    /// </summary>
    public async Task<SharedIdCombinedArtists> CombineArtists(IEnumerable<Guid> songIds,
        Func<string, Task<ServiceTrack>> lookup, bool force)
    {
        var songs = await FindSongs(songIds);
        return songs.Count < 2 ? null : await SharedIdArtists.Propose(songs, lookup, force);
    }

    // The songs still in the catalog
    private async Task<List<Song>> FindSongs(IEnumerable<Guid> songIds)
    {
        var songs = new List<Song>();
        foreach (var id in (songIds ?? []).Distinct())
        {
            var song = await SongIndex.FindSong(id);
            if (song is { IsNull: false })
            {
                songs.Add(song);
            }
        }

        return songs;
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

    // As above, with the names worked out from the songs being merged and the merged song. Songs
    // just saved come from saved, since the index may not have caught up with them
    private async Task<Song> Merge(ApplicationUser user, IReadOnlyCollection<Guid> songIds,
        Func<IReadOnlyList<Song>, Song, SongNames> names, IReadOnlyDictionary<Guid, Song> saved = null)
    {
        var songs = new List<Song>();
        foreach (var id in songIds.Distinct())
        {
            var song = saved?.GetValueOrDefault(id) ?? await SongIndex.FindSong(id);
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
            names == null ? null : m => names(songs, m), namesUser, reload: false);
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
    /// Applies a page of review choices: deletions, merges, not-the-same, and clusters left for
    /// later. A merge whose songs changed since the page loaded (fewer than two are still in the
    /// catalog) is counted as stale and left alone.
    /// </summary>
    public async Task<SharedIdBatchResult> Apply(ApplicationUser user, IEnumerable<SharedIdChoice> choices)
    {
        var result = new SharedIdBatchResult();
        foreach (var asChosen in choices)
        {
            var choice = asChosen.Without(await ApplyDeletions(user, asChosen, result));
            var edited = await ApplyIdEdits(user, choice, result);
            var merges = result.Merges.Count;
            switch (choice.Action)
            {
                case SharedIdAction.Merge:
                    await ApplyMerge(user, choice, edited, result);
                    break;
                case SharedIdAction.Reject:
                    await Reject(user, choice.CandidateIds);
                    result.Rejected += 1;
                    break;
                default:
                    result.Left += 1;
                    break;
            }

            if (edited.Count > 0)
            {
                // The edited songs, or the songs they were merged into
                var songIds = edited.Keys.ToHashSet();
                foreach (var (from, into) in result.Merges.Skip(merges))
                {
                    if (from.Any(songIds.Contains))
                    {
                        _ = songIds.Add(into);
                    }
                }

                await RejectUnshared(user, songIds, edited, result);
            }
        }

        return result;
    }

    // Deletes the choice's songs to delete (only the cluster's own), and marks their pending rows
    // deleted. Returns the songs deleted
    private async Task<IReadOnlyCollection<Guid>> ApplyDeletions(ApplicationUser user, SharedIdChoice choice,
        SharedIdBatchResult result)
    {
        var deleted = new List<Guid>();
        foreach (var id in (choice.Deletions ?? []).Where(choice.SongIds.Contains).Distinct())
        {
            var song = await SongIndex.FindSong(id);
            if (song == null || song.IsNull)
            {
                continue;
            }

            await SongIndex.DeleteSong(user, song);
            deleted.Add(id);
            result.Deleted += 1;
        }

        if (deleted.Count > 0)
        {
            await Repoint(deleted.ToDictionary(id => id, _ => (Guid?)null), user.UserName);
        }

        return deleted;
    }

    // Gives the choice's songs the ids its additions move to them, then takes its albums and ids
    // off them: an edit by the reviewer for each, saved once per song. An addition copies its
    // album from the song that had the id as it was before any of this. Returns the songs edited,
    // as saved
    private async Task<Dictionary<Guid, Song>> ApplyIdEdits(ApplicationUser user, SharedIdChoice choice,
        SharedIdBatchResult result)
    {
        // Only the cluster's own songs
        var removals = (choice.Removals ?? []).Where(r => r != null && choice.SongIds.Contains(r.SongId)).ToList();
        var additions = (choice.Additions ?? [])
            .Where(a => a != null && a.SongId != a.FromSong &&
                choice.SongIds.Contains(a.SongId) && choice.SongIds.Contains(a.FromSong))
            .ToList();

        var sources = new Dictionary<Guid, Song>();
        foreach (var id in additions.Select(a => a.FromSong).Distinct())
        {
            if (await SongIndex.FindSong(id) is { IsNull: false } source)
            {
                sources[id] = source;
            }
        }

        var edited = new Dictionary<Guid, Song>();
        foreach (var songId in additions.Select(a => a.SongId).Concat(removals.Select(r => r.SongId)).Distinct())
        {
            var song = await SongIndex.FindSong(songId);
            if (song == null || song.IsNull)
            {
                continue;
            }

            var added = song.AppendIdAddition(user, additions
                .Where(a => a.SongId == songId)
                .Select(a => (sources.GetValueOrDefault(a.FromSong)?.Albums.FirstOrDefault(al => al.Index == a.FromAlbum),
                    a.Id, a.Isrc)));
            var removed = song.AppendIdRemoval(user,
                removals.Where(r => r.SongId == songId).Select(r => (r.Album, r.Id)));
            if (!added && !removed)
            {
                continue;
            }

            var saved = await Song.Create(song.SongId, [.. song.SongProperties], dms);
            await SongIndex.SaveSong(saved);
            edited[saved.SongId] = saved;
            result.Edited += 1;
        }

        return edited;
    }

    // Marks not the same the pending pairs of these songs that no longer share an id: removing
    // the shared id from one of them says they're different songs. Songs just saved come from
    // <paramref name="saved"/>, since the index may not have caught up with them
    private async Task RejectUnshared(ApplicationUser user, IReadOnlyCollection<Guid> songIds,
        IReadOnlyDictionary<Guid, Song> saved, SharedIdBatchResult result)
    {
        var rows = await Context.SharedIdCandidates
            .Where(c => c.Status == SharedIdStatus.Pending && (songIds.Contains(c.SongA) || songIds.Contains(c.SongB)))
            .ToListAsync();
        foreach (var row in rows)
        {
            var a = saved.GetValueOrDefault(row.SongA) ?? await SongIndex.FindSong(row.SongA);
            var b = saved.GetValueOrDefault(row.SongB) ?? await SongIndex.FindSong(row.SongB);
            if (a == null || a.IsNull || b == null || b.IsNull ||
                a.GetExtendedPurchaseIds().Intersect(b.GetExtendedPurchaseIds()).Any())
            {
                continue;
            }

            Resolve(row, SharedIdStatus.Rejected, user.UserName);
            result.UnsharedPairs += 1;
        }

        _ = await Context.SaveChangesAsync();
    }

    // Merges each group of the choice, then marks the pairs between groups not the same. When a
    // group's merge is stale, nothing is rejected: its songs changed elsewhere
    private async Task ApplyMerge(ApplicationUser user, SharedIdChoice choice,
        IReadOnlyDictionary<Guid, Song> saved, SharedIdBatchResult result)
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
                choice.Names == null ? null : (songs, m) => PickNames(choice, songs, m), saved);
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

    // The names and tempo a group's merge lands on: each as typed when the choice merges one
    // group, else from the song picked for it when that song is in the group, otherwise from the
    // feat. cleanup of the merge (for the tempo, what the merge gives)
    private static SongNames PickNames(SharedIdChoice choice, IReadOnlyList<Song> songs, Song merged)
    {
        var proposal = FeaturedCleanup.Propose(merged, songs);
        Song From(Guid? id) => id == null ? null : songs.FirstOrDefault(s => s.SongId == id);

        var names = choice.Names;
        var typed = choice.MergeGroups.Count == 1 ? names.Typed : null;
        var typedArtists = typed?.Artists?.Where(a => !string.IsNullOrWhiteSpace(a)).Select(a => a.Trim()).ToList();
        return new SongNames(
            Typed(typed?.Title) ?? From(names.Title)?.Title ?? proposal.Title,
            Typed(typed?.Artist) ?? From(names.Artist)?.Artist ?? proposal.Artist,
            typedArtists is { Count: > 0 } ? typedArtists
                : From(names.Artists)?.EffectiveArtists ?? proposal.Artists,
            typed?.Tempo is > 0 ? typed.Tempo : From(names.Tempo)?.Tempo);
    }

    private static string Typed(string name) => string.IsNullOrWhiteSpace(name) ? null : name.Trim();

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
    /// (they share an id by construction), skipping clusters with a rejected pair, a song marked
    /// .NoMerge, a song with no artist, or lengths more than SharedIdMatcher.LengthTolerance
    /// apart. Each merge gets the feat. cleanup, as on the review
    /// page. Returns the number of clusters merged.
    /// </summary>
    public Task<int> MergeMatching(ApplicationUser user, Action<string, int> progress = null) =>
        MergePlanned(user, p => p.WillMerge, progress);

    /// <summary>
    /// Merges every pending cluster whose titles and artists are the same once their feat.
    /// clauses come out, with the feat. cleanup (the featured artists move into the individual
    /// artists), skipping the same clusters MergeMatching does. Returns the number merged.
    /// </summary>
    public Task<int> MergeFeaturing(ApplicationUser user, Action<string, int> progress = null) =>
        MergePlanned(user, p => p.WillMergeFeaturing, progress);

    /// <summary>
    /// Merges every pending cluster whose titles match without feat. and whose songs name the
    /// same individual artists, however they're credited ("X and His Orchestra" and "X"), or
    /// whose credits only leave artists out ("X" and "X &amp; Y") when <paramref name="lookup"/>
    /// finds that each song credited to the lead artist alone has a Spotify or iTunes id of its
    /// own whose track has the title and lists them all (see ConfirmArtists). Each
    /// merge gets the feat. cleanup, which keeps every artist. Skips the same clusters
    /// MergeMatching does. Returns the number merged.
    /// </summary>
    public Task<int> MergeCredits(ApplicationUser user, Func<string, Task<ServiceTrack>> lookup,
        Action<string, int> progress = null) =>
        MergePlanned(user, p => p.WillMergeCredits, progress, lookup);

    /// <summary>
    /// Merges the pending clusters whose songs share their lead artist that MergeCredits leaves,
    /// on an admin's request: the ones where a song credited to the lead artist alone has no
    /// track of its own to check, or, with <paramref name="listsFewer"/>, the ones where that
    /// track leaves artists out. Each merge gets the feat. cleanup with the lead artists as
    /// evidence, so "X" and "X &amp; Y" land on "X &amp; Y". Needs <paramref name="lookup"/> to
    /// tell the two apart. Returns the number merged.
    /// </summary>
    public Task<int> MergeLeadArtist(ApplicationUser user, bool listsFewer,
        Func<string, Task<ServiceTrack>> lookup, Action<string, int> progress = null)
    {
        ArgumentNullException.ThrowIfNull(lookup);
        return MergePlanned(user, p => listsFewer ? p.WillMergeListsFewer : p.WillMergeUnchecked, progress, lookup);
    }

    private async Task<int> MergePlanned(ApplicationUser user, Func<SharedIdMergePlan, bool> which,
        Action<string, int> progress, Func<string, Task<ServiceTrack>> lookup = null)
    {
        var merged = 0;
        foreach (var plan in (await PlanMergeMatching(progress, lookup: lookup)).Where(which))
        {
            var knowledge = plan.Knowledge;
            if (await Merge(user, plan.Cluster.SongIds,
                    (songs, m) => FeaturedCleanup.Propose(m, songs, knowledge)) != null &&
                ++merged % 100 == 0)
            {
                progress?.Invoke("Merged", merged);
            }
        }

        return merged;
    }

    /// <summary>
    /// What the Merge all tasks would do with each pending cluster: merge it, or skip it and why.
    /// The clusters' songs are loaded. With <paramref name="lookup"/> (an extended id, "S:…" or
    /// "I:…", to its track), a cluster whose credits leave artists out is checked against the
    /// songs' own Spotify and iTunes ids (ConfirmArtists); without it, it's skipped. With
    /// <paramref name="previewAs"/>, each cluster one of the tasks would merge also gets the
    /// names its merge would land on. Changes nothing but the bookkeeping LoadSongs does for
    /// songs merged or deleted since they were queued.
    /// </summary>
    public async Task<IReadOnlyList<SharedIdMergePlan>> PlanMergeMatching(
        Action<string, int> progress = null, ApplicationUser previewAs = null,
        Func<string, Task<ServiceTrack>> lookup = null)
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

                var plan = new SharedIdMergePlan(cluster, SkipReason(cluster));
                if (plan.SkipReason == SharedIdMergePlan.FewerArtists && lookup != null &&
                    await ConfirmArtists(cluster, lookup) is { } check)
                {
                    plan = plan with
                    {
                        SkipReason = check.Reason, CheckedBy = check.Ids, ServiceArtists = check.Artists
                    };
                }

                if (previewAs != null && plan.WouldMerge)
                {
                    plan = plan with { Names = await PreviewNames(previewAs, cluster, plan.Knowledge) };
                }

                plans.Add(plan);
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

    private sealed record ArtistCheck(string Reason, IReadOnlyList<string> Ids, IReadOnlyList<string> Artists);

    // Whether the songs credited to the lead artist alone ("X", against "X & Y") really are the
    // version with every artist. The shared id can't say: it may be the very id that was
    // attached to the wrong version, and an old classic often has a later version with a
    // collaborator. So each such song (each song, when none is) is checked against an id of its
    // own, one no other song in the cluster has: the first of its Spotify ids, then iTunes ids,
    // whose track has the cluster's title (without feat.). ConfirmedArtists when each of those
    // tracks lists every artist the songs name; ServiceListsFewer as soon as one leaves some
    // out; null (no evidence, so the cluster stays FewerArtists) when a song has no such track.
    // The tracks' artists are the evidence for splitting the songs' credits ("X & Y" is X and Y
    // when the tracks list both)
    private static async Task<ArtistCheck> ConfirmArtists(SharedIdCluster cluster,
        Func<string, Task<ServiceTrack>> lookup)
    {
        var songs = cluster.Songs;
        var title = SharedIdMatcher.TitleKey(songs[0].Title);
        // The lead artists, the evidence for splitting an iTunes credit
        var leads = FeaturedCleanup.Knowing(songs.Select(SharedIdMatcher.Lead));
        var leadOnly = songs.Where(s => SharedIdMatcher.ArtistKeys(s).SetEquals([SharedIdMatcher.LeadKey(s)])).ToList();

        var ids = new List<string>();
        var artists = new List<string>();
        foreach (var song in leadOnly.Count > 0 ? leadOnly : songs)
        {
            var others = songs.Where(s => s.SongId != song.SongId)
                .SelectMany(s => s.GetExtendedPurchaseIds()).ToHashSet();
            var own = song.GetExtendedPurchaseIds().Where(i => !others.Contains(i)).Distinct().ToList();
            var listed = await OwnTrackArtists(
                own.Where(i => i.StartsWith("S:")).Concat(own.Where(i => i.StartsWith("I:"))),
                title, leads, lookup);
            if (listed == null)
            {
                return null;
            }

            ids.Add(listed.Value.Id);
            artists.AddRange(listed.Value.Artists.Where(a =>
                !artists.Any(n => ArtistSplitter.ArtistKey(n) == ArtistSplitter.ArtistKey(a))));
            var knowledge = FeaturedCleanup.Knowing(listed.Value.Artists);
            var named = listed.Value.Artists.Select(ArtistSplitter.ArtistKey).ToHashSet();
            if (!songs.All(s => SharedIdMatcher.ArtistKeys(s, knowledge).IsSubsetOf(named)))
            {
                return new ArtistCheck(SharedIdMergePlan.ServiceListsFewer, [listed.Value.Id], listed.Value.Artists);
            }
        }

        return new ArtistCheck(SharedIdMergePlan.ConfirmedArtists, ids, artists);
    }

    // The first of the ids whose track has the title, with the artists it lists (Spotify lists
    // each one; an iTunes credit is split with the lead artists as evidence); null when none has
    private static async Task<(string Id, IReadOnlyList<string> Artists)?> OwnTrackArtists(
        IEnumerable<string> ids, int title, IArtistKnowledge leads, Func<string, Task<ServiceTrack>> lookup)
    {
        foreach (var id in ids)
        {
            ServiceTrack track;
            try
            {
                track = await lookup(id);
            }
            catch (Exception e)
            {
                Trace.WriteLineIf(TraceLevels.General.TraceError,
                    $"SharedIdLog: couldn't look up {id}: {e.Message}");
                continue;
            }

            if (track != null && SharedIdMatcher.TitleKey(track.Name) == title)
            {
                return (id, track.Artists is { Length: > 0 } ? track.Artists
                    : ArtistSplitter.Split(track.Artist, track.Name, leads).Artists);
            }
        }

        return null;
    }

    // The names the cluster's merge would land on, or null when it can't be previewed
    private async Task<SongNames> PreviewNames(ApplicationUser user, SharedIdCluster cluster,
        IArtistKnowledge knowledge)
    {
        try
        {
            var preview = await PreviewMerge(user, cluster);
            return preview == null ? null : FeaturedCleanup.Propose(preview, cluster.Songs, knowledge);
        }
        catch (Exception e)
        {
            Trace.WriteLineIf(TraceLevels.General.TraceError,
                $"SharedIdLog: couldn't preview the merge of {string.Join(",", cluster.SongIds)}: {e.Message}");
            return null;
        }
    }

    private static string SkipReason(SharedIdCluster cluster) =>
        cluster.HasRejectedPair ? SharedIdMergePlan.Rejected
        : cluster.HasNoMergeSong ? SharedIdMergePlan.NoMerge
        : cluster.Songs.Any(s => string.IsNullOrWhiteSpace(s.Artist)) ? SharedIdMergePlan.NoArtist
        : cluster.LengthsDiffer ? SharedIdMergePlan.LengthsDiffer
        : cluster.Match switch
        {
            SharedIdMatch.Exact => null,
            SharedIdMatch.WithoutFeaturing => SharedIdMergePlan.WithoutFeaturing,
            SharedIdMatch.SameArtists => SharedIdMergePlan.SameArtists,
            SharedIdMatch.FewerArtists => SharedIdMergePlan.FewerArtists,
            SharedIdMatch.Close => SharedIdMergePlan.Close,
            _ => SharedIdMergePlan.Different
        };

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
