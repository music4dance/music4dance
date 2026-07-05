using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using m4d.Utilities;
using m4dModels;
using m4dModels.Tests;
using System.Security.Principal;
using Microsoft.EntityFrameworkCore;
using DanceLibrary;

namespace m4d.Tests.Utilities;

[TestClass]
public class MusicServiceManagerTests
{
    private Mock<IConfiguration> _mockConfiguration = null!;
    private MusicServiceManager _manager = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockConfiguration = new Mock<IConfiguration>();
        _manager = new MusicServiceManager(_mockConfiguration.Object);
    }

    #region Constructor Tests

    [TestMethod]
    public void Constructor_ValidConfiguration_CreatesInstance()
    {
        var config = new Mock<IConfiguration>().Object;
        var manager = new MusicServiceManager(config);
        Assert.IsNotNull(manager);
    }

    #endregion

    #region DefaultServiceSearch Tests

    [TestMethod]
    public void DefaultServiceSearch_CleanFalse_ReturnsTitleAndArtist()
    {
        var song = new Song
        {
            Title = "Test Song (Live)",
            Artist = "Test Artist [Remix]"
        };

        var result = MusicServiceManager.DefaultServiceSearch(song, clean: false);

        Assert.AreEqual("Test Song (Live) Test Artist [Remix]", result);
    }

    [TestMethod]
    public void DefaultServiceSearch_CleanTrue_ReturnsCleanedTitleAndArtist()
    {
        // CleanTitle and CleanArtist are computed properties from Title/Artist
        // They remove parenthetical content and brackets
        var song = new Song
        {
            Title = "Test Song",
            Artist = "Test Artist"
        };

        var result = MusicServiceManager.DefaultServiceSearch(song, clean: true);

        // When clean=true, it uses CleanTitle and CleanArtist (which are the same in this case)
        Assert.AreEqual("Test Song Test Artist", result);
    }

    [TestMethod]
    public void DefaultServiceSearch_EmptyTitleAndArtist_ReturnsSpace()
    {
        var song = new Song
        {
            Title = "",
            Artist = ""
        };

        var result = MusicServiceManager.DefaultServiceSearch(song, clean: false);

        Assert.AreEqual(" ", result);
    }

    #endregion

    #region UpdateMusicService Tests

    [TestMethod]
    public void UpdateMusicService_EmptyTitle_SetsTitle()
    {
        var song = new Song { Title = "", Artist = "Artist" };
        var service = MusicService.GetService(ServiceType.Spotify);

        MusicServiceManager.UpdateMusicService(
            song, service, "New Title", "Album", "Artist",
            "track123", "collection456", null, "180", 1);

        Assert.AreEqual("New Title", song.Title);
    }

    [TestMethod]
    public void UpdateMusicService_ExistingTitle_DoesNotChangeTitle()
    {
        var song = new Song { Title = "Original Title", Artist = "Artist" };
        var service = MusicService.GetService(ServiceType.Spotify);

        MusicServiceManager.UpdateMusicService(
            song, service, "New Title", "Album", "Artist",
            "track123", "collection456", null, "180", 1);

        Assert.AreEqual("Original Title", song.Title);
    }

    [TestMethod]
    public void UpdateMusicService_EmptyArtist_SetsArtist()
    {
        var song = new Song { Title = "Title", Artist = "" };
        var service = MusicService.GetService(ServiceType.Spotify);

        MusicServiceManager.UpdateMusicService(
            song, service, "Title", "Album", "New Artist",
            "track123", "collection456", null, "180", 1);

        Assert.AreEqual("New Artist", song.Artist);
    }

    [TestMethod]
    public void UpdateMusicService_ExistingArtist_DoesNotChangeArtist()
    {
        var song = new Song { Title = "Title", Artist = "Original Artist" };
        var service = MusicService.GetService(ServiceType.Spotify);

        MusicServiceManager.UpdateMusicService(
            song, service, "Title", "Album", "New Artist",
            "track123", "collection456", null, "180", 1);

        Assert.AreEqual("Original Artist", song.Artist);
    }

    [TestMethod]
    public void UpdateMusicService_NoLength_SetsDuration()
    {
        var song = new Song { Title = "Title", Artist = "Artist", Length = null };
        var service = MusicService.GetService(ServiceType.Spotify);

        MusicServiceManager.UpdateMusicService(
            song, service, "Title", "Album", "Artist",
            "track123", "collection456", null, "3:15", 1);

        Assert.AreEqual(195, song.Length);
    }

    [TestMethod]
    public void UpdateMusicService_ExistingLength_DoesNotChangeDuration()
    {
        var song = new Song { Title = "Title", Artist = "Artist", Length = 180 };
        var service = MusicService.GetService(ServiceType.Spotify);

        MusicServiceManager.UpdateMusicService(
            song, service, "Title", "Album", "Artist",
            "track123", "collection456", null, "3:15", 1);

        Assert.AreEqual(180, song.Length);
    }

    [TestMethod]
    public void UpdateMusicService_NewAlbum_AddsAlbum()
    {
        var song = new Song { Title = "Title", Artist = "Artist" };
        var service = MusicService.GetService(ServiceType.Spotify);

        MusicServiceManager.UpdateMusicService(
            song, service, "Title", "New Album", "Artist",
            "track123", "collection456", null, "180", 1);

        Assert.AreEqual(1, song.Albums.Count);
        Assert.AreEqual("New Album", song.Albums[0].Name);
        Assert.AreEqual(1, song.Albums[0].Track);
    }

    [TestMethod]
    public void UpdateMusicService_ExistingAlbum_UpdatesExistingAlbum()
    {
        var song = new Song { Title = "Title", Artist = "Artist" };
        song.Albums.Add(new AlbumDetails { Name = "Existing Album", Track = 1 });
        var service = MusicService.GetService(ServiceType.Spotify);

        MusicServiceManager.UpdateMusicService(
            song, service, "Title", "Existing Album", "Artist",
            "track123", "collection456", null, "180", 1);

        Assert.AreEqual(1, song.Albums.Count);
        Assert.AreEqual("Existing Album", song.Albums[0].Name);
    }

    [TestMethod]
    public void UpdateMusicService_SameTrackIdAlreadyOnAlbum_DoesNotAddAlbum()
    {
        var song = new Song { Title = "Title", Artist = "Artist" };
        var existing = new AlbumDetails { Name = "Existing Album", Track = 1 };
        existing.SetPurchaseInfo(PurchaseType.Song, ServiceType.Spotify, "track123");
        song.Albums.Add(existing);
        var service = MusicService.GetService(ServiceType.Spotify);

        MusicServiceManager.UpdateMusicService(
            song, service, "Title", "Existing Album", "Artist",
            "track123", "collection456", null, "180", 1);

        Assert.AreEqual(1, song.Albums.Count);
        Assert.AreEqual(
            "track123",
            song.Albums[0].GetPurchaseIdentifier(ServiceType.Spotify, PurchaseType.Song));
    }

    [TestMethod]
    public void UpdateMusicService_DifferentTrackIdOnMatchedAlbum_AccumulatesBothIdsOnSameAlbum()
    {
        // Spotify (and other services) periodically reissue a different track id for what is
        // otherwise the same recording. The matched album already carries a different id;
        // both should stay registered on that *same* album (no duplicate album entry, no lost
        // id) - see AlbumDetails.AddPurchaseId.
        var song = new Song { Title = "Title", Artist = "Artist" };
        var existing = new AlbumDetails { Name = "Existing Album", Track = 1 };
        existing.SetPurchaseInfo(PurchaseType.Song, ServiceType.Spotify, "oldTrack123");
        song.Albums.Add(existing);
        var service = MusicService.GetService(ServiceType.Spotify);

        MusicServiceManager.UpdateMusicService(
            song, service, "Title", "Existing Album", "Artist",
            "newTrack456", "collection456", null, "180", 1);

        Assert.AreEqual(1, song.Albums.Count);
        CollectionAssert.AreEqual(
            new[] { "oldTrack123", "newTrack456" },
            (System.Collections.ICollection)song.Albums[0]
                .GetPurchaseIdentifiers(ServiceType.Spotify, PurchaseType.Song));
        Assert.AreEqual(
            "oldTrack123",
            song.Albums[0].GetPurchaseIdentifier(ServiceType.Spotify, PurchaseType.Song));
    }

    #endregion

    // NOTE: ValidateAndCorrectTempo integration tests are in MusicServiceManagerIntegrationTests.cs
    // Those tests use DanceMusicTester to create real service instances and test the full workflow.
}

[TestClass]
public class GetISRCDataTests
{
    [ClassInitialize]
    public static async Task ClassSetup(TestContext _)
    {
        await DanceMusicTester.LoadDances();
    }

    // Subclass that replaces the Spotify HTTP call with an in-process dictionary lookup.
    private class FakeMusicServiceManager(Dictionary<string, string> isrcBySpotifyId)
        : MusicServiceManager(new Mock<IConfiguration>().Object)
    {
        public override async Task<ServiceTrack> GetMusicServiceTrack(string id, MusicService service)
        {
            await Task.CompletedTask;
            return isrcBySpotifyId.TryGetValue(id, out var isrc) && isrc != null
                ? new ServiceTrack { Service = ServiceType.Spotify, TrackId = id, ISRC = isrc }
                : null!;
        }
    }

    private static async Task<(DanceMusicService dms, TestSongIndex testIndex)> CreateTestEnv(string dbName)
    {
        var dms = await DanceMusicTester.CreateService(dbName, useTestSongIndex: true);
        await DanceMusicTester.AddUser(dms, "batch-s", true);
        var testIndex = (TestSongIndex)dms.SongIndex;
        return (dms, testIndex);
    }

    // Creates a song via the property-string format so that Song.Create(song, dms) inside
    // GetISRCData correctly reconstructs all albums (SongProperties is the source of truth).
    private static async Task<Song> CreateSongWithSpotifyIds(
        DanceMusicService dms, string albumA, string spotifyA, string albumB, string spotifyB)
    {
        return await Song.Create(
            $".Create=\tUser=batch-s\tTitle=Test Song\tArtist=Test Artist\t" +
            $"Album:00={albumA}\tTrack:00=1\tPurchase:00:SS={spotifyA}\t" +
            $"Album:01={albumB}\tTrack:01=2\tPurchase:01:SS={spotifyB}",
            dms);
    }

    [TestMethod]
    public async Task GetISRCData_TwoSpotifyIdsDistinctISRCs_AttachesEachISRCToMatchingAlbum()
    {
        var (dms, testIndex) = await CreateTestEnv("GetISRC_DistinctISRCs");
        var song = await CreateSongWithSpotifyIds(dms, "Album A", "spotifyId1", "Album B", "spotifyId2");

        var manager = new FakeMusicServiceManager(new Dictionary<string, string>
        {
            ["spotifyId1"] = "USRC1",
            ["spotifyId2"] = "USRC2",
        });

        var result = await manager.GetISRCData(dms, song);

        Assert.IsTrue(result, "Should return true when ISRCs were newly added");
        Assert.AreEqual(1, testIndex.EditCalls.Count);
        var edit = testIndex.EditCalls[0].Edit;
        Assert.AreEqual(
            "USRC1",
            edit.Albums[0].GetPurchaseIdentifier(ServiceType.ISRC, PurchaseType.Song),
            "ISRC should be on the album whose Spotify ID matched spotifyId1");
        Assert.AreEqual(
            "USRC2",
            edit.Albums[1].GetPurchaseIdentifier(ServiceType.ISRC, PurchaseType.Song),
            "ISRC should be on the album whose Spotify ID matched spotifyId2");
    }

    [TestMethod]
    public async Task GetISRCData_TwoSpotifyIdsSameISRC_ISRCAttachedOnlyToFirstMatchingAlbum()
    {
        // Two Spotify IDs (e.g. original release and re-release) that share the same ISRC.
        // The seenISRCs HashSet should prevent the duplicate from being written a second time.
        var (dms, testIndex) = await CreateTestEnv("GetISRC_DuplicateISRC");
        var song = await CreateSongWithSpotifyIds(dms, "Album A", "spotifyId1", "Album B", "spotifyId2");

        var manager = new FakeMusicServiceManager(new Dictionary<string, string>
        {
            ["spotifyId1"] = "USRC_SHARED",
            ["spotifyId2"] = "USRC_SHARED",
        });

        var result = await manager.GetISRCData(dms, song);

        Assert.IsTrue(result, "Should return true because at least one ISRC was added");
        Assert.AreEqual(1, testIndex.EditCalls.Count);
        var edit = testIndex.EditCalls[0].Edit;
        Assert.AreEqual(
            "USRC_SHARED",
            edit.Albums[0].GetPurchaseIdentifier(ServiceType.ISRC, PurchaseType.Song),
            "First album should receive the shared ISRC");
        Assert.IsNull(
            edit.Albums[1].GetPurchaseIdentifier(ServiceType.ISRC, PurchaseType.Song),
            "Duplicate ISRC must be skipped — second album should have no ISRC");
    }

    [TestMethod]
    public async Task GetISRCData_NoSpotifyIds_ReturnsFalse()
    {
        var (dms, testIndex) = await CreateTestEnv("GetISRC_NoSpotify");
        var song = await Song.Create(
            ".Create=\tUser=batch-s\tTitle=No Spotify Song\tArtist=Test Artist", dms);

        var manager = new FakeMusicServiceManager([]);

        var result = await manager.GetISRCData(dms, song);

        Assert.IsFalse(result, "No Spotify IDs means nothing to look up — should return false");
        Assert.AreEqual(0, testIndex.EditCalls.Count);
    }

    [TestMethod]
    public async Task GetISRCData_SpotifyTrackHasNoISRC_ReturnsFalse()
    {
        var (dms, testIndex) = await CreateTestEnv("GetISRC_NoISRCOnTrack");
        var song = await Song.Create(
            ".Create=\tUser=batch-s\tTitle=Test Song\tArtist=Test Artist\t" +
            "Album:00=Album A\tTrack:00=1\tPurchase:00:SS=spotifyId1",
            dms);

        // null ISRC simulates a Spotify track that has no external_ids.isrc
        var manager = new FakeMusicServiceManager(new Dictionary<string, string>
        {
            ["spotifyId1"] = null!,
        });

        var result = await manager.GetISRCData(dms, song);

        Assert.IsFalse(result, "Track with no ISRC should result in no change");
        Assert.AreEqual(0, testIndex.EditCalls.Count);
    }
}

// Covers MusicServiceManager.FindSongByISRC — the helper CreateSong uses to check for an
// existing catalog song sharing an ISRC before falling back to SongIndex.FindMatchingSong's
// (Azure-search-backed) title dedup. CreateSong itself isn't covered end-to-end here: once the
// ISRC branch misses, UpdateSongAndServices makes live HTTP calls to iTunes/Spotify with no
// available test seam (GetMusicServiceResults is private, non-virtual) — the same reason no
// other CreateSong tests exist in this suite yet.
[TestClass]
public class FindSongByISRCTests
{
    [ClassInitialize]
    public static async Task ClassSetup(TestContext _)
    {
        await DanceMusicTester.LoadDances();
    }

    private static async Task<(DanceMusicService dms, TestSongIndex testIndex)> CreateTestEnv(string dbName)
    {
        var dms = await DanceMusicTester.CreateService(dbName, useTestSongIndex: true);
        await DanceMusicTester.AddUser(dms, "batch-s", true);
        var testIndex = (TestSongIndex)dms.SongIndex;
        return (dms, testIndex);
    }

    private static async Task<Song> CreateAndSaveSongWithIsrc(
        DanceMusicService dms, TestSongIndex testIndex, string spotifyId, string isrc)
    {
        var song = await Song.Create(
            $".Create=\tUser=batch-s\tTitle=Existing Song\tArtist=Existing Artist\t" +
            $"Album:00=Album A\tTrack:00=1\tPurchase:00:SS={spotifyId}\tPurchase:00:RS={isrc}",
            dms);
        await testIndex.SaveSong(song);
        return song;
    }

    [TestMethod]
    public async Task FindSongByISRC_NullIsrc_ReturnsNullWithoutSearching()
    {
        var (dms, _) = await CreateTestEnv("FindByISRC_Null");
        var manager = new MusicServiceManager(new Mock<IConfiguration>().Object);

        var result = await manager.FindSongByISRC(dms, null);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task FindSongByISRC_EmptyIsrc_ReturnsNullWithoutSearching()
    {
        var (dms, _) = await CreateTestEnv("FindByISRC_Empty");
        var manager = new MusicServiceManager(new Mock<IConfiguration>().Object);

        var result = await manager.FindSongByISRC(dms, "   ");

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task FindSongByISRC_MatchingIsrcOnFile_ReturnsThatSong()
    {
        var (dms, testIndex) = await CreateTestEnv("FindByISRC_Match");
        var existing = await CreateAndSaveSongWithIsrc(dms, testIndex, "oldSpotifyId", "USRC1");
        var manager = new MusicServiceManager(new Mock<IConfiguration>().Object);

        var result = await manager.FindSongByISRC(dms, "USRC1");

        Assert.IsNotNull(result);
        Assert.AreEqual(existing.SongId, result.SongId);
    }

    [TestMethod]
    public async Task FindSongByISRC_NoSongHasThatIsrc_ReturnsNull()
    {
        var (dms, testIndex) = await CreateTestEnv("FindByISRC_NoMatch");
        _ = await CreateAndSaveSongWithIsrc(dms, testIndex, "oldSpotifyId", "USRC1");
        var manager = new MusicServiceManager(new Mock<IConfiguration>().Object);

        var result = await manager.FindSongByISRC(dms, "USRC-UNRELATED");

        Assert.IsNull(result);
    }
}

// Covers MusicServiceManager.AttachTracksToSong — the playlist viewer's batched counterpart to
// CreateSong's single-track ISRC-fallback attach step: once a song is matched by ISRC, this is
// what records the new Spotify id on it.
[TestClass]
public class AttachTracksToSongTests
{
    [ClassInitialize]
    public static async Task ClassSetup(TestContext _)
    {
        await DanceMusicTester.LoadDances();
    }

    [TestMethod]
    public async Task AttachTracksToSong_NewSpotifyId_AccumulatesOnExistingAlbumAndSaves()
    {
        var dms = await DanceMusicTester.CreateService("AttachTracksToSong_Accumulate", useTestSongIndex: true);
        await DanceMusicTester.AddUser(dms, "batch-s", true);
        var testIndex = (TestSongIndex)dms.SongIndex;

        var song = await Song.Create(
            ".Create=\tUser=batch-s\tTitle=Existing Song\tArtist=Existing Artist\t" +
            "Album:00=Album A\tTrack:00=1\tPurchase:00:SS=oldSpotifyId\tPurchase:00:RS=USRC1",
            dms);
        await testIndex.SaveSong(song);

        var manager = new MusicServiceManager(new Mock<IConfiguration>().Object);
        var track = new ServiceTrack
        {
            Service = ServiceType.Spotify,
            TrackId = "newSpotifyId",
            Name = "Existing Song",
            Artist = "Existing Artist",
            Album = "Album A",
            TrackNumber = 1,
            ISRC = "USRC1"
        };

        await manager.AttachTracksToSong(dms, song, [track]);

        var spotify = MusicService.GetService(ServiceType.Spotify);
        var spotifyIds = song.GetPurchaseIds(spotify);
        CollectionAssert.Contains(spotifyIds.ToList(), "oldSpotifyId");
        CollectionAssert.Contains(spotifyIds.ToList(), "newSpotifyId");

        var saved = await testIndex.FindSong(song.SongId);
        Assert.IsNotNull(saved);
    }
}

