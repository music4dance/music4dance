using System.Collections.Concurrent;

using m4dModels;

namespace m4d.ViewModels;

public class SharedIdsPreviewModel
{
    public DateTimeOffset Created { get; init; }
    public IReadOnlyList<SharedIdMergePlan> Plans { get; init; } = [];

    public IEnumerable<SharedIdMergePlan> Merges => Plans.Where(p => p.WillMerge);
    public IEnumerable<IGrouping<string, SharedIdMergePlan>> Skips =>
        Plans.Where(p => !p.WillMerge).GroupBy(p => p.SkipReason).OrderByDescending(g => g.Count());

    // The review page's order: clusters that differ only by feat., then close matches, then the rest
    public const int WithoutFeaturingRank = 0;
    public const int CloseRank = 1;
    public const int OtherRank = 2;

    public static int Rank(SharedIdMatch match) => match switch
    {
        SharedIdMatch.WithoutFeaturing => WithoutFeaturingRank,
        SharedIdMatch.Close => CloseRank,
        _ => OtherRank
    };

    // Each song's rank, from its cluster's plan or, for a song merged on the review page since,
    // from the songs it was merged from. Keyed by song so a cluster keeps its place when some of
    // its songs are merged and the rest stay in the queue.
    private ConcurrentDictionary<Guid, int> _songRanks;

    private ConcurrentDictionary<Guid, int> SongRanks => LazyInitializer.EnsureInitialized(ref _songRanks,
        () => new ConcurrentDictionary<Guid, int>(Plans
            .SelectMany(p => p.Cluster.SongIds.Select(id => (Id: id, Rank: Rank(p.Match))))
            .GroupBy(s => s.Id)
            .ToDictionary(g => g.Key, g => g.Min(s => s.Rank))));

    private int SongRank(Guid id) => SongRanks.TryGetValue(id, out var rank) ? rank : OtherRank;

    public int RankOf(SharedIdCluster cluster) =>
        cluster.SongIds.Select(SongRank).DefaultIfEmpty(OtherRank).Min();

    public void Merged(IEnumerable<Guid> from, Guid into)
    {
        var rank = from.Select(SongRank).DefaultIfEmpty(OtherRank).Min();
        _ = SongRanks.AddOrUpdate(into, rank, (_, old) => Math.Min(old, rank));
    }
}
