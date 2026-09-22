using AutoMapper;

using m4d.Controllers;
using m4d.Services;
using m4d.Tests.TestHelpers;
using m4d.Utilities;

using m4dModels;
using m4dModels.Tests;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.FeatureManagement;
using Microsoft.Net.Http.Headers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Moq;

using System.Net;
using System.Security.Claims;
using System.Security.Principal;

namespace m4d.Tests.Controllers;

[TestClass]
public class SongControllerTests
{
    #region RemoveDeadPurchaseId Tests

    [TestMethod]
    public void RemoveDeadPurchaseId_SingleId_RemovesTheWholeProperty()
    {
        var props = new List<SongProperty> { new("Purchase:00:SS", "deadTrack123") };
        var del = new List<SongProperty>();

        var changed = SongController.RemoveDeadPurchaseId(props, del, 'S', "deadTrack123");

        Assert.IsTrue(changed);
        Assert.AreEqual(1, del.Count);
        Assert.AreEqual("deadTrack123", del[0].Value);
    }

    [TestMethod]
    public void RemoveDeadPurchaseId_MultipleIds_StripsOnlyTheDeadOnePreservingSiblings()
    {
        // A property may hold more than one id for the same service/type (see
        // AlbumDetails.AddPurchaseId) - removing a dead one should not lose a sibling
        // that's still good.
        var prop = new SongProperty("Purchase:00:SS", "deadTrack123,liveTrack456");
        var props = new List<SongProperty> { prop };
        var del = new List<SongProperty>();

        var changed = SongController.RemoveDeadPurchaseId(props, del, 'S', "deadTrack123");

        Assert.IsTrue(changed);
        Assert.AreEqual(0, del.Count);
        Assert.AreEqual("liveTrack456", prop.Value);
    }

    [TestMethod]
    public void RemoveDeadPurchaseId_IdNotPresent_NoOp()
    {
        var props = new List<SongProperty> { new("Purchase:00:SS", "liveTrack456") };
        var del = new List<SongProperty>();

        var changed = SongController.RemoveDeadPurchaseId(props, del, 'S', "deadTrack123");

        Assert.IsFalse(changed);
        Assert.AreEqual(0, del.Count);
    }

    [TestMethod]
    public void RemoveDeadPurchaseId_EmptyDeadId_NoOp()
    {
        var props = new List<SongProperty> { new("Purchase:00:SS", "liveTrack456") };
        var del = new List<SongProperty>();

        var changed = SongController.RemoveDeadPurchaseId(props, del, 'S', "");

        Assert.IsFalse(changed);
        Assert.AreEqual(0, del.Count);
    }

    #endregion

    #region Bare /song/artist Tests

    // Every artist link the site emits carries a name, so a bare /song/artist is a crawler that
    // truncated the query (issue #284). It redirects to the browsable index instead of erroring -
    // but only when that index is actually on.

    [TestMethod]
    public async Task Artist_NoName_ArtistIndexEnabled_RedirectsPermanentlyToIndex()
    {
        var controller = await CreateController(artistIndexEnabled: true);

        var result = await controller.Artist(null);

        var redirect = (RedirectResult)result;
        Assert.AreEqual("/song/artists", redirect.Url);
        Assert.IsTrue(redirect.Permanent, "A bare artist URL should 301 so crawlers stop asking.");
    }

    [TestMethod]
    public async Task Artist_BlankName_ArtistIndexEnabled_RedirectsPermanentlyToIndex()
    {
        var controller = await CreateController(artistIndexEnabled: true);

        var result = await controller.Artist("   ");

        var redirect = (RedirectResult)result;
        Assert.AreEqual("/song/artists", redirect.Url);
        Assert.IsTrue(redirect.Permanent);
    }

    [TestMethod]
    public async Task Artist_NoName_ArtistIndexDisabled_ReturnsNotFound()
    {
        // With the flag off, Artists() itself 404s - redirecting there would only add a hop.
        var controller = await CreateController(artistIndexEnabled: false);

        var result = await controller.Artist(null);

        Assert.IsInstanceOfType<ViewResult>(result);
        Assert.AreEqual("HttpError", ((ViewResult)result).ViewName);
        Assert.AreEqual(
            (int)HttpStatusCode.NotFound, controller.ControllerContext.HttpContext.Response.StatusCode);
    }

    #endregion

    #region Test factory

    private static async Task<SongController> CreateController(bool artistIndexEnabled)
    {
        var dms = await DanceMusicTester.CreateServiceWithUsers("BareArtistUrl");

        var featureManager = new Mock<IFeatureManagerSnapshot>();
        featureManager
            .Setup(m => m.IsEnabledAsync(FeatureFlags.ArtistIndex))
            .ReturnsAsync(artistIndexEnabled);

        var userStore = new Mock<IUserStore<ApplicationUser>>();
#pragma warning disable CS8625 // UserManager's optional dependencies are all nullable in practice
        var spotifyUserManager = new Mock<UserManager<ApplicationUser>>(
            userStore.Object, null, null, null, null, null, null, null, null);
#pragma warning restore CS8625
        var configuration = new ConfigurationBuilder().Build();

        var controller = new SongController(
            dms.Context,
            dms.UserManager,
            new Mock<ISearchServiceManager>().Object,
            new Mock<IDanceStatsManager>().Object,
            configuration,
            new Mock<IFileProvider>().Object,
            new TestBackgroundTaskQueue(),
            featureManager.Object,
            NullLogger<SongController>.Instance,
            new Mock<LinkGenerator>().Object,
            new Mock<IMapper>().Object,
            new SpotifyAuthService(configuration, spotifyUserManager.Object),
            serviceHealth: null);

        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new GenericIdentity("", "TestAuthentication"))
        };

        // CheckSpiders() treats a missing user agent as a bad bot and short-circuits every action
        // in this controller, so the request needs one to reach the code under test.
        httpContext.Request.Headers[HeaderNames.UserAgent] = "Mozilla/5.0 (Test)";

        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return controller;
    }

    #endregion
}
