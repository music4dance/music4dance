using m4d.PublicApi;

using Microsoft.AspNetCore.Http;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace m4d.Tests.Middleware;

[TestClass]
public class PublicApiProtectionTests
{
    [TestMethod]
    [DataRow("/connect/authorize", "", true)]
    [DataRow("/CONNECT/token", "", true)]
    [DataRow("/Identity/Account/Manage/ConnectedApps", "", true)]
    [DataRow("/Identity/Account/Login", "?returnUrl=%2Fconnect%2Fauthorize%3Frequest_uri%3Dsecret", true)]
    [DataRow("/Identity/Account/LoginWith2fa", "?ReturnUrl=%2Fconnect%2Fauthorize%3Frequest_uri%3Dsecret", true)]
    [DataRow("/Identity/Account/Login", "?returnUrl=%2Fsong", false)]
    [DataRow("/Identity/Account/Login", "?returnUrl=%2Fconnection", false)]
    [DataRow("/Identity/Account/Login", "", false)]
    [DataRow("/song", "?returnUrl=%2Fconnect%2Fauthorize", false)]
    [DataRow("/connection", "", false)]
    public void IsSensitiveRequest_IdentifiesAuthorizationPages(string path, string query, bool expected)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Request.QueryString = new QueryString(query);

        Assert.AreEqual(expected, PublicApiProtection.IsSensitiveRequest(context.Request));
    }
}
