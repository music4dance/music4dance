using m4d.Middleware;
using m4d.Security;

using Microsoft.AspNetCore.Http;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace m4d.Tests.Middleware;

[TestClass]
public class Http4xxTrackingMiddlewareTests
{
    private static Http4xxTrackingMiddleware CreateMiddleware(Http4xxTracker tracker, int responseStatusCode)
    {
        RequestDelegate next = ctx =>
        {
            ctx.Response.StatusCode = responseStatusCode;
            return Task.CompletedTask;
        };
        return new Http4xxTrackingMiddleware(next, tracker);
    }

    [TestMethod]
    public async Task InvokeAsync_404Response_RecordsEvent()
    {
        // Arrange
        var tracker = new Http4xxTracker();
        var middleware = CreateMiddleware(tracker, 404);
        var context = new DefaultHttpContext();
        context.Request.Path = $"/song/details/{Guid.NewGuid()}";

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var stats = tracker.GetStats();
        Assert.AreEqual(1, stats.TotalEventsTracked);
        Assert.AreEqual(404, stats.TopUrls[0].StatusCode);
    }

    [TestMethod]
    public async Task InvokeAsync_IncludesQueryStringInTrackedUrl()
    {
        // Arrange
        var tracker = new Http4xxTracker();
        var middleware = CreateMiddleware(tracker, 400);
        var context = new DefaultHttpContext();
        context.Request.Path = "/customsearch";
        context.Request.QueryString = new QueryString("?filter=bad-value");

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var stats = tracker.GetStats();
        Assert.AreEqual("/customsearch?filter=bad-value", stats.TopUrls[0].Url);
    }

    [TestMethod]
    public async Task InvokeAsync_200Response_DoesNotRecord()
    {
        // Arrange
        var tracker = new Http4xxTracker();
        var middleware = CreateMiddleware(tracker, 200);
        var context = new DefaultHttpContext();
        context.Request.Path = "/song";

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var stats = tracker.GetStats();
        Assert.AreEqual(0, stats.TotalEventsTracked);
    }

    [TestMethod]
    public async Task InvokeAsync_500Response_DoesNotRecord()
    {
        // Arrange
        var tracker = new Http4xxTracker();
        var middleware = CreateMiddleware(tracker, 500);
        var context = new DefaultHttpContext();
        context.Request.Path = "/song";

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var stats = tracker.GetStats();
        Assert.AreEqual(0, stats.TotalEventsTracked);
    }

    [TestMethod]
    public async Task InvokeAsync_429Response_DoesNotRecord()
    {
        // Arrange - already tracked in detail by RateLimitingTracker, would just be noise here
        var tracker = new Http4xxTracker();
        var middleware = CreateMiddleware(tracker, 429);
        var context = new DefaultHttpContext();
        context.Request.Path = "/identity/account/login";

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var stats = tracker.GetStats();
        Assert.AreEqual(0, stats.TotalEventsTracked);
    }

    [TestMethod]
    public async Task InvokeAsync_VeryLongUrl_TruncatesToSafeLength()
    {
        // Arrange
        var tracker = new Http4xxTracker();
        var middleware = CreateMiddleware(tracker, 404);
        var context = new DefaultHttpContext();
        context.Request.Path = "/song";
        context.Request.QueryString = new QueryString("?" + new string('a', 1000));

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var stats = tracker.GetStats();
        Assert.IsTrue(stats.TopUrls[0].Url.Length <= 512, "URL should be truncated to protect tracker memory");
    }
}
