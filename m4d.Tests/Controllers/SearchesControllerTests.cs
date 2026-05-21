using AutoMapper;

using m4d.Controllers;
using m4d.Tests.TestHelpers;

using m4dModels;
using m4dModels.Tests;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.FeatureManagement;

using Moq;

using System.Security.Claims;
using System.Security.Principal;

namespace m4d.Tests.Controllers;

/// <summary>
/// Integration tests for SearchesController delete operations.
///
/// Both DeleteConfirmed (single item) and DeleteAllConfirmed (bulk) anonymize rather than
/// hard-deleting so search data is preserved for site-wide statistics.
///
/// Each user search is either:
///   (a) merged into an existing anonymous row with the same Query (counts summed, dates widened), or
///   (b) detached from the user by setting ApplicationUserId = null.
/// </summary>
[TestClass]
public class SearchesControllerTests
{
    [ClassInitialize]
    public static async Task ClassInitialize(TestContext _)
    {
        await DanceMusicTester.LoadDances();
    }

    // -------------------------------------------------------------------------
    // Test factory
    // -------------------------------------------------------------------------

    private static SearchesController CreateController(DanceMusicService dms, string userName)
    {
        var mockSearchService = new Mock<ISearchServiceManager>();
        mockSearchService
            .Setup(m => m.GetSongFilter(It.IsAny<string>()))
            .Returns<string>(s => SongFilter.Create(false, s));

        // Access internal IDanceStatsManager via the compiler-generated backing field
        var danceStatsField = typeof(DanceMusicCoreService)
            .GetField("<DanceStatsManager>k__BackingField",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        if (danceStatsField == null)
        {
            throw new InvalidOperationException(
                "Could not find DanceStatsManager backing field via reflection. " +
                "Update this test helper if the property implementation changed.");
        }
        var danceStats = (IDanceStatsManager)danceStatsField.GetValue(dms)!;

        var controller = new SearchesController(
            dms.Context,
            dms.UserManager,
            mockSearchService.Object,
            danceStats,
            new ConfigurationBuilder().Build(),
            new Mock<IFileProvider>().Object,
            new TestBackgroundTaskQueue(),
            new Mock<IFeatureManagerSnapshot>().Object,
            NullLogger<SearchesController>.Instance,
            new Mock<LinkGenerator>().Object,
            new Mock<IMapper>().Object,
            serviceHealth: null
        );

        var identity = new GenericIdentity(userName, "TestAuthentication");
        var principal = new ClaimsPrincipal(identity);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };

        return controller;
    }

    /// <summary>
    /// Adds a Search row directly to the in-memory database and returns it.
    /// </summary>
    private static async Task<Search> AddSearch(
        DanceMusicContext context,
        string? userId,
        string query,
        int count,
        DateTime created,
        DateTime modified)
    {
        var search = new Search
        {
            ApplicationUserId = userId,
            Query = query,
            Count = count,
            Created = created,
            Modified = modified,
        };
        context.Searches.Add(search);
        await context.SaveChangesAsync();
        return search;
    }

    // -------------------------------------------------------------------------
    // Tests
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task DeleteAllConfirmed_NoAnonymousCounterpart_DetachesRowFromUser()
    {
        // Arrange
        var dms = await DanceMusicTester.CreateServiceWithUsers("ClearHistory_Detach");
        var user = await dms.FindUser("dwgray");
        const string query = ".-CHA-.-.-.-.-120.0-124.0";

        await AddSearch(dms.Context, user.Id, query, count: 5,
            created: new DateTime(2024, 1, 1),
            modified: new DateTime(2025, 1, 1));

        var controller = CreateController(dms, "dwgray");

        // Act
        var result = await controller.DeleteAllConfirmed(sort: null);

        // Assert — redirect back to Index
        Assert.IsInstanceOfType<RedirectToActionResult>(result);

        // The user's row is now anonymous
        var searches = await dms.Context.Searches.Where(s => s.Query == query).ToListAsync();
        Assert.AreEqual(1, searches.Count, "Row should still exist");
        Assert.IsNull(searches[0].ApplicationUserId, "Row should be detached from the user");
        Assert.AreEqual(5, searches[0].Count, "Count should be unchanged");
    }

    [TestMethod]
    public async Task DeleteAllConfirmed_AnonymousCounterpartExists_MergesAndRemovesUserRow()
    {
        // Arrange
        var dms = await DanceMusicTester.CreateServiceWithUsers("ClearHistory_Merge");
        var user = await dms.FindUser("dwgray");
        const string query = ".-WLZ-.-.-.-.-90.0-95.0";

        // User row: count 4
        await AddSearch(dms.Context, user.Id, query, count: 4,
            created: new DateTime(2024, 6, 1),
            modified: new DateTime(2025, 3, 1));

        // Pre-existing anonymous row: count 10
        await AddSearch(dms.Context, userId: null, query, count: 10,
            created: new DateTime(2024, 8, 1),
            modified: new DateTime(2025, 1, 1));

        var controller = CreateController(dms, "dwgray");

        // Act
        await controller.DeleteAllConfirmed(sort: null);

        // Assert — only 1 row remains (anonymous), user row removed
        var searches = await dms.Context.Searches.Where(s => s.Query == query).ToListAsync();
        Assert.AreEqual(1, searches.Count, "User row should be removed; only anonymous row remains");
        Assert.IsNull(searches[0].ApplicationUserId, "Remaining row should be anonymous");
        Assert.AreEqual(14, searches[0].Count, "Counts should be summed (4 + 10)");
    }

    [TestMethod]
    public async Task DeleteAllConfirmed_MergesDateRange_TakesEarlierCreatedAndLaterModified()
    {
        // Arrange
        var dms = await DanceMusicTester.CreateServiceWithUsers("ClearHistory_Dates");
        var user = await dms.FindUser("dwgray");
        const string query = ".-FOX-.-.-.-.-112.0-120.0";

        var userCreated = new DateTime(2023, 1, 1);   // earlier
        var userModified = new DateTime(2025, 6, 1);  // later

        var anonCreated = new DateTime(2024, 1, 1);   // later
        var anonModified = new DateTime(2024, 12, 1); // earlier

        await AddSearch(dms.Context, user.Id, query, count: 3,
            created: userCreated, modified: userModified);
        await AddSearch(dms.Context, userId: null, query, count: 7,
            created: anonCreated, modified: anonModified);

        var controller = CreateController(dms, "dwgray");

        // Act
        await controller.DeleteAllConfirmed(sort: null);

        // Assert — date range widened correctly
        var anon = await dms.Context.Searches.SingleAsync(s => s.Query == query);
        Assert.AreEqual(userCreated, anon.Created,
            "Created should be the earlier of the two (user's)");
        Assert.AreEqual(userModified, anon.Modified,
            "Modified should be the later of the two (user's)");
    }

    [TestMethod]
    public async Task DeleteAllConfirmed_MergesDateRange_PreservesAnonDatesWhenWider()
    {
        // Arrange
        var dms = await DanceMusicTester.CreateServiceWithUsers("ClearHistory_AnonDateWider");
        var user = await dms.FindUser("dwgray");
        const string query = ".-SBA-.-.-.-.-96.0-104.0";

        var anonCreated = new DateTime(2022, 1, 1);   // earlier
        var anonModified = new DateTime(2026, 1, 1);  // later

        var userCreated = new DateTime(2024, 1, 1);   // later (within anon range)
        var userModified = new DateTime(2025, 1, 1);  // earlier (within anon range)

        await AddSearch(dms.Context, user.Id, query, count: 2,
            created: userCreated, modified: userModified);
        await AddSearch(dms.Context, userId: null, query, count: 8,
            created: anonCreated, modified: anonModified);

        var controller = CreateController(dms, "dwgray");

        // Act
        await controller.DeleteAllConfirmed(sort: null);

        // Assert — anon's already-wider dates are preserved
        var anon = await dms.Context.Searches.SingleAsync(s => s.Query == query);
        Assert.AreEqual(anonCreated, anon.Created,
            "Created should be the earlier of the two (anon's)");
        Assert.AreEqual(anonModified, anon.Modified,
            "Modified should be the later of the two (anon's)");
    }

    [TestMethod]
    public async Task DeleteAllConfirmed_MultipleSearches_MixedMergeAndDetach()
    {
        // Arrange — two searches: one has an anon match, one does not
        var dms = await DanceMusicTester.CreateServiceWithUsers("ClearHistory_Mixed");
        var user = await dms.FindUser("dwgray");
        const string queryWithAnon = ".-TAN-.-.-.-.-120.0-128.0";
        const string queryNoAnon  = ".-QST-.-.-.-.-180.0-200.0";

        await AddSearch(dms.Context, user.Id, queryWithAnon, count: 5,
            created: new DateTime(2024, 1, 1), modified: new DateTime(2025, 1, 1));
        await AddSearch(dms.Context, userId: null, queryWithAnon, count: 15,
            created: new DateTime(2023, 1, 1), modified: new DateTime(2024, 6, 1));

        await AddSearch(dms.Context, user.Id, queryNoAnon, count: 3,
            created: new DateTime(2024, 3, 1), modified: new DateTime(2024, 9, 1));

        var controller = CreateController(dms, "dwgray");

        // Act
        await controller.DeleteAllConfirmed(sort: null);

        // Assert — no user rows remain
        var userRows = await dms.Context.Searches.Where(s => s.ApplicationUserId == user.Id).ToListAsync();
        Assert.AreEqual(0, userRows.Count, "All user rows should be gone");

        // The merged query has one anonymous row with combined count
        var mergedRows = await dms.Context.Searches.Where(s => s.Query == queryWithAnon).ToListAsync();
        Assert.AreEqual(1, mergedRows.Count);
        Assert.AreEqual(20, mergedRows[0].Count, "Counts should be summed (5 + 15)");
        Assert.IsNull(mergedRows[0].ApplicationUserId);

        // The no-anon query has been detached (row kept, user dissociated)
        var detachedRows = await dms.Context.Searches.Where(s => s.Query == queryNoAnon).ToListAsync();
        Assert.AreEqual(1, detachedRows.Count);
        Assert.AreEqual(3, detachedRows[0].Count, "Count unchanged for detached row");
        Assert.IsNull(detachedRows[0].ApplicationUserId);
    }

    [TestMethod]
    public async Task DeleteAllConfirmed_UserHasNoSearches_RedirectsWithoutError()
    {
        // Arrange — user exists but has no searches
        var dms = await DanceMusicTester.CreateServiceWithUsers("ClearHistory_Empty");
        var controller = CreateController(dms, "dwgray");

        // Act
        var result = await controller.DeleteAllConfirmed(sort: null);

        // Assert — no exception, redirect returned
        Assert.IsInstanceOfType<RedirectToActionResult>(result);
        Assert.AreEqual(0, await dms.Context.Searches.CountAsync(), "Database should remain empty");
    }

    // -------------------------------------------------------------------------
    // Single-item delete (DeleteConfirmed) — same anonymize/merge logic
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task DeleteConfirmed_NoAnonymousCounterpart_DetachesRowFromUser()
    {
        // Arrange
        var dms = await DanceMusicTester.CreateServiceWithUsers("DeleteSingle_Detach");
        var user = await dms.FindUser("dwgray");
        const string query = ".-CHA-.-.-.-.-120.0-124.0";

        var search = await AddSearch(dms.Context, user.Id, query, count: 5,
            created: new DateTime(2024, 1, 1),
            modified: new DateTime(2025, 1, 1));

        var controller = CreateController(dms, "dwgray");

        // Act
        var result = await controller.DeleteConfirmed(search.Id, sort: null);

        // Assert — redirect, row kept, user dissociated
        Assert.IsInstanceOfType<RedirectToActionResult>(result);

        var rows = await dms.Context.Searches.Where(s => s.Query == query).ToListAsync();
        Assert.AreEqual(1, rows.Count, "Row should still exist");
        Assert.IsNull(rows[0].ApplicationUserId, "Row should be detached from the user");
        Assert.AreEqual(5, rows[0].Count, "Count should be unchanged");
    }

    [TestMethod]
    public async Task DeleteConfirmed_AnonymousCounterpartExists_MergesAndRemovesUserRow()
    {
        // Arrange
        var dms = await DanceMusicTester.CreateServiceWithUsers("DeleteSingle_Merge");
        var user = await dms.FindUser("dwgray");
        const string query = ".-WLZ-.-.-.-.-90.0-95.0";

        var search = await AddSearch(dms.Context, user.Id, query, count: 4,
            created: new DateTime(2024, 6, 1),
            modified: new DateTime(2025, 3, 1));

        await AddSearch(dms.Context, userId: null, query, count: 10,
            created: new DateTime(2024, 8, 1),
            modified: new DateTime(2025, 1, 1));

        var controller = CreateController(dms, "dwgray");

        // Act
        await controller.DeleteConfirmed(search.Id, sort: null);

        // Assert — only anonymous row remains with merged count
        var rows = await dms.Context.Searches.Where(s => s.Query == query).ToListAsync();
        Assert.AreEqual(1, rows.Count, "User row should be removed; only anonymous row remains");
        Assert.IsNull(rows[0].ApplicationUserId);
        Assert.AreEqual(14, rows[0].Count, "Counts should be summed (4 + 10)");
    }

    [TestMethod]
    public async Task DeleteConfirmed_MergesDateRange_WidensToEarlierCreatedAndLaterModified()
    {
        // Arrange — user Created is earlier, user Modified is later
        var dms = await DanceMusicTester.CreateServiceWithUsers("DeleteSingle_Dates");
        var user = await dms.FindUser("dwgray");
        const string query = ".-FOX-.-.-.-.-112.0-120.0";

        var userCreated  = new DateTime(2023, 1, 1);
        var userModified = new DateTime(2025, 6, 1);

        var search = await AddSearch(dms.Context, user.Id, query, count: 3,
            created: userCreated, modified: userModified);
        await AddSearch(dms.Context, userId: null, query, count: 7,
            created: new DateTime(2024, 1, 1),
            modified: new DateTime(2024, 12, 1));

        var controller = CreateController(dms, "dwgray");

        // Act
        await controller.DeleteConfirmed(search.Id, sort: null);

        // Assert
        var anon = await dms.Context.Searches.SingleAsync(s => s.Query == query);
        Assert.AreEqual(userCreated,  anon.Created,  "Created should take the earlier date");
        Assert.AreEqual(userModified, anon.Modified, "Modified should take the later date");
    }

    [TestMethod]
    public async Task DeleteConfirmed_UnknownId_ReturnsError()
    {
        // Arrange
        var dms = await DanceMusicTester.CreateServiceWithUsers("DeleteSingle_NotFound");
        var controller = CreateController(dms, "dwgray");

        // Act
        var result = await controller.DeleteConfirmed(id: -1, sort: null);

        // Assert — error view returned, nothing in DB
        Assert.IsInstanceOfType<ViewResult>(result);
        Assert.AreEqual(0, await dms.Context.Searches.CountAsync());
    }
}
