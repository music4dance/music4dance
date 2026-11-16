using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace m4dModels.Tests;

/// <summary>
/// Sort out ids on the shared-id review: which of the songs each id they share belongs to
/// (SharedIdOwners.Propose), and the removals that carry it out.
/// </summary>
[TestClass]
public class SharedIdOwnersTests
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

    // A song with albums: each entry is an album's name and its ids ("SS=sp1", "IS=it1", "RS=…")
    private static async Task<Song> SaveSong(DanceMusicService dms, string title, string artist, int length,
        params (string Name, string[] Ids)[] albums)
    {
        var properties = albums.SelectMany((a, i) =>
            new[] { $"Album:{i:00}={a.Name}", $"Track:{i:00}=1" }.Concat(a.Ids.Select(id => $"Purchase:{i:00}:{id}")));
        var song = await Song.Create(
            $".Create=\tUser=dwgray\tTime=01/01/2020 10:00:00 AM\tTitle={title}\tArtist={artist}\t" +
            $"Length={length}\t{string.Join("\t", properties)}\tDanceRating=SLS+1",
            dms);
        await dms.SongIndex.SaveSong(song);
        return song;
    }

    // A lookup that knows these tracks, by extended id
    private static Func<string, Task<ServiceTrack>> Tracks(params ServiceTrack[] tracks) =>
        id => Task.FromResult(tracks.FirstOrDefault(t => t.TrackId == id));

    private static ServiceTrack Track(string id, string title, string artist, int length, string isrc = null) =>
        new() { TrackId = id, Name = title, Artist = artist, Duration = length, ISRC = isrc };

    private static async Task<(Song Love, Song Other)> TwoSongs(DanceMusicService dms)
    {
        var love = await SaveSong(dms, "Love Song", "The Artist", 200,
            ("Album A", ["SS=sp1", "SS=a1", "RS=USRC1"]));
        var other = await SaveSong(dms, "Other Song", "Someone Else", 300,
            ("Album B", ["SS=sp1", "RS=USRC1"]), ("Album C", ["SS=c1"]));
        return (love, other);
    }

    [TestMethod]
    public async Task Propose_TheSongThatIsTheTrack_OwnsItAndItsIsrc()
    {
        var (dms, _) = await CreateService("SharedIdOwners_Owned");
        var (love, other) = await TwoSongs(dms);

        var ownership = await SharedIdOwners.Propose([love, other],
            Tracks(Track("S:sp1", "Love Song (feat. Crazy Man)", "The Artist", 203, "usrc1")));

        var ids = ownership.Ids.ToDictionary(p => p.Id);
        CollectionAssert.AreEqual(new[] { "S:sp1", "R:USRC1" }, ownership.Ids.Select(p => p.Id).ToList());
        Assert.AreEqual((SharedIdOwnerStatus.Owned, love.SongId), (ids["S:sp1"].Status, ids["S:sp1"].Song));
        Assert.AreEqual((SharedIdOwnerStatus.Owned, love.SongId, "S:sp1"),
            (ids["R:USRC1"].Status, ids["R:USRC1"].Song, ids["R:USRC1"].Via));
        // Album B is left with no ids, so it goes
        CollectionAssert.AreEqual(new[] { new SharedIdRemoval(other.SongId, 0) }, ownership.Removals.ToList());
        Assert.AreEqual("Love Song (feat. Crazy Man)", ownership.Tracks["S:sp1"].Name);
    }

    [TestMethod]
    public async Task Propose_ATrackNoSongIs_IsBadAndComesOffEverySong()
    {
        var (dms, _) = await CreateService("SharedIdOwners_Bad");
        var (love, other) = await TwoSongs(dms);

        var ownership = await SharedIdOwners.Propose([love, other],
            Tracks(Track("S:sp1", "Unrelated", "Nobody", 200, "USRC1")));

        var ids = ownership.Ids.ToDictionary(p => p.Id);
        Assert.AreEqual(SharedIdOwnerStatus.Bad, ids["S:sp1"].Status);
        Assert.AreEqual(SharedIdOwnerStatus.Unsure, ids["R:USRC1"].Status, "An ISRC only moves with an owned id");
        CollectionAssert.AreEquivalent(
            new[] { new SharedIdRemoval(love.SongId, 0, "S:sp1"), new SharedIdRemoval(other.SongId, 0, "S:sp1") },
            ownership.Removals.ToList());
    }

    [TestMethod]
    [DataRow("Love Song", "The Artist", 260, "the length")]
    [DataRow(null, null, 0, "not found")]
    public async Task Propose_UnsureIds_ProposeNothing(string title, string artist, int length, string why)
    {
        var (dms, _) = await CreateService($"SharedIdOwners_Unsure_{length}");
        var (love, other) = await TwoSongs(dms);
        var lookup = title == null ? Tracks() : Tracks(Track("S:sp1", title, artist, length));

        var ownership = await SharedIdOwners.Propose([love, other], lookup);

        Assert.IsTrue(ownership.Ids.All(p => p.Status == SharedIdOwnerStatus.Unsure), why);
        Assert.IsEmpty(ownership.Removals);
    }

    [TestMethod]
    public async Task Propose_TwoSongsThatAreBothTheTrack_IsUnsure()
    {
        var (dms, _) = await CreateService("SharedIdOwners_Both");
        var a = await SaveSong(dms, "Love Song", "The Artist", 200, ("Album A", ["SS=sp1"]));
        var b = await SaveSong(dms, "Love Song", "The Artist", 205, ("Album B", ["SS=sp1"]));

        var ownership = await SharedIdOwners.Propose([a, b], Tracks(Track("S:sp1", "Love Song", "The Artist", 202)));

        Assert.AreEqual(SharedIdOwnerStatus.Unsure, ownership.Ids.Single().Status);
        Assert.IsEmpty(ownership.Removals);
    }

    [TestMethod]
    public async Task ProposeOwners_ThenApply_SeparatesTheSongs()
    {
        var (dms, user) = await CreateService("SharedIdOwners_Apply");
        var (love, other) = await TwoSongs(dms);
        _ = await dms.SharedIds.Seed();
        var cluster = (await dms.SharedIds.PendingClusters()).Single();

        var ownership = await dms.SharedIds.ProposeOwners(cluster.SongIds,
            Tracks(Track("S:sp1", "Love Song", "The Artist", 200, "USRC1")));
        var result = await dms.SharedIds.Apply(user,
        [
            new SharedIdChoice(SharedIdAction.Leave, [.. cluster.SongIds], [.. cluster.Candidates.Select(c => c.Id)],
                Removals: ownership.Removals)
        ]);

        Assert.AreEqual(1, result.UnsharedPairs);
        Assert.IsEmpty(await dms.SharedIds.PendingClusters());
        var separated = await dms.SongIndex.FindSong(other.SongId);
        Assert.AreEqual("Album C", separated.Albums.Single().Name);
        CollectionAssert.AreEquivalent(new[] { "S:sp1", "S:a1", "R:USRC1" },
            (await dms.SongIndex.FindSong(love.SongId)).GetExtendedPurchaseIds().ToList());
    }
}
