#nullable disable

using System.Security.Claims;

using m4d.Middleware;
using m4d.Services.ServiceHealth;

using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace m4d.Tests.Middleware;

[TestClass]
public class CacheControlMiddlewareTests
{
    private const string Browser = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/156.0";
    private const string GoogleBot = "Mozilla/5.0 (compatible; Googlebot/2.1; +http://www.google.com/bot.html)";
    private const string DotBot = "Mozilla/5.0 (compatible; DotBot/1.2; +https://opensiteexplorer.org/dotbot)";

    // DefaultHttpContext's response feature never runs OnStarting callbacks; this one runs them
    // when the test "starts" the response.
    private sealed class StartableResponseFeature : HttpResponseFeature
    {
        private readonly List<(Func<object, Task> callback, object state)> _starting = [];

        public override void OnStarting(Func<object, Task> callback, object state) =>
            _starting.Add((callback, state));

        public async Task StartAsync()
        {
            foreach (var (callback, state) in Enumerable.Reverse(_starting))
            {
                await callback(state);
            }
        }
    }

    private static IConfiguration Config(bool enabled = true, int? maxAge = null)
    {
        var values = new Dictionary<string, string>
        {
            { "Configuration:BotFilter:ExcludeFragments", "spider;bot" },
            { "EdgeCache:Enabled", enabled.ToString() },
        };
        if (maxAge != null)
        {
            values["EdgeCache:MaxAgeSeconds"] = maxAge.ToString();
        }
        return new ConfigurationBuilder().AddInMemoryCollection(values!).Build();
    }

    private static ServiceHealthManager Healthy() => new(NullLogger<ServiceHealthManager>.Instance);

    private static async Task<HttpResponse> Invoke(
        string path = "/song/album",
        IConfiguration configuration = null,
        ServiceHealthManager health = null,
        string userAgent = Browser,
        bool signedIn = false,
        int status = StatusCodes.Status200OK,
        string contentType = "text/html; charset=utf-8",
        string method = "GET",
        bool setCookie = false,
        Action<HttpResponse> respond = null,
        bool? renderedHealthy = null)
    {
        var feature = new StartableResponseFeature();
        var context = new DefaultHttpContext();
        context.Features.Set<IHttpResponseFeature>(feature);
        context.Request.Method = method;
        context.Request.Path = path;
        context.Request.Headers.UserAgent = userAgent;
        if (signedIn)
        {
            context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "dancer")], "Test"));
        }

        var middleware = new CacheControlMiddleware(ctx =>
        {
            ctx.Response.StatusCode = status;
            ctx.Response.ContentType = contentType;
            if (setCookie)
            {
                ctx.Response.Headers.SetCookie = "music4dance=abc; path=/";
            }
            if (renderedHealthy != null)
            {
                ctx.Items[CacheControlMiddleware.RenderedHealthKey] = renderedHealthy.Value;
            }
            respond?.Invoke(ctx.Response);
            return Task.CompletedTask;
        }, configuration ?? Config(), health ?? Healthy());

        await middleware.InvokeAsync(context);
        await feature.StartAsync();
        return context.Response;
    }

    private static string Edge(HttpResponse response) =>
        response.Headers[CacheControlMiddleware.EdgeCacheHeader].ToString();

    [TestMethod]
    [DataRow("/song/album")]
    [DataRow("/song/details/208a10f7-ebf1-43b6-8e57-97efce82a13e")]
    [DataRow("/customsearch")]
    [DataRow("/")]
    public async Task AnonymousPage_NoCacheForBrowsers_EdgeKeepsItForADay(string path)
    {
        var response = await Invoke(path);

        Assert.AreEqual("no-cache", response.Headers.CacheControl.ToString());
        Assert.AreEqual("max-age=86400", Edge(response));
    }

    [TestMethod]
    public async Task MaxAge_ComesFromConfiguration()
    {
        var response = await Invoke(configuration: Config(maxAge: 3600));
        Assert.AreEqual("max-age=3600", Edge(response));
    }

    [TestMethod]
    public async Task Disabled_NoEdgeHeader_BrowsersStillNoCache()
    {
        var response = await Invoke(configuration: Config(enabled: false));

        Assert.AreEqual("no-cache", response.Headers.CacheControl.ToString());
        Assert.AreEqual("", Edge(response));
    }

    [TestMethod]
    public async Task SignedIn_NoStore_NoEdgeHeader()
    {
        var response = await Invoke(signedIn: true);

        Assert.AreEqual(CacheControlMiddleware.NoStore, response.Headers.CacheControl.ToString());
        Assert.AreEqual("", Edge(response));
    }

    [TestMethod]
    [DataRow(GoogleBot)]
    [DataRow(DotBot)]
    [DataRow("")]
    public async Task BotVariants_AreNotEdgeCached(string userAgent)
    {
        // Googlebot's page has no AdSense, and spiders can get BotWarning or the bot stub;
        // a shared copy of either must never reach people.
        var response = await Invoke(userAgent: userAgent);

        Assert.AreEqual("no-cache", response.Headers.CacheControl.ToString());
        Assert.AreEqual("", Edge(response));
    }

    [TestMethod]
    [DataRow("SearchService")]
    [DataRow("Database")]
    [DataRow("AppConfiguration")]
    public async Task UnhealthyService_IsNotFrozenIntoTheCache(string service)
    {
        var health = Healthy();
        health.MarkUnavailable(service, "test outage");

        var response = await Invoke(health: health);

        Assert.AreEqual("", Edge(response));
    }

    [TestMethod]
    [DataRow(StatusCodes.Status404NotFound)]
    [DataRow(StatusCodes.Status503ServiceUnavailable)]
    [DataRow(StatusCodes.Status301MovedPermanently)]
    [DataRow(StatusCodes.Status429TooManyRequests)]
    public async Task NonSuccess_IsNotEdgeCached(int status)
    {
        var response = await Invoke(status: status);
        Assert.AreEqual("", Edge(response));
    }

    [TestMethod]
    [DataRow("/api/health/status")]
    [DataRow("/identity/account/login")]
    [DataRow("/song/rawsearchform")]
    [DataRow("/admin/diagnostics")]
    [DataRow("/payment/success?session_id=cs_test_123")]
    [DataRow("/payment/cancel")]
    public async Task ExcludedPaths_AreNotEdgeCached(string path)
    {
        var response = await Invoke(path);
        Assert.AreEqual("", Edge(response));
    }

    [TestMethod]
    public async Task SetCookie_JsonAndPost_AreLeftAlone()
    {
        Assert.AreEqual("", Edge(await Invoke(setCookie: true)));
        Assert.AreEqual("", Edge(await Invoke(contentType: "application/json")));
        Assert.AreEqual("", Edge(await Invoke(method: "POST")));
    }

    [TestMethod]
    public async Task IdentityRedirect_IsNoStore()
    {
        var response = await Invoke("/identity/account/login", status: StatusCodes.Status302Found);
        Assert.AreEqual(CacheControlMiddleware.NoStore, response.Headers.CacheControl.ToString());
    }

    [TestMethod]
    [DataRow("no-cache, no-store", null)]
    [DataRow("private, max-age=60", null)]
    [DataRow(null, "no-cache")]
    public async Task ResponseThatOptedOutOfCaching_KeepsItsHeaders_AndIsNotEdgeCached(
        string cacheControl, string pragma)
    {
        var response = await Invoke(respond: r =>
        {
            if (cacheControl != null)
            {
                r.Headers.CacheControl = cacheControl;
            }
            if (pragma != null)
            {
                r.Headers.Pragma = pragma;
            }
        });

        Assert.AreEqual("", Edge(response));
        Assert.AreEqual(cacheControl ?? "", response.Headers.CacheControl.ToString());
        Assert.AreEqual(pragma ?? "", response.Headers.Pragma.ToString());
    }

    [TestMethod]
    public async Task FormTokenPage_WithTheVisitorsExistingCookie_IsNotEdgeCached()
    {
        // The real antiforgery service: the second visit reuses the cookie from the first, so the
        // response sets no cookie, but it still carries that visitor's request token
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Production,
        });
        builder.WebHost.UseTestServer();
        builder.Services.AddAntiforgery();
        await using var app = builder.Build();
        app.UseMiddleware<CacheControlMiddleware>(Config(), Healthy());
        app.MapGet("/home/contribute", (HttpContext context, IAntiforgery antiforgery) =>
        {
            var token = antiforgery.GetAndStoreTokens(context).RequestToken;
            return Results.Content($"<form><input name=\"t\" value=\"{token}\"></form>", "text/html");
        });
        await app.StartAsync();
        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd(Browser);

        var first = await client.GetAsync("/home/contribute");
        var cookie = first.Headers.GetValues("Set-Cookie").First().Split(';')[0];

        using var second = new HttpRequestMessage(HttpMethod.Get, "/home/contribute");
        second.Headers.Add("Cookie", cookie);
        var response = await client.SendAsync(second);

        Assert.IsFalse(response.Headers.Contains("Set-Cookie"));
        Assert.IsFalse(response.Headers.Contains(CacheControlMiddleware.EdgeCacheHeader));
        Assert.IsTrue(response.Headers.CacheControl?.NoStore, "antiforgery's no-store survives");
    }

    [TestMethod]
    public async Task PageRenderedWhileAServiceWasDown_IsNotEdgeCached_EvenIfHealthyNow()
    {
        Assert.AreEqual("", Edge(await Invoke(renderedHealthy: false)));
        Assert.AreEqual("max-age=86400", Edge(await Invoke(renderedHealthy: true)));
    }
}
