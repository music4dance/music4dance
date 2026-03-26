using m4d.Services;
using m4d.Tests.TestHelpers;
using m4dModels;
using m4dModels.Tests;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace m4d.Tests.Services;

/// <summary>
/// Integration tests for SongSearch.LogSearch using TestBackgroundTaskQueue.
///
/// Test infrastructure:
/// - DanceMusicTester creates the real in-memory database and UserManager.
/// - A lightweight DanceMusicService (same DB, mock SearchService) is provided to
///   the mocked SongIndex so that SongSearch can initialise its Filter property.
/// - BuildTaskServiceProvider registers the same in-memory database so
///   TestBackgroundTaskQueue.ExecuteAllAsync can resolve DanceMusicContext and
///   actually write/read Searches rows.
///
/// After Phase 1 (MostRecentPage column), add MostRecentPage assertions to the two
/// page-related tests that are marked with TODO comments.
/// </summary>
[TestClass]
public class SongSearchLogTests
{
    [ClassInitialize]
    public static async Task ClassInitialize(TestContext _)
    {
        await DanceMusicTester.LoadDances();
    }

    // -------------------------------------------------------------------------
    // Infrastructure helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Builds a ServiceProvider containing a DanceMusicContext backed by the
    /// named in-memory database.  The task lambda inside LogSearch resolves
    /// DanceMusicContext from this scope factory when executed via ExecuteAllAsync.
    /// </summary>
    private static ServiceProvider BuildTaskServiceProvider(string dbName)
    {
        var services = new ServiceCollection();
        services.AddDbContext<DanceMusicContext>(options =>
            options.UseInMemoryDatabase(dbName));
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Creates a SongSearch instance wired up for testing LogSearch:
    ///   - dms  : real DanceMusicService with real UserManager (same in-memory DB)
    ///   - mockSongIndex : returns a minimal DanceMusicService whose SearchService
    ///                     mock handles GetSongFilter so that the SongSearch
    ///                     constructor can initialise its Filter property.
    ///   - queue : TestBackgroundTaskQueue that captures the enqueued task.
    ///   - taskProvider : ServiceProvider sharing the same in-memory DB so that
    ///                    ExecuteAllAsync can actually persist Search rows.
    /// </summary>
    private static async Task<(SongSearch songSearch, TestBackgroundTaskQueue queue, ServiceProvider taskProvider)>
        CreateSongSearchAsync(string dbName, string userName, SongFilter filter)
    {
        // Real service: UserManager + in-memory database
        var dms = await DanceMusicTester.CreateServiceWithUsers(dbName);

        // Mock SearchService so SongSearch constructor can call GetSongFilter
        var mockSearchService = new Mock<ISearchServiceManager>();
        mockSearchService
            .Setup(m => m.GetSongFilter(It.IsAny<string>()))
            .Returns<string>(s => SongFilter.Create(false, s));

        // A second context instance pointing at the SAME in-memory database
        var sharedCtxOptions = new DbContextOptionsBuilder<DanceMusicContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        // Minimal DanceMusicService for the SongIndex mock.
        // Only SearchService needs to work; DanceStatsManager methods are never
        // called during LogSearch so a default Moq mock is sufficient.
        var mockStats = new Mock<IDanceStatsManager>();
        var serviceForSongIndex = new DanceMusicService(
            new DanceMusicContext(sharedCtxOptions),
            dms.UserManager,
            mockSearchService.Object,
            mockStats.Object);

        // Mock SongIndex whose DanceMusicService property returns the above service
        var mockSongIndex = new Mock<SongIndex>();
        mockSongIndex.Setup(m => m.DanceMusicService).Returns(serviceForSongIndex);

        var queue = new TestBackgroundTaskQueue();
        var songSearch = new SongSearch(
            filter, userName, false, mockSongIndex.Object, dms.UserManager, queue, null);

        var taskProvider = BuildTaskServiceProvider(dbName);

        return (songSearch, queue, taskProvider);
    }

    // -------------------------------------------------------------------------
    // Tests
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task LogSearch_FirstVisit_CreatesRow()
    {
        const string dbName = "SongSearchLogTests_FirstVisit";
        var filter = SongFilter.Create(false, ".-CHA-.-.-.-.-120.0-124.0");
        var (songSearch, queue, taskProvider) =
            await CreateSongSearchAsync(dbName, "dwgray", filter);

        await songSearch.LogSearch(filter);

        Assert.AreEqual(1, queue.Count, "One background task should be enqueued");

        await queue.ExecuteAllAsync(taskProvider);

        var verifyOpts = new DbContextOptionsBuilder<DanceMusicContext>()
            .UseInMemoryDatabase(dbName).Options;
        using var verifyCtx = new DanceMusicContext(verifyOpts);

        var searches = verifyCtx.Searches.ToList();
        Assert.AreEqual(1, searches.Count, "One Search row should be created");
        Assert.AreEqual(1, searches[0].Count, "Count should be 1 on first visit");
    }

    [TestMethod]
    public async Task LogSearch_SecondVisit_IncrementsCount()
    {
        const string dbName = "SongSearchLogTests_SecondVisit";
        var filter = SongFilter.Create(false, ".-CHA-.-.-.-.-120.0-124.0");
        var (songSearch, queue, taskProvider) =
            await CreateSongSearchAsync(dbName, "dwgray", filter);

        // First visit
        await songSearch.LogSearch(filter);
        await queue.ExecuteAllAsync(taskProvider);

        // Second visit
        await songSearch.LogSearch(filter);
        await queue.ExecuteAllAsync(taskProvider);

        var verifyOpts = new DbContextOptionsBuilder<DanceMusicContext>()
            .UseInMemoryDatabase(dbName).Options;
        using var verifyCtx = new DanceMusicContext(verifyOpts);

        var searches = verifyCtx.Searches.ToList();
        Assert.AreEqual(1, searches.Count, "Still exactly one Search row");
        Assert.AreEqual(2, searches[0].Count, "Count should be 2 after second visit");
        // TODO (Phase 1): Assert.IsNull(searches[0].MostRecentPage) after second page-1 visit
    }

    [TestMethod]
    public async Task LogSearch_PageOne_StoresSearchRow()
    {
        // Establishes baseline: page 1 is logged and creates a row.
        // TODO (Phase 1): Assert that MostRecentPage is null for a page-1 visit.
        const string dbName = "SongSearchLogTests_PageOne";
        var filter = SongFilter.Create(false, ".-CHA-.-.-.-.-120.0-124.0");
        filter.Page = 1;

        var (songSearch, queue, taskProvider) =
            await CreateSongSearchAsync(dbName, "dwgray", filter);

        await songSearch.LogSearch(filter);
        await queue.ExecuteAllAsync(taskProvider);

        var verifyOpts = new DbContextOptionsBuilder<DanceMusicContext>()
            .UseInMemoryDatabase(dbName).Options;
        using var verifyCtx = new DanceMusicContext(verifyOpts);

        var searches = verifyCtx.Searches.ToList();
        Assert.AreEqual(1, searches.Count, "Page-1 visit should create a Search row");
        Assert.AreEqual(1, searches[0].Count);
    }

    [TestMethod]
    public async Task LogSearch_AnonymousUser_CreatesRowWithNullUserId()
    {
        // Documents current behaviour: anonymous searches are logged with
        // ApplicationUserId = null.  (Contrast with authenticated case above.)
        const string dbName = "SongSearchLogTests_Anonymous";
        var filter = SongFilter.Create(false, ".-CHA-.-.-.-.-120.0-124.0");
        var (songSearch, queue, taskProvider) =
            await CreateSongSearchAsync(dbName, "", filter);

        await songSearch.LogSearch(filter);

        Assert.AreEqual(1, queue.Count, "Task should be enqueued for anonymous user");

        await queue.ExecuteAllAsync(taskProvider);

        var verifyOpts = new DbContextOptionsBuilder<DanceMusicContext>()
            .UseInMemoryDatabase(dbName).Options;
        using var verifyCtx = new DanceMusicContext(verifyOpts);

        var searches = verifyCtx.Searches.ToList();
        Assert.AreEqual(1, searches.Count, "Anonymous search should create a row");
        Assert.IsNull(searches[0].ApplicationUserId, "Anonymous user should have null ApplicationUserId");
    }

    [TestMethod]
    public async Task LogSearch_CustomsearchAction_SkipsLogging()
    {
        const string dbName = "SongSearchLogTests_Customsearch";
        var filter = SongFilter.Create(false, null);
        filter.Action = "customsearch";

        var (songSearch, queue, taskProvider) =
            await CreateSongSearchAsync(dbName, "dwgray", filter);

        await songSearch.LogSearch(filter);

        Assert.AreEqual(0, queue.Count, "'customsearch' action must not enqueue any background task");
    }
}
