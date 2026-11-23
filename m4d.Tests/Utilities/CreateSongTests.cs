using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using m4d.Utilities;
using m4dModels;
using m4dModels.Tests;

namespace m4d.Tests.Utilities;

/// <summary>
/// The add path end to end (MusicServiceManager.CreateSong) with the Spotify Web API served by
/// SpotifyFixtureHandler: a track lookup, the title match against the catalog, enrichment (search,
/// audio features, sample, ISRC) and the save. iTunes is 404 throughout.
/// </summary>
[TestClass]
public class CreateSongTests
{
    [ClassInitialize]
    public static async Task ClassSetup(TestContext _)
    {
        await DanceMusicTester.LoadDances();
    }

    private static async Task<(DanceMusicService dms, TestSongIndex testIndex, ApplicationUser user)> CreateTestEnv(string dbName)
    {
        var dms = await DanceMusicTester.CreateService(dbName, useTestSongIndex: true);
        await DanceMusicTester.AddUser(dms, "dwgray", false);
        await DanceMusicTester.AddUser(dms, "batch-s", true);
        await DanceMusicTester.AddUser(dms, "batch-i", true);
        return (dms, (TestSongIndex)dms.SongIndex, await dms.FindUser("dwgray"));
    }

    private static MusicServiceManager Manager() =>
        new(new Mock<IConfiguration>().Object)
        {
            Http = new HttpClient(new SpotifyFixtureHandler()),
        };

    [TestMethod]
    public async Task CreateSong_NewTrack_BuildsTheSongFromTheTrack()
    {
        var (dms, _, user) = await CreateTestEnv("CreateSong_NewTrack");

        var song = await Manager().CreateSong(
            dms, user, SpotifyFixtureHandler.TrackId, MusicService.GetService(ServiceType.Spotify));

        Assert.IsNotNull(song);
        Assert.AreEqual("Cut To The Feeling", song.Title);
        Assert.AreEqual("Carly Rae Jepsen", song.Artist);
        Assert.AreEqual(SpotifyFixtureHandler.TrackSeconds, song.Length);
        Assert.AreEqual("USUM71703861", song.GetPurchaseId(ServiceType.ISRC));
        Assert.IsNotNull(song.Tempo, "The audio features supply the tempo");
    }

    [TestMethod]
    public async Task CreateSong_TitleMatchInTheCatalog_AddsTheTrackToTheExistingSong()
    {
        var (dms, testIndex, user) = await CreateTestEnv("CreateSong_TitleMatch");
        var existing = await Song.Create(
            ".Create=\tUser=batch-s\tTime=01/01/2020 10:00:00 AM\tTitle=Cut To The Feeling\t" +
            "Artist=Carly Rae Jepsen\tLength=208\tAlbum:00=Cut To The Feeling\tTrack:00=1\t" +
            "Purchase:00:SS=oldSpotifyId\tDanceRating=SLS+1",
            dms);
        await testIndex.SaveSong(existing);

        var song = await Manager().CreateSong(
            dms, user, SpotifyFixtureHandler.TrackId, MusicService.GetService(ServiceType.Spotify));

        Assert.IsNotNull(song);
        Assert.AreEqual(existing.SongId, song.SongId, "The track joins the catalog song, not a new one");
        CollectionAssert.Contains(
            song.GetPurchaseIds(MusicService.GetService(ServiceType.Spotify)).ToList(),
            SpotifyFixtureHandler.TrackId);
    }

    [TestMethod]
    public async Task CreateSong_UnknownTrack_ReturnsNull()
    {
        var (dms, _, user) = await CreateTestEnv("CreateSong_Unknown");

        var song = await Manager().CreateSong(
            dms, user, "noSuchTrackId", MusicService.GetService(ServiceType.Spotify));

        Assert.IsNull(song);
    }
}
