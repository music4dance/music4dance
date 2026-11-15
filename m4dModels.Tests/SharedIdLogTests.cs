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
    public async Task PreviewMerge_ShowsTheMergedSongAndSavesNothing()
    {
        var (dms, user) = await CreateService("SharedIds_PreviewMerge");
        var a = await SaveSong(dms, "Purchase:00:IS=111");
        var b = await SaveSong(dms, "Purchase:00:IS=111\tPurchase:00:SS=sp1");
        _ = await dms.SharedIds.Seed();
        var cluster = (await dms.SharedIds.PendingClusters()).Single();
        Assert.IsTrue(await dms.SharedIds.LoadSongs(cluster));

        var preview = await dms.SharedIds.PreviewMerge(user, cluster);

        Assert.AreEqual("Shared Song", preview.Title);
        CollectionAssert.IsSubsetOf(new[] { "I:111", "S:sp1" }, preview.GetExtendedPurchaseIds().ToList());
        Assert.IsNull(await dms.SongIndex.FindSong(preview.SongId));
        Assert.IsFalse((await dms.SongIndex.FindSong(a.SongId)).IsNull);
        Assert.IsFalse((await dms.SongIndex.FindSong(b.SongId)).IsNull);
        Assert.AreEqual(SharedIdStatus.Pending, (await Rows(dms)).Single().Status);
    }

    [TestMethod]
    public async Task Apply_MergesRejectsAndLeavesEachCluster()
    {
        var (dms, user) = await CreateService("SharedIds_Apply");
        var a = await SaveSong(dms, "Purchase:00:IS=111");
        var b = await SaveSong(dms, "Purchase:00:IS=111");
        _ = await SaveSong(dms, "Purchase:00:IS=222");
        _ = await SaveSong(dms, "Purchase:00:IS=222", title: "Other Song");
        _ = await SaveSong(dms, "Purchase:00:IS=333");
        _ = await SaveSong(dms, "Purchase:00:IS=333");
        _ = await dms.SharedIds.Seed();
        var clusters = (await dms.SharedIds.PendingClusters())
            .ToDictionary(c => c.ServiceIds.Single());
        SharedIdChoice Choose(string id, SharedIdAction action) =>
            new(action, clusters[id].SongIds, [.. clusters[id].Candidates.Select(c => c.Id)]);

        var result = await dms.SharedIds.Apply(user,
        [
            Choose("I:111", SharedIdAction.Merge),
            Choose("I:222", SharedIdAction.Reject),
            Choose("I:333", SharedIdAction.Leave)
        ]);

        Assert.AreEqual((1, 1, 1, 0), (result.Merged, result.Rejected, result.Left, result.Stale));
        Assert.IsTrue((await dms.SongIndex.FindSong(a.SongId)).IsNull);
        Assert.IsTrue((await dms.SongIndex.FindSong(b.SongId)).IsNull);
        var status = (await Rows(dms)).ToDictionary(r => r.ServiceIds, r => r.Status);
        Assert.AreEqual(SharedIdStatus.Merged, status["I:111"]);
        Assert.AreEqual(SharedIdStatus.Rejected, status["I:222"]);
        Assert.AreEqual(SharedIdStatus.Pending, status["I:333"]);
    }

    [TestMethod]
    public async Task Apply_MergeOfSongsAlreadyMerged_CountsAsStale()
    {
        var (dms, user) = await CreateService("SharedIds_ApplyStale");
        var a = await SaveSong(dms, "Purchase:00:IS=111");
        var b = await SaveSong(dms, "Purchase:00:IS=111");
        _ = await dms.SharedIds.Seed();
        var cluster = (await dms.SharedIds.PendingClusters()).Single();
        // Merged in another tab (Merge and edit) after the page loaded
        _ = await dms.SharedIds.Merge(user, [a.SongId, b.SongId]);

        var result = await dms.SharedIds.Apply(user,
            [new SharedIdChoice(SharedIdAction.Merge, cluster.SongIds, [])]);

        Assert.AreEqual((0, 1), (result.Merged, result.Stale));
    }

    // Songs 1 and 2 share I:111, 3 and 4 share I:222, and 2 and 3 share S:sp1, so all four are one
    // cluster
    private static async Task<(SharedIdCluster cluster, Song[] songs)> FourSongCluster(DanceMusicService dms)
    {
        Song[] songs =
        [
            await SaveSong(dms, "Purchase:00:IS=111", title: "First Song (feat. Crazy Man)"),
            await SaveSong(dms, "Purchase:00:IS=111\tPurchase:00:SS=sp1", title: "First Song"),
            await SaveSong(dms, "Purchase:00:IS=222\tPurchase:00:SS=sp1", title: "Second Song"),
            await SaveSong(dms, "Purchase:00:IS=222", title: "Second Song (Radio Edit)")
        ];
        _ = await dms.SharedIds.Seed();
        var cluster = (await dms.SharedIds.PendingClusters()).Single();
        Assert.HasCount(4, cluster.SongIds);
        return (cluster, songs);
    }

    private static SharedIdChoice Groups(SharedIdCluster cluster, Song[] songs, string[] groups,
        SharedIdNamePicks names = null)
    {
        // The choice lists the songs in column order, which is the order they were created here
        var ids = songs.Select(s => s.SongId).ToList();
        CollectionAssert.AreEquivalent(ids, cluster.SongIds.ToList());
        return new SharedIdChoice(SharedIdAction.Merge, ids, [.. cluster.Candidates.Select(c => c.Id)],
            groups, names ?? new SharedIdNamePicks());
    }

    [TestMethod]
    public async Task Apply_TwoGroups_MergesEachAndRejectsThePairBetweenThem()
    {
        var (dms, user) = await CreateService("SharedIds_ApplyGroups");
        var (cluster, songs) = await FourSongCluster(dms);

        var result = await dms.SharedIds.Apply(user, [Groups(cluster, songs, ["A", "A", "B", "B"])]);

        Assert.AreEqual((2, 1, 0), (result.Merged, result.Rejected, result.Stale));
        Assert.HasCount(2, result.Merges);
        var first = await dms.SongIndex.FindSong(result.Merges[0].Into);
        var second = await dms.SongIndex.FindSong(result.Merges[1].Into);
        // Each group gets the feat. cleanup of its own merge
        Assert.AreEqual("First Song", first.Title);
        CollectionAssert.AreEqual(new[] { "The Artist", "Crazy Man" }, first.EffectiveArtists.ToList());
        Assert.AreEqual("Second Song (Radio Edit)", second.Title);
        Assert.IsEmpty(await dms.SharedIds.PendingClusters());
        var rejected = (await Rows(dms)).Single(r => r.Status == SharedIdStatus.Rejected);
        Assert.AreEqual(Pair(first, second), (rejected.SongA, rejected.SongB));
    }

    [TestMethod]
    public async Task Apply_SongLeftOut_StaysPendingWithTheMergedSong()
    {
        var (dms, user) = await CreateService("SharedIds_ApplyLeaveOut");
        var (cluster, songs) = await FourSongCluster(dms);

        var result = await dms.SharedIds.Apply(user, [Groups(cluster, songs, ["A", "A", "B", ""])]);

        Assert.AreEqual((1, 1), (result.Merged, result.Rejected));
        var merged = result.Merges.Single().Into;
        var pending = (await Rows(dms)).Where(r => r.Status == SharedIdStatus.Pending).ToList();
        // Song 4 shares I:222 with song 3 only, so it stays queued with song 3, which was rejected
        // against the merged song
        Assert.AreEqual(Pair(songs[2], songs[3]), (pending.Single().SongA, pending.Single().SongB));
        var rejected = (await Rows(dms)).Single(r => r.Status == SharedIdStatus.Rejected);
        CollectionAssert.AreEquivalent(new[] { merged, songs[2].SongId }, new[] { rejected.SongA, rejected.SongB });
    }

    [TestMethod]
    public async Task Apply_NamePickedFromASong_AppliesOnlyToThatSongsGroup()
    {
        var (dms, user) = await CreateService("SharedIds_ApplyGroupNames");
        var (cluster, songs) = await FourSongCluster(dms);

        // The title of song 3, which is in group B (the merge alone gives song 4's)
        var result = await dms.SharedIds.Apply(user,
            [Groups(cluster, songs, ["A", "A", "B", "B"], new SharedIdNamePicks(Title: songs[2].SongId))]);

        var first = await dms.SongIndex.FindSong(result.Merges[0].Into);
        var second = await dms.SongIndex.FindSong(result.Merges[1].Into);
        Assert.AreEqual("First Song", first.Title);
        Assert.AreEqual("Second Song", second.Title);
    }

    [TestMethod]
    public async Task Apply_OneGroup_MergesEverySongAsBefore()
    {
        var (dms, user) = await CreateService("SharedIds_ApplyOneGroup");
        var (cluster, songs) = await FourSongCluster(dms);

        var result = await dms.SharedIds.Apply(user, [Groups(cluster, songs, ["A", "A", "A", "A"])]);

        Assert.AreEqual((1, 0), (result.Merged, result.Rejected));
        Assert.IsTrue((await Rows(dms)).All(r => r.Status == SharedIdStatus.Merged));
    }

    // Two songs that share I:111; the second also has S:sp2
    private static async Task<(SharedIdCluster cluster, Song[] songs)> TwoSongCluster(DanceMusicService dms)
    {
        Song[] songs =
        [
            await SaveSong(dms, "Purchase:00:IS=111"),
            await SaveSong(dms, "Purchase:00:IS=111	Purchase:00:SS=sp2", title: "Other Song")
        ];
        _ = await dms.SharedIds.Seed();
        var cluster = (await dms.SharedIds.PendingClusters()).Single();
        CollectionAssert.AreEquivalent(songs.Select(s => s.SongId).ToList(), cluster.SongIds.ToList());
        return (cluster, songs);
    }

    private static SharedIdChoice Removing(SharedIdAction action, SharedIdCluster cluster, Song[] songs,
        params SharedIdRemoval[] removals) =>
        new(action, [.. songs.Select(s => s.SongId)], [.. cluster.Candidates.Select(c => c.Id)],
            Removals: removals);

    [TestMethod]
    public async Task Apply_RemoveTheSharedId_TakesItOffThatSongAndRejectsThePair()
    {
        var (dms, user) = await CreateService("SharedIds_RemoveId");
        var (cluster, songs) = await TwoSongCluster(dms);

        var result = await dms.SharedIds.Apply(user,
            [Removing(SharedIdAction.Leave, cluster, songs, new SharedIdRemoval(songs[1].SongId, 0, "I:111"))]);

        Assert.AreEqual((1, 1), (result.Edited, result.UnsharedPairs));
        var edited = await dms.SongIndex.FindSong(songs[1].SongId);
        CollectionAssert.AreEqual(new[] { "S:sp2" }, edited.GetExtendedPurchaseIds().ToList());
        Assert.AreEqual("Album", edited.Albums.Single().Name);
        CollectionAssert.AreEqual(new[] { "I:111" },
            (await dms.SongIndex.FindSong(songs[0].SongId)).GetExtendedPurchaseIds().ToList());
        Assert.AreEqual(SharedIdStatus.Rejected, (await Rows(dms)).Single().Status);
        Assert.IsEmpty(await dms.SharedIds.PendingClusters());
    }

    [TestMethod]
    public async Task Apply_RemoveTheAlbum_TakesTheAlbumOffThatSong()
    {
        var (dms, user) = await CreateService("SharedIds_RemoveAlbum");
        var (cluster, songs) = await TwoSongCluster(dms);

        var result = await dms.SharedIds.Apply(user,
            [Removing(SharedIdAction.Leave, cluster, songs, new SharedIdRemoval(songs[0].SongId, 0))]);

        Assert.AreEqual((1, 1), (result.Edited, result.UnsharedPairs));
        Assert.IsEmpty((await dms.SongIndex.FindSong(songs[0].SongId)).Albums);
        Assert.AreEqual(SharedIdStatus.Rejected, (await Rows(dms)).Single().Status);
    }

    [TestMethod]
    public async Task Apply_RemoveAnIdTheyDoNotOnlyShare_LeavesThePairPending()
    {
        var (dms, user) = await CreateService("SharedIds_RemoveUnshared");
        var (cluster, songs) = await TwoSongCluster(dms);

        var result = await dms.SharedIds.Apply(user,
            [Removing(SharedIdAction.Leave, cluster, songs, new SharedIdRemoval(songs[1].SongId, 0, "S:sp2"))]);

        Assert.AreEqual((1, 0), (result.Edited, result.UnsharedPairs));
        CollectionAssert.AreEqual(new[] { "I:111" },
            (await dms.SongIndex.FindSong(songs[1].SongId)).GetExtendedPurchaseIds().ToList());
        Assert.AreEqual(SharedIdStatus.Pending, (await Rows(dms)).Single().Status);
    }

    [TestMethod]
    public async Task Apply_RemoveAnIdTheSongDoesNotHave_ChangesNothing()
    {
        var (dms, user) = await CreateService("SharedIds_RemoveMissing");
        var (cluster, songs) = await TwoSongCluster(dms);

        var result = await dms.SharedIds.Apply(user,
            [Removing(SharedIdAction.Leave, cluster, songs, new SharedIdRemoval(songs[0].SongId, 0, "S:sp2"),
                new SharedIdRemoval(songs[0].SongId, 3))]);

        Assert.AreEqual(0, result.Edited);
        Assert.AreEqual(SharedIdStatus.Pending, (await Rows(dms)).Single().Status);
    }

    [TestMethod]
    public async Task Apply_RemoveAnIdThenMergeGroups_TheMergedSongLacksIt()
    {
        var (dms, user) = await CreateService("SharedIds_RemoveThenMerge");
        var (cluster, songs) = await FourSongCluster(dms);
        var choice = Groups(cluster, songs, ["A", "A", "B", "B"]) with
        {
            Removals = [new SharedIdRemoval(songs[2].SongId, 0, "S:sp1")]
        };

        var result = await dms.SharedIds.Apply(user, [choice]);

        Assert.AreEqual((1, 2), (result.Edited, result.Merged));
        var second = await dms.SongIndex.FindSong(result.Merges[1].Into);
        CollectionAssert.AreEquivalent(new[] { "I:222" }, second.GetExtendedPurchaseIds().Distinct().ToList());
        Assert.IsEmpty(await dms.SharedIds.PendingClusters());
    }

    [TestMethod]
    public async Task Merge_WithNames_AppendsAMusic4danceEditForWhatTheMergeDoesNotGive()
    {
        var (dms, user) = await CreateService("SharedIds_MergeNames");
        // The merge alone takes the later song's title, the one with the feat.
        var a = await SaveSong(dms, "Purchase:00:IS=111");
        var b = await SaveSong(dms, "Purchase:00:IS=111", title: "Shared Song (feat. Crazy Man)");
        Assert.AreEqual(b.Title, (await dms.SongIndex.BuildSimpleMerge(user, [a, b])).Title);

        var merged = await dms.SharedIds.Merge(user, [a.SongId, b.SongId],
            new SongNames("Shared Song", "The Artist", ["The Artist", "Crazy Man"]));

        Assert.AreEqual("Shared Song", merged.Title);
        CollectionAssert.AreEqual(new[] { "The Artist", "Crazy Man" }, merged.EffectiveArtists.ToList());
        var last = merged.SongProperties.FindLastIndex(p => p.Name == Song.EditCommand);
        Assert.IsTrue(last > merged.SongProperties.FindLastIndex(p => p.Name == Song.MergeCommand));
        Assert.AreEqual(Song.M4dUser, merged.SongProperties[last + 1].Value);
        Assert.AreEqual("Shared Song", (await dms.SongIndex.FindSong(merged.SongId)).Title);
    }

    [TestMethod]
    public async Task Merge_WithNamesTheMergeAlreadyGives_AddsNoEdit()
    {
        var (dms, user) = await CreateService("SharedIds_MergeSameNames");
        var a = await SaveSong(dms, "Purchase:00:IS=111");
        var b = await SaveSong(dms, "Purchase:00:IS=111");

        var merged = await dms.SharedIds.Merge(user, [a.SongId, b.SongId],
            new SongNames("Shared Song", "The Artist", ["The Artist"]));

        Assert.AreEqual(Song.MergeCommand,
            merged.SongProperties.Last(p => p.IsAction).Name);
    }

    #region Following songs that changed elsewhere

    [TestMethod]
    public async Task LoadSongs_SongDeleted_MarksItsRowsDeleted()
    {
        var (dms, user) = await CreateService("SharedIds_Deleted");
        var a = await SaveSong(dms, "Purchase:00:IS=111");
        _ = await SaveSong(dms, "Purchase:00:IS=111");
        _ = await dms.SharedIds.Seed();
        await dms.SongIndex.DeleteSong(user, await dms.SongIndex.FindSong(a.SongId));

        var cluster = (await dms.SharedIds.PendingClusters()).Single();
        Assert.IsFalse(await dms.SharedIds.LoadSongs(cluster));

        Assert.AreEqual(SharedIdStatus.Deleted, (await Rows(dms)).Single().Status);
        Assert.IsEmpty(await dms.SharedIds.PendingClusters());
    }

    [TestMethod]
    public async Task Merge_TwoRowsLandingOnOnePair_FoldIntoOneWithBothIds()
    {
        var (dms, user) = await CreateService("SharedIds_Fold");
        // a and b each share a different id with c, and nothing with each other
        var a = await SaveSong(dms, "Purchase:00:IS=111");
        var b = await SaveSong(dms, "Purchase:00:SS=sp1");
        var c = await SaveSong(dms, "Purchase:00:IS=111\tPurchase:00:SS=sp1", title: "Other Song");
        _ = await dms.SharedIds.Seed();
        Assert.HasCount(2, await Rows(dms));

        var merged = await dms.SharedIds.Merge(user, [a.SongId, b.SongId]);

        var row = (await Rows(dms)).Single();
        Assert.AreEqual(Pair(merged, c), (row.SongA, row.SongB));
        Assert.AreEqual("I:111,S:sp1", row.ServiceIds);
        Assert.AreEqual(SharedIdStatus.Pending, row.Status);
    }

    [TestMethod]
    public async Task Merge_RejectedAndPendingRowsLandingOnOnePair_StayRejected()
    {
        var (dms, user) = await CreateService("SharedIds_FoldRejected");
        var a = await SaveSong(dms, "Purchase:00:IS=111");
        var b = await SaveSong(dms, "Purchase:00:SS=sp1");
        var c = await SaveSong(dms, "Purchase:00:IS=111\tPurchase:00:SS=sp1", title: "Other Song");
        _ = await dms.SharedIds.Seed();
        var ac = (await Rows(dms)).Single(r => (r.SongA, r.SongB) == Pair(a, c));
        await dms.SharedIds.Reject(user, [ac.Id]);

        var merged = await dms.SharedIds.Merge(user, [a.SongId, b.SongId]);

        var row = (await Rows(dms)).Single();
        Assert.AreEqual(Pair(merged, c), (row.SongA, row.SongB));
        Assert.AreEqual(SharedIdStatus.Rejected, row.Status);
        Assert.IsEmpty(await dms.SharedIds.PendingClusters());
    }

    #endregion

    #region More of Apply

    [TestMethod]
    public async Task Apply_TwoGroupsOneStale_RejectsNothing()
    {
        var (dms, user) = await CreateService("SharedIds_ApplyGroupStale");
        var (cluster, songs) = await FourSongCluster(dms);
        // Group A was merged in another tab after the page loaded
        var mergedA = await dms.SharedIds.Merge(user, [songs[0].SongId, songs[1].SongId]);

        var result = await dms.SharedIds.Apply(user, [Groups(cluster, songs, ["A", "A", "B", "B"])]);

        Assert.AreEqual((1, 1, 0), (result.Merged, result.Stale, result.Rejected));
        var rows = await Rows(dms);
        Assert.IsFalse(rows.Any(r => r.Status == SharedIdStatus.Rejected));
        var pending = rows.Single(r => r.Status == SharedIdStatus.Pending);
        Assert.AreEqual(Pair(mergedA, await dms.SongIndex.FindSong(result.Merges.Single().Into)),
            (pending.SongA, pending.SongB));
    }

    [TestMethod]
    public async Task Apply_RemoveAndNotTheSame_DoesBoth()
    {
        var (dms, user) = await CreateService("SharedIds_RemoveReject");
        var (cluster, songs) = await TwoSongCluster(dms);

        var result = await dms.SharedIds.Apply(user,
            [Removing(SharedIdAction.Reject, cluster, songs, new SharedIdRemoval(songs[1].SongId, 0, "S:sp2"))]);

        Assert.AreEqual((1, 1, 0), (result.Edited, result.Rejected, result.UnsharedPairs));
        CollectionAssert.AreEquivalent(new[] { "I:111" },
            (await dms.SongIndex.FindSong(songs[1].SongId)).GetExtendedPurchaseIds().Distinct().ToList());
        Assert.AreEqual(SharedIdStatus.Rejected, (await Rows(dms)).Single().Status);
    }

    [TestMethod]
    public async Task Apply_RemoveInAThreeSongCluster_RejectsOnlyThePairThatStopsSharing()
    {
        var (dms, user) = await CreateService("SharedIds_RemoveThree");
        Song[] songs =
        [
            await SaveSong(dms, "Purchase:00:IS=111"),
            await SaveSong(dms, "Purchase:00:IS=111\tPurchase:00:SS=sp1"),
            await SaveSong(dms, "Purchase:00:SS=sp1", title: "Other Song")
        ];
        _ = await dms.SharedIds.Seed();
        var cluster = (await dms.SharedIds.PendingClusters()).Single();

        var result = await dms.SharedIds.Apply(user,
            [Removing(SharedIdAction.Leave, cluster, songs, new SharedIdRemoval(songs[2].SongId, 0, "S:sp1"))]);

        Assert.AreEqual((1, 1), (result.Edited, result.UnsharedPairs));
        var rows = await Rows(dms);
        Assert.AreEqual(SharedIdStatus.Rejected, rows.Single(r => (r.SongA, r.SongB) == Pair(songs[1], songs[2])).Status);
        Assert.AreEqual(SharedIdStatus.Pending, rows.Single(r => (r.SongA, r.SongB) == Pair(songs[0], songs[1])).Status);
    }

    [TestMethod]
    public async Task Apply_DeleteASong_DeletesItAndMergesTheRest()
    {
        var (dms, user) = await CreateService("SharedIds_ApplyDelete");
        var (cluster, songs) = await FourSongCluster(dms);
        var choice = Groups(cluster, songs, ["A", "A", "A", "A"]) with { Deletions = [songs[3].SongId] };

        var result = await dms.SharedIds.Apply(user, [choice]);

        Assert.AreEqual((1, 1, 0), (result.Deleted, result.Merged, result.Stale));
        CollectionAssert.AreEquivalent(songs.Take(3).Select(s => s.SongId).ToList(),
            result.Merges.Single().From.ToList());
        Assert.IsTrue((await dms.SongIndex.FindSong(songs[3].SongId)).IsNull);
        Assert.IsFalse((await dms.SongIndex.FindSong(result.Merges.Single().Into)).IsNull);
        var deleted = (await Rows(dms)).Single(r => r.SongA == songs[3].SongId || r.SongB == songs[3].SongId);
        Assert.AreEqual(SharedIdStatus.Deleted, deleted.Status);
        Assert.IsEmpty(await dms.SharedIds.PendingClusters());
    }

    [TestMethod]
    public async Task Apply_DeleteWithLeaveForLater_TakesThePairOutOfTheQueue()
    {
        var (dms, user) = await CreateService("SharedIds_ApplyDeleteLeave");
        var (cluster, songs) = await TwoSongCluster(dms);
        var choice = new SharedIdChoice(SharedIdAction.Leave, [.. songs.Select(s => s.SongId)],
            [.. cluster.Candidates.Select(c => c.Id)], Deletions: [songs[1].SongId],
            Removals: [new SharedIdRemoval(songs[1].SongId, 0, "I:111")]);

        var result = await dms.SharedIds.Apply(user, [choice]);

        Assert.AreEqual((1, 1, 0), (result.Deleted, result.Left, result.Edited), "No removal from a deleted song");
        Assert.IsTrue((await dms.SongIndex.FindSong(songs[1].SongId)).IsNull);
        Assert.IsFalse((await dms.SongIndex.FindSong(songs[0].SongId)).IsNull);
        Assert.AreEqual(SharedIdStatus.Deleted, (await Rows(dms)).Single().Status);
        Assert.IsEmpty(await dms.SharedIds.PendingClusters());
    }

    [TestMethod]
    public async Task Apply_DeletionOfASongOutsideTheCluster_IsIgnored()
    {
        var (dms, user) = await CreateService("SharedIds_DeleteOutside");
        var (cluster, songs) = await TwoSongCluster(dms);
        var outsider = await SaveSong(dms, "Purchase:00:IS=999", title: "Outsider");
        var choice = new SharedIdChoice(SharedIdAction.Leave, [.. songs.Select(s => s.SongId)],
            [.. cluster.Candidates.Select(c => c.Id)], Deletions: [outsider.SongId]);

        var result = await dms.SharedIds.Apply(user, [choice]);

        Assert.AreEqual(0, result.Deleted);
        Assert.IsFalse((await dms.SongIndex.FindSong(outsider.SongId)).IsNull);
        Assert.AreEqual(SharedIdStatus.Pending, (await Rows(dms)).Single().Status);
    }

    [TestMethod]
    public async Task Apply_RemovalFromASongOutsideTheCluster_IsIgnored()
    {
        var (dms, user) = await CreateService("SharedIds_RemoveOutside");
        var (cluster, songs) = await TwoSongCluster(dms);
        var outsider = await SaveSong(dms, "Purchase:00:IS=999", title: "Outsider");

        var result = await dms.SharedIds.Apply(user,
            [Removing(SharedIdAction.Leave, cluster, songs, new SharedIdRemoval(outsider.SongId, 0, "I:999"))]);

        Assert.AreEqual(0, result.Edited);
        CollectionAssert.Contains((await dms.SongIndex.FindSong(outsider.SongId)).GetExtendedPurchaseIds().ToList(), "I:999");
    }

    [TestMethod]
    public async Task Apply_NamePickedFromASongOutsideEveryGroup_GetsTheProposal()
    {
        var (dms, user) = await CreateService("SharedIds_ApplyNameOutside");
        var (cluster, songs) = await FourSongCluster(dms);

        // Song 4 is left out, so group A doesn't take its title
        var result = await dms.SharedIds.Apply(user,
            [Groups(cluster, songs, ["A", "A", "A", ""], new SharedIdNamePicks(Title: songs[3].SongId))]);

        Assert.AreNotEqual(songs[3].Title, (await dms.SongIndex.FindSong(result.Merges.Single().Into)).Title);
    }

    #endregion

    #region Merge all reasons

    [TestMethod]
    public async Task PlanMergeMatching_GivesTheNoMergeFeatAndCloseReasons()
    {
        var (dms, _) = await CreateService("SharedIds_PlanReasons");
        var noMerge = await Song.Create(
            ".Create=\tUser=dwgray\tTime=01/01/2020 10:00:00 AM\tTitle=Shared Song\tArtist=The Artist\t" +
            "Album:00=Album\tTrack:00=1\tPurchase:00:IS=111\tDanceRating=SLS+1\t" +
            ".NoMerge=\tUser=dwgray\tTime=01/02/2020 10:00:00 AM",
            dms);
        await dms.SongIndex.SaveSong(noMerge);
        _ = await SaveSong(dms, "Purchase:00:IS=111");
        _ = await SaveSong(dms, "Purchase:00:IS=222", title: "Feat Song feat. Crazy Man");
        _ = await SaveSong(dms, "Purchase:00:IS=222", title: "Feat Song");
        _ = await SaveSong(dms, "Purchase:00:IS=333", title: "Close Song - 2011 Remaster");
        _ = await SaveSong(dms, "Purchase:00:IS=333", title: "Close Song");
        _ = await dms.SharedIds.Seed();

        var plans = (await dms.SharedIds.PlanMergeMatching()).ToDictionary(p => p.Cluster.ServiceIds.Single());

        Assert.AreEqual(SharedIdMergePlan.NoMerge, plans["I:111"].SkipReason);
        Assert.AreEqual(SharedIdMergePlan.WithoutFeaturing, plans["I:222"].SkipReason);
        Assert.AreEqual(SharedIdMatch.WithoutFeaturing, plans["I:222"].Match);
        Assert.AreEqual(SharedIdMergePlan.Close, plans["I:333"].SkipReason);
        Assert.AreEqual(SharedIdMatch.Close, plans["I:333"].Match);
    }

    #endregion

    #region LoadPage

    // Three two-song clusters, I:111 oldest
    private static async Task ThreeClusters(DanceMusicService dms)
    {
        foreach (var id in new[] { "111", "222", "333" })
        {
            _ = await SaveSong(dms, $"Purchase:00:IS={id}", title: $"Song {id} feat. Crazy Man");
            _ = await SaveSong(dms, $"Purchase:00:IS={id}", title: $"Song {id}");
            _ = await dms.SharedIds.Seed();
        }
    }

    [TestMethod]
    public async Task LoadPage_PagesTheClustersAndPreviewsEach()
    {
        var (dms, user) = await CreateService("SharedIds_LoadPage");
        await ThreeClusters(dms);

        var first = await dms.SharedIds.LoadPage(user, 1, pageSize: 2);
        var second = await dms.SharedIds.LoadPage(user, 2, pageSize: 2);
        var past = await dms.SharedIds.LoadPage(user, 5, pageSize: 2);

        Assert.AreEqual((3, 3, 2), (first.PendingCandidates, first.ClusterCount, first.PageCount));
        CollectionAssert.AreEqual(new[] { "I:111", "I:222" },
            first.Clusters.Select(c => c.Cluster.ServiceIds.Single()).ToList());
        var entry = second.Clusters.Single();
        Assert.AreEqual("I:333", entry.Cluster.ServiceIds.Single());
        Assert.HasCount(2, entry.Cluster.Songs);
        Assert.AreEqual(SharedIdMatch.WithoutFeaturing, entry.Cluster.Match);
        Assert.IsNotNull(entry.Preview);
        Assert.AreEqual("Song 333", entry.Proposal.Title);
        Assert.IsTrue(entry.CleansFeaturing);
        Assert.AreEqual(SharedIdAction.Merge, entry.DefaultAction);
        Assert.IsEmpty(past.Clusters);
        Assert.IsEmpty(first.RankCounts, "Unsorted");
        Assert.AreEqual(1, (await dms.SharedIds.LoadPage(user, 0, pageSize: 2)).Page);
    }

    [TestMethod]
    public async Task LoadPage_SortsByRankAndCountsEachRank()
    {
        var (dms, user) = await CreateService("SharedIds_LoadPageRank");
        await ThreeClusters(dms);
        var clusters = await dms.SharedIds.PendingClusters();
        var last = clusters[2].Key;

        var page = await dms.SharedIds.LoadPage(user, 1, c => c.Key == last ? 0 : 2);

        Assert.AreEqual(last, page.Clusters[0].Cluster.Key);
        CollectionAssert.AreEqual(new[] { clusters[0].Key, clusters[1].Key },
            page.Clusters.Skip(1).Select(c => c.Cluster.Key).ToList());
        Assert.AreEqual(1, page.RankCounts[0]);
        Assert.AreEqual(2, page.RankCounts[2]);
        Assert.AreEqual(0, page.DeferredCount);
    }

    [TestMethod]
    public async Task LoadPage_PutsTheDeferredClustersLastAndLeavesThemOutOfTheRankCounts()
    {
        var (dms, user) = await CreateService("SharedIds_LoadPageDeferred");
        await ThreeClusters(dms);
        var clusters = await dms.SharedIds.PendingClusters();
        var (first, second, third) = (clusters[0].Key, clusters[1].Key, clusters[2].Key);

        // The third ranks first, but it and the first were left for later
        var page = await dms.SharedIds.LoadPage(user, 1, c => c.Key == third ? 0 : 2,
            c => c.Key == first || c.Key == third);

        CollectionAssert.AreEqual(new[] { second, third, first },
            page.Clusters.Select(c => c.Cluster.Key).ToList());
        Assert.AreEqual(2, page.DeferredCount);
        Assert.AreEqual(3, page.ClusterCount);
        Assert.IsFalse(page.RankCounts.ContainsKey(0));
        Assert.AreEqual(1, page.RankCounts[2]);

        var unranked = await dms.SharedIds.LoadPage(user, 1, deferred: c => c.Key == first);
        CollectionAssert.AreEqual(new[] { second, third, first },
            unranked.Clusters.Select(c => c.Cluster.Key).ToList());
    }

    [TestMethod]
    public async Task LoadPage_SongMergedElsewhere_ShowsTheCurrentCluster()
    {
        var (dms, user) = await CreateService("SharedIds_LoadPageStale");
        var a = await SaveSong(dms, "Purchase:00:IS=111");
        var b = await SaveSong(dms, "Purchase:00:IS=111\tPurchase:00:SS=sp1");
        var c = await SaveSong(dms, "Purchase:00:SS=sp1");
        _ = await dms.SharedIds.Seed();
        var merged = await dms.SongIndex.SimpleMergeSongs(user, [b, c]);

        var page = await dms.SharedIds.LoadPage(user, 1);

        var cluster = page.Clusters.Single().Cluster;
        CollectionAssert.AreEquivalent(new[] { a.SongId, merged.SongId }, cluster.SongIds.ToList());
        Assert.HasCount(2, cluster.Songs);
    }

    [TestMethod]
    public async Task LoadPage_ClusterWithARejectedPair_StartsAsLeave()
    {
        var (dms, user) = await CreateService("SharedIds_LoadPageRejected");
        var a = await SaveSong(dms, "Purchase:00:IS=111\tPurchase:00:RS=USRC10000001");
        var b = await SaveSong(dms, "Purchase:00:IS=111\tPurchase:00:SS=sp1");
        _ = await SaveSong(dms, "Purchase:00:SS=sp1\tPurchase:00:RS=USRC10000001");
        _ = await dms.SharedIds.Seed();
        await dms.SharedIds.Reject(user, [(await Rows(dms)).Single(r => (r.SongA, r.SongB) == Pair(a, b)).Id]);

        var entry = (await dms.SharedIds.LoadPage(user, 1)).Clusters.Single();

        Assert.IsTrue(entry.Cluster.HasRejectedPair);
        Assert.AreEqual(SharedIdAction.Leave, entry.DefaultAction);
    }

    #endregion
}
