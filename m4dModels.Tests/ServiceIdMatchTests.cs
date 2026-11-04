using m4dModels.Sandbox;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace m4dModels.Tests;

/// <summary>
/// Adding a song whose Spotify, iTunes or ISRC id is already held by a catalog song appends to
/// that song instead of creating a duplicate: the exact ServiceIds lookup, MatchSongs' id match,
/// the post-enrichment pass of a catalog commit, and POST /api/song (CreateOrMergeSong).
/// </summary>
[TestClass]
public class ServiceIdMatchTests
{
    [ClassInitialize]
    public static async Task ClassInitialize(TestContext context)
    {
        await DanceMusicTester.LoadDances();
    }

    private static async Task<DanceMusicService> CreateService(string name)
    {
        var dms = await DanceMusicTester.CreateService(name, useTestSongIndex: true);
        await DanceMusicTester.AddUser(dms, "dwgray", false);
        await DanceMusicTester.AddUser(dms, "batch-i", true);
        return dms;
    }

    private static async Task<Song> SaveSong(DanceMusicService dms, string purchase,
        string album = "Album A", string title = "Existing Song")
    {
        var song = await Song.Create(
            $".Create=\tUser=dwgray\tTime=01/01/2020 10:00:00 AM\tTitle={title}\tArtist=The Artist\t" +
            $"Album:00={album}\tTrack:00=1\t{purchase}",
            dms);
        await dms.SongIndex.SaveSong(song);
        return song;
    }

    private static Task<Song> NewSong(DanceMusicService dms, string purchase,
        string album = "Album B", string extra = "DanceRating=SLS+1")
    {
        return Song.Create(
            $".Create=\tUser=dwgray\tTime=02/01/2020 10:00:00 AM\tTitle=New Song\tArtist=The Artist\t" +
            $"Album:00={album}\tTrack:00=2\t{purchase}\t{extra}",
            dms);
    }

    private static ICollection<string> SpotifyIds(Song song) =>
        song.GetPurchaseIds(MusicService.GetService(ServiceType.Spotify));

    [TestMethod]
    public async Task FindSongsByServiceIds_MatchesAnyOfSpotifyITunesOrIsrc()
    {
        var dms = await CreateService("ServiceIdMatch_FindAny");
        var bySpotify = await SaveSong(dms, "Purchase:00:SS=sp1");
        var byITunes = await SaveSong(dms, "Purchase:00:IS=111");
        var byIsrc = await SaveSong(dms, "Purchase:00:RS=USRC10000001");
        _ = await SaveSong(dms, "Purchase:00:SS=unrelated");

        var found = await dms.SongIndex.FindSongsByServiceIds(
            ["S:sp1", "I:111", "R:USRC10000001", "S:nobody"]);

        CollectionAssert.AreEquivalent(
            new[] { bySpotify.SongId, byITunes.SongId, byIsrc.SongId },
            found.Select(s => s.SongId).ToList());
    }

    [TestMethod]
    public async Task FindSongsByServiceIds_NoIds_ReturnsEmpty()
    {
        var dms = await CreateService("ServiceIdMatch_FindNone");
        _ = await SaveSong(dms, "Purchase:00:SS=sp1");

        Assert.AreEqual(0, (await dms.SongIndex.FindSongsByServiceIds([])).Count);
        Assert.AreEqual(0, (await dms.SongIndex.FindSongsByServiceIds(null)).Count);
    }

    [TestMethod]
    public async Task FindSongByServiceIds_PrefersTheSongSharingMostIds_AndSkipsItself()
    {
        var dms = await CreateService("ServiceIdMatch_Best");
        _ = await SaveSong(dms, "Purchase:00:SS=sp1");
        var both = await SaveSong(dms, "Purchase:00:SS=sp2\tPurchase:00:RS=USRC10000002");

        var probe = await NewSong(dms, "Purchase:00:SS=sp1\tPurchase:00:SS=sp2\tPurchase:00:RS=USRC10000002");
        var match = await dms.SongIndex.FindSongByServiceIds(probe);
        Assert.AreEqual(both.SongId, match?.SongId);

        Assert.IsNull(await dms.SongIndex.FindSongByServiceIds(both),
            "A song never matches itself");
    }

    [TestMethod]
    public async Task GetSongFromService_UsesTheExactLookup()
    {
        var dms = await CreateService("ServiceIdMatch_GetFromService");
        var existing = await SaveSong(dms, "Purchase:00:RS=USRC10000003");

        var match = await dms.SongIndex.GetSongFromService(
            MusicService.GetService(ServiceType.ISRC), "USRC10000003");

        Assert.AreEqual(existing.SongId, match?.SongId);
    }

    [TestMethod]
    public async Task CreateFromTrack_KeepsTheTracksIsrc()
    {
        var dms = await CreateService("ServiceIdMatch_CreateFromTrack");
        var user = await dms.FindUser("dwgray");
        var track = new ServiceTrack
        {
            Service = ServiceType.Spotify, TrackId = "sp9", CollectionId = "al9",
            Name = "New Song", Artist = "The Artist", Album = "Album B", ISRC = "USRC10000009",
        };

        var song = await Song.CreateFromTrack(user, track, null, null, null, dms);

        Assert.AreEqual("USRC10000009", song.GetPurchaseId(ServiceType.ISRC));
        Assert.AreEqual("sp9", song.GetPurchaseId(ServiceType.Spotify));
    }

    [TestMethod]
    public async Task MatchSongs_MatchesOnIsrcWhenTheSpotifyIdIsNew()
    {
        var dms = await CreateService("ServiceIdMatch_MatchSongsIsrc");
        var existing = await SaveSong(dms, "Purchase:00:SS=oldSpotify\tPurchase:00:RS=USRC10000004");
        var incoming = await NewSong(dms, "Purchase:00:SS=newSpotify\tPurchase:00:RS=USRC10000004");

        var merges = await dms.SongIndex.MatchSongs(
            [incoming], DanceMusicCoreService.MatchMethod.Merge);

        Assert.AreEqual(1, merges.Count);
        Assert.AreEqual(MatchType.Exact, merges[0].MatchType);
        Assert.AreEqual(existing.SongId, merges[0].Right.SongId);
    }

    [TestMethod]
    public async Task MergeNewSongsByServiceIds_NewSongHoldingACatalogId_FoldsIntoThatSong()
    {
        var dms = await CreateService("ServiceIdMatch_CommitIndex");
        var user = await dms.FindUser("dwgray");
        var existing = await SaveSong(dms, "Purchase:00:SS=oldSpotify\tPurchase:00:IS=222");
        // As enrichment would leave it: an iTunes id that the existing song already holds
        var incoming = await NewSong(dms, "Purchase:00:SS=newSpotify\tPurchase:00:IS=222");
        var merger = new LocalMerger { Left = incoming, MatchType = MatchType.None };

        var result = await dms.SongIndex.MergeNewSongsByServiceIds(user, [incoming], [merger]);

        Assert.AreEqual(1, result.Count);
        var target = result[0];
        Assert.AreEqual(existing.SongId, target.SongId);
        CollectionAssert.AreEquivalent(new[] { "oldSpotify", "newSpotify" }, SpotifyIds(target).ToList());
        Assert.AreEqual(MatchType.Exact, merger.MatchType);
        Assert.AreEqual(existing.SongId, merger.Right.SongId);
        Assert.IsTrue(target.DanceRatings.Any(dr => dr.DanceId == "SLS"),
            "The new song's dance rating carries over");
    }

    [TestMethod]
    public async Task MergeNewSongsByServiceIds_TwoNewSongsSharingAnIsrc_BecomeOne()
    {
        var dms = await CreateService("ServiceIdMatch_CommitBatch");
        var user = await dms.FindUser("dwgray");
        var first = await NewSong(dms, "Purchase:00:SS=spA\tPurchase:00:RS=USRC10000005");
        var second = await NewSong(dms, "Purchase:00:SS=spB\tPurchase:00:RS=USRC10000005", album: "Album C");
        var lonely = await NewSong(dms, "Purchase:00:SS=spC\tPurchase:00:RS=USRC10000006");
        List<LocalMerger> merges =
        [
            new() { Left = first, MatchType = MatchType.None },
            new() { Left = second, MatchType = MatchType.None },
            new() { Left = lonely, MatchType = MatchType.None },
        ];

        var result = await dms.SongIndex.MergeNewSongsByServiceIds(
            user, [first, second, lonely], merges);

        CollectionAssert.AreEqual(
            new[] { first.SongId, lonely.SongId }, result.Select(s => s.SongId).ToList());
        CollectionAssert.AreEquivalent(new[] { "spA", "spB" }, SpotifyIds(result[0]).ToList());
        Assert.AreEqual(first.SongId, merges[1].Right.SongId);
        Assert.AreEqual(MatchType.None, merges[2].MatchType);
    }

    [TestMethod]
    public async Task MergeNewSongsByServiceIds_TargetAlreadyInTheBatch_EditsTheBatchCopy()
    {
        var dms = await CreateService("ServiceIdMatch_CommitInBatch");
        var user = await dms.FindUser("dwgray");
        var existing = await SaveSong(dms, "Purchase:00:SS=oldSpotify\tPurchase:00:IS=333");
        // The batch holds its own copy of the existing song (matched earlier by title)
        var batchCopy = await Song.Create(existing, dms);
        var incoming = await NewSong(dms, "Purchase:00:SS=newSpotify\tPurchase:00:IS=333");
        List<LocalMerger> merges =
        [
            new() { Left = await NewSong(dms, ""), Right = batchCopy, MatchType = MatchType.Weak },
            new() { Left = incoming, MatchType = MatchType.None },
        ];

        var result = await dms.SongIndex.MergeNewSongsByServiceIds(user, [batchCopy, incoming], merges);

        Assert.AreEqual(1, result.Count);
        Assert.AreSame(batchCopy, result[0]);
        CollectionAssert.AreEquivalent(new[] { "oldSpotify", "newSpotify" }, SpotifyIds(batchCopy).ToList());
    }

    [TestMethod]
    public async Task MergeIntoSong_PutsIdsOnTheAlbumOfTheSameName()
    {
        var dms = await CreateService("ServiceIdMatch_Albums");
        var user = await dms.FindUser("dwgray");
        var existing = await Song.Create(
            ".Create=\tUser=dwgray\tTime=01/01/2020 10:00:00 AM\tTitle=Existing Song\tArtist=The Artist\t" +
            "Album:00=Album A\tTrack:00=1\tPurchase:00:SS=spA\t" +
            "Album:01=Album B\tTrack:01=2\tPurchase:01:IS=444",
            dms);
        var incoming = await NewSong(dms, "Purchase:00:SS=spB\tPurchase:00:IS=444", album: "Album B");

        _ = await dms.SongIndex.MergeIntoSong(user, existing, incoming);

        var spotify = MusicService.GetService(ServiceType.Spotify);
        Assert.AreEqual(2, existing.Albums.Count);
        CollectionAssert.AreEqual(
            new[] { "spA" }, existing.FindAlbum("Album A").GetPurchaseIdentifiers(spotify.Id, PurchaseType.Song).ToList());
        CollectionAssert.AreEqual(
            new[] { "spB" }, existing.FindAlbum("Album B").GetPurchaseIdentifiers(spotify.Id, PurchaseType.Song).ToList());
    }

    [TestMethod]
    public async Task MergeIntoSong_UnknownAlbum_IsAddedAfterTheExistingOnes()
    {
        var dms = await CreateService("ServiceIdMatch_NewAlbum");
        var user = await dms.FindUser("dwgray");
        var existing = await SaveSong(dms, "Purchase:00:IS=555");
        var incoming = await NewSong(dms, "Purchase:00:SS=spB\tPurchase:00:IS=555", album: "Album Z");

        _ = await dms.SongIndex.MergeIntoSong(user, existing, incoming);

        Assert.AreEqual("Album A", existing.FindAlbum("Album A")?.Name);
        var added = existing.FindAlbum("Album Z");
        Assert.IsNotNull(added);
        Assert.AreEqual(1, added.Index);
        Assert.AreEqual("spB", added.GetPurchaseIdentifier(ServiceType.Spotify, PurchaseType.Song));
    }

    [TestMethod]
    public async Task CreateOrMergeSong_IdAlreadyInTheCatalog_AppendsInsteadOfCreating()
    {
        var dms = await CreateService("ServiceIdMatch_Post");
        var user = await dms.FindUser("dwgray");
        var existing = await SaveSong(dms, "Purchase:00:SS=sp7");
        var posted = await NewSong(dms, "Purchase:00:SS=sp7", album: "Album A");

        Assert.IsTrue(await dms.SongIndex.CreateOrMergeSong([.. posted.SongProperties], user));

        var holders = await dms.SongIndex.FindSongsByServiceIds(["S:sp7"]);
        Assert.AreEqual(1, holders.Count, "No second song holds the id");
        Assert.AreEqual(existing.SongId, holders[0].SongId);
        Assert.IsTrue(holders[0].DanceRatings.Any(dr => dr.DanceId == "SLS"),
            "The user's dance rating lands on the existing song");
    }

    [TestMethod]
    public async Task CreateOrMergeSong_NewIds_CreatesTheSong()
    {
        var dms = await CreateService("ServiceIdMatch_PostNew");
        var user = await dms.FindUser("dwgray");
        _ = await SaveSong(dms, "Purchase:00:SS=sp7");
        var posted = await NewSong(dms, "Purchase:00:SS=sp8");

        Assert.IsTrue(await dms.SongIndex.CreateOrMergeSong([.. posted.SongProperties], user));

        var holders = await dms.SongIndex.FindSongsByServiceIds(["S:sp8"]);
        Assert.AreEqual(1, holders.Count);
        Assert.AreEqual("New Song", holders[0].Title);
    }
}
