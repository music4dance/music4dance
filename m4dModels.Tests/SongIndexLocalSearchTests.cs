namespace m4dModels.Tests;

// Exercises SongIndexLocal.Search(SongFilter, ...) - the Option A structured-query evaluation
// added for the sandbox's song-list/browse UI (architecture/contributor-test-environments.md,
// L1f). Calls the override directly rather than going through SongSearch, since SongSearch
// needs a working ISearchServiceManager that DanceMusicTester.CreateService doesn't wire up.
[TestClass]
public class SongIndexLocalSearchTests
{
    [ClassInitialize]
    public static async Task ClassInitialize(TestContext _)
    {
        await DanceMusicTester.LoadDances();
    }

    private static async Task<TestSongIndex> CreateEnv(string name)
    {
        var dms = await DanceMusicTester.CreateService(name, useTestSongIndex: true);
        return (TestSongIndex)dms.SongIndex;
    }

    private static async Task<Song> CreateSong(TestSongIndex index, string raw)
    {
        var song = await Song.Create(raw, index.DanceMusicService);
        await index.SaveSong(song);
        return song;
    }

    [TestMethod]
    public async Task Search_DanceFilter_ExcludesSongsWithoutThatDance()
    {
        var index = await CreateEnv("SILSearch_DanceFilter");
        await CreateSong(index,
            ".Create=\tUser=dwgray\tTime=00/00/0000 0:00:00 PM\tTitle=Cha Song\tArtist=A\tTempo=120.0\tTag+=Cha Cha:Dance\tDanceRating=CHA+1");
        await CreateSong(index,
            ".Create=\tUser=dwgray\tTime=00/00/0000 0:00:00 PM\tTitle=Rumba Song\tArtist=A\tTempo=100.0\tTag+=Rumba:Dance\tDanceRating=RMB+1");

        var filter = SongFilter.Create(false, "");
        filter.Dances = "CHA";

        var results = await index.Search(filter, 25, CruftFilter.AllCruft);

        Assert.AreEqual(1, results.Songs.Count());
        Assert.AreEqual("Cha Song", results.Songs.First().Title);
    }

    [TestMethod]
    public async Task Search_TempoRange_ExcludesSongsOutsideRange()
    {
        var index = await CreateEnv("SILSearch_TempoRange");
        await CreateSong(index,
            ".Create=\tUser=dwgray\tTime=00/00/0000 0:00:00 PM\tTitle=Slow Song\tArtist=A\tTempo=80.0\tTag+=Rumba:Dance\tDanceRating=RMB+1");
        await CreateSong(index,
            ".Create=\tUser=dwgray\tTime=00/00/0000 0:00:00 PM\tTitle=Fast Song\tArtist=A\tTempo=200.0\tTag+=Rumba:Dance\tDanceRating=RMB+1");

        var filter = SongFilter.Create(false, "");
        filter.TempoMin = 150;

        var results = await index.Search(filter, 25, CruftFilter.AllCruft);

        Assert.AreEqual(1, results.Songs.Count());
        Assert.AreEqual("Fast Song", results.Songs.First().Title);
    }

    [TestMethod]
    public async Task Search_Keyword_MatchesTitleSubstringCaseInsensitively()
    {
        var index = await CreateEnv("SILSearch_Keyword");
        await CreateSong(index,
            ".Create=\tUser=dwgray\tTime=00/00/0000 0:00:00 PM\tTitle=Blue Moon\tArtist=A\tTempo=100.0");
        await CreateSong(index,
            ".Create=\tUser=dwgray\tTime=00/00/0000 0:00:00 PM\tTitle=Red Sun\tArtist=A\tTempo=100.0");

        var filter = SongFilter.Create(false, "");
        filter.SearchString = "blue";

        var results = await index.Search(filter, 25, CruftFilter.AllCruft);

        Assert.AreEqual(1, results.Songs.Count());
        Assert.AreEqual("Blue Moon", results.Songs.First().Title);
    }

    [TestMethod]
    public async Task Search_TagFilter_MatchesSongLevelGenreTag()
    {
        var index = await CreateEnv("SILSearch_TagFilter");
        await CreateSong(index,
            ".Create=\tUser=dwgray\tTime=00/00/0000 0:00:00 PM\tTitle=Rock Song\tArtist=A\tTempo=100.0\tTag+=Rock:Music");
        await CreateSong(index,
            ".Create=\tUser=dwgray\tTime=00/00/0000 0:00:00 PM\tTitle=Jazz Song\tArtist=A\tTempo=100.0\tTag+=Jazz:Music");

        var filter = SongFilter.Create(false, "");
        filter.Tags = "Rock:Music";

        var results = await index.Search(filter, 25, CruftFilter.AllCruft);

        Assert.AreEqual(1, results.Songs.Count());
        Assert.AreEqual("Rock Song", results.Songs.First().Title);
    }

    [TestMethod]
    public async Task Search_SortByTitleDescending_OrdersZToA()
    {
        var index = await CreateEnv("SILSearch_SortTitle");
        await CreateSong(index,
            ".Create=\tUser=dwgray\tTime=00/00/0000 0:00:00 PM\tTitle=Alpha\tArtist=A\tTempo=100.0");
        await CreateSong(index,
            ".Create=\tUser=dwgray\tTime=00/00/0000 0:00:00 PM\tTitle=Beta\tArtist=A\tTempo=100.0");

        var filter = SongFilter.Create(false, "");
        filter.SortOrder = "Title_desc";

        var results = await index.Search(filter, 25, CruftFilter.AllCruft);

        var titles = results.Songs.Select(s => s.Title).ToList();
        CollectionAssert.AreEqual(new[] { "Beta", "Alpha" }, titles);
    }

    [TestMethod]
    public async Task Search_Paging_ReturnsRequestedPageAndTotalCount()
    {
        var index = await CreateEnv("SILSearch_Paging");
        for (var i = 0; i < 5; i++)
        {
            await CreateSong(index,
                $".Create=\tUser=dwgray\tTime=00/00/0000 0:00:00 PM\tTitle=Song {i:00}\tArtist=A\tTempo=100.0");
        }

        var filter = SongFilter.Create(false, "");
        filter.SortOrder = "Title";
        filter.Page = 2;

        var results = await index.Search(filter, 2, CruftFilter.AllCruft);

        Assert.AreEqual(5, results.TotalCount, "Total count should reflect all matches, not just the page");
        Assert.AreEqual(2, results.Songs.Count());
        Assert.AreEqual("Song 02", results.Songs.First().Title, "Page 2 with size 2 should start at the 3rd song");
    }
}
