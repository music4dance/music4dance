using Microsoft.VisualStudio.TestTools.UnitTesting;
using m4d.Utilities;
using m4dModels;
using m4dModels.Tests;

namespace m4d.Tests.Utilities;

/// <summary>
/// An add that matches a catalog song by title names the song the way the shared-id review does
/// (MusicServiceManager.NameMergedSong): a feat. credit the track brings moves into the individual
/// artists, and a remaster note doesn't overwrite a clean title. CreateSong itself can't be run
/// here (its service lookups are live HTTP), so these cover the naming step it calls.
/// </summary>
[TestClass]
public class NameMergedSongTests
{
    [ClassInitialize]
    public static async Task ClassSetup(TestContext _)
    {
        await DanceMusicTester.LoadDances();
    }

    private static async Task<(DanceMusicService dms, TestSongIndex testIndex)> CreateTestEnv(string dbName)
    {
        var dms = await DanceMusicTester.CreateService(dbName, useTestSongIndex: true);
        await DanceMusicTester.AddUser(dms, "batch-n", true);
        return (dms, (TestSongIndex)dms.SongIndex);
    }

    private static Task<Song> Track(DanceMusicService dms, string title, string artist) =>
        Song.Create(
            $".Create=\tUser=batch-n\tTime=01/01/2020 10:00:00 AM\tTitle={title}\tArtist={artist}\t" +
            "Album:00=Album B\tTrack:00=2\tPurchase:00:SS=newTrack",
            dms);

    [TestMethod]
    public async Task FeaturedArtistOfTheTrack_MovesIntoTheIndividualArtists()
    {
        var (dms, testIndex) = await CreateTestEnv("NameMerged_Featured");
        var existing = await Song.Create(
            ".Create=\tUser=batch-n\tTime=01/01/2020 10:00:00 AM\tTitle=Crazy Song\tArtist=The Artist\t" +
            "Album:00=Album A\tTrack:00=1\tPurchase:00:SS=oldTrack",
            dms);
        await testIndex.SaveSong(existing);
        var incoming = await Track(dms, "Crazy Song", "The Artist feat. Crazy Man");

        var named = await MusicServiceManager.NameMergedSong(dms, existing, incoming);

        Assert.AreEqual("The Artist feat. Crazy Man", named.Artist);
        CollectionAssert.Contains(named.EffectiveArtists.ToList(), "Crazy Man");
    }

    [TestMethod]
    public async Task RemasterNoteOnTheTrack_DoesNotOverwriteTheCleanTitle()
    {
        var (dms, testIndex) = await CreateTestEnv("NameMerged_Remaster");
        var existing = await Song.Create(
            ".Create=\tUser=batch-n\tTime=01/01/2020 10:00:00 AM\tTitle=Crazy Song\tArtist=The Artist\t" +
            "Album:00=Album A\tTrack:00=1\tPurchase:00:SS=oldTrack",
            dms);
        await testIndex.SaveSong(existing);
        var incoming = await Track(dms, "Crazy Song - Remastered 2011", "The Artist");

        var named = await MusicServiceManager.NameMergedSong(dms, existing, incoming);

        Assert.AreSame(existing, named, "Nothing to name, so the song isn't reloaded");
        Assert.AreEqual("Crazy Song", named.Title);
        Assert.AreEqual("The Artist", named.Artist);
    }
}
