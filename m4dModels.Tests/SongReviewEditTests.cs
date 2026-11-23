using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace m4dModels.Tests;

/// <summary>
/// The edits the shared-id review appends to a song: taking albums and ids off it
/// (Song.AppendIdRemoval), and setting its title, artist and individual artists
/// (Song.AppendNameEdit). Both only append to the log, so each test reloads the song to see
/// the result.
/// </summary>
[TestClass]
public class SongReviewEditTests
{
    private static DanceMusicCoreService _dms;
    private static ApplicationUser _user;

    [ClassInitialize]
    public static async Task ClassInitialize(TestContext _)
    {
        await DanceMusicTester.LoadDances();
        _dms = await DanceMusicTester.CreateServiceWithUsers("SongReviewEdits");
        _user = new ApplicationUser("dwgray");
    }

    // Album 0 holds I:111 and S:sp1; album 1 holds S:sp2
    private static Task<Song> TwoAlbums(string extra = "") => Song.Create(
        ".Create=\tUser=dwgray\tTime=01/01/2020 10:00:00 AM\tTitle=Shared Song\tArtist=The Artist\t" +
        "Album:00=First Album\tTrack:00=1\tPurchase:00:IS=111\tPurchase:00:SS=sp1\t" +
        "Album:01=Second Album\tTrack:01=2\tPurchase:01:SS=sp2" + extra,
        _dms);

    private static Task<Song> Reload(Song song) => Song.Create(song.SongId, [.. song.SongProperties], _dms);

    [TestMethod]
    public async Task AppendIdRemoval_OneId_LeavesTheAlbumAndItsOtherIds()
    {
        var song = await TwoAlbums();

        Assert.IsTrue(song.AppendIdRemoval(_user, [(0, "I:111")]));

        var reloaded = await Reload(song);
        CollectionAssert.AreEquivalent(new[] { "S:sp1", "S:sp2" }, reloaded.GetExtendedPurchaseIds().Distinct().ToList());
        Assert.HasCount(2, reloaded.Albums);
        Assert.AreEqual(Song.EditCommand, song.SongProperties.Last(p => p.IsAction).Name);
    }

    [TestMethod]
    public async Task AppendIdRemoval_AnAlbumAndAnIdOnIt_RemovesTheAlbumOnce()
    {
        var song = await TwoAlbums();
        var count = song.SongProperties.Count;

        Assert.IsTrue(song.AppendIdRemoval(_user, [(0, "I:111"), (0, null), (0, null)]));

        var added = song.SongProperties.Skip(count).Where(p => !p.IsAction && p.Name != Song.UserField && p.Name != Song.TimeField).ToList();
        Assert.AreEqual("Album:00=", $"{added.Single().Name}={added.Single().Value}");
        var reloaded = await Reload(song);
        Assert.AreEqual("Second Album", reloaded.Albums.Single().Name);
    }

    [TestMethod]
    [DataRow(5, null, DisplayName = "An album the song doesn't have")]
    [DataRow(1, "I:111", DisplayName = "An id on another of its albums")]
    [DataRow(0, "X:111", DisplayName = "An unknown service")]
    [DataRow(0, "111", DisplayName = "No service prefix")]
    public async Task AppendIdRemoval_NothingToRemove_AddsNothing(int album, string id)
    {
        var song = await TwoAlbums();
        var count = song.SongProperties.Count;

        Assert.IsFalse(song.AppendIdRemoval(_user, [(album, id)]));
        Assert.HasCount(count, song.SongProperties);
    }

    [TestMethod]
    public async Task AppendIdAddition_CopiesTheAlbum_OrUsesTheSongsAlbumOfThatName()
    {
        var song = await TwoAlbums();
        var from = (await Song.Create(
            ".Create=\tUser=dwgray\tTime=01/01/2020 10:00:00 AM\tTitle=Other\tArtist=Other\t" +
            "Album:00=New Album\tTrack:00=4\tPurchase:00:SS=sp9\tPurchase:00:SA=al9\tPurchase:00:IS=it9\t" +
            "Album:01=Second Album\tTrack:01=2\tPurchase:01:IS=it8",
            _dms)).Albums;

        Assert.IsTrue(song.AppendIdAddition(_user,
        [
            (from[0], "S:sp9", "USRC9"), (from[0], "I:it9", null), (from[1], "I:it8", null), (from[1], "S:sp1", null)
        ]));

        var reloaded = await Reload(song);
        var added = reloaded.Albums.Single(a => a.Name == "New Album");
        Assert.AreEqual(4, added.Track);
        CollectionAssert.AreEquivalent(new[] { "S:sp9", "I:it9", "R:USRC9" },
            added.GetExtendedPurchaseIds(PurchaseType.Song).ToList());
        Assert.AreEqual("al9", added.GetPurchaseIdentifier(ServiceType.Spotify, PurchaseType.Album));
        // Second Album has no iTunes id, so it takes it; S:sp1 is the song's already
        CollectionAssert.AreEquivalent(new[] { "S:sp2", "I:it8" },
            reloaded.Albums.Single(a => a.Name == "Second Album").GetExtendedPurchaseIds(PurchaseType.Song).ToList());
        Assert.HasCount(3, reloaded.Albums);
        Assert.IsFalse(reloaded.AppendIdAddition(_user, [(from[0], "S:sp9", null)]), "It has it already");
    }

    [TestMethod]
    public async Task AppendNameEdit_SetsOnlyWhatDiffers()
    {
        var song = await TwoAlbums();
        var count = song.SongProperties.Count;

        Assert.IsTrue(song.AppendNameEdit(_user, new SongNames("Shared Song", "The Artist feat. Crazy Man",
            ["The Artist", "Crazy Man"])));

        var added = song.SongProperties.Skip(count).Select(p => p.Name).ToList();
        CollectionAssert.DoesNotContain(added, Song.TitleField);
        CollectionAssert.Contains(added, Song.ArtistField);
        var reloaded = await Reload(song);
        Assert.AreEqual("The Artist feat. Crazy Man", reloaded.Artist);
        CollectionAssert.AreEqual(new[] { "The Artist", "Crazy Man" }, reloaded.EffectiveArtists.ToList());
    }

    [TestMethod]
    public async Task AppendNameEdit_OneIndividualArtist_ClearsTheListBackToTheCredit()
    {
        var song = await TwoAlbums("\tArtists=The Artist|Crazy Man");
        CollectionAssert.AreEqual(new[] { "The Artist", "Crazy Man" }, song.EffectiveArtists.ToList());

        Assert.IsTrue(song.AppendNameEdit(_user, new SongNames(null, null, ["The Artist"])));

        Assert.AreEqual("Artists=", $"{song.SongProperties[^1].Name}={song.SongProperties[^1].Value}");
        CollectionAssert.AreEqual(new[] { "The Artist" }, (await Reload(song)).EffectiveArtists.ToList());
    }

    [TestMethod]
    public async Task AppendNameEdit_SetsTheTempo_OverOneARealUserSet()
    {
        var song = await TwoAlbums("\tTempo=120.0");

        Assert.IsTrue(song.AppendNameEdit(_user, new SongNames(null, null, null, 124.5m)));

        Assert.AreEqual(124.5m, (await Reload(song)).Tempo);
        Assert.IsFalse((await Reload(song)).AppendNameEdit(_user, new SongNames(null, null, null, 124.5m)),
            "It already has the tempo");
        Assert.IsFalse((await TwoAlbums()).AppendNameEdit(_user, new SongNames(null, null, null, 0m)),
            "Zero is left alone");
    }

    [TestMethod]
    public async Task AppendNameEdit_NamesItAlreadyHasOrBlank_AddsNothing()
    {
        var song = await TwoAlbums();
        var count = song.SongProperties.Count;

        Assert.IsFalse(song.AppendNameEdit(_user, new SongNames("Shared Song", "The Artist", ["the artist"])));
        Assert.IsFalse(song.AppendNameEdit(_user, new SongNames(" ", null, [])));
        Assert.HasCount(count, song.SongProperties);
    }
}
