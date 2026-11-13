using m4d.Middleware;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace m4d.Tests.Middleware;

[TestClass]
public class StaticAssetCacheMiddlewareTests
{
    // DefaultHttpContext's response feature never runs OnStarting callbacks; this one runs them
    // when the test "starts" the response.
    private sealed class StartableResponseFeature : HttpResponseFeature
    {
        private readonly List<(Func<object, Task> callback, object state)> _starting = [];

        public override void OnStarting(Func<object, Task> callback, object state) =>
            _starting.Add((callback, state));

        public async Task StartAsync()
        {
            // ASP.NET Core runs OnStarting callbacks in reverse registration order.
            foreach (var (callback, state) in Enumerable.Reverse(_starting))
            {
                await callback(state);
            }
        }
    }

    private static async Task<HttpResponse> Invoke(
        string path, int status = StatusCodes.Status200OK, string method = "GET")
    {
        var feature = new StartableResponseFeature();
        var context = new DefaultHttpContext();
        context.Features.Set<IHttpResponseFeature>(feature);
        context.Request.Method = method;
        context.Request.Path = path;

        // Stand-in for MapStaticAssets: sets its own headers, then the response starts.
        var middleware = new StaticAssetCacheMiddleware(ctx =>
        {
            ctx.Response.StatusCode = status;
            ctx.Response.Headers.CacheControl = "no-cache";
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);
        await feature.StartAsync();
        return context.Response;
    }

    [TestMethod]
    [DataRow("/vclient/assets/PageFrame-D3OuvLIN.js")]
    [DataRow("/vclient/assets/style-DzksZ-sh.css")]
    [DataRow("/VCLIENT/Assets/bsvn-CWyILkvc.js")]
    public async Task HashedBundles_AreImmutable(string path)
    {
        var response = await Invoke(path);
        Assert.AreEqual(StaticAssetCacheMiddleware.ImmutableCacheControl,
            response.Headers.CacheControl.ToString());
    }

    [TestMethod]
    public async Task NotModified_AlsoGetsTheLongPolicy()
    {
        var response = await Invoke("/vclient/assets/a-12345678.js", StatusCodes.Status304NotModified);
        Assert.AreEqual(StaticAssetCacheMiddleware.ImmutableCacheControl,
            response.Headers.CacheControl.ToString());
    }

    [TestMethod]
    [DataRow("/images/header-logo.png")]
    [DataRow("/css/bootstrap-icons.min.css")]
    [DataRow("/vendor/jquery/jquery.min.js")]
    [DataRow("/favicon.ico")]
    public async Task UnhashedStaticFiles_CacheForADay(string path)
    {
        var response = await Invoke(path);
        Assert.AreEqual(StaticAssetCacheMiddleware.DailyCacheControl,
            response.Headers.CacheControl.ToString());
    }

    [TestMethod]
    [DataRow("/")]
    [DataRow("/song/details/123")]
    [DataRow("/vclient/.vite/manifest.json")]
    [DataRow("/content/dances.json")]
    [DataRow("/api/health/ping")]
    [DataRow("/imagesextra/x.png")]
    public async Task OtherPaths_AreLeftAlone(string path)
    {
        var response = await Invoke(path);
        Assert.AreEqual("no-cache", response.Headers.CacheControl.ToString());
    }

    [TestMethod]
    public async Task MissingBundle_IsNotPinned()
    {
        var response = await Invoke("/vclient/assets/gone-12345678.js", StatusCodes.Status404NotFound);
        Assert.AreEqual("no-cache", response.Headers.CacheControl.ToString());
    }

    [TestMethod]
    public async Task Head_IsCached_Post_IsNot()
    {
        var head = await Invoke("/vclient/assets/a-12345678.js", method: "HEAD");
        Assert.AreEqual(StaticAssetCacheMiddleware.ImmutableCacheControl, head.Headers.CacheControl.ToString());

        var post = await Invoke("/vclient/assets/a-12345678.js", method: "POST");
        Assert.AreEqual("no-cache", post.Headers.CacheControl.ToString());
    }
}
