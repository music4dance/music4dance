using m4d.Areas.Identity.Pages.Account;
using m4d.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace m4d.Tests.Identity;

/// <summary>
/// Tests for LoginModelBase.CleanUrl — verifies that chained auth page returnUrls
/// are unwound to a safe destination rather than propagating the chain.
/// </summary>
[TestClass]
public class CleanUrlTests
{
    private TestableLoginModel CreateModel()
    {
        var urlHelper = new Mock<IUrlHelper>();
        // IsLocalUrl: returns true for relative paths starting with /
        urlHelper.Setup(u => u.IsLocalUrl(It.IsAny<string>()))
            .Returns((string url) => url != null && url.StartsWith("/") && !url.StartsWith("//"));
        // Content("~/") returns "/"
        urlHelper.Setup(u => u.Content("~/")).Returns("/");

        var urlHelperFactory = new Mock<IUrlHelperFactory>();
        urlHelperFactory.Setup(f => f.GetUrlHelper(It.IsAny<ActionContext>()))
            .Returns(urlHelper.Object);

        var logger = NullLogger<TestableLoginModel>.Instance;
        var authTracker = new AuthenticationTracker();

        var model = new TestableLoginModel(urlHelperFactory.Object, logger, authTracker);

        // Supply a minimal PageContext so GetUrlHelper has a non-null ActionContext
        var httpContext = new DefaultHttpContext();
        var actionContext = new ActionContext(httpContext, new Microsoft.AspNetCore.Routing.RouteData(), new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor());
        model.PageContext = new Microsoft.AspNetCore.Mvc.RazorPages.PageContext(actionContext);
        model.Url = urlHelper.Object;

        return model;
    }

    [TestMethod]
    public void CleanUrl_NormalPath_ReturnsUnchanged()
    {
        var model = CreateModel();
        Assert.AreEqual("/song?filter=CHA", model.PublicCleanUrl("/song?filter=CHA"));
    }

    [TestMethod]
    public void CleanUrl_Null_ReturnsHome()
    {
        var model = CreateModel();
        Assert.AreEqual("/", model.PublicCleanUrl(null));
    }

    [TestMethod]
    public void CleanUrl_EmptyString_ReturnsHome()
    {
        var model = CreateModel();
        // Empty string passes IsLocalUrl (which would return false for ""), so returns home
        Assert.AreEqual("/", model.PublicCleanUrl(""));
    }

    [TestMethod]
    public void CleanUrl_LoginPage_ReturnsHome()
    {
        var model = CreateModel();
        Assert.AreEqual("/", model.PublicCleanUrl("/identity/account/login"));
    }

    [TestMethod]
    public void CleanUrl_RegisterPage_ReturnsHome()
    {
        var model = CreateModel();
        Assert.AreEqual("/", model.PublicCleanUrl("/identity/account/register"));
    }

    [TestMethod]
    public void CleanUrl_LogoutPage_ReturnsHome()
    {
        var model = CreateModel();
        Assert.AreEqual("/", model.PublicCleanUrl("/identity/account/logout"));
    }

    [TestMethod]
    public void CleanUrl_LoginWithNestedRealUrl_ReturnsNestedUrl()
    {
        var model = CreateModel();
        // Login page with a real returnUrl — should unwrap to the nested destination
        Assert.AreEqual("/song?filter=CHA", model.PublicCleanUrl("/identity/account/login?returnUrl=/song?filter=CHA"));
    }

    [TestMethod]
    public void CleanUrl_RegisterWithNestedRealUrl_ReturnsNestedUrl()
    {
        var model = CreateModel();
        Assert.AreEqual("/home/contribute", model.PublicCleanUrl("/identity/account/register?returnUrl=/home/contribute"));
    }

    [TestMethod]
    public void CleanUrl_TwoLevelChain_UnwindsBothLevels()
    {
        var model = CreateModel();
        // login?returnUrl=/identity/account/register?returnUrl=/home
        // Should extract /identity/account/register?returnUrl=/home → auth page → extract /home → return /home
        Assert.AreEqual("/home", model.PublicCleanUrl("/identity/account/login?returnUrl=/identity/account/register?returnUrl=/home"));
    }

    [TestMethod]
    public void CleanUrl_CircularChain_ReturnsHome()
    {
        var model = CreateModel();
        // The real bug scenario: login→register→login chain should return /
        var chain = "/identity/account/login?returnUrl=/identity/account/register?returnUrl=/identity/account/login?returnUrl=/";
        Assert.AreEqual("/", model.PublicCleanUrl(chain));
    }

    [TestMethod]
    public void CleanUrl_DeepChain_ReturnsHome()
    {
        var model = CreateModel();
        // A very deep chain should hit the depth limit and return home, not throw
        var deepChain = "/identity/account/login?returnUrl=/identity/account/register?returnUrl=/identity/account/login?returnUrl=/identity/account/register?returnUrl=/identity/account/login?returnUrl=/identity/account/register?returnUrl=/identity/account/login?returnUrl=/identity/account/register?returnUrl=/identity/account/login?returnUrl=/identity/account/register?returnUrl=/identity/account/login?returnUrl=/identity/account/register";
        var result = model.PublicCleanUrl(deepChain);
        Assert.AreEqual("/", result);
    }

    [TestMethod]
    public void CleanUrl_NonLocalUrl_ReturnsHome()
    {
        var model = CreateModel();
        Assert.AreEqual("/", model.PublicCleanUrl("https://evil.example.com/steal"));
    }

    [TestMethod]
    public void CleanUrl_UppercaseAuthPath_IsHandled()
    {
        var model = CreateModel();
        // Case-insensitive matching — uppercase should still be treated as auth page
        Assert.AreEqual("/", model.PublicCleanUrl("/Identity/Account/Login"));
    }

    [TestMethod]
    public void CleanUrl_LoginPageNoReturnUrl_ReturnsHome()
    {
        var model = CreateModel();
        // Auth page with no returnUrl — no nested value, should return home
        Assert.AreEqual("/", model.PublicCleanUrl("/identity/account/login?someOtherParam=value"));
    }
}

/// <summary>
/// Exposes protected CleanUrl for testing. Overrides IsLocalUrl to avoid needing
/// a full ASP.NET Core URL helper with HttpContext routing infrastructure.
/// </summary>
public class TestableLoginModel : LoginModelBase
{
    public TestableLoginModel(
        IUrlHelperFactory urlHelperFactory,
        ILogger<TestableLoginModel> logger,
        AuthenticationTracker authTracker)
        : base(urlHelperFactory, logger, authTracker)
    {
    }

    public string PublicCleanUrl(string returnUrl) => CleanUrl(returnUrl);

    protected override bool IsLocalUrl(string url)
    {
        // Simplified local URL check: relative paths starting with / (but not //)
        return url != null && url.StartsWith("/") && !url.StartsWith("//");
    }
}
