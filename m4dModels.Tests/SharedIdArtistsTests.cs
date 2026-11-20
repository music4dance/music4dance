using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace m4dModels.Tests;

/// <summary>
/// Merge artist and Force merge artist on the shared-id review: the songs' artists combined, and
/// checked against the songs' tracks.
/// </summary>
[TestClass]
public class SharedIdArtistsTests
{
    [ClassInitialize]
    public static async Task ClassInitialize(TestContext _)
    {
        await DanceMusicTester.LoadDances();
    }

    private static async Task<Song> Save(DanceMusicService dms, string artist, string ids, string title = "Cheek to Cheek")
    {
        var song = await Song.Create(
            $".Create=\tUser=dwgray\tTime=01/01/2020 10:00:00 AM\tTitle={title}\tArtist={artist}\t" +
            $"Length=200\tAlbum:00=Album\tTrack:00=1\t{ids}\tDanceRating=SLS+1",
            dms);
        await dms.SongIndex.SaveSong(song);
        return song;
    }

    private static async Task<DanceMusicService> CreateService(string name)
    {
        var dms = await DanceMusicTester.CreateService(name, useTestSongIndex: true);
        await DanceMusicTester.AddUser(dms, "dwgray", false);
        return dms;
    }

    private static Func<string, Task<ServiceTrack>> Tracks(params ServiceTrack[] tracks) =>
        id => Task.FromResult(tracks.FirstOrDefault(t => t.TrackId == id));

    [TestMethod]
    public async Task Combine_NamesEachArtistOnce_UnderTheNameToUse()
    {
        var dms = await CreateService("SharedIdArtists_Combine");
        var songs = new[]
        {
            await Save(dms, "Tony Bennett", "Purchase:00:SS=sp1"),
            await Save(dms, "Lady Gaga", "Purchase:00:SS=sp1"),
            await Save(dms, "tony bennett", "Purchase:00:SS=sp1"),
            await Save(dms, "Pink", "Purchase:00:SS=sp1"),
            await Save(dms, "P!nk", "Purchase:00:SS=sp1"),
        };

        var combined = await SharedIdArtists.Propose(songs, Tracks(), force: true);

        CollectionAssert.AreEqual(new[] { "Tony Bennett", "Lady Gaga", "P!nk" }, combined.Artists.ToList());
        Assert.AreEqual("Tony Bennett & Lady Gaga & P!nk", combined.Artist);
        Assert.IsFalse(combined.Confirmed, "Force doesn't check");
    }

    [TestMethod]
    public async Task Propose_ATrackCreditingEveryArtist_ConfirmsIt()
    {
        var dms = await CreateService("SharedIdArtists_Confirmed");
        var songs = new[]
        {
            await Save(dms, "Tony Bennett", "Purchase:00:SS=sp1\tPurchase:00:IS=it1"),
            await Save(dms, "Lady Gaga", "Purchase:00:SS=sp1"),
        };

        var combined = await SharedIdArtists.Propose(songs, Tracks(new ServiceTrack
        {
            TrackId = "S:sp1", Name = "Cheek to Cheek", Artist = "Tony Bennett", Artists = ["Tony Bennett", "Lady Gaga"]
        }), force: false);

        Assert.IsTrue(combined.Confirmed);
        Assert.AreEqual("S:sp1", combined.Via);
        Assert.AreEqual("Tony Bennett & Lady Gaga", combined.Artist);
    }

    [TestMethod]
    public async Task Propose_AniTunesCredit_IsSplitWithTheSongsArtists()
    {
        var dms = await CreateService("SharedIdArtists_iTunes");
        var songs = new[]
        {
            await Save(dms, "Tony Bennett", "Purchase:00:IS=it1"),
            await Save(dms, "Lady Gaga", "Purchase:00:IS=it1"),
        };

        var combined = await SharedIdArtists.Propose(songs, Tracks(new ServiceTrack
        {
            TrackId = "I:it1", Name = "Cheek to Cheek", Artist = "Tony Bennett & Lady Gaga"
        }), force: false);

        Assert.IsTrue(combined.Confirmed);
    }

    [TestMethod]
    public async Task Propose_ATrackThatLeavesAnArtistOut_SaysWhichAndDoesntConfirm()
    {
        var dms = await CreateService("SharedIdArtists_Missing");
        var songs = new[]
        {
            await Save(dms, "Tony Bennett", "Purchase:00:SS=sp1"),
            await Save(dms, "Lady Gaga", "Purchase:00:SS=sp1"),
        };

        var combined = await SharedIdArtists.Propose(songs, Tracks(new ServiceTrack
        {
            TrackId = "S:sp1", Name = "Cheek to Cheek", Artist = "Tony Bennett", Artists = ["Tony Bennett"]
        }), force: false);

        Assert.IsFalse(combined.Confirmed);
        Assert.AreEqual("S:sp1", combined.Via);
        CollectionAssert.AreEqual(new[] { "Lady Gaga" }, combined.Missing.ToList());
    }

    [TestMethod]
    public async Task Propose_NoTrackFound_GivesTheReason()
    {
        var dms = await CreateService("SharedIdArtists_None");
        var songs = new[]
        {
            await Save(dms, "Tony Bennett", "Purchase:00:SS=sp1"),
            await Save(dms, "Lady Gaga", "Purchase:00:SS=sp1"),
        };

        var combined = await SharedIdArtists.Propose(songs, Tracks(), force: false);

        Assert.IsFalse(combined.Confirmed);
        Assert.IsNull(combined.Via);
        Assert.AreEqual("None of the songs' tracks was found", combined.Reason);
    }
}
