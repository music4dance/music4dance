using m4d.Controllers;
using m4d.Services.ServiceHealth;
using m4d.Tests.TestHelpers;

using m4dModels;
using m4dModels.Tests;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.FeatureManagement;

using Moq;

using System.Security.Claims;
using System.Security.Principal;

namespace m4d.Tests.Controllers;

/// <summary>
/// Tests for the bulk "Delete Unconfirmed" admin action: deletes users who have never
/// confirmed their email and signed up more than 90 days ago.
///
/// Note: the actual per-user anonymize-then-remove step (Database.ChangeUserName, shared with
/// the single-user Delete/DeleteConfirmed action) calls into SongIndex.FindUserSongs, which is
/// not virtual and always resolves a real Azure Search client - there is no test double for it
/// in this codebase (the single-user Delete action has the same gap). These tests cover
/// everything that IS testable without a live search backend: the candidate query and the
/// per-user failure isolation.
/// </summary>
[TestClass]
public class ApplicationUsersControllerTests
{
    [ClassInitialize]
    public static async Task ClassInitialize(TestContext _)
    {
        await DanceMusicTester.LoadDances();
    }

    private static ApplicationUsersController CreateController(
        DanceMusicService dms, ServiceHealthManager serviceHealth = null!)
    {
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

        var controller = new ApplicationUsersController(
            dms.Context,
            dms.UserManager,
            new Mock<ISearchServiceManager>().Object,
            danceStats,
            new ConfigurationBuilder().Build(),
            new Mock<IFileProvider>().Object,
            new TestBackgroundTaskQueue(),
            new Mock<IFeatureManagerSnapshot>().Object,
            NullLogger<ActivityLogController>.Instance,
            serviceHealth
        );

        var identity = new GenericIdentity("dwgray", "TestAuthentication");
        var principal = new ClaimsPrincipal(identity);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };

        return controller;
    }

    private static ServiceHealthManager CreateHealthySearchService()
    {
        var health = new ServiceHealthManager(NullLogger<ServiceHealthManager>.Instance);
        health.MarkHealthy("SearchService");
        return health;
    }

    private static async Task<ApplicationUser> MakeUnconfirmed(
        DanceMusicService dms, string userName, int daysOld)
    {
        var user = await dms.FindUser(userName);
        user.EmailConfirmed = false;
        user.StartDate = DateTime.Now.AddDays(-daysOld);
        _ = await dms.Context.SaveChangesAsync();
        return user;
    }

    [TestMethod]
    public async Task DeleteUnconfirmed_ReturnsOnlyUnconfirmedUsersOlderThan90Days()
    {
        // Arrange — "dwgray" and "Charlie" are unconfirmed; only "dwgray" is old enough.
        // "ohdwg" is unconfirmed but recent. "batch" (pseudo) is always confirmed already.
        var dms = await DanceMusicTester.CreateServiceWithUsers("DeleteUnconfirmed_Get");
        await MakeUnconfirmed(dms, "dwgray", daysOld: 120);
        await MakeUnconfirmed(dms, "Charlie", daysOld: 91);
        await MakeUnconfirmed(dms, "ohdwg", daysOld: 10);

        var controller = CreateController(dms);

        // Act
        var result = await controller.DeleteUnconfirmed() as ViewResult;

        // Assert
        var model = (List<ApplicationUser>)result!.Model!;
        var names = model.Select(u => u.UserName).ToHashSet();
        Assert.AreEqual(2, model.Count);
        CollectionAssert.Contains(names.ToList(), "dwgray");
        CollectionAssert.Contains(names.ToList(), "Charlie");
        CollectionAssert.DoesNotContain(names.ToList(), "ohdwg");
    }

    [TestMethod]
    public async Task DeleteUnconfirmedConfirmed_AnonymizationFailsForOneUser_SkipsItAndKeepsGoing()
    {
        // Arrange — search reports healthy so the guard passes, but the actual anonymize call
        // (Database.ChangeUserName -> SongIndex.FindUserSongs) always throws against the mocked
        // SongIndex used by CreateServiceWithUsers. This exercises the per-user try/catch in
        // DeleteUnconfirmedConfirmed: one user's anonymization failure must not crash the batch
        // or delete that user, and the action must still redirect normally.
        var dms = await DanceMusicTester.CreateServiceWithUsers("DeleteUnconfirmed_Post");
        await MakeUnconfirmed(dms, "dwgray", daysOld: 120);
        await MakeUnconfirmed(dms, "ohdwg", daysOld: 10);

        var controller = CreateController(dms, CreateHealthySearchService());

        // Act
        var result = await controller.DeleteUnconfirmedConfirmed();

        // Assert — redirects back to Index without throwing
        Assert.IsInstanceOfType<RedirectToActionResult>(result);

        // The qualifying user's anonymization failed, so it must be skipped rather than deleted
        Assert.IsNotNull(await dms.FindUser("dwgray"), "User should be kept when anonymization fails");
        Assert.IsNotNull(await dms.FindUser("ohdwg"), "Recently-signed-up unconfirmed user should be kept");
        Assert.IsNotNull(await dms.FindUser("batch"), "Pseudo user should be kept");
    }

    [TestMethod]
    public async Task DeleteUnconfirmedConfirmed_SearchServiceUnavailable_AbortsWithoutDeleting()
    {
        // Arrange — IsServiceHealthy optimistically defaults to true for an unmarked service
        // (matching every other caller in the app), so the guard must be tested by explicitly
        // marking the service unavailable, not just leaving it unmarked.
        var dms = await DanceMusicTester.CreateServiceWithUsers("DeleteUnconfirmed_Unavailable");
        await MakeUnconfirmed(dms, "dwgray", daysOld: 120);

        var unhealthy = new ServiceHealthManager(NullLogger<ServiceHealthManager>.Instance);
        unhealthy.MarkUnavailable("SearchService", "test: search index unreachable");
        var controller = CreateController(dms, unhealthy);

        // Act
        var result = await controller.DeleteUnconfirmedConfirmed() as ObjectResult;

        // Assert
        Assert.AreEqual(503, result!.StatusCode);
        Assert.IsNotNull(await dms.FindUser("dwgray"), "User should not be deleted when search is unavailable");
    }
}
