using System.Net;
using System.Text;

using m4d.Middleware;
using m4d.Services.RequestLog;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Configuration;

namespace m4d.Tests.Middleware;

[TestClass]
public class RequestLogMiddlewareTests
{
    private const string Visitor = "203.0.113.45";
    private const string CloudflareEdge = "172.70.1.20"; // in 172.64.0.0/13
    private const string Direct = "198.51.100.7";
    private const string AppServiceFrontEnd = "169.254.129.3";

    private static IConfiguration Config(bool cloudflare = true) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
        {
            { "CloudflareClientIp:Enabled", cloudflare.ToString() },
        }!).Build();

    private static readonly RequestDelegate WriteHello = ctx => ctx.Response.WriteAsync("hello");

    private static Task<(RequestLogEntry Entry, HttpContext Context)> SendAsync(
        string remoteIp, Action<HttpRequest> setup) =>
        SendAsync(remoteIp, setup, WriteHello, Config());

    private static Task<(RequestLogEntry Entry, HttpContext Context)> SendAsync(
        string remoteIp, RequestDelegate next) =>
        SendAsync(remoteIp, _ => { }, next, Config());

    private static async Task<(RequestLogEntry Entry, HttpContext Context)> SendAsync(
        string remoteIp,
        Action<HttpRequest> setup,
        RequestDelegate next,
        IConfiguration configuration)
    {
        var queue = new RequestLogQueue(10);
        var middleware = new RequestLogMiddleware(next, queue, configuration);

        var context = new DefaultHttpContext();
        var body = new MemoryStream();
        context.Features.Set<IHttpResponseBodyFeature>(new StreamResponseBodyFeature(body));
        context.Connection.RemoteIpAddress = IPAddress.Parse(remoteIp);
        context.Request.Method = "GET";
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("www.music4dance.net");
        context.Request.Path = "/song/details/1";
        context.Request.Protocol = "HTTP/1.1";
        setup(context.Request);

        try
        {
            await middleware.InvokeAsync(context);
        }
        catch (InvalidOperationException)
        {
            // The failure test rethrows; the entry is still queued.
        }

        var entries = queue.Drain();
        Assert.AreEqual(1, entries.Count);
        return (entries[0], context);
    }

    [TestMethod]
    public async Task ViaCloudflare_RecordsVisitorAndEdge()
    {
        var (entry, _) = await SendAsync(CloudflareEdge,
            r => r.Headers[CloudflareClientIpMiddleware.ConnectingIpHeader] = Visitor);
        Assert.AreEqual(Visitor, entry.ClientIp);
        Assert.AreEqual(CloudflareEdge, entry.RelayIp);
    }

    [TestMethod]
    public async Task ViaCloudflare_BehindAppServiceFrontEnd_UsesLastForwardedFor()
    {
        var (entry, _) = await SendAsync(AppServiceFrontEnd, r =>
        {
            r.Headers[CloudflareClientIpMiddleware.ConnectingIpHeader] = Visitor;
            r.Headers[CloudflareClientIpMiddleware.ForwardedForHeader] = $"{Visitor}, {CloudflareEdge}:443";
        });
        Assert.AreEqual(Visitor, entry.ClientIp);
        Assert.AreEqual(CloudflareEdge, entry.RelayIp);
    }

    [TestMethod]
    public async Task Direct_SpoofedHeaderIsIgnored()
    {
        var (entry, _) = await SendAsync(Direct,
            r => r.Headers[CloudflareClientIpMiddleware.ConnectingIpHeader] = Visitor);
        Assert.AreEqual(Direct, entry.ClientIp);
        Assert.AreEqual(Direct, entry.RelayIp);
    }

    [TestMethod]
    public async Task CloudflareResolutionDisabled_RecordsRelay()
    {
        var (entry, _) = await SendAsync(CloudflareEdge,
            r => r.Headers[CloudflareClientIpMiddleware.ConnectingIpHeader] = Visitor,
            WriteHello, Config(cloudflare: false));
        Assert.AreEqual(CloudflareEdge, entry.ClientIp);
    }

    [TestMethod]
    public async Task RecordsRequestFields()
    {
        var (entry, _) = await SendAsync(Direct, r =>
        {
            r.QueryString = new QueryString("?filter=v2-index");
            r.Headers.UserAgent = "Mozilla/5.0 test";
            r.Headers.Referer = "https://www.google.com/";
            r.Headers.Cookie = "_ga=GA1.2.3; music4dance=secret-ticket";
            r.ContentLength = 12;
        });

        Assert.AreEqual("www.music4dance.net", entry.Host);
        Assert.AreEqual("GET", entry.Method);
        Assert.AreEqual("/song/details/1", entry.Path);
        Assert.AreEqual("filter=v2-index", entry.Query);
        Assert.AreEqual(200, entry.Status);
        Assert.AreEqual(5, entry.ResponseBytes);
        Assert.AreEqual(12, entry.RequestBytes);
        Assert.AreEqual("Mozilla/5.0 test", entry.UserAgent);
        Assert.AreEqual("https://www.google.com/", entry.Referer);
        Assert.AreEqual("_ga=; music4dance=", entry.CookieNames);
        Assert.AreEqual("HTTP/1.1", entry.Protocol);
    }

    [TestMethod]
    public async Task CountsBytesWrittenThroughBodyWriter()
    {
        var (entry, context) = await SendAsync(Direct, async ctx =>
        {
            await ctx.Response.BodyWriter.WriteAsync(Encoding.UTF8.GetBytes("twelve bytes"));
        });
        Assert.AreEqual(12, entry.ResponseBytes);
        // The original body feature is back in place afterwards.
        Assert.IsNotInstanceOfType<RequestLogMiddleware.CountingStream>(context.Response.Body);
    }

    [TestMethod]
    public async Task RecordsStatusSetDownstream()
    {
        var (entry, _) = await SendAsync(Direct, ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status404NotFound;
            return Task.CompletedTask;
        });
        Assert.AreEqual(404, entry.Status);
        Assert.AreEqual(0, entry.ResponseBytes);
    }

    [TestMethod]
    public async Task UnhandledException_IsRecordedAs500AndRethrown()
    {
        var (entry, _) = await SendAsync(Direct, _ => throw new InvalidOperationException("boom"));
        Assert.AreEqual(500, entry.Status);
    }
}
