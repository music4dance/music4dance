using System.IO.Pipelines;
using System.Net;
using System.Text;

using m4d.Middleware;
using m4d.Services.RequestLog;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace m4d.Tests.Middleware;

[TestClass]
public class RequestLogMiddlewareTests
{
    private const string Visitor = "203.0.113.45";
    private const string CloudflareEdge = "172.70.1.20"; // in 172.64.0.0/13
    private const string Direct = "198.51.100.7";
    private const string AppServiceFrontEnd = "169.254.129.3";

    /// <summary>
    /// A response body that records whether anything started or completed it, the way Kestrel's
    /// does: starting early is what would switch an empty response to chunked encoding.
    /// </summary>
    private sealed class SpyBodyFeature : IHttpResponseBodyFeature
    {
        public readonly MemoryStream Body = new();

        public SpyBodyFeature()
        {
            Writer = PipeWriter.Create(Body, new StreamPipeWriterOptions(leaveOpen: true));
        }

        public int Starts { get; private set; }
        public int Completes { get; private set; }
        public long SentFileBytes { get; private set; }

        public Stream Stream => Body;
        public PipeWriter Writer { get; }

        public void DisableBuffering()
        {
        }

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            Starts++;
            return Task.CompletedTask;
        }

        public Task CompleteAsync()
        {
            Completes++;
            return Task.CompletedTask;
        }

        public Task SendFileAsync(string path, long offset, long? count, CancellationToken cancellationToken = default)
        {
            SentFileBytes += count ?? new FileInfo(path).Length - offset;
            return Task.CompletedTask;
        }
    }

    /// <summary>Lets a test mark the response as started, as Kestrel would after a write.</summary>
    private sealed class StartedResponseFeature : HttpResponseFeature
    {
        public bool Started { get; set; }
        public override bool HasStarted => Started;
    }

    private static IConfiguration Config(bool cloudflare = true) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
        {
            { "CloudflareClientIp:Enabled", cloudflare.ToString() },
        }!).Build();

    private static readonly RequestDelegate WriteHello = ctx => ctx.Response.WriteAsync("hello");

    private sealed record Sent(RequestLogEntry Entry, HttpContext Context, SpyBodyFeature Body);

    private static Task<Sent> SendAsync(string remoteIp, Action<HttpContext> setup) =>
        SendAsync(remoteIp, setup, WriteHello, Config(), withCloudflare: true);

    private static Task<Sent> SendAsync(string remoteIp, RequestDelegate next) =>
        SendAsync(remoteIp, _ => { }, next, Config(), withCloudflare: true);

    /// <summary>Runs CloudflareClientIpMiddleware then RequestLogMiddleware, as in the app.</summary>
    private static async Task<Sent> SendAsync(
        string remoteIp,
        Action<HttpContext> setup,
        RequestDelegate next,
        IConfiguration configuration,
        bool withCloudflare)
    {
        var queue = new RequestLogQueue(10);
        RequestDelegate pipeline = new RequestLogMiddleware(next, queue).InvokeAsync;
        if (withCloudflare)
        {
            pipeline = new CloudflareClientIpMiddleware(pipeline, configuration,
                NullLogger<CloudflareClientIpMiddleware>.Instance).InvokeAsync;
        }

        var context = new DefaultHttpContext();
        var body = new SpyBodyFeature();
        context.Features.Set<IHttpResponseBodyFeature>(body);
        context.Connection.RemoteIpAddress = IPAddress.Parse(remoteIp);
        context.Request.Method = "GET";
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("www.music4dance.net");
        context.Request.Path = "/song/details/1";
        context.Request.Protocol = "HTTP/1.1";
        setup(context);

        try
        {
            await pipeline(context);
        }
        catch (InvalidOperationException)
        {
            // The failure tests rethrow; the entry is still queued.
        }

        var entries = queue.Drain();
        Assert.AreEqual(1, entries.Count);
        return new Sent(entries[0], context, body);
    }

    [TestMethod]
    public async Task ViaCloudflare_RecordsVisitorAndEdge()
    {
        var sent = await SendAsync(CloudflareEdge,
            c => c.Request.Headers[CloudflareClientIpMiddleware.ConnectingIpHeader] = Visitor);
        Assert.AreEqual(Visitor, sent.Entry.ClientIp);
        Assert.AreEqual(CloudflareEdge, sent.Entry.RelayIp);
    }

    [TestMethod]
    public async Task ViaCloudflare_BehindAppServiceFrontEnd_UsesLastForwardedFor()
    {
        var sent = await SendAsync(AppServiceFrontEnd, c =>
        {
            c.Request.Headers[CloudflareClientIpMiddleware.ConnectingIpHeader] = Visitor;
            c.Request.Headers[CloudflareClientIpMiddleware.ForwardedForHeader] = $"{Visitor}, {CloudflareEdge}:443";
        });
        Assert.AreEqual(Visitor, sent.Entry.ClientIp);
        Assert.AreEqual(CloudflareEdge, sent.Entry.RelayIp);
    }

    [TestMethod]
    public async Task Direct_SpoofedHeaderIsIgnored()
    {
        var sent = await SendAsync(Direct,
            c => c.Request.Headers[CloudflareClientIpMiddleware.ConnectingIpHeader] = Visitor);
        Assert.AreEqual(Direct, sent.Entry.ClientIp);
        Assert.AreEqual(Direct, sent.Entry.RelayIp);
    }

    [TestMethod]
    public async Task CloudflareResolutionDisabled_RecordsRelay()
    {
        var sent = await SendAsync(CloudflareEdge,
            c => c.Request.Headers[CloudflareClientIpMiddleware.ConnectingIpHeader] = Visitor,
            WriteHello, Config(cloudflare: false), withCloudflare: true);
        Assert.AreEqual(CloudflareEdge, sent.Entry.ClientIp);
    }

    [TestMethod]
    public async Task IPv4MappedAddresses_AreNormalized()
    {
        // A private front end with no X-Forwarded-For (an internal probe).
        var sent = await SendAsync("::ffff:10.0.0.5", WriteHello);
        Assert.AreEqual("10.0.0.5", sent.Entry.ClientIp);
        Assert.AreEqual("10.0.0.5", sent.Entry.RelayIp);
    }

    [TestMethod]
    public async Task WithoutCloudflareMiddleware_FallsBackToTheNormalizedConnection()
    {
        var sent = await SendAsync("::ffff:198.51.100.7", _ => { }, WriteHello, Config(), withCloudflare: false);
        Assert.AreEqual(Direct, sent.Entry.ClientIp);
        Assert.AreEqual(Direct, sent.Entry.RelayIp);
    }

    [TestMethod]
    public async Task RecordsRequestFields()
    {
        var sent = await SendAsync(Direct, c =>
        {
            c.Request.QueryString = new QueryString("?filter=v2-index");
            c.Request.Headers.UserAgent = "Mozilla/5.0 test";
            c.Request.Headers.Referer = "https://www.google.com/";
            c.Request.Headers.Cookie = "_ga=GA1.2.3; music4dance=secret-ticket";
            c.Request.ContentLength = 12;
        });

        var entry = sent.Entry;
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
    public async Task EmptyResponse_IsNeitherStartedNorCompletedByTheLog()
    {
        // A 301 from the rewriter: Kestrel should still be the one to start it, with
        // Content-Length: 0, rather than seeing an early flush and going chunked.
        var sent = await SendAsync(Direct, ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status301MovedPermanently;
            return Task.CompletedTask;
        });
        Assert.AreEqual(301, sent.Entry.Status);
        Assert.AreEqual(0, sent.Entry.ResponseBytes);
        Assert.AreEqual(0, sent.Body.Starts);
        Assert.AreEqual(0, sent.Body.Completes);
    }

    [TestMethod]
    public async Task CountsBytesWrittenThroughBodyWriter()
    {
        var sent = await SendAsync(Direct, async ctx =>
        {
            await ctx.Response.BodyWriter.WriteAsync(Encoding.UTF8.GetBytes("twelve bytes"));
            var span = ctx.Response.BodyWriter.GetSpan(3);
            "abc"u8.CopyTo(span);
            ctx.Response.BodyWriter.Advance(3);
            await ctx.Response.BodyWriter.FlushAsync();
        });
        Assert.AreEqual(15, sent.Entry.ResponseBytes);
        Assert.AreEqual("twelve bytesabc", Encoding.UTF8.GetString(sent.Body.Body.ToArray()));
        // The original body feature is back in place afterwards.
        Assert.AreSame(sent.Body, sent.Context.Features.Get<IHttpResponseBodyFeature>());
    }

    [TestMethod]
    public async Task CountsSendFile()
    {
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllBytesAsync(path, new byte[100]);
            var sent = await SendAsync(Direct, ctx => ctx.Response.SendFileAsync(path, 10, null));
            Assert.AreEqual(90, sent.Entry.ResponseBytes);
            Assert.AreEqual(90, sent.Body.SentFileBytes);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public async Task Head_RecordsNoBodyBytes()
    {
        var sent = await SendAsync(Direct, c => c.Request.Method = "HEAD");
        Assert.AreEqual(0, sent.Entry.ResponseBytes);
    }

    [TestMethod]
    public async Task RecordsStatusSetDownstream()
    {
        var sent = await SendAsync(Direct, ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status404NotFound;
            return Task.CompletedTask;
        });
        Assert.AreEqual(404, sent.Entry.Status);
        Assert.AreEqual(0, sent.Entry.ResponseBytes);
    }

    [TestMethod]
    public async Task UnhandledException_IsRecordedAs500AndRethrown()
    {
        var sent = await SendAsync(Direct, _ => throw new InvalidOperationException("boom"));
        Assert.AreEqual(500, sent.Entry.Status);
    }

    [TestMethod]
    public async Task ExceptionAfterTheResponseStarted_IsStillA500()
    {
        var response = new StartedResponseFeature();
        var sent = await SendAsync(Direct, c => c.Features.Set<IHttpResponseFeature>(response), ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status200OK;
            response.Started = true;
            throw new InvalidOperationException("mid-stream");
        }, Config(), withCloudflare: true);
        Assert.AreEqual(500, sent.Entry.Status);
    }

    [TestMethod]
    public async Task ClientThatGaveUp_IsA499()
    {
        using var aborted = new CancellationTokenSource();
        await aborted.CancelAsync();
        var sent = await SendAsync(Direct, c => c.RequestAborted = aborted.Token);
        Assert.AreEqual(RequestLogMiddleware.ClientClosedRequest, sent.Entry.Status);
    }
}
