using System.Security.Claims;

using m4d.Middleware;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace m4d.Tests.Middleware;

[TestClass]
public class AnonymousCompressionMiddlewareTests
{
    private sealed class TestCompressionFeature : IHttpsCompressionFeature
    {
        public HttpsCompressionMode Mode { get; set; } = HttpsCompressionMode.Default;
    }

    private static async Task<HttpsCompressionMode> RunAsync(ClaimsPrincipal user)
    {
        var feature = new TestCompressionFeature();
        var context = new DefaultHttpContext { User = user };
        context.Features.Set<IHttpsCompressionFeature>(feature);

        var nextCalled = false;
        var middleware = new AnonymousCompressionMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        Assert.IsTrue(nextCalled);
        return feature.Mode;
    }

    [TestMethod]
    public async Task InvokeAsync_Anonymous_OptsIntoCompression()
    {
        var mode = await RunAsync(new ClaimsPrincipal(new ClaimsIdentity()));

        Assert.AreEqual(HttpsCompressionMode.Compress, mode);
    }

    [TestMethod]
    public async Task InvokeAsync_Authenticated_OptsOutOfCompression()
    {
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, "dwgray")], authenticationType: "Test");

        var mode = await RunAsync(new ClaimsPrincipal(identity));

        Assert.AreEqual(HttpsCompressionMode.DoNotCompress, mode);
    }

    [TestMethod]
    public async Task InvokeAsync_NoCompressionFeature_CallsNext()
    {
        var context = new DefaultHttpContext();
        var nextCalled = false;
        var middleware = new AnonymousCompressionMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        Assert.IsTrue(nextCalled);
    }
}
