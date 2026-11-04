using System.Net;

using m4d.Middleware;
using m4d.Security;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace m4d.Tests.Middleware;

[TestClass]
public class CloudflareClientIpMiddlewareTests
{
    private const string Visitor = "203.0.113.45";
    private const string VisitorV6 = "2001:db8:85a3::8a2e:370:7334";
    private const string CloudflareEdge = "172.70.1.20"; // in 172.64.0.0/13
    private const string CloudflareEdgeV6 = "2606:4700:10::6816:1";
    private const string Direct = "198.51.100.7"; // not Cloudflare
    private const string AppServiceFrontEnd = "169.254.129.3";

    private static IConfiguration Config(bool enabled = true, string ranges = "")
    {
        var values = new Dictionary<string, string>
        {
            { "CloudflareClientIp:Enabled", enabled.ToString() },
        };
        if (ranges.Length > 0)
        {
            values["CloudflareClientIp:IpRanges"] = ranges;
        }
        return new ConfigurationBuilder().AddInMemoryCollection(values!).Build();
    }

    private static Task<(HttpContext Context, bool NextCalled)> SendAsync(
        string remoteIp, string connectingIp = "", string forwardedFor = "") =>
        SendAsync(Config(), remoteIp, connectingIp, forwardedFor);

    private static async Task<(HttpContext Context, bool NextCalled)> SendAsync(
        IConfiguration configuration, string remoteIp, string connectingIp,
        string forwardedFor = "")
    {
        var nextCalled = false;
        var middleware = new CloudflareClientIpMiddleware(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            configuration, NullLogger<CloudflareClientIpMiddleware>.Instance);

        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(remoteIp);
        if (connectingIp.Length > 0)
        {
            context.Request.Headers[CloudflareClientIpMiddleware.ConnectingIpHeader] = connectingIp;
        }
        if (forwardedFor.Length > 0)
        {
            context.Request.Headers[CloudflareClientIpMiddleware.ForwardedForHeader] = forwardedFor;
        }

        await middleware.InvokeAsync(context);
        return (context, nextCalled);
    }

    [TestMethod]
    public async Task FromCloudflare_UsesConnectingIp()
    {
        var (context, nextCalled) = await SendAsync(CloudflareEdge, Visitor);

        Assert.AreEqual(IPAddress.Parse(Visitor), context.Connection.RemoteIpAddress);
        Assert.IsTrue(nextCalled);
    }

    [TestMethod]
    public async Task FromCloudflareOverIpv6_UsesConnectingIp()
    {
        var (context, _) = await SendAsync(CloudflareEdgeV6, VisitorV6);

        Assert.AreEqual(IPAddress.Parse(VisitorV6), context.Connection.RemoteIpAddress);
    }

    [TestMethod]
    public async Task FromCloudflareAsIpv4MappedIpv6_UsesConnectingIp()
    {
        var (context, _) = await SendAsync("::ffff:" + CloudflareEdge, Visitor);

        Assert.AreEqual(IPAddress.Parse(Visitor), context.Connection.RemoteIpAddress);
    }

    [TestMethod]
    public async Task DirectRequestWithSpoofedHeader_IsLeftAlone()
    {
        var (context, nextCalled) = await SendAsync(Direct, Visitor);

        Assert.AreEqual(IPAddress.Parse(Direct), context.Connection.RemoteIpAddress);
        Assert.IsTrue(nextCalled);
    }

    [TestMethod]
    public async Task FromCloudflareWithoutHeader_IsLeftAlone()
    {
        var (context, _) = await SendAsync(CloudflareEdge);

        Assert.AreEqual(IPAddress.Parse(CloudflareEdge), context.Connection.RemoteIpAddress);
    }

    [TestMethod]
    [DataRow("not-an-ip")]
    [DataRow("   ")]
    public async Task FromCloudflareWithMalformedHeader_IsLeftAlone(string header)
    {
        var (context, _) = await SendAsync(CloudflareEdge, header);

        Assert.AreEqual(IPAddress.Parse(CloudflareEdge), context.Connection.RemoteIpAddress);
    }

    [TestMethod]
    public async Task ViaAppServiceFrontEnd_FromCloudflare_UsesConnectingIp()
    {
        // The front end appends the address that connected to it, with a port.
        var (context, _) = await SendAsync(AppServiceFrontEnd, Visitor,
            forwardedFor: $"{Visitor}, {CloudflareEdge}:51234");

        Assert.AreEqual(IPAddress.Parse(Visitor), context.Connection.RemoteIpAddress);
    }

    [TestMethod]
    public async Task ViaAppServiceFrontEnd_FromCloudflareOverIpv6_UsesConnectingIp()
    {
        var (context, _) = await SendAsync(AppServiceFrontEnd, Visitor,
            forwardedFor: $"{Visitor}, [{CloudflareEdgeV6}]:51234");

        Assert.AreEqual(IPAddress.Parse(Visitor), context.Connection.RemoteIpAddress);
    }

    [TestMethod]
    public async Task ViaAppServiceFrontEnd_DirectWithSpoofedHeaders_IsLeftAlone()
    {
        // A direct client can put a Cloudflare address early in X-Forwarded-For, but the front
        // end appends the client's real address last.
        var (context, _) = await SendAsync(AppServiceFrontEnd, Visitor,
            forwardedFor: $"{CloudflareEdge}, {Direct}:443");

        Assert.AreEqual(IPAddress.Parse(AppServiceFrontEnd), context.Connection.RemoteIpAddress);
    }

    [TestMethod]
    public async Task ViaAppServiceFrontEnd_NoForwardedFor_IsLeftAlone()
    {
        var (context, _) = await SendAsync(AppServiceFrontEnd, Visitor);

        Assert.AreEqual(IPAddress.Parse(AppServiceFrontEnd), context.Connection.RemoteIpAddress);
    }

    [TestMethod]
    public async Task Disabled_IsLeftAlone()
    {
        var (context, nextCalled) = await SendAsync(Config(enabled: false),
            CloudflareEdge, Visitor);

        Assert.AreEqual(IPAddress.Parse(CloudflareEdge), context.Connection.RemoteIpAddress);
        Assert.IsTrue(nextCalled);
    }

    [TestMethod]
    public async Task ConfiguredRanges_ReplaceTheDefaults()
    {
        var configuration = Config(ranges: "198.51.100.0/24;garbage");

        var (fromConfigured, _) = await SendAsync(configuration, Direct, Visitor);
        var (fromDefault, _) = await SendAsync(configuration, CloudflareEdge, Visitor);

        Assert.AreEqual(IPAddress.Parse(Visitor), fromConfigured.Connection.RemoteIpAddress);
        Assert.AreEqual(IPAddress.Parse(CloudflareEdge), fromDefault.Connection.RemoteIpAddress);
    }

    [TestMethod]
    public void DefaultRanges_AllParse()
    {
        var options = new CloudflareClientIpOptions();

        Assert.AreEqual(0, options.InvalidRanges.Count);
        Assert.AreEqual(22, options.Networks.Count);
    }
}
