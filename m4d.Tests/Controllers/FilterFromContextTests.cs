#nullable disable

using m4d.Controllers;

using Microsoft.AspNetCore.Http;

using System.Text;

namespace m4d.Tests.Controllers;

[TestClass]
public class FilterFromContextTests
{
    private static HttpRequest Request(string method, string query = null,
        string contentType = null, string body = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        if (query != null)
        {
            context.Request.QueryString = new QueryString(query);
        }
        if (contentType != null)
        {
            context.Request.ContentType = contentType;
        }
        if (body != null)
        {
            var bytes = Encoding.UTF8.GetBytes(body);
            context.Request.Body = new MemoryStream(bytes);
            context.Request.ContentLength = bytes.Length;
        }
        return context.Request;
    }

    [TestMethod]
    public void ReadFilterString_UsesTheQueryString()
    {
        Assert.AreEqual("v2-Index-SWZ",
            DanceMusicController.ReadFilterString(Request("GET", "?filter=v2-Index-SWZ")));
    }

    [TestMethod]
    public void ReadFilterString_ReadsAFormPost()
    {
        var request = Request("POST", contentType: "application/x-www-form-urlencoded",
            body: "filter=v2-Index-SWZ");

        Assert.AreEqual("v2-Index-SWZ", DanceMusicController.ReadFilterString(request));
    }

    [TestMethod]
    public void ReadFilterString_PrefersTheQueryStringOnAPost()
    {
        var request = Request("POST", "?filter=v2-Index-TGO",
            "application/x-www-form-urlencoded", "filter=v2-Index-SWZ");

        Assert.AreEqual("v2-Index-TGO", DanceMusicController.ReadFilterString(request));
    }

    [TestMethod]
    [DataRow(null, null)]
    [DataRow(null, "filter=v2-Index-SWZ")]
    [DataRow("application/json", "{\"filter\":\"v2-Index-SWZ\"}")]
    [DataRow("text/plain", "filter=v2-Index-SWZ")]
    public void ReadFilterString_IgnoresAPostThatIsNotAForm(string contentType, string body)
    {
        // Production 2026-11-18: a POST with no Content-Type threw from Request.Form, giving a 500
        var request = Request("POST", contentType: contentType, body: body);

        Assert.IsTrue(string.IsNullOrEmpty(DanceMusicController.ReadFilterString(request)));
    }
}
