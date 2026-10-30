using System.Security.Claims;

using m4d.Middleware;
using m4d.Security;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace m4d.Tests.Middleware;

[TestClass]
public class CrawlerThrottleMiddlewareTests
{
    private const string DotBot = "Mozilla/5.0 (compatible; DotBot/1.2; +https://opensiteexplorer.org/dotbot)";
    private const string GoogleBot = "Mozilla/5.0 (compatible; Googlebot/2.1; +http://www.google.com/bot.html)";
    private const string Browser = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/130.0";

    private const string Reflectionbot =
        "Mozilla/5.0 AppleWebKit/537.36 (KHTML, like Gecko; compatible; Reflectionbot/1.0; +https://reflection.ai/bot) Chrome/151.0.0.0 Safari/537.36";
    private const string MetaAgent =
        "meta-externalagent/1.1 (+https://developers.facebook.com/docs/sharing/webmasters/crawler)";

    private static IConfiguration Config(int limit = 3, bool enabled = true, string agentLimits = "")
    {
        var values = new Dictionary<string, string>
        {
            { "Configuration:BotFilter:ExcludeFragments", "spider;bot" },
            { "CrawlerThrottle:Enabled", enabled.ToString() },
            { "CrawlerThrottle:MaxRequestsPerMinute", limit.ToString() },
        };
        if (agentLimits.Length > 0)
        {
            values["CrawlerThrottle:AgentLimits"] = agentLimits;
        }
        return new ConfigurationBuilder().AddInMemoryCollection(values!).Build();
    }

    private sealed class Harness
    {
        public Harness(IConfiguration configuration)
        {
            Middleware = new CrawlerThrottleMiddleware(
                _ =>
                {
                    NextCalls++;
                    return Task.CompletedTask;
                },
                new MemoryCache(new MemoryCacheOptions()), configuration, Tracker,
                NullLogger<CrawlerThrottleMiddleware>.Instance);
        }

        public CrawlerThrottleTracker Tracker { get; } = new();
        public CrawlerThrottleMiddleware Middleware { get; }
        public int NextCalls { get; private set; }
    }

    private static async Task<HttpContext> SendAsync(Harness harness, string userAgent,
        string path = "/song/details/1", bool signedIn = false)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Request.Headers.UserAgent = userAgent;
        context.Response.Body = new MemoryStream();
        if (signedIn)
        {
            context.User = new ClaimsPrincipal(
                new ClaimsIdentity([new Claim(ClaimTypes.Name, "dwgray")], "Test"));
        }

        await harness.Middleware.InvokeAsync(context);
        return context;
    }

    [TestMethod]
    public async Task Crawler_OverTheLimit_Gets429WithRetryAfter()
    {
        var harness = new Harness(Config(limit: 3));

        for (var i = 0; i < 3; i++)
        {
            Assert.AreEqual(200, (await SendAsync(harness, DotBot)).Response.StatusCode);
        }
        var throttled = await SendAsync(harness, DotBot);

        Assert.AreEqual(429, throttled.Response.StatusCode);
        Assert.IsTrue(int.Parse(throttled.Response.Headers.RetryAfter!) is > 0 and <= 60);
        Assert.AreEqual(3, harness.NextCalls);

        var stats = harness.Tracker.GetStats();
        Assert.HasCount(1, stats);
        Assert.AreEqual(1, stats[0].ThrottledCount);
        Assert.AreEqual(DotBot.ToLowerInvariant(), stats[0].UserAgent);
    }

    [TestMethod]
    public async Task ExemptSearchEngine_IsNeverThrottled()
    {
        var harness = new Harness(Config(limit: 1));

        for (var i = 0; i < 5; i++)
        {
            Assert.AreEqual(200, (await SendAsync(harness, GoogleBot)).Response.StatusCode);
        }
        Assert.AreEqual(5, harness.NextCalls);
    }

    [TestMethod]
    public async Task Browser_IsNeverThrottled()
    {
        var harness = new Harness(Config(limit: 1));

        for (var i = 0; i < 5; i++)
        {
            await SendAsync(harness, Browser);
        }
        Assert.AreEqual(5, harness.NextCalls);
    }

    [TestMethod]
    public async Task SignedInUser_IsNeverThrottled()
    {
        var harness = new Harness(Config(limit: 1));

        for (var i = 0; i < 5; i++)
        {
            await SendAsync(harness, DotBot, signedIn: true);
        }
        Assert.AreEqual(5, harness.NextCalls);
    }

    [TestMethod]
    [DataRow("/robots.txt")]
    [DataRow("/sitemap.xml")]
    [DataRow("/vclient/assets/song-index.js")]
    [DataRow("/songbook")]
    [DataRow("/")]
    public async Task PathsOutsideTheCatalog_AreNotCounted(string path)
    {
        var harness = new Harness(Config(limit: 1));

        for (var i = 0; i < 3; i++)
        {
            await SendAsync(harness, DotBot, path);
        }
        Assert.AreEqual(3, harness.NextCalls);
    }

    [TestMethod]
    [DataRow("/song")]
    [DataRow("/Song/Sort")]
    [DataRow("/dances/waltz")]
    [DataRow("/customsearch")]
    public async Task CatalogPaths_AreCounted(string path)
    {
        var harness = new Harness(Config(limit: 1));

        await SendAsync(harness, DotBot, path);
        var second = await SendAsync(harness, DotBot, path);

        Assert.AreEqual(429, second.Response.StatusCode);
    }

    [TestMethod]
    public async Task EachCrawlerHasItsOwnWindow()
    {
        var harness = new Harness(Config(limit: 1));

        await SendAsync(harness, DotBot);
        var other = await SendAsync(harness, "PetalBot");

        Assert.AreEqual(200, other.Response.StatusCode);
    }

    [TestMethod]
    public async Task AgentLimit_LowersTheLimitForThatAgent()
    {
        var harness = new Harness(Config(limit: 20, agentLimits: "reflectionbot=2"));

        await SendAsync(harness, Reflectionbot);
        await SendAsync(harness, Reflectionbot);
        var third = await SendAsync(harness, Reflectionbot);
        var other = await SendAsync(harness, DotBot);

        Assert.AreEqual(429, third.Response.StatusCode);
        Assert.AreEqual(200, other.Response.StatusCode);
    }

    [TestMethod]
    public async Task AgentLimit_ThrottlesAnAgentTheBotFilterDoesNotRecognize()
    {
        var harness = new Harness(Config(limit: 1, agentLimits: "meta-externalagent"));

        await SendAsync(harness, MetaAgent);
        var second = await SendAsync(harness, MetaAgent);

        Assert.AreEqual(429, second.Response.StatusCode);
    }

    [TestMethod]
    public async Task AgentLimit_WithoutANumber_UsesMaxRequestsPerMinute()
    {
        var harness = new Harness(Config(limit: 2, agentLimits: "meta-externalagent;reflectionbot=oops"));

        await SendAsync(harness, Reflectionbot);
        await SendAsync(harness, Reflectionbot);
        var third = await SendAsync(harness, Reflectionbot);

        Assert.AreEqual(429, third.Response.StatusCode);
        Assert.AreEqual(2, harness.NextCalls);
    }

    [TestMethod]
    public async Task ExemptFragment_WinsOverAgentLimit()
    {
        var harness = new Harness(Config(limit: 1, agentLimits: "googlebot=1"));

        for (var i = 0; i < 3; i++)
        {
            await SendAsync(harness, GoogleBot);
        }
        Assert.AreEqual(3, harness.NextCalls);
    }

    [TestMethod]
    public async Task Disabled_PassesEverythingThrough()
    {
        var harness = new Harness(Config(limit: 1, enabled: false));

        for (var i = 0; i < 3; i++)
        {
            await SendAsync(harness, DotBot);
        }
        Assert.AreEqual(3, harness.NextCalls);
    }
}
