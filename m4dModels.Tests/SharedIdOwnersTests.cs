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

    // A lookup that knows these tracks, by extended id, and the songs' own tracks (Own)
    private static Func<string, Task<ServiceTrack>> Tracks(params ServiceTrack[] tracks) =>
        id => Task.FromResult(tracks.Concat(Own).FirstOrDefault(t => t.TrackId == id));

    // A lookup the services never answer
    private static Task<ServiceTrack> NoAnswer(string id) => Task.FromResult<ServiceTrack>(null);

    private static ServiceTrack Track(string id, string title, string artist, int length, string isrc = null) =>
        new() { TrackId = id, Name = title, Artist = artist, Duration = length, ISRC = isrc };

    // The tracks of the ids only one song in these tests has, each that song's
    private static readonly ServiceTrack[] Own =
    [
        Track("S:a1", "Love Song", "The Artist", 200),
        Track("S:c1", "Other Song", "Someone Else", 300),
        Track("I:c2", "Other Song", "Someone Else", 300),
        Track("I:it2", "Other Song", "Someone Else", 300),
    ];

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
    [DataRow("Tony Bennett", true)]
    [DataRow("Tony Bennett & Lady Gaga", false)]
    public async Task Propose_TheSongThatNamesTheTracksArtists_OwnsIt(string trackCredit, bool spotify)
    {
        var (dms, _) = await CreateService("SharedIdOwners_Artists" + spotify);
        var solo = await SaveSong(dms, "Cheek to Cheek", "Tony Bennett", 200, ("Album A", ["SS=sp1", "IS=it1"]));
        var duet = await SaveSong(dms, "Cheek to Cheek", "Tony Bennett & Lady Gaga", 200,
            ("Album B", ["SS=sp1", "IS=it1"]));
        // Spotify lists each artist and credits the first; iTunes gives the whole credit
        var track = spotify
            ? new ServiceTrack
            {
                TrackId = "S:sp1", Name = "Cheek to Cheek", Artist = trackCredit, Duration = 200,
                Artists = ["Tony Bennett", "Lady Gaga"]
            }
            : Track("I:it1", "Cheek to Cheek", trackCredit, 200);

        var ownership = await SharedIdOwners.Propose([solo, duet], Tracks(track));

        var proposal = ownership.Ids.Single(p => p.Id == track.TrackId);
        Assert.AreEqual((SharedIdOwnerStatus.Owned, duet.SongId), (proposal.Status, proposal.Song));
    }

    [TestMethod]
    public async Task Propose_ATrackBySomeOfTheSongsArtists_IsTheSongThatNamesThem()
    {
        var (dms, _) = await CreateService("SharedIdOwners_Solo");
        var solo = await SaveSong(dms, "Cheek to Cheek", "Tony Bennett", 200, ("Album A", ["IS=it1"]));
        var duet = await SaveSong(dms, "Cheek to Cheek", "Tony Bennett & Lady Gaga", 200, ("Album B", ["IS=it1"]));

        var ownership = await SharedIdOwners.Propose([solo, duet],
            Tracks(Track("I:it1", "Cheek to Cheek", "Tony Bennett", 200)));

        var proposal = ownership.Ids.Single(p => p.Id == "I:it1");
        Assert.AreEqual((SharedIdOwnerStatus.Owned, solo.SongId), (proposal.Status, proposal.Song));
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
        // The ISRC goes with the bad id from each album, which leaves Album B with nothing
        CollectionAssert.AreEquivalent(
            new[]
            {
                new SharedIdRemoval(love.SongId, 0, "S:sp1"), new SharedIdRemoval(love.SongId, 0, "R:USRC1"),
                new SharedIdRemoval(other.SongId, 0)
            },
            ownership.Removals.ToList());
    }

    [TestMethod]
    public async Task Propose_AnAlbumThatLosesItsSpotifyId_LosesItsIsrc()
    {
        var (dms, _) = await CreateService("SharedIdOwners_Isrc");
        var love = await SaveSong(dms, "Love Song", "The Artist", 200, ("Album A", ["SS=sp1", "RS=USRC1"]));
        var other = await SaveSong(dms, "Other Song", "Someone Else", 300,
            ("Album B", ["SS=sp1", "IS=it2", "RS=USRC2"]), ("Album C", ["SS=c1", "IS=c2", "RS=USRC3"]));

        var ownership = await SharedIdOwners.Propose([love, other],
            Tracks(Track("S:sp1", "Love Song", "The Artist", 200, "USRC1")));

        // Album B's ISRC isn't shared, but it came with one of its tracks, so it goes too
        CollectionAssert.AreEquivalent(
            new[] { new SharedIdRemoval(other.SongId, 0, "S:sp1"), new SharedIdRemoval(other.SongId, 0, "R:USRC2") },
            ownership.Removals.ToList());
    }

    [TestMethod]
    public async Task Propose_AnIsrcAKeptTrackReports_Stays()
    {
        var (dms, _) = await CreateService("SharedIdOwners_IsrcKept");
        var love = await SaveSong(dms, "Love Song", "The Artist", 200, ("Album A", ["SS=sp1", "IS=it1", "RS=USRC1"]));
        var other = await SaveSong(dms, "Other Song", "Someone Else", 300,
            ("Album B", ["SS=sp1", "IS=it1", "RS=USRC1"]));

        // sp1 is Other Song's; it1 is Love Song's, and its track reports the ISRC
        var ownership = await SharedIdOwners.Propose([love, other],
            Tracks(Track("S:sp1", "Other Song", "Someone Else", 300, "USRC9"),
                Track("I:it1", "Love Song", "The Artist", 200, "USRC1")));

        CollectionAssert.AreEquivalent(
            new[]
            {
                new SharedIdRemoval(love.SongId, 0, "S:sp1"),
                new SharedIdRemoval(other.SongId, 0, "I:it1"), new SharedIdRemoval(other.SongId, 0, "R:USRC1")
            },
            ownership.Removals.ToList());
    }

    [TestMethod]
    public async Task Propose_TheSongWhoseTitleIsTheTracks_OwnsIt()
    {
        var (dms, _) = await CreateService("SharedIdOwners_Title");
        var plain = await SaveSong(dms, "Song Name", "The Artist", 200, ("Album A", ["SS=plain", "SS=salsa"]));
        var salsa = await SaveSong(dms, "Song Name - Salsa Version", "The Artist", 205,
            ("Album B", ["SS=plain", "SS=salsa"]));

        // Both songs pass the fuzzy match for either track, and both lengths are close
        var ownership = await SharedIdOwners.Propose([plain, salsa],
            Tracks(Track("S:plain", "Song Name", "The Artist", 202),
                Track("S:salsa", "Song Name - Salsa Version", "The Artist", 202)));

        var ids = ownership.Ids.ToDictionary(p => p.Id);
        Assert.AreEqual((SharedIdOwnerStatus.Owned, plain.SongId), (ids["S:plain"].Status, ids["S:plain"].Song));
        Assert.AreEqual((SharedIdOwnerStatus.Owned, salsa.SongId), (ids["S:salsa"].Status, ids["S:salsa"].Song));
        CollectionAssert.AreEquivalent(
            new[] { new SharedIdRemoval(plain.SongId, 0, "S:salsa"), new SharedIdRemoval(salsa.SongId, 0, "S:plain") },
            ownership.Removals.ToList());
    }

    [TestMethod]
    [DataRow("Love Song", "The Artist", 260, "the length")]
    [DataRow(null, null, 0, "the service never answered")]
    public async Task Propose_UnsureIds_ProposeNothing(string title, string artist, int length, string why)
    {
        var (dms, _) = await CreateService($"SharedIdOwners_Unsure_{length}");
        var (love, other) = await TwoSongs(dms);
        var lookup = title == null ? NoAnswer : Tracks(Track("S:sp1", title, artist, length));

        var ownership = await SharedIdOwners.Propose([love, other], lookup);

        Assert.IsTrue(ownership.Ids.All(p => p.Status == SharedIdOwnerStatus.Unsure), why);
        Assert.IsEmpty(ownership.Removals);
    }

    [TestMethod]
    public async Task Propose_ATrackTheServiceDoesntHave_IsBad_WhenItHasTheOthers()
    {
        var (dms, _) = await CreateService("SharedIdOwners_NotFound");
        var (love, other) = await TwoSongs(dms);

        // sp1 isn't there, but the songs' own tracks are
        var ownership = await SharedIdOwners.Propose([love, other], Tracks());

        var sp1 = ownership.Ids.Single(p => p.Id == "S:sp1");
        Assert.AreEqual((SharedIdOwnerStatus.Bad, "The service doesn't have the track"), (sp1.Status, sp1.Reason));
        CollectionAssert.Contains(ownership.Removals.ToList(), new SharedIdRemoval(other.SongId, 0));
    }

    [TestMethod]
    public async Task Propose_AnIdOnTheWrongSong_MovesToTheSongItBelongsTo()
    {
        var (dms, _) = await CreateService("SharedIdOwners_Move");
        var plain = await SaveSong(dms, "Song Title", "The Artist", 200,
            ("Plain Album", ["SS=plain", "RS=USPLAIN"]), ("Acoustic Album", ["SS=acoustic", "SS=shared"]));
        var acoustic = await SaveSong(dms, "Song Title - Acoustic", "The Artist", 240,
            ("Live Album", ["SS=live", "SS=shared"]));

        var ownership = await SharedIdOwners.Propose([plain, acoustic], Tracks(
            Track("S:plain", "Song Title", "The Artist", 200, "USPLAIN"),
            Track("S:acoustic", "Song Title - Acoustic", "The Artist", 241, "USACOU"),
            Track("S:live", "Song Title - Acoustic", "The Artist", 239),
            Track("S:shared", "Song Title - Acoustic", "The Artist", 240)));

        var ids = ownership.Ids.ToDictionary(p => p.Id);
        // The plain song's own S:plain is where it belongs, so it isn't listed
        CollectionAssert.AreEquivalent(new[] { "S:shared", "S:acoustic" }, ids.Keys.ToList());
        Assert.AreEqual((SharedIdOwnerStatus.Owned, acoustic.SongId, false),
            (ids["S:acoustic"].Status, ids["S:acoustic"].Song, ids["S:acoustic"].Shared));
        // The plain song's Acoustic Album is left with nothing
        CollectionAssert.AreEquivalent(new[] { new SharedIdRemoval(plain.SongId, 1) }, ownership.Removals.ToList());
        CollectionAssert.AreEqual(new[] { new SharedIdAddition(acoustic.SongId, "S:acoustic", plain.SongId, 1, "USACOU") },
            ownership.Additions.ToList());
    }

    [TestMethod]
    public async Task Propose_SongsThatShareOnlyAnIsrc_GiveItToTheSongWhoseTrackReportsIt()
    {
        var (dms, _) = await CreateService("SharedIdOwners_IsrcOnly");
        var love = await SaveSong(dms, "Love Song", "The Artist", 200, ("Album A", ["SS=a1", "RS=USRC1"]));
        var other = await SaveSong(dms, "Other Song", "Someone Else", 300, ("Album C", ["SS=c1", "RS=USRC1"]));

        var ownership = await SharedIdOwners.Propose([love, other],
            Tracks(Track("S:a1", "Love Song", "The Artist", 200, "USRC1")));

        var isrc = ownership.Ids.Single();
        Assert.AreEqual(("R:USRC1", SharedIdOwnerStatus.Owned, love.SongId, "S:a1"),
            (isrc.Id, isrc.Status, isrc.Song, isrc.Via));
        CollectionAssert.AreEqual(new[] { new SharedIdRemoval(other.SongId, 0, "R:USRC1") },
            ownership.Removals.ToList());
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
                Removals: ownership.Removals, Additions: ownership.Additions)
        ]);

        Assert.AreEqual(1, result.UnsharedPairs);
        Assert.IsEmpty(await dms.SharedIds.PendingClusters());
        var separated = await dms.SongIndex.FindSong(other.SongId);
        Assert.AreEqual("Album C", separated.Albums.Single().Name);
        CollectionAssert.AreEquivalent(new[] { "S:sp1", "S:a1", "R:USRC1" },
            (await dms.SongIndex.FindSong(love.SongId)).GetExtendedPurchaseIds().ToList());
    }

    [TestMethod]
    public async Task ProposeOwners_ThenApply_MovesAnIdWithItsAlbum()
    {
        var (dms, user) = await CreateService("SharedIdOwners_ApplyMove");
        var plain = await SaveSong(dms, "Song Title", "The Artist", 200,
            ("Plain Album", ["SS=plain", "SS=shared"]), ("Acoustic Album", ["SS=acoustic", "SA=album9"]));
        var acoustic = await SaveSong(dms, "Song Title - Acoustic", "The Artist", 240,
            ("Live Album", ["SS=shared"]));
        _ = await dms.SharedIds.Seed();
        var cluster = (await dms.SharedIds.PendingClusters()).Single();

        var ownership = await dms.SharedIds.ProposeOwners(cluster.SongIds, Tracks(
            Track("S:plain", "Song Title", "The Artist", 200),
            Track("S:acoustic", "Song Title - Acoustic", "The Artist", 240, "USACOU"),
            Track("S:shared", "Song Title - Acoustic", "The Artist", 240)));
        var result = await dms.SharedIds.Apply(user,
        [
            new SharedIdChoice(SharedIdAction.Leave, [.. cluster.SongIds], [.. cluster.Candidates.Select(c => c.Id)],
                Removals: ownership.Removals, Additions: ownership.Additions)
        ]);

        Assert.AreEqual((2, 1), (result.Edited, result.UnsharedPairs));
        var moved = await dms.SongIndex.FindSong(acoustic.SongId);
        var album = moved.Albums.Single(a => a.Name == "Acoustic Album");
        CollectionAssert.AreEquivalent(new[] { "S:acoustic", "R:USACOU" },
            album.GetExtendedPurchaseIds(PurchaseType.Song).ToList());
        Assert.AreEqual("album9", album.GetPurchaseIdentifier(ServiceType.Spotify, PurchaseType.Album));
        CollectionAssert.AreEquivalent(new[] { "S:plain" },
            (await dms.SongIndex.FindSong(plain.SongId)).GetExtendedPurchaseIds().ToList());
    }
}
