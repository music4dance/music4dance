using m4d.Configuration;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Http;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace m4d.Tests.Configuration;

[TestClass]
public class AuthenticationBuilderExtensionsTests
{
    [TestMethod]
    public async Task HandleRemoteFailure_RedirectsToLoginInsteadOf500()
    {
        // Production 2026-11-22: a `/signin-google` callback with a missing correlation cookie
        // threw "Correlation failed" and returned a 500.
        var httpContext = new DefaultHttpContext();
        var context = new RemoteFailureContext(
            httpContext,
            new AuthenticationScheme("Google", null, typeof(GoogleHandler)),
            new GoogleOptions(),
            new AuthenticationFailureException("Correlation failed."));

        await AuthenticationBuilderExtensions.HandleRemoteFailure(context);

        Assert.IsTrue(context.Result.Handled);
        Assert.AreEqual(StatusCodes.Status302Found, httpContext.Response.StatusCode);
        Assert.AreEqual(
            AuthenticationBuilderExtensions.RemoteFailureLoginPath,
            httpContext.Response.Headers.Location.ToString());
    }
}
