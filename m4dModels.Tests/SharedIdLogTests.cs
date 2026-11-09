using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace m4dModels.Tests;

/// <summary>
/// The shared-id review queue: the enrichment guard (CheckIds), seeding from the index,
/// clustering, and the merge / not-the-same / merge-all-matching actions.
/// </summary>
[TestClass]
public class SharedIdLogTests
{
    [ClassInitialize]
    public static async Task ClassInitialize(TestContext context)
    {
        await DanceMusicTester.LoadDances();
    }

    private static async Task<(DanceMusicService dms, ApplicationUser user)> CreateService(string name)
    {
        var dms = await DanceMusicTester.CreateService(name, useTestSongIndex: true);
        await DanceMusicTester.AddUser(dms, "dwgray", false);
        return (dms, await dms.FindUser("dwgray"));
    }

    private static async Task<Song> SaveSong(DanceMusicService dms, string purchase,
        string title = "Shared Song", string artist = "The Artist")
    {
        var song = await Song.Create(
            $".Create=\tUser=dwgray\tTime=01/01/2020 10:00:00 AM\tTitle={title}\tArtist={artist}\t" +
            $"Album:00=Album\tTrack:00=1\t{purchase}\tDanceRating=SLS+1",
            dms);
        await dms.SongIndex.SaveSong(song);
        return song;
    }

    private static Task<List<SharedIdCandidate>> Rows(DanceMusicService dms) =>
        dms.Context.SharedIdCandidates.OrderBy(c => c.Id).ToListAsync();

    private static (Guid, Guid) Pair(Song a, Song b) =>
        a.SongId.CompareTo(b.SongId) < 0 ? (a.SongId, b.SongId) : (b.SongId, a.SongId);

    [TestMethod]
    public async Task CheckIds_IdHeldByAnotherSong_ReturnsItAndQueuesThePair()
    {
        var (dms, _) = await CreateService("SharedIds_CheckHeld");
        var holder = await SaveSong(dms, "Purchase:00:IS=111");
        var song = await SaveSong(dms, "Purchase:00:SS=sp1", title: "Other Song");

        var held = await dms.SharedIds.CheckIds(song,
            new Dictionary<string, string> { ["I:111"] = "iTunes 111", ["R:USRC10000001"] = "iTunes 111" },
            "iTunes lookup");

        CollectionAssert.AreEquivalent(new[] { "I:111" }, held.ToList());
        var row = (await Rows(dms)).Single();
        Assert.AreEqual(Pair(holder, song), (row.SongA, row.SongB));
        Assert.AreEqual("I:111", row.ServiceIds);
        Assert.AreEqual("iTunes lookup", row.Source);
        Assert.AreEqual("iTunes 111", row.Detail);
        Assert.AreEqual(SharedIdStatus.Pending, row.Status);
    }

    [TestMethod]
    public async Task CheckIds_SongNotInCatalog_NeverBlocks()
    {
        var (dms, _) = await CreateService("SharedIds_CheckNew");
        _ = await SaveSong(dms, "Purchase:00:IS=111");
        var unsaved = await Song.Create(
            ".Create=\tUser=dwgray\tTitle=New Song\tArtist=The Artist\tAlbum:00=Album\tPurchase:00:SS=sp1", dms);

        var held = await dms.SharedIds.CheckIds(unsaved,
            new Dictionary<string, string> { ["I:111"] = "iTunes 111" }, "iTunes lookup");

        Assert.AreEqual(0, held.Count);
        Assert.AreEqual(0, (await Rows(dms)).Count);
    }

    [TestMethod]
    public async Task CheckIds_IdTheSongAlreadyHolds_IsNotAConflict()
    {
        var (dms, _) = await CreateService("SharedIds_CheckOwned");
        _ = await SaveSong(dms, "Purchase:00:IS=111");
        var song = await SaveSong(dms, "Purchase:00:IS=111", title: "Other Song");

        var held = await dms.SharedIds.CheckIds(song,
            new Dictionary<string, string> { ["I:111"] = "iTunes 111" }, "iTunes lookup");

        Assert.AreEqual(0, held.Count);
        Assert.AreEqual(0, (await Rows(dms)).Count);
    }

    [TestMethod]
    public async Task CheckIds_SamePairAgain_AddsIdsToTheOneRow()
    {
        var (dms, _) = await CreateService("SharedIds_CheckAgain");
        _ = await SaveSong(dms, "Purchase:00:IS=111\tPurchase:00:RS=USRC10000001");
        var song = await SaveSong(dms, "Purchase:00:SS=sp1", title: "Other Song");

        _ = await dms.SharedIds.CheckIds(song,
            new Dictionary<string, string> { ["I:111"] = "iTunes 111" }, "iTunes lookup");
        _ = await dms.SharedIds.CheckIds(song,
            new Dictionary<string, string> { ["R:USRC10000001"] = "Spotify sp1" }, "ISRC lookup");

        var row = (await Rows(dms)).Single();
        Assert.AreEqual("I:111,R:USRC10000001", row.ServiceIds);
    }

    [TestMethod]
    public async Task CheckIds_RejectedPair_StillSkipsTheIdButStaysRejected()
    {
        var (dms, user) = await CreateService("SharedIds_CheckRejected");
        _ = await SaveSong(dms, "Purchase:00:IS=111");
        var song = await SaveSong(dms, "Purchase:00:SS=sp1", title: "Other Song");
        var claims = new Dictionary<string, string> { ["I:111"] = "iTunes 111" };
        _ = await dms.SharedIds.CheckIds(song, claims, "iTunes lookup");
        await dms.SharedIds.Reject(user, (await Rows(dms)).Select(r => r.Id));

        var held = await dms.SharedIds.CheckIds(song, claims, "iTunes lookup");

        CollectionAssert.AreEquivalent(new[] { "I:111" }, held.ToList());
        var row = (await Rows(dms)).Single();
        Assert.AreEqual(SharedIdStatus.Rejected, row.Status);
        Assert.AreEqual("dwgray", row.ResolvedBy);
    }

    [TestMethod]
    public async Task Seed_QueuesEveryPairSharingAnId_Once()
    {
        var (dms, _) = await CreateService("SharedIds_Seed");
        var a = await SaveSong(dms, "Purchase:00:IS=111\tPurchase:00:RS=USRC10000001");
        var b = await SaveSong(dms, "Purchase:00:IS=111\tPurchase:00:RS=USRC10000001");
        var c = await SaveSong(dms, "Purchase:00:SS=sp1\tPurchase:00:RS=USRC10000001");
        _ = await SaveSong(dms, "Purchase:00:SS=sp2");

        Assert.AreEqual(3, await dms.SharedIds.Seed());
        Assert.AreEqual(0, await dms.SharedIds.Seed(), "a second seed adds nothing");

        var rows = await Rows(dms);
        Assert.AreEqual(3, rows.Count);
        var ab = rows.Single(r => (r.SongA, r.SongB) == Pair(a, b));
        Assert.AreEqual("I:111,R:USRC10000001", ab.ServiceIds);
        Assert.AreEqual(SharedIdLog.SeedSource, ab.Source);
        Assert.AreEqual("R:USRC10000001", rows.Single(r => (r.SongA, r.SongB) == Pair(a, c)).ServiceIds);
    }

    [TestMethod]
    public async Task PendingClusters_GroupsConnectedSongs()
    {
        var (dms, _) = await CreateService("SharedIds_Clusters");
        var a = await SaveSong(dms, "Purchase:00:IS=111");
        var b = await SaveSong(dms, "Purchase:00:IS=111\tPurchase:00:SS=sp1");
        var c = await SaveSong(dms, "Purchase:00:SS=sp1");
        var d = await SaveSong(dms, "Purchase:00:IS=222");
        var e = await SaveSong(dms, "Purchase:00:IS=222");
        _ = await dms.SharedIds.Seed();

        var clusters = await dms.SharedIds.PendingClusters();

        Assert.AreEqual(2, clusters.Count);
        var abc = clusters.Single(cl => cl.SongIds.Count == 3);
        CollectionAssert.AreEquivalent(new[] { a.SongId, b.SongId, c.SongId }, abc.SongIds.ToList());
        CollectionAssert.AreEquivalent(new[] { "I:111", "S:sp1" }, abc.ServiceIds.ToList());
        CollectionAssert.AreEquivalent(new[] { d.SongId, e.SongId },
            clusters.Single(cl => cl.SongIds.Count == 2).SongIds.ToList());
    }

    [TestMethod]
    public async Task Merge_MarksPairsMerged_AndMovesTheOthersToTheMergedSong()
    {
        var (dms, user) = await CreateService("SharedIds_Merge");
        var a = await SaveSong(dms, "Purchase:00:IS=111");
        var b = await SaveSong(dms, "Purchase:00:IS=111\tPurchase:00:SS=sp1");
        var c = await SaveSong(dms, "Purchase:00:SS=sp1", title: "Different Song");
        _ = await dms.SharedIds.Seed();

        var merged = await dms.SharedIds.Merge(user, [a.SongId, b.SongId]);

        Assert.IsNotNull(merged);
        var rows = await Rows(dms);
        Assert.AreEqual(SharedIdStatus.Merged, rows.Single(r => (r.SongA, r.SongB) == Pair(a, b)).Status);
        var moved = rows.Single(r => r.Status == SharedIdStatus.Pending);
        CollectionAssert.AreEquivalent(new[] { merged.SongId, c.SongId }, new[] { moved.SongA, moved.SongB });
        CollectionAssert.Contains(merged.GetExtendedPurchaseIds().ToList(), "S:sp1");
    }

    [TestMethod]
    public async Task Merge_KeepsARejectionWithTheMergedSong()
    {
        var (dms, user) = await CreateService("SharedIds_MergeKeepsRejection");
        var a = await SaveSong(dms, "Purchase:00:IS=111\tPurchase:00:SS=sp1");
        var b = await SaveSong(dms, "Purchase:00:IS=111");
        var c = await SaveSong(dms, "Purchase:00:SS=sp1", title: "Different Song");
        _ = await dms.SharedIds.Seed();
        var ac = (await Rows(dms)).Single(r => (r.SongA, r.SongB) == Pair(a, c));
        await dms.SharedIds.Reject(user, [ac.Id]);

        var merged = await dms.SharedIds.Merge(user, [a.SongId, b.SongId]);

        var rejected = (await Rows(dms)).Single(r => r.Status == SharedIdStatus.Rejected);
        CollectionAssert.AreEquivalent(new[] { merged.SongId, c.SongId }, new[] { rejected.SongA, rejected.SongB });
        Assert.AreEqual(0, (await dms.SharedIds.PendingClusters()).Count);
    }

    [TestMethod]
    public async Task LoadSongs_SongMergedElsewhere_ResolvesTheRow()
    {
        var (dms, user) = await CreateService("SharedIds_MergedElsewhere");
        var a = await SaveSong(dms, "Purchase:00:IS=111");
        var b = await SaveSong(dms, "Purchase:00:IS=111");
        _ = await dms.SharedIds.Seed();
        _ = await dms.SongIndex.SimpleMergeSongs(user, [a, b]);

        var cluster = (await dms.SharedIds.PendingClusters()).Single();
        Assert.IsFalse(await dms.SharedIds.LoadSongs(cluster));

        Assert.AreEqual(SharedIdStatus.Merged, (await Rows(dms)).Single().Status);
        Assert.AreEqual(0, (await dms.SharedIds.PendingClusters()).Count);
    }

    [TestMethod]
    public async Task MergeMatching_MergesOnlyClustersWithTheSameTitleAndArtist()
    {
        var (dms, user) = await CreateService("SharedIds_MergeMatching");
        _ = await SaveSong(dms, "Purchase:00:IS=111");
        _ = await SaveSong(dms, "Purchase:00:IS=111", title: "shared song!");
        var differentTitle = await SaveSong(dms, "Purchase:00:IS=222", title: "First Title");
        _ = await SaveSong(dms, "Purchase:00:IS=222", title: "Second Title");
        _ = await SaveSong(dms, "Purchase:00:IS=333", artist: "One Artist");
        _ = await SaveSong(dms, "Purchase:00:IS=333", artist: "Another Artist");
        _ = await dms.SharedIds.Seed();

        Assert.AreEqual(1, await dms.SharedIds.MergeMatching(user));

        var rows = await Rows(dms);
        Assert.AreEqual(1, rows.Count(r => r.Status == SharedIdStatus.Merged));
        Assert.AreEqual(2, rows.Count(r => r.Status == SharedIdStatus.Pending));
        Assert.IsFalse((await dms.SongIndex.FindSong(differentTitle.SongId)).IsNull);
    }

    [TestMethod]
    public async Task MergeMatching_SkipsAClusterWithARejectedPair()
    {
        var (dms, user) = await CreateService("SharedIds_MergeMatchingRejected");
        // a-b is rejected, but a-c and b-c keep all three in one pending cluster
        var a = await SaveSong(dms, "Purchase:00:IS=111\tPurchase:00:RS=USRC10000001");
        var b = await SaveSong(dms, "Purchase:00:IS=111\tPurchase:00:SS=sp1");
        var c = await SaveSong(dms, "Purchase:00:SS=sp1\tPurchase:00:RS=USRC10000001");
        _ = await dms.SharedIds.Seed();
        var ab = (await Rows(dms)).Single(r => (r.SongA, r.SongB) == Pair(a, b));
        await dms.SharedIds.Reject(user, [ab.Id]);

        Assert.AreEqual(0, await dms.SharedIds.MergeMatching(user));
        Assert.IsFalse((await dms.SongIndex.FindSong(c.SongId)).IsNull);
    }

    [TestMethod]
    public async Task PlanMergeMatching_GivesEachClusterItsReasonAndMergesNothing()
    {
        var (dms, _) = await CreateService("SharedIds_PlanMergeMatching");
        _ = await SaveSong(dms, "Purchase:00:IS=111");
        _ = await SaveSong(dms, "Purchase:00:IS=111", title: "shared song!");
        _ = await SaveSong(dms, "Purchase:00:IS=222", title: "First Title");
        _ = await SaveSong(dms, "Purchase:00:IS=222", title: "Second Title");
        _ = await SaveSong(dms, "Purchase:00:IS=333", artist: "");
        _ = await SaveSong(dms, "Purchase:00:IS=333", artist: "");
        _ = await dms.SharedIds.Seed();

        var plans = await dms.SharedIds.PlanMergeMatching();

        var reasons = plans.ToDictionary(p => p.Cluster.ServiceIds.Single(), p => p.SkipReason);
        Assert.IsNull(reasons["I:111"]);
        Assert.AreEqual(SharedIdMergePlan.Different, reasons["I:222"]);
        Assert.AreEqual(SharedIdMergePlan.NoArtist, reasons["I:333"]);
        Assert.IsTrue((await Rows(dms)).All(r => r.Status == SharedIdStatus.Pending));
    }

    [TestMethod]
    public async Task PlanMergeMatching_FollowsASongMergedSinceItWasQueued()
    {
        var (dms, user) = await CreateService("SharedIds_PlanMergeMatchingStale");
        var a = await SaveSong(dms, "Purchase:00:IS=111");
        var b = await SaveSong(dms, "Purchase:00:IS=111\tPurchase:00:SS=sp1");
        var c = await SaveSong(dms, "Purchase:00:SS=sp1");
        _ = await dms.SharedIds.Seed();
        // Merged elsewhere: b's rows now point at a song the queue doesn't know yet
        var merged = await dms.SongIndex.SimpleMergeSongs(user, [b, c]);

        var plan = (await dms.SharedIds.PlanMergeMatching()).Single();

        CollectionAssert.AreEquivalent(new[] { a.SongId, merged.SongId }, plan.Cluster.SongIds.ToList());
        Assert.IsTrue(plan.WillMerge);
    }

    [TestMethod]
    public void TitleArtistMatch_RequiresAnArtist()
    {
        var songs = new[]
        {
            new Song { Title = "Shared Song", Artist = "" },
            new Song { Title = "Shared Song", Artist = "" }
        };

        Assert.IsFalse(SharedIdLog.TitleArtistMatch(songs));
    }
}
