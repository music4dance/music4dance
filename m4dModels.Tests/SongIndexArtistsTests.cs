using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;

namespace m4dModels.Tests;

/// <summary>
/// Index-side behavior for individual artists (architecture/artist-index-plan.md §7-§9), exercised
/// through the in-memory SongIndexLocal.
/// </summary>
[TestClass]
public class SongIndexArtistsTests
{
    private static async Task<(DanceMusicService dms, TestSongIndex index)> CreateService(string name)
    {
        await DanceMusicTester.LoadDances();
        var dms = await DanceMusicTester.CreateService(name, useTestSongIndex: true);
        _ = await dms.AddPseudoUser(Song.ArtistBotUser, "artist-bot@music4dance.net");
        return (dms, (TestSongIndex)dms.SongIndex);
    }

    private static Task<Song> CreateSong(DanceMusicCoreService dms, string title, string artist) =>
        Song.Create(
            $".Create=\tUser=dwgray\tTime=01/15/2024 14:30:00\tTitle={title}\tArtist={artist}\t" +
            "Tag+=Cha Cha:Dance\tDanceRating=CHA+1",
            dms);

    [TestMethod]
    public void BuildIndex_HasFilterableFacetableArtistsCollection()
    {
        var index = new TestSongIndex();
        var field = index.BuildIndexFields().Single(f => f.Name == Song.ArtistsField);

        Assert.AreEqual(SearchFieldDataType.Collection(SearchFieldDataType.String), field.Type);
        Assert.IsTrue(field.IsFilterable);
        Assert.IsTrue(field.IsFacetable);
        Assert.IsTrue(field.IsSearchable);
    }

    [TestMethod]
    public async Task DocumentFromSong_IncludesArtistsOnlyWhenIndexHasField()
    {
        var (dms, index) = await CreateService("ArtistsDocument");
        var song = await CreateSong(dms, "Hold My Heart (feat. ZZ Ward)", "Lindsey Stirling");
        _ = song.UpdateArtists(null);

        var without = (SearchDocument)index.CallDocumentFromSong(song);
        Assert.IsFalse(without.ContainsKey(Song.ArtistsField));

        var with = (SearchDocument)index.CallDocumentFromSong(song, includeArtists: true);
        CollectionAssert.AreEqual(new[] { "Lindsey Stirling", "ZZ Ward" }, (string[])with[Song.ArtistsField]);
    }

    [TestMethod]
    public async Task DocumentFromSong_SingleArtistSongIndexesTheCredit()
    {
        var (dms, index) = await CreateService("ArtistsDocumentSingle");
        var song = await CreateSong(dms, "Come Away With Me", "Norah Jones");

        var doc = (SearchDocument)index.CallDocumentFromSong(song, includeArtists: true);
        CollectionAssert.AreEqual(new[] { "Norah Jones" }, (string[])doc[Song.ArtistsField]);
    }

    [TestMethod]
    public async Task SaveSong_AppliesArtistSplitter()
    {
        var (dms, index) = await CreateService("ArtistsSaveHook");
        var song = await CreateSong(dms, "Hold My Heart (feat. ZZ Ward)", "Lindsey Stirling");

        await index.SaveSong(song);

        var saved = await index.FindSong(song.SongId);
        CollectionAssert.AreEqual(new[] { "Lindsey Stirling", "ZZ Ward" }, saved.Artists.ToList());
        Assert.AreEqual(ArtistsSource.Heuristic, saved.ArtistsSource);
        Assert.IsTrue(saved.SongProperties.Any(p => p.Name == Song.UserField && p.Value == "artist-bot|P"));
    }

    [TestMethod]
    public async Task SaveSong_SplitterIsDormantWithoutArtistBotUser()
    {
        await DanceMusicTester.LoadDances();
        var dms = await DanceMusicTester.CreateService("ArtistsDormant", useTestSongIndex: true);
        var index = (TestSongIndex)dms.SongIndex;
        var song = await CreateSong(dms, "Hold My Heart (feat. ZZ Ward)", "Lindsey Stirling");

        await index.SaveSong(song);

        var saved = await index.FindSong(song.SongId);
        Assert.IsNull(saved.Artists);
        Assert.IsFalse(saved.SongProperties.Any(p => p.Value == "artist-bot|P"));
    }

    [TestMethod]
    public async Task SaveSong_UsesCatalogEvidenceForAmbiguousSplits()
    {
        var (dms, index) = await CreateService("ArtistsEvidence");
        await index.SaveSong(await CreateSong(dms, "Jolene", "Dolly Parton"));

        var duet = await CreateSong(dms, "Islands in the Stream", "Dolly Parton & Kenny Rogers");
        await index.SaveSong(duet);
        Assert.IsNull((await index.FindSong(duet.SongId)).Artists, "Only one half is a known artist");

        await index.SaveSong(await CreateSong(dms, "The Gambler", "Kenny Rogers"));
        var resaved = await index.FindSong(duet.SongId);
        await index.SaveSong(resaved);

        CollectionAssert.AreEqual(new[] { "Dolly Parton", "Kenny Rogers" },
            (await index.FindSong(duet.SongId)).Artists.ToList());
    }

    [TestMethod]
    public async Task FindArtist_IndividualArtistsMatchesCollaborationsCaseInsensitively()
    {
        var (dms, index) = await CreateService("ArtistsFind");
        var solo = await CreateSong(dms, "Everything", "Michael Bublé");
        var duet = await CreateSong(dms, "Someday (feat. Meghan Trainor)", "Michael Buble");
        var other = await CreateSong(dms, "All About That Bass", "Meghan Trainor");
        await index.SaveSongs([solo, duet, other]);

        var legacy = (await index.FindArtist("Meghan Trainor", CruftFilter.AllCruft)).Select(s => s.SongId).ToList();
        CollectionAssert.AreEquivalent(new[] { other.SongId }, legacy);

        var individual = (await index.FindArtist("meghan trainor", CruftFilter.AllCruft, individualArtists: true))
            .Select(s => s.SongId).ToList();
        CollectionAssert.AreEquivalent(new[] { duet.SongId, other.SongId }, individual);

        var bubles = (await index.FindArtist("Michael Buble", CruftFilter.AllCruft, individualArtists: true))
            .Select(s => s.SongId).ToList();
        CollectionAssert.AreEquivalent(new[] { solo.SongId, duet.SongId }, bubles);
    }
}
