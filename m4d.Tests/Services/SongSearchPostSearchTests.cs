using Azure.Search.Documents;

using m4d.Services;
using m4d.Tests.TestHelpers;
using m4dModels;
using m4dModels.Tests;

using Moq;

using System.Linq;

namespace m4d.Tests.Services;

/// <summary>
/// Tests for SongSearch.PostSearch and SongSearch.EditedBySearch.
/// Uses a mock SongIndex to return predetermined songs so Azure Search is not called.
/// </summary>
[TestClass]
public class SongSearchPostSearchTests
{
    [ClassInitialize]
    public static async Task ClassInitialize(TestContext _)
    {
        await DanceMusicTester.LoadDances();
    }

    // ── Helper to yield a list as IAsyncEnumerable ──────────────────────────

    private static async IAsyncEnumerable<Song> AsAsyncEnumerable(IEnumerable<Song> songs)
    {
        foreach (var song in songs)
            yield return song;
        await Task.CompletedTask; // satisfies compiler for async iterator
    }

    // ── Helper to build a song with WasEditedBy support ─────────────────────

    private static Song MakeSong(string title, string userName, DateTime timestamp)
    {
        var raw = $".Create=\tUser={userName}\tTime={timestamp:MM/dd/yyyy HH:mm:ss}\tTitle={title}\tArtist=Test\tTempo=120.0";
        var song = new Song { Title = title, Artist = "Test" };
        SongProperty.Load(raw, song.SongProperties);
        return song;
    }

    // ── Helper to build a SongSearch with mocked SongIndex ──────────────────

    private static async Task<SongSearch> CreateSongSearchAsync(
        string dbName,
        IEnumerable<Song> songsToReturn)
    {
        var dms = await DanceMusicTester.CreateServiceWithUsers(dbName);

        var mockSearchService = new Mock<ISearchServiceManager>();
        mockSearchService
            .Setup(m => m.GetSongFilter(It.IsAny<string>()))
            .Returns<string>(s => SongFilter.Create(false, s));

        var mockStats = new Mock<IDanceStatsManager>();
        var serviceForSongIndex = new DanceMusicService(
            dms.Context,
            dms.UserManager,
            mockSearchService.Object,
            mockStats.Object);

        var mockSongIndex = new Mock<SongIndex>();
        mockSongIndex.Setup(m => m.DanceMusicService).Returns(serviceForSongIndex);

        var songList = songsToReturn.ToList();
        // Set up StreamAll to yield the provided songs one by one, mirroring the virtual method
        // PostSearch now uses for memory-efficient paging.
        mockSongIndex
            .Setup(m => m.StreamAll(
                It.IsAny<string>(),
                It.IsAny<SearchOptions>(),
                It.IsAny<CruftFilter>()))
            .Returns<string, SearchOptions, CruftFilter>((_, _, _) => AsAsyncEnumerable(songList));

        var queue = new TestBackgroundTaskQueue();
        return new SongSearch(
            SongFilter.Create(false, ""), "dwgray", true,
            mockSongIndex.Object, dms.UserManager, queue, null);
    }

    // ── EditedBySearch tests ─────────────────────────────────────────────────

    [TestMethod]
    public async Task EditedBySearch_ReturnsOnlySongsInDateRangeByUser()
    {
        var song2015 = MakeSong("2015 Song", "dwgray", new DateTime(2015, 6, 1));
        var song2016 = MakeSong("2016 Song", "dwgray", new DateTime(2016, 3, 1));
        var songOther = MakeSong("Other Song", "batch-a", new DateTime(2015, 6, 1));

        var songSearch = await CreateSongSearchAsync(
            "SongSearchPostSearch_EditedBy", [song2015, song2016, songOther]);

        var opts = new SearchOptions { Size = 25, Skip = 0 };
        var results = await songSearch.EditedBySearch(
            opts, "dwgray",
            new DateTime(2015, 1, 1), new DateTime(2015, 12, 31));

        Assert.AreEqual(1, results.Songs.Count(),
            "Only the 2015-dwgray song should match");
        Assert.AreEqual("2015 Song", results.Songs.First().Title);
    }

    [TestMethod]
    public async Task EditedBySearch_NoMatches_ReturnsEmpty()
    {
        var song = MakeSong("Some Song", "batch-a", new DateTime(2015, 6, 1));

        var songSearch = await CreateSongSearchAsync(
            "SongSearchPostSearch_NoMatch", [song]);

        var opts = new SearchOptions { Size = 25, Skip = 0 };
        var results = await songSearch.EditedBySearch(
            opts, "dwgray",
            new DateTime(2015, 1, 1), new DateTime(2015, 12, 31));

        Assert.AreEqual(0, results.Songs.Count());
        Assert.AreEqual(0, results.TotalCount);
    }

    [TestMethod]
    public async Task EditedBySearch_AllSongsMatch_ReturnsAll()
    {
        var songs = Enumerable.Range(1, 5)
            .Select(i => MakeSong($"Song {i}", "dwgray", new DateTime(2015, i, 1)))
            .ToList();

        var songSearch = await CreateSongSearchAsync(
            "SongSearchPostSearch_AllMatch", songs);

        var opts = new SearchOptions { Size = 25, Skip = 0 };
        var results = await songSearch.EditedBySearch(
            opts, "dwgray",
            new DateTime(2015, 1, 1), new DateTime(2015, 12, 31));

        Assert.AreEqual(5, results.Songs.Count());
        Assert.AreEqual(5, results.TotalCount);
    }

    // ── PostSearch pagination ────────────────────────────────────────────────

    [TestMethod]
    public async Task EditedBySearch_Pagination_SkipsCorrectly()
    {
        // 6 matching songs, page size 3, second page (skip 3)
        var songs = Enumerable.Range(1, 6)
            .Select(i => MakeSong($"Song {i}", "dwgray", new DateTime(2015, 1, i)))
            .ToList();

        var songSearch = await CreateSongSearchAsync(
            "SongSearchPostSearch_Paging", songs);

        var opts = new SearchOptions { Size = 3, Skip = 3 };
        var results = await songSearch.EditedBySearch(
            opts, "dwgray",
            new DateTime(2015, 1, 1), new DateTime(2015, 12, 31));

        Assert.AreEqual(3, results.Songs.Count(), "Page should contain 3 items");
        Assert.AreEqual(6, results.TotalCount, "TotalCount should reflect all matches");
    }
}
