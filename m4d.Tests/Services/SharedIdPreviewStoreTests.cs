using m4d.Services;

using m4dModels;

namespace m4d.Tests.Services;

/// <summary>
/// The order the last merge-all preview gives the shared-id review page, how a song merged on the
/// page keeps its cluster's place, and the clusters left for later.
/// </summary>
[TestClass]
public class SharedIdPreviewStoreTests
{
    private static readonly Guid A = new("00000000-0000-0000-0000-00000000000a");
    private static readonly Guid B = new("00000000-0000-0000-0000-00000000000b");
    private static readonly Guid C = new("00000000-0000-0000-0000-00000000000c");
    private static readonly Guid D = new("00000000-0000-0000-0000-00000000000d");
    private static readonly Guid M = new("00000000-0000-0000-0000-0000000000ff");

    private static SharedIdCluster Cluster(params Guid[] songs) => new() { SongIds = songs };

    private sealed record Plan(SharedIdMatch Match, params Guid[] Songs);

    private static SharedIdPreviewStore Store(params Plan[] plans)
    {
        var store = new SharedIdPreviewStore();
        store.Set([.. plans.Select(p => new SharedIdMergePlan(Cluster(p.Songs), null) { Match = p.Match })],
            DateTimeOffset.UnixEpoch);
        return store;
    }

    [TestMethod]
    public void NoPreview_NoRankerAndNoPreview()
    {
        var store = new SharedIdPreviewStore();

        Assert.IsNull(store.Ranker());
        Assert.IsNull(store.Preview);
        store.Merged([A, B], M); // nothing to update, and no failure
    }

    [TestMethod]
    public void Ranker_RanksFeatThenCloseThenTheRest()
    {
        var store = Store(new Plan(SharedIdMatch.Close, A, B), new Plan(SharedIdMatch.WithoutFeaturing, C, D));
        var rank = store.Ranker();

        Assert.AreEqual(SharedIdPreviewStore.CloseRank, rank(Cluster(A, B)));
        Assert.AreEqual(SharedIdPreviewStore.WithoutFeaturingRank, rank(Cluster(C, D)));
        Assert.AreEqual(SharedIdPreviewStore.OtherRank, rank(Cluster(M)), "A song the preview never saw");
        Assert.AreEqual(SharedIdPreviewStore.WithoutFeaturingRank, rank(Cluster(B, C)), "The best of its songs");
        Assert.AreEqual(DateTimeOffset.UnixEpoch, store.Preview.Created);
    }

    [TestMethod]
    public void Merged_TheMergedSongTakesTheBestRankOfItsSources()
    {
        var store = Store(new Plan(SharedIdMatch.Close, A, B), new Plan(SharedIdMatch.WithoutFeaturing, C, D));

        store.Merged([A, C], M);

        Assert.AreEqual(SharedIdPreviewStore.WithoutFeaturingRank, store.Ranker()(Cluster(M)));
    }

    [TestMethod]
    public void Merged_NeverWorsensASongsRank()
    {
        var store = Store(new Plan(SharedIdMatch.WithoutFeaturing, A, B), new Plan(SharedIdMatch.Exact, C, D));

        store.Merged([A], M);
        store.Merged([C], M);

        Assert.AreEqual(SharedIdPreviewStore.WithoutFeaturingRank, store.Ranker()(Cluster(M)));
    }

    [TestMethod]
    public void Set_ReplacesTheLastPreview()
    {
        var store = Store(new Plan(SharedIdMatch.Close, A, B));
        store.Merged([A, B], M);

        store.Set([], DateTimeOffset.UnixEpoch.AddDays(1));

        Assert.AreEqual(SharedIdPreviewStore.OtherRank, store.Ranker()(Cluster(A, B)));
        Assert.AreEqual(SharedIdPreviewStore.OtherRank, store.Ranker()(Cluster(M)));
        Assert.IsEmpty(store.Preview.Plans);
    }

    [TestMethod]
    public void Defer_AClusterIsDeferredWhenAllItsSongsAre()
    {
        var store = new SharedIdPreviewStore();
        Assert.IsFalse(store.IsDeferred(Cluster(A, B)));

        store.Defer([A, B]);

        Assert.IsTrue(store.IsDeferred(Cluster(A, B)));
        Assert.IsTrue(store.IsDeferred(Cluster(B)), "Part of it, after the rest were merged elsewhere");
        Assert.IsFalse(store.IsDeferred(Cluster(A, B, C)), "A song joined it since");
        Assert.IsFalse(store.IsDeferred(Cluster()));
    }

    [TestMethod]
    public void Defer_IsKeptAcrossPreviews()
    {
        var store = Store(new Plan(SharedIdMatch.Close, A, B));
        store.Defer([A, B]);

        store.Set([], DateTimeOffset.UnixEpoch.AddDays(1));

        Assert.IsTrue(store.IsDeferred(Cluster(A, B)));
    }

    [TestMethod]
    public void Preview_SplitsTheMergesByTaskAndGroupsTheSkipsByReasonMostFirst()
    {
        var featuring = Guid.NewGuid();
        var preview = new SharedIdPreview(DateTimeOffset.UnixEpoch,
        [
            new SharedIdMergePlan(Cluster(A), null),
            new SharedIdMergePlan(Cluster(featuring), SharedIdMergePlan.WithoutFeaturing),
            new SharedIdMergePlan(Cluster(Guid.NewGuid()), SharedIdMergePlan.FewerArtists),
            new SharedIdMergePlan(Cluster(Guid.NewGuid()), SharedIdMergePlan.ServiceListsFewer),
            new SharedIdMergePlan(Cluster(B), SharedIdMergePlan.NoArtist),
            new SharedIdMergePlan(Cluster(C), SharedIdMergePlan.Different),
            new SharedIdMergePlan(Cluster(D), SharedIdMergePlan.Different)
        ]);

        Assert.AreEqual(A, preview.Merges.Single().Cluster.SongIds.Single());
        Assert.AreEqual(featuring, preview.FeaturingMerges.Single().Cluster.SongIds.Single());
        Assert.AreEqual(1, preview.UncheckedMerges.Count());
        Assert.AreEqual(1, preview.ListsFewerMerges.Count());
        CollectionAssert.AreEqual(new[] { SharedIdMergePlan.Different, SharedIdMergePlan.NoArtist },
            preview.Skips.Select(g => g.Key).ToList());
    }
}
