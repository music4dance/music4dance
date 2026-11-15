using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace m4dModels.Tests;

/// <summary>
/// The shared-id review's value types: clustering candidate rows, a choice's groups, the removal
/// form value, and the summary of an Apply.
/// </summary>
[TestClass]
public class SharedIdTypesTests
{
    private static readonly Guid A = new("00000000-0000-0000-0000-00000000000a");
    private static readonly Guid B = new("00000000-0000-0000-0000-00000000000b");
    private static readonly Guid C = new("00000000-0000-0000-0000-00000000000c");
    private static readonly Guid D = new("00000000-0000-0000-0000-00000000000d");
    private static readonly Guid E = new("00000000-0000-0000-0000-00000000000e");

    private static SharedIdCandidate Row(int id, Guid a, Guid b, string ids = "I:1") =>
        new() { Id = id, SongA = a, SongB = b, ServiceIds = ids };

    [TestMethod]
    public void Group_ConnectsSongsThroughSharedSongs()
    {
        var clusters = SharedIdCluster.Group([Row(1, A, B), Row(2, D, E), Row(3, B, C)]);

        Assert.HasCount(2, clusters);
        CollectionAssert.AreEqual(new[] { A, B, C }, clusters[0].SongIds.ToList());
        CollectionAssert.AreEqual(new[] { 1, 3 }, clusters[0].Candidates.Select(c => c.Id).ToList());
        CollectionAssert.AreEqual(new[] { D, E }, clusters[1].SongIds.ToList());
    }

    [TestMethod]
    public void Group_JoinsTwoClustersALaterRowConnects()
    {
        // A-B and C-D look like two clusters until B-C joins them
        var clusters = SharedIdCluster.Group([Row(1, A, B), Row(2, C, D), Row(3, B, C)]);

        CollectionAssert.AreEquivalent(new[] { A, B, C, D }, clusters.Single().SongIds.ToList());
    }

    [TestMethod]
    public void Group_OrdersClustersByTheirOldestRow()
    {
        var clusters = SharedIdCluster.Group([Row(5, A, B), Row(2, C, D), Row(9, D, E)]);

        CollectionAssert.AreEqual(new[] { C, A }, clusters.Select(c => c.SongIds[0]).ToList());
    }

    [TestMethod]
    public void Group_NoRows_NoClusters()
    {
        Assert.IsEmpty(SharedIdCluster.Group([]));
    }

    [TestMethod]
    public void Cluster_ServiceIdsAndKeyIgnoreOrder()
    {
        var cluster = SharedIdCluster.Group([Row(1, B, A, "S:x,I:1"), Row(2, A, C, "I:1,R:US1")]).Single();

        CollectionAssert.AreEqual(new[] { "I:1", "R:US1", "S:x" }, cluster.ServiceIds.ToList());
        Assert.AreEqual(string.Join(",", new[] { A, B, C }), cluster.Key);
        Assert.AreEqual(SharedIdMatch.Different, cluster.Match, "Not loaded yet");
    }

    [TestMethod]
    public void MergeGroups_GroupsSongsAndLeavesOutTheUngrouped()
    {
        var choice = new SharedIdChoice(SharedIdAction.Merge, [A, B, C, D], [], [" B", "A", "", "B"]);

        var groups = choice.MergeGroups;

        Assert.HasCount(2, groups);
        CollectionAssert.AreEqual(new[] { A, D }, groups[0].ToList());
        CollectionAssert.AreEqual(new[] { B }, groups[1].ToList());
    }

    [TestMethod]
    public void MergeGroups_NoGroups_PutsEverySongInOne()
    {
        var choice = new SharedIdChoice(SharedIdAction.Merge, [A, B, C], []);

        CollectionAssert.AreEqual(new[] { A, B, C }, choice.MergeGroups.Single().ToList());
    }

    [TestMethod]
    public void MergeGroups_FewerGroupsThanSongs_LeavesTheRestOut()
    {
        var choice = new SharedIdChoice(SharedIdAction.Merge, [A, B, C], [], ["A", "A"]);

        CollectionAssert.AreEqual(new[] { A, B }, choice.MergeGroups.Single().ToList());
    }

    [TestMethod]
    public void Without_LeavesTheSongsOutOfEveryGroupAndDropsTheirRemovals()
    {
        var choice = new SharedIdChoice(SharedIdAction.Merge, [A, B, C], [], ["A", "B", "B"],
            Removals: [new SharedIdRemoval(B, 0), new SharedIdRemoval(C, 1, "S:x")]);

        var without = choice.Without([B]);

        CollectionAssert.AreEqual(new[] { "A", "", "B" }, without.Groups.ToList());
        Assert.AreEqual(C, without.Removals.Single().SongId);
        CollectionAssert.AreEqual(new[] { C }, without.MergeGroups[1].ToList());
        Assert.AreSame(choice, choice.Without([]));
    }

    [TestMethod]
    public void Without_NoGroups_PutsTheRestInOne()
    {
        var choice = new SharedIdChoice(SharedIdAction.Merge, [A, B, C], []);

        CollectionAssert.AreEqual(new[] { A, C }, choice.Without([B]).MergeGroups.Single().ToList());
    }

    [TestMethod]
    public void Summary_OnlyMentionsRemovalsAndStaleMergesWhenThereAreSome()
    {
        var plain = new SharedIdBatchResult { Merged = 2, Rejected = 1, Left = 3 }.Summary();
        var full = new SharedIdBatchResult { Merged = 1, Edited = 2, UnsharedPairs = 1, Stale = 1, Deleted = 2 }.Summary();

        Assert.AreEqual("Merged 2, marked 1 not the same, left 3.", plain);
        StringAssert.StartsWith(full, "Merged 1, marked 0 not the same, left 0.");
        StringAssert.Contains(full, "Removed albums or ids from 2 songs; 1 pairs no longer shared an id");
        StringAssert.Contains(full, "1 weren't merged");
        StringAssert.Contains(full, "Deleted 2 songs.");
        Assert.DoesNotContain("Deleted", plain);
    }
}
