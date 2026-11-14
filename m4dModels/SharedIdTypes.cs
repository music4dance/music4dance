namespace m4dModels;

/// <summary>
/// Songs linked by pending shared-id candidates (directly or through other songs), reviewed and
/// merged as a unit.
/// </summary>
public class SharedIdCluster
{
    public IReadOnlyList<SharedIdCandidate> Candidates { get; init; } = [];
    public IReadOnlyList<Guid> SongIds { get; init; } = [];

    // Set by SharedIdLog.LoadSongs: the songs in SongIds order, and what's worked out from them.
    // Until then Songs is empty and Match is Different
    public IReadOnlyList<Song> Songs { get; private set; } = [];

    // A rejected pair among these songs: they can't all be the same song
    public bool HasRejectedPair { get; private set; }

    public bool HasNoMergeSong { get; private set; }

    public SharedIdMatch Match { get; private set; } = SharedIdMatch.Different;

    // Identifies the cluster by its songs, whatever order they're listed in
    public string Key => string.Join(",", SongIds.Order());

    public IReadOnlyList<string> ServiceIds =>
        [.. Candidates.SelectMany(c => SharedIdLog.SplitIds(c.ServiceIds)).Distinct().Order()];

    internal void Loaded(IReadOnlyList<Song> songs, bool hasRejectedPair)
    {
        Songs = songs;
        HasRejectedPair = hasRejectedPair;
        HasNoMergeSong = songs.Any(s => s.SongProperties.Any(p => p.Name == Song.NoMergeCommand));
        Match = SharedIdMatcher.Match(songs);
    }

    /// <summary>
    /// Groups candidate rows into clusters of connected songs (union-find), ordered by each
    /// cluster's oldest row. A cluster lists its rows in the order given and its songs in the
    /// order they first appear in them.
    /// </summary>
    public static IReadOnlyList<SharedIdCluster> Group(IEnumerable<SharedIdCandidate> candidates)
    {
        var rows = candidates.ToList();
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
}

/// <summary>What the review does with one cluster.</summary>
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
/// the music4dance account sets any a merge doesn't produce on its own. Removals, which are
/// made first whatever the action, take albums or ids off the cluster's songs.
/// </summary>
public record SharedIdChoice(SharedIdAction Action, IReadOnlyList<Guid> SongIds,
    IReadOnlyCollection<int> CandidateIds, IReadOnlyList<string> Groups = null,
    SharedIdNamePicks Names = null, IReadOnlyList<SharedIdRemoval> Removals = null)
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
/// Where a merged song's title, artist and individual artists come from: the song to take each
/// from, or null for the feat. cleanup of the merge (FeaturedCleanup.Propose). A song that isn't
/// in the group being merged counts as null.
/// </summary>
public record SharedIdNamePicks(Guid? Title = null, Guid? Artist = null, Guid? Artists = null);

/// <summary>
/// An album, or one song-level id on it, to take off one of a cluster's songs: Album is an
/// AlbumDetails.Index, and Id the extended id ("S:…", as AlbumDetails.GetExtendedPurchaseIds
/// spells it), or null for the whole album.
/// </summary>
public record SharedIdRemoval(Guid SongId, int Album, string Id = null)
{
    /// <summary>
    /// Parses the review form's value: "song|album" for an album, "song|album|id" for an id;
    /// null if it's malformed.
    /// </summary>
    public static SharedIdRemoval Parse(string value)
    {
        var parts = (value ?? "").Split('|', 3);
        return parts.Length >= 2 && Guid.TryParse(parts[0], out var song) && int.TryParse(parts[1], out var album)
            ? new SharedIdRemoval(song, album, parts.Length == 3 && parts[2].Length > 0 ? parts[2] : null)
            : null;
    }

    public string Format() => Id == null ? $"{SongId}|{Album}" : $"{SongId}|{Album}|{Id}";
}

public class SharedIdBatchResult
{
    // Songs that had albums or ids removed, and the pairs marked not the same because their songs
    // no longer share an id afterwards
    public int Edited { get; set; }
    public int UnsharedPairs { get; set; }

    // Clusters: merged (one per group merged), marked not the same (Not the same, or a split into
    // groups), left for later, and merges skipped because their songs changed elsewhere
    public int Merged { get; set; }
    public int Rejected { get; set; }
    public int Left { get; set; }
    public int Stale { get; set; }

    // Each merge made: the songs merged and the song they became
    public List<(IReadOnlyList<Guid> From, Guid Into)> Merges { get; } = [];

    public string Summary()
    {
        var message = $"Merged {Merged}, marked {Rejected} not the same, left {Left}.";
        if (Edited > 0)
        {
            message += $" Removed albums or ids from {Edited} songs; {UnsharedPairs} pairs " +
                "no longer shared an id and were marked not the same.";
        }
        if (Stale > 0)
        {
            message += $" {Stale} weren't merged: fewer than two of their grouped songs " +
                "are still in the catalog (already merged with Merge and edit, for example).";
        }

        return message;
    }
}

/// <summary>
/// What SharedIdLog.MergeMatching does with one cluster: merges it when SkipReason is null.
/// </summary>
public record SharedIdMergePlan(SharedIdCluster Cluster, string SkipReason)
{
    public const string Rejected = "A pair in it was marked not the same";
    public const string NoMerge = "A song is marked .NoMerge";
    public const string NoArtist = "A song has no artist";
    public const string WithoutFeaturing = "The titles or artists differ only by feat.";
    public const string Close = "The titles and artists are close but not the same";
    public const string Different = "The titles or artists differ";

    public bool WillMerge => SkipReason == null;

    // The cluster's, as it was when the plan was made
    public SharedIdMatch Match { get; init; } = Cluster.Match;
}

/// <summary>One cluster of a review page, with what merging all of its songs would make.</summary>
public record SharedIdPageCluster(SharedIdCluster Cluster, Song Preview, SongNames Proposal)
{
    // A cluster with a rejected pair or a .NoMerge song needs a closer look, so it starts as Leave
    public SharedIdAction DefaultAction =>
        Cluster.HasRejectedPair || Cluster.HasNoMergeSong ? SharedIdAction.Leave : SharedIdAction.Merge;

    // The proposal changes the merged title, or adds featured artists to the individual artists
    public bool CleansFeaturing => Preview != null && Proposal != null &&
        (Proposal.Title != Preview.Title || Proposal.Artists is { Count: > 1 } &&
            !Proposal.Artists.Select(ArtistSplitter.ArtistKey)
                .SequenceEqual(Preview.EffectiveArtists.Select(ArtistSplitter.ArtistKey)));
}

/// <summary>A page of the review queue (SharedIdLog.LoadPage).</summary>
public class SharedIdPage
{
    public const int DefaultPageSize = 20;

    public int Page { get; init; }
    public int PageSize { get; init; } = DefaultPageSize;
    public int PendingCandidates { get; init; }
    public int ClusterCount { get; init; }
    public IReadOnlyList<SharedIdPageCluster> Clusters { get; init; } = [];

    // How many of the pending clusters have each rank, when the page is sorted by rank
    public IReadOnlyDictionary<int, int> RankCounts { get; init; } = new Dictionary<int, int>();

    public int PageCount => Math.Max(1, (ClusterCount + PageSize - 1) / PageSize);
}
