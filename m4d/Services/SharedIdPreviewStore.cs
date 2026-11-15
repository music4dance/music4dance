using m4dModels;

using System.Collections.Concurrent;

namespace m4d.Services;

/// <summary>
/// The last Preview merge all of the shared-id review (SharedIdLog.PlanMergeMatching), kept in
/// memory until the next preview or a restart, and the order it gives the review page: clusters
/// that differ only by feat., then close matches, then the rest. The order is kept per song, so a
/// cluster keeps its place when some of its songs are merged on the review page and the rest stay
/// in the queue. It also keeps, until a restart, the songs of the clusters left for later on the
/// review page, which then come after every other cluster so they don't fill the first page. See
/// architecture/songs/shared-id-review.md.
/// </summary>
public class SharedIdPreviewStore
{
    public const int WithoutFeaturingRank = 0;
    public const int CloseRank = 1;
    public const int OtherRank = 2;

    private sealed record Snapshot(SharedIdPreview Preview, ConcurrentDictionary<Guid, int> SongRanks);

    private volatile Snapshot _current;

    // Songs of the clusters left for later; kept across previews
    private readonly ConcurrentDictionary<Guid, bool> _deferred = new();

    public SharedIdPreview Preview => _current?.Preview;

    public static int Rank(SharedIdMatch match) => match switch
    {
        SharedIdMatch.WithoutFeaturing or SharedIdMatch.SameArtists or SharedIdMatch.FewerArtists =>
            WithoutFeaturingRank,
        SharedIdMatch.Close => CloseRank,
        _ => OtherRank
    };

    public void Set(IReadOnlyList<SharedIdMergePlan> plans, DateTimeOffset created)
    {
        var ranks = plans
            .SelectMany(p => p.Cluster.SongIds.Select(id => (Id: id, Rank: Rank(p.Match))))
            .GroupBy(s => s.Id)
            .ToDictionary(g => g.Key, g => g.Min(s => s.Rank));
        _current = new Snapshot(new SharedIdPreview(created, plans), new ConcurrentDictionary<Guid, int>(ranks));
    }

    /// <summary>
    /// The rank of the cluster's best-ranked song, or null when no preview has run (the page is
    /// then in queue order).
    /// </summary>
    public Func<SharedIdCluster, int> Ranker()
    {
        var current = _current;
        return current == null ? null
            : cluster => cluster.SongIds.Select(id => SongRank(current, id)).DefaultIfEmpty(OtherRank).Min();
    }

    /// <summary>A song merged on the review page takes the best rank of the songs it came from.</summary>
    public void Merged(IEnumerable<Guid> from, Guid into)
    {
        var current = _current;
        if (current == null)
        {
            return;
        }

        var rank = from.Select(id => SongRank(current, id)).DefaultIfEmpty(OtherRank).Min();
        _ = current.SongRanks.AddOrUpdate(into, rank, (_, old) => Math.Min(old, rank));
    }

    /// <summary>The songs of a cluster left for later on the review page.</summary>
    public void Defer(IEnumerable<Guid> songIds)
    {
        foreach (var id in songIds)
        {
            _deferred[id] = true;
        }
    }

    /// <summary>
    /// Whether the cluster was left for later: every one of its songs was. A cluster that gains a
    /// song since then has something new to review, so it isn't.
    /// </summary>
    public bool IsDeferred(SharedIdCluster cluster) =>
        cluster.SongIds.Count > 0 && cluster.SongIds.All(_deferred.ContainsKey);

    private static int SongRank(Snapshot current, Guid id) =>
        current.SongRanks.TryGetValue(id, out var rank) ? rank : OtherRank;
}

/// <summary>
/// What the Merge all tasks would do with each pending cluster, as of when the preview ran:
/// Merges (matching title and artist), FeaturingMerges (differ only by feat.), CreditMerges (the
/// same artists credited differently, or confirmed by a music service) and the rest, skipped by
/// all of them, grouped by why.
/// </summary>
public record SharedIdPreview(DateTimeOffset Created, IReadOnlyList<SharedIdMergePlan> Plans)
{
    public IEnumerable<SharedIdMergePlan> Merges => Plans.Where(p => p.WillMerge);

    public IEnumerable<SharedIdMergePlan> FeaturingMerges => Plans.Where(p => p.WillMergeFeaturing);

    public IEnumerable<SharedIdMergePlan> CreditMerges => Plans.Where(p => p.WillMergeCredits);

    public IEnumerable<IGrouping<string, SharedIdMergePlan>> Skips =>
        Plans.Where(p => !p.WouldMerge).GroupBy(p => p.SkipReason).OrderByDescending(g => g.Count());
}
