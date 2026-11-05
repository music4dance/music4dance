#nullable disable

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using m4d.Utilities;
using m4dModels;
using m4dModels.Tests;

namespace m4d.Tests.Utilities;

// Enrichment of a catalog song leaves off any id another song already holds and queues the pair
// in the shared-id review queue (SharedIdLog) instead of giving the id a second song.
[TestClass]
public class SharedIdGuardTests
{
    [ClassInitialize]
    public static async Task ClassSetup(TestContext _)
    {
        await DanceMusicTester.LoadDances();
    }

    private class FakeMusicServiceManager(Dictionary<string, string> isrcBySpotifyId)
        : MusicServiceManager(new Mock<IConfiguration>().Object)
    {
        public override async Task<ServiceTrack> GetMusicServiceTrack(string id, MusicService service)
        {
            await Task.CompletedTask;
            return isrcBySpotifyId.TryGetValue(id, out var isrc)
                ? new ServiceTrack { Service = ServiceType.Spotify, TrackId = id, ISRC = isrc }
                : null!;
        }
    }

    private static async Task<(DanceMusicService dms, TestSongIndex testIndex)> CreateTestEnv(string dbName)
    {
        var dms = await DanceMusicTester.CreateService(dbName, useTestSongIndex: true);
        await DanceMusicTester.AddUser(dms, "batch-s", true);
        return (dms, (TestSongIndex)dms.SongIndex);
    }

    private static async Task<Song> SaveSong(DanceMusicService dms, string title, string purchase)
    {
        var song = await Song.Create(
            $".Create=\tUser=batch-s\tTitle={title}\tArtist=Test Artist\t" +
            $"Album:00=Album A\tTrack:00=1\t{purchase}",
            dms);
        await dms.SongIndex.SaveSong(song);
        return song;
    }

    private static ServiceTrack Track(string id, string isrc = null) => new()
    {
        Service = ServiceType.Spotify,
        TrackId = id,
        Name = "Catalog Song",
        Artist = "Test Artist",
        Album = "Album A",
        TrackNumber = 1,
        Duration = 200,
        ISRC = isrc
    };

    [TestMethod]
    public async Task GetISRCData_IsrcHeldByAnotherSong_SkipsItAndQueuesThePair()
    {
        var (dms, testIndex) = await CreateTestEnv("SharedIdGuard_Isrc");
        var holder = await SaveSong(dms, "Holder", "Purchase:00:RS=USRC10000001");
        var song = await SaveSong(dms, "Catalog Song", "Purchase:00:SS=spotifyId1");
        var manager = new FakeMusicServiceManager(new Dictionary<string, string>
        {
            ["spotifyId1"] = "USRC10000001"
        });

        var result = await manager.GetISRCData(dms, song);

        Assert.IsFalse(result);
        Assert.AreEqual(0, testIndex.EditCalls.Count);
        var row = await dms.Context.SharedIdCandidates.SingleAsync();
        CollectionAssert.AreEquivalent(new[] { holder.SongId, song.SongId }, new[] { row.SongA, row.SongB });
        Assert.AreEqual("R:USRC10000001", row.ServiceIds);
        Assert.AreEqual("ISRC lookup", row.Source);
        StringAssert.Contains(row.Detail, "spotifyId1");
    }

    [TestMethod]
    public async Task AttachTracksToSong_TrackIdHeldByAnotherSong_LeavesItOff()
    {
        var (dms, _) = await CreateTestEnv("SharedIdGuard_TrackId");
        _ = await SaveSong(dms, "Holder", "Purchase:00:SS=sp9");
        var song = await SaveSong(dms, "Catalog Song", "Purchase:00:SS=sp1");
        var manager = new MusicServiceManager(new Mock<IConfiguration>().Object);

        await manager.AttachTracksToSong(dms, song, [Track("sp9")]);

        var saved = await dms.SongIndex.FindSong(song.SongId);
        CollectionAssert.DoesNotContain(saved.GetExtendedPurchaseIds().ToList(), "S:sp9");
        var row = await dms.Context.SharedIdCandidates.SingleAsync();
        Assert.AreEqual("S:sp9", row.ServiceIds);
        Assert.AreEqual("Spotify lookup", row.Source);
    }

    [TestMethod]
    public async Task AttachTracksToSong_OnlyIsrcHeld_AttachesTheTrackWithoutIt()
    {
        var (dms, _) = await CreateTestEnv("SharedIdGuard_TrackIsrc");
        _ = await SaveSong(dms, "Holder", "Purchase:00:RS=USRC10000002");
        var song = await SaveSong(dms, "Catalog Song", "Purchase:00:SS=sp1");
        var manager = new MusicServiceManager(new Mock<IConfiguration>().Object);

        await manager.AttachTracksToSong(dms, song, [Track("sp10", "USRC10000002")]);

        var ids = (await dms.SongIndex.FindSong(song.SongId)).GetExtendedPurchaseIds().ToList();
        CollectionAssert.Contains(ids, "S:sp10");
        CollectionAssert.DoesNotContain(ids, "R:USRC10000002");
        Assert.AreEqual("R:USRC10000002", (await dms.Context.SharedIdCandidates.SingleAsync()).ServiceIds);
    }

    [TestMethod]
    public async Task AttachTracksToSong_NoOtherHolder_AttachesBothIds()
    {
        var (dms, _) = await CreateTestEnv("SharedIdGuard_Clear");
        var song = await SaveSong(dms, "Catalog Song", "Purchase:00:SS=sp1");
        var manager = new MusicServiceManager(new Mock<IConfiguration>().Object);

        await manager.AttachTracksToSong(dms, song, [Track("sp11", "USRC10000003")]);

        var ids = (await dms.SongIndex.FindSong(song.SongId)).GetExtendedPurchaseIds().ToList();
        CollectionAssert.IsSubsetOf(new[] { "S:sp11", "R:USRC10000003" }, ids);
        Assert.AreEqual(0, await dms.Context.SharedIdCandidates.CountAsync());
    }
}
