#nullable disable

using m4d.Controllers;
using m4d.Services;
using m4d.Tests.TestHelpers;
using m4d.Utilities;

using m4dModels;
using m4dModels.Tests;

using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.FeatureManagement;
using Microsoft.Net.Http.Headers;

using Moq;

using Newtonsoft.Json.Linq;

using System.Diagnostics;
using System.Security.Claims;
using System.Security.Principal;
using System.Text;

namespace m4d.Tests.Services;

/// <summary>
/// The Spotify service account: the stored token, its expiry alerts, the auth path that uses it,
/// and UpdateBatch?type=SpotifyFromSearch, which runs as it. See
/// architecture/music-services/spotify-playlist-automation.md.
/// </summary>
[TestClass]
[DoNotParallelize] // Shares the process-wide AdminMonitor slot and AdmAuthentication caches
public class ServiceAccountTests
{
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

        AdmAuthentication.Clear();
    }

    #region Store

    [TestMethod]
    public async Task Store_Connect_ProtectsAndReturnsTheRefreshToken()
    {
        var store = CreateStore(nameof(Store_Connect_ProtectsAndReturnsTheRefreshToken));

        await store.Connect(ServiceType.Spotify, "refresh-1", "m4d", "music4dance", "playlist-modify-public", "admin");

        var token = await store.Get(ServiceType.Spotify);
        Assert.IsNotNull(token);
        Assert.AreEqual("m4d", token.AccountId);
        Assert.AreEqual("admin", token.AuthorizedBy);
        Assert.AreNotEqual("refresh-1", token.ProtectedRefreshToken);
        Assert.AreEqual(token.AuthorizedAt.AddMonths(6), token.ExpiresAt);
        Assert.AreEqual("refresh-1", await store.GetRefreshToken(ServiceType.Spotify));
    }

    [TestMethod]
    public async Task Store_MarkInvalid_KeepsTheFirstReasonUntilReconnected()
    {
        var store = CreateStore(nameof(Store_MarkInvalid_KeepsTheFirstReasonUntilReconnected));
        await store.Connect(ServiceType.Spotify, "refresh-1", "m4d", "music4dance", null, "admin");

        await store.MarkInvalid(ServiceType.Spotify, "invalid_grant");
        await store.MarkInvalid(ServiceType.Spotify, "not connected");

        var token = await store.Get(ServiceType.Spotify);
        Assert.IsFalse(token.IsValid);
        Assert.AreEqual("invalid_grant", token.InvalidReason);
        Assert.IsNull(await store.GetRefreshToken(ServiceType.Spotify));

        await store.Connect(ServiceType.Spotify, "refresh-2", "m4d", "music4dance", null, "admin");
        Assert.IsTrue((await store.Get(ServiceType.Spotify)).IsValid);
        Assert.AreEqual("refresh-2", await store.GetRefreshToken(ServiceType.Spotify));
    }

    [TestMethod]
    public async Task Store_UpdatesOnlyAnExistingConnection()
    {
        var store = CreateStore(nameof(Store_UpdatesOnlyAnExistingConnection));

        await store.UpdateRefreshToken(ServiceType.Spotify, "refresh-1");
        await store.MarkInvalid(ServiceType.Spotify, "invalid_grant");

        Assert.IsNull(await store.Get(ServiceType.Spotify));
    }

    #endregion

    #region Monitor

    [TestMethod]
    public void Problem_NoneWhileMoreThanTwoWeeksRemain()
    {
        var token = ValidToken(DateTimeOffset.UtcNow.AddMonths(-5));

        Assert.IsNull(ServiceAccountMonitor.Problem(token, token.ExpiresAt.AddDays(-15)));
    }

    [TestMethod]
    public void Problem_WarnsInTheLastTwoWeeks()
    {
        var token = ValidToken(DateTimeOffset.UtcNow.AddMonths(-6));

        var problem = ServiceAccountMonitor.Problem(token, token.ExpiresAt.AddDays(-3));

        StringAssert.Contains(problem, "in 3 days");
    }

    [TestMethod]
    public void Problem_ReportsExpiredAndRejectedTokens()
    {
        var token = ValidToken(DateTimeOffset.UtcNow.AddMonths(-7));
        StringAssert.Contains(ServiceAccountMonitor.Problem(token, DateTimeOffset.UtcNow), "expired");

        token.InvalidatedAt = DateTimeOffset.UtcNow;
        token.InvalidReason = "invalid_grant";
        StringAssert.Contains(ServiceAccountMonitor.Problem(token, DateTimeOffset.UtcNow), "invalid_grant");
    }

    [TestMethod]
    public void ShouldAlert_OnceADayAndAtOnceOnANewRejection()
    {
        var now = DateTimeOffset.UtcNow;
        var token = ValidToken(now.AddMonths(-6));
        Assert.IsTrue(ServiceAccountMonitor.ShouldAlert(token, now));

        token.LastAlertSent = now.AddHours(-6);
        Assert.IsFalse(ServiceAccountMonitor.ShouldAlert(token, now));

        token.LastAlertSent = now.AddHours(-25);
        Assert.IsTrue(ServiceAccountMonitor.ShouldAlert(token, now));

        token.LastAlertSent = now.AddHours(-6);
        token.InvalidatedAt = now.AddHours(-1);
        Assert.IsTrue(ServiceAccountMonitor.ShouldAlert(token, now));
    }

    [TestMethod]
    public async Task CheckSpotify_NeverConnected_IsQuiet()
    {
        var monitor = CreateMonitor(new FakeStore());

        Assert.IsNull(await monitor.CheckSpotify());
    }

    [TestMethod]
    public async Task CheckSpotify_Rejected_ReportsTheProblem()
    {
        var store = new FakeStore { Token = ValidToken(DateTimeOffset.UtcNow.AddMonths(-1)) };
        store.Token.InvalidatedAt = DateTimeOffset.UtcNow;
        store.Token.InvalidReason = "invalid_grant";

        var problem = await CreateMonitor(store).CheckSpotify();

        StringAssert.Contains(problem, "invalid_grant");
    }

    #endregion

    #region Auth

    [TestMethod]
    public async Task ServiceAccount_NotConnected_ThrowsInsteadOfUsingTheAppToken()
    {
        var store = new FakeStore();
        var principal = new ServiceAccountPrincipal(ServiceType.Spotify, store);

        _ = await Assert.ThrowsExactlyAsync<SpotifyAuthExpiredException>(() =>
            AdmAuthentication.GetServiceAuthorization(Configuration(), ServiceType.Spotify, principal));
    }

    [TestMethod]
    public void ServiceAccountPrincipal_IsNotAnAuthenticatedUser()
    {
        var principal = new ServiceAccountPrincipal(ServiceType.Spotify, new FakeStore());

        Assert.IsFalse(principal.Identity.IsAuthenticated);
        Assert.IsNull(principal.Identity.Name);
    }

    #endregion

    #region Playlists

    [TestMethod]
    [DataRow("Salsa", false)]
    [DataRow("Holiday Salsa", true)]
    [DataRow("Halloween West Coast Swing", true)]
    [DataRow("Holidays", false)]
    public void IsSeasonal_MatchesBulkCreateNames(string name, bool seasonal)
    {
        var playlist = new PlayList { Type = PlayListType.SpotifyFromSearch, Name = name };

        Assert.AreEqual(seasonal, playlist.IsSeasonal);
    }

    [TestMethod]
    public void BuildPlaylistTrackCalls_ReplacesWithAPutThenAppends()
    {
        var tracks = Enumerable.Range(0, 250).Select(i => $"t{i}").Append(null);

        var calls = MusicServiceManager.BuildPlaylistTrackCalls(tracks, HttpMethod.Put).ToList();

        CollectionAssert.AreEqual(
            new[] { HttpMethod.Put, HttpMethod.Post, HttpMethod.Post }, calls.Select(c => c.Method).ToList());
        CollectionAssert.AreEqual(new[] { 100, 100, 50 }, calls.Select(c => Uris(c.Body).Count).ToList());
        Assert.AreEqual("spotify:track:t0", Uris(calls[0].Body)[0]);
        Assert.AreEqual("spotify:track:t249", Uris(calls[2].Body)[^1]);
    }

    [TestMethod]
    public void BuildPlaylistTrackCalls_EmptyReplaceClearsThePlaylist()
    {
        var calls = MusicServiceManager.BuildPlaylistTrackCalls([], HttpMethod.Put).ToList();

        Assert.HasCount(1, calls);
        Assert.IsEmpty(Uris(calls[0].Body));
        Assert.IsEmpty(MusicServiceManager.BuildPlaylistTrackCalls([], HttpMethod.Post).ToList());
    }

    #endregion

    #region UpdateBatch

    [TestMethod]
    public async Task UpdateBatch_FromSearch_NotConnected_Returns424WithoutTakingTheSlot()
    {
        var controller = await CreateController("NotConnected");

        var result = await controller.UpdateBatch(
            PlayListType.SpotifyFromSearch, serviceAccountMonitor: CreateMonitor(new FakeStore()));

        Assert.AreEqual(StatusCodes.Status424FailedDependency, ((ObjectResult)result).StatusCode);
        Assert.IsFalse(AdminMonitor.IsRunning);
    }

    [TestMethod]
    public async Task UpdateBatch_FromSearch_Rejected_Returns424()
    {
        var store = new FakeStore { Token = ValidToken(DateTimeOffset.UtcNow) };
        store.Token.InvalidatedAt = DateTimeOffset.UtcNow;
        var controller = await CreateController("Rejected");

        var result = await controller.UpdateBatch(
            PlayListType.SpotifyFromSearch, serviceAccountMonitor: CreateMonitor(store));

        Assert.AreEqual(StatusCodes.Status424FailedDependency, ((ObjectResult)result).StatusCode);
    }

    [TestMethod]
    public async Task UpdateBatch_FromSearch_Connected_Returns200AndReleasesTheSlot()
    {
        var store = new FakeStore { Token = ValidToken(DateTimeOffset.UtcNow) };
        var controller = await CreateController("Connected");

        var result = await controller.UpdateBatch(
            PlayListType.SpotifyFromSearch, seasonal: false, serviceAccountMonitor: CreateMonitor(store));

        Assert.IsInstanceOfType<OkObjectResult>(result);
        await WaitForAdminMonitor();
        Assert.IsTrue(AdminMonitor.Succeeded, AdminMonitor.Status.Status);
    }

    [TestMethod]
    public async Task UpdateBatch_SeasonalWithoutFromSearch_Returns400()
    {
        var controller = await CreateController("Seasonal");

        var result = await controller.UpdateBatch(PlayListType.SongsFromSpotify, seasonal: true);

        Assert.IsInstanceOfType<BadRequestObjectResult>(result);
        Assert.IsFalse(AdminMonitor.IsRunning);
    }

    [TestMethod]
    public async Task UpdateBatch_OtherTypes_Return400()
    {
        var controller = await CreateController("Other");

        var result = await controller.UpdateBatch(PlayListType.Music4Dance);

        Assert.IsInstanceOfType<BadRequestObjectResult>(result);
    }

    #endregion

    #region Helpers

    private static List<string> Uris(string body) =>
        [.. JObject.Parse(body)["uris"].Select(u => (string)u)];

    private static ServiceAccountToken ValidToken(DateTimeOffset authorizedAt) => new()
    {
        Service = ServiceType.Spotify.ToString(),
        AccountId = "m4d",
        ProtectedRefreshToken = "protected",
        AuthorizedAt = authorizedAt
    };

    private static IConfiguration Configuration()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
        configuration["Authentication:RecomputeJob:Key"] = Key;
        return configuration;
    }

    private static ServiceAccountMonitor CreateMonitor(IServiceAccountTokenStore store) =>
        new(Configuration(), store, null, NullLogger<ServiceAccountMonitor>.Instance);

    private static ServiceAccountTokenStore CreateStore(string name)
    {
        var services = new ServiceCollection();
        _ = services.AddDbContext<DanceMusicContext>(options =>
            options.UseInMemoryDatabase($"ServiceAccount{name}"));
        var provider = services.BuildServiceProvider();

        return new ServiceAccountTokenStore(
            provider.GetRequiredService<IServiceScopeFactory>(),
            new EphemeralDataProtectionProvider(),
            NullLogger<ServiceAccountTokenStore>.Instance);
    }

    private static async Task WaitForAdminMonitor()
    {
        var stopwatch = Stopwatch.StartNew();
        while (AdminMonitor.IsRunning && stopwatch.Elapsed < TimeSpan.FromSeconds(10))
        {
            await Task.Delay(25);
        }

        Assert.IsFalse(AdminMonitor.IsRunning);
    }

    private static async Task<PlayListController> CreateController(string name)
    {
        var dms = await DanceMusicTester.CreateServiceWithUsers($"ServiceAccountBatch{name}");

        var controller = new PlayListController(
            dms.Context,
            dms.UserManager,
            new Mock<ISearchServiceManager>().Object,
            new Mock<IDanceStatsManager>().Object,
            Configuration(),
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
        httpContext.Request.Headers[HeaderNames.Authorization] =
            $"Token {Convert.ToBase64String(Encoding.UTF8.GetBytes(Key))}";

        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return controller;
    }

    private sealed class FakeStore : IServiceAccountTokenStore
    {
        public ServiceAccountToken Token { get; set; }

        public Task<ServiceAccountToken> Get(ServiceType service) => Task.FromResult(Token);

        public Task<string> GetRefreshToken(ServiceType service) =>
            Task.FromResult(Token?.IsValid == true ? "refresh" : null);

        public Task Connect(ServiceType service, string refreshToken, string accountId,
            string accountName, string scopes, string authorizedBy) => throw new NotImplementedException();

        public Task UpdateRefreshToken(ServiceType service, string refreshToken) => Task.CompletedTask;

        public Task MarkInvalid(ServiceType service, string reason)
        {
            if (Token != null)
            {
                Token.InvalidatedAt ??= DateTimeOffset.UtcNow;
            }

            return Task.CompletedTask;
        }

        public Task MarkAlertSent(ServiceType service)
        {
            Token.LastAlertSent = DateTimeOffset.UtcNow;
            return Task.CompletedTask;
        }
    }

    #endregion
}
