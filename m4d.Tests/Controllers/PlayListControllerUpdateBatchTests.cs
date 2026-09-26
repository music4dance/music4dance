using m4d.Controllers;
using m4d.Tests.TestHelpers;

using m4dModels;
using m4dModels.Tests;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.FeatureManagement;
using Microsoft.Net.Http.Headers;

using Moq;

using System.Diagnostics;
using System.Security.Claims;
using System.Security.Principal;
using System.Text;

namespace m4d.Tests.Controllers;

/// <summary>
/// UpdateBatch is called by the UpdatePlaylists Logic App. It used to hold the request for the whole
/// import, so the Logic App timed out and its retry got a 200 "already running" false success.
/// It now returns 200 as soon as the work is started (the Logic App uses a polling trigger, which
/// skips the run on a 202) and real error codes otherwise.
/// </summary>
[TestClass]
[DoNotParallelize] // Tests share the process-wide AdminMonitor slot
public class PlayListControllerUpdateBatchTests
{
    // TokenRequirement caches the first key it sees, so every test uses the same one
    private const string Key = "update-batch-test-key";

    [ClassInitialize]
    public static async Task ClassInitialize(TestContext _)
    {
        await DanceMusicTester.LoadDances();
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (AdminMonitor.IsRunning)
        {
            AdminMonitor.CompleteTask(true, "test cleanup");
        }
    }

    [TestMethod]
    public async Task UpdateBatch_NoToken_Returns401()
    {
        var controller = await CreateController("NoToken", authorized: false);

        var result = await controller.UpdateBatch();

        Assert.IsInstanceOfType<UnauthorizedObjectResult>(result);
        Assert.IsFalse(AdminMonitor.IsRunning);
    }

    [TestMethod]
    public async Task UpdateBatch_SpotifyFromSearch_Returns400WithoutTakingTheSlot()
    {
        var controller = await CreateController("FromSearch");

        var result = await controller.UpdateBatch(PlayListType.SpotifyFromSearch);

        Assert.IsInstanceOfType<BadRequestObjectResult>(result);
        Assert.IsFalse(AdminMonitor.IsRunning);
    }

    [TestMethod]
    public async Task UpdateBatch_SlotBusy_Returns409()
    {
        var controller = await CreateController("Busy");
        Assert.IsTrue(AdminMonitor.StartTask("SomethingElse"));

        var result = await controller.UpdateBatch();

        Assert.IsInstanceOfType<ConflictObjectResult>(result);
        Assert.AreEqual("SomethingElse", AdminMonitor.Name);
    }

    [TestMethod]
    public async Task UpdateBatch_Authorized_Returns200AndReleasesTheSlotWhenDone()
    {
        var controller = await CreateController("Accepted");

        var result = await controller.UpdateBatch();

        Assert.IsInstanceOfType<OkObjectResult>(result);

        // No playlists in the test database, so the background work finishes almost immediately
        var stopwatch = Stopwatch.StartNew();
        while (AdminMonitor.IsRunning && stopwatch.Elapsed < TimeSpan.FromSeconds(10))
        {
            await Task.Delay(25);
        }

        Assert.IsFalse(AdminMonitor.IsRunning);
        Assert.IsTrue(AdminMonitor.Succeeded, AdminMonitor.Status.Status);
    }

    [TestMethod]
    public async Task UpdateBatchStatus_NoToken_Returns401()
    {
        var controller = await CreateController("StatusNoToken", authorized: false);

        Assert.IsInstanceOfType<UnauthorizedObjectResult>(controller.UpdateBatchStatus());
    }

    [TestMethod]
    public async Task UpdateBatchStatus_Authorized_ReportsTheRunningTask()
    {
        var controller = await CreateController("Status");
        Assert.IsTrue(AdminMonitor.StartTask("UpdateAllPlayLists"));

        var result = controller.UpdateBatchStatus();

        var ok = (OkObjectResult)result;
        var isRunning = ok.Value?.GetType().GetProperty("IsRunning")?.GetValue(ok.Value);
        Assert.AreEqual(true, isRunning);
    }

    private static async Task<PlayListController> CreateController(string name, bool authorized = true)
    {
        var dms = await DanceMusicTester.CreateServiceWithUsers($"UpdateBatch{name}");
        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
        configuration["Authentication:RecomputeJob:Key"] = Key;

        var controller = new PlayListController(
            dms.Context,
            dms.UserManager,
            new Mock<ISearchServiceManager>().Object,
            new Mock<IDanceStatsManager>().Object,
            configuration,
            new Mock<IFileProvider>().Object,
            new TestBackgroundTaskQueue(),
            new Mock<IFeatureManagerSnapshot>().Object,
            NullLogger<PlayListController>.Instance,
            serviceHealth: null);

        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new GenericIdentity("", "TestAuthentication"))
        };
        httpContext.Request.Headers[HeaderNames.UserAgent] = "Mozilla/5.0 (Test)";
        if (authorized)
        {
            httpContext.Request.Headers[HeaderNames.Authorization] =
                $"Token {Convert.ToBase64String(Encoding.UTF8.GetBytes(Key))}";
        }

        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return controller;
    }
}
