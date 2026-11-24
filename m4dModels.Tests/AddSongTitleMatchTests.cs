using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace m4dModels.Tests;

/// <summary>
/// The add-time title match (SongIndex.MergeFromTitle, reached through MatchSongs): versions of a
/// title that differ only in a remaster or single-version note (or a feat. clause) match, a dash
/// qualifier never matches the plain title, artists compare under their aliases, without a
/// leader's backing group and without a feat. credit, and length counts for every match type.
/// </summary>
[TestClass]
public class AddSongTitleMatchTests
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
        return dms;
    }

    private static async Task<Song> Save(DanceMusicService dms, string title, string artist,
        int? length = null, string album = "Album A")
    {
        var song = await Song.Create(Raw(title, artist, length, album, "Track:00=1"), dms);
        await dms.SongIndex.SaveSong(song);
        return song;
    }

    private static Task<Song> Incoming(DanceMusicService dms, string title, string artist,
        int? length = null) =>
        Song.Create(Raw(title, artist, length, "Album B", "Track:00=2"), dms);

    private static string Raw(string title, string artist, int? length, string album, string track)
    {
        var lengthProperty = length is { } seconds ? $"Length={seconds}\t" : "";
        return $".Create=\tUser=dwgray\tTime=01/01/2020 10:00:00 AM\tTitle={title}\tArtist={artist}\t" +
            $"{lengthProperty}Album:00={album}\t{track}\tDanceRating=SLS+1";
    }

    private static async Task<LocalMerger> Match(DanceMusicService dms, Song song) =>
        (await dms.SongIndex.MatchSongs([song], DanceMusicCoreService.MatchMethod.Merge))[0];

    [TestMethod]
    public async Task Artist_AnAliasMatchesTheSongCreditedUnderThePreferredName()
    {
        var dms = await CreateService("AddTitle_Alias");
        var existing = await Save(dms, "Just Give Me a Reason", "P!nk");

        var merge = await Match(dms, await Incoming(dms, "Just Give Me a Reason", "Pink"));

        Assert.AreEqual(existing.SongId, merge.Right?.SongId);
    }

    [TestMethod]
    public async Task Artist_ABackingGroupMatchesItsLeader()
    {
        var dms = await CreateService("AddTitle_BackingGroup");
        var existing = await Save(dms, "Sing Sing Sing", "Benny Goodman");

        var merge = await Match(dms, await Incoming(dms, "Sing Sing Sing", "Benny Goodman and His Orchestra"));

        Assert.AreEqual(existing.SongId, merge.Right?.SongId);
    }

    [TestMethod]
    public async Task Artist_ADifferentArtistStillDoesNotMatch()
    {
        var dms = await CreateService("AddTitle_DifferentArtist");
        _ = await Save(dms, "Song", "Artist A");

        var merge = await Match(dms, await Incoming(dms, "Song", "Artist B"));

        Assert.IsNull(merge.Right);
        Assert.AreEqual(MatchType.None, merge.MatchType);
    }

    [TestMethod]
    public async Task Artist_AFeaturedCreditMatchesTheSongOfItsPrimaryArtist()
    {
        // The track brings a guest ("feat.") onto the catalog's "The Artist" song: the same recording
        // (NameMergedSong then moves the guest into the individual artists)
        var dms = await CreateService("AddTitle_Featured");
        var existing = await Save(dms, "Crazy Song", "The Artist", 208);

        var merge = await Match(dms, await Incoming(dms, "Crazy Song", "The Artist feat. Crazy Man", 210));

        Assert.AreEqual(existing.SongId, merge.Right?.SongId);
    }

    [TestMethod]
    public async Task Title_AFeatClauseMatchesThePlainTitle()
    {
        var dms = await CreateService("AddTitle_FeatTitle");
        var existing = await Save(dms, "Crazy Song", "The Artist", 208);

        var merge = await Match(dms, await Incoming(dms, "Crazy Song (feat. Crazy Man)", "The Artist", 208));

        Assert.AreEqual(existing.SongId, merge.Right?.SongId);
    }

    [TestMethod]
    public async Task Artist_ABlankCreditStillFindsItsSong()
    {
        var dms = await CreateService("AddTitle_BlankCredit");
        var existing = await Save(dms, "Song", "Artist A");

        var merge = await Match(dms, await Incoming(dms, "Song", ""));

        Assert.AreEqual(existing.SongId, merge.Right?.SongId);
    }

    [TestMethod]
    public async Task Title_ARemasterNoteMatchesThePlainTitle()
    {
        var dms = await CreateService("AddTitle_Remaster");
        var existing = await Save(dms, "Song Name", "The Artist");

        var merge = await Match(dms, await Incoming(dms, "Song Name - Remastered 2011", "The Artist"));

        Assert.AreEqual(existing.SongId, merge.Right?.SongId);
    }

    [TestMethod]
    public async Task Title_ASingleVersionNoteMatchesThePlainTitle()
    {
        var dms = await CreateService("AddTitle_SingleVersion");
        var existing = await Save(dms, "Song Name", "The Artist");

        var merge = await Match(dms, await Incoming(dms, "Song Name (Single Version)", "The Artist"));

        Assert.AreEqual(existing.SongId, merge.Right?.SongId);
    }

    [TestMethod]
    public async Task Title_AQualifierDoesNotMatchEvenWhenTheLengthsAgree()
    {
        // "Kinney - The Remainder" and "Kinney - Don't Talk Like" are different songs: a dash
        // qualifier isn't a version note, so it never joins the plain title at add time
        var dms = await CreateService("AddTitle_QualifierNoMatch");
        _ = await Save(dms, "Song", "The Artist", 200);

        Assert.IsNull((await Match(dms, await Incoming(dms, "Song - Radio Edit", "The Artist", 205))).Right);
        Assert.IsNull((await Match(dms, await Incoming(dms, "Kinney - The Remainder", "The Artist", 200))).Right);
    }

    [TestMethod]
    public async Task Title_ADanceQualifierIsADifferentSong()
    {
        var dms = await CreateService("AddTitle_DanceQualifier");
        _ = await Save(dms, "Song", "The Artist", 200);

        var merge = await Match(dms, await Incoming(dms, "Song - Salsa Version", "The Artist", 200));

        Assert.IsNull(merge.Right);
    }

    [TestMethod]
    public async Task Length_AWeakMatchIsRejectedWhenTheLengthsAreFarApart()
    {
        var dms = await CreateService("AddTitle_WeakFar");
        _ = await Save(dms, "Song", "The Artist", 200);

        var merge = await Match(dms, await Incoming(dms, "Song", "The Artist", 260));

        Assert.IsNull(merge.Right);
        Assert.AreEqual(MatchType.None, merge.MatchType);
    }

    [TestMethod]
    public async Task Length_AWeakMatchStillTakesASongWithAnUnknownLength()
    {
        var dms = await CreateService("AddTitle_WeakUnknown");
        var existing = await Save(dms, "Song", "The Artist", 200);

        var merge = await Match(dms, await Incoming(dms, "Song", "The Artist"));

        Assert.AreEqual(MatchType.Weak, merge.MatchType);
        Assert.AreEqual(existing.SongId, merge.Right?.SongId);
    }

    [TestMethod]
    public async Task Length_TheLengthMatchWidensToTheSharedTolerance()
    {
        var dms = await CreateService("AddTitle_LengthTolerance");
        _ = await Save(dms, "Song", "The Artist", 215, "Album Z");
        _ = await Save(dms, "Song", "The Artist", 210, "Album Y");

        // Both candidates are 10 to 15 seconds from the new song: beyond the old 5-second window,
        // within the shared 15 now, so the length tier takes one of them
        var merge = await Match(dms, await Incoming(dms, "Song", "The Artist", 200));

        Assert.AreEqual(MatchType.Length, merge.MatchType);
        Assert.IsNotNull(merge.Right?.Length);
        Assert.IsTrue(Math.Abs(merge.Right.Length.Value - 200) <= SharedIdMatcher.LengthTolerance);
    }

    [TestMethod]
    public async Task Length_AnAlbumMatchRejectedWhenTheLengthsAreFarApart()
    {
        var dms = await CreateService("AddTitle_AlbumFar");
        _ = await Save(dms, "Song", "The Artist", 200, "Album B");

        var merge = await Match(dms, await Incoming(dms, "Song", "The Artist", 260));

        Assert.IsNull(merge.Right);
    }
}
