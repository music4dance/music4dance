using Microsoft.AspNetCore.Http;

using static m4d.Controllers.PaymentController;

namespace m4d.Tests.Controllers;

/// <summary>
/// Stripe's success/cancel URLs only use the request's Host header when it's one of our hosts.
/// </summary>
[TestClass]
public class PaymentControllerReturnHostTests
{
    private const string AppService = "m4d-test.azurewebsites.net";

    [TestMethod]
    [DataRow("www.music4dance.net")]
    [DataRow("music4dance.net")]
    [DataRow("staging.music4dance.net")]
    [DataRow("WWW.Music4Dance.net")]
    [DataRow("localhost:5001")]
    [DataRow("127.0.0.1:5001")]
    [DataRow("m4d-test.azurewebsites.net")]
    public void OurHostsAreKept(string host) =>
        Assert.AreEqual(host, StripeReturnHost(new HostString(host), AppService));

    [TestMethod]
    [DataRow("evil.example.com")]
    [DataRow("music4dance.net.evil.example.com")]
    [DataRow("evilmusic4dance.net")]
    [DataRow("other-app.azurewebsites.net")]
    [DataRow("")]
    public void OtherHostsFallBackToProduction(string host) =>
        Assert.AreEqual(DefaultReturnHost, StripeReturnHost(new HostString(host), AppService));

    [TestMethod]
    public void AzureHostIsRejectedOutsideAppService() =>
        Assert.AreEqual(DefaultReturnHost, StripeReturnHost(new HostString(AppService), null));
}
