#nullable disable

using m4d.Middleware;
using m4d.Services;
using m4d.Tests.Services;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

using Moq;

namespace m4d.Tests.Middleware;

[TestClass]
public class ArchivedAssetMiddlewareTests
{
    private const string OldBundle = "old-AAAA1111.js";
    private const string CurrentBundle = "current-BBBB2222.js";

    private string _root;
    private string _webRoot;
    private AssetArchive _archive;
    private PhysicalFileProvider _fileProvider;

    [TestInitialize]
    public void Setup()
    {
        _root = Path.Combine(Path.GetTempPath(), $"archived-asset-{Guid.NewGuid():N}");
        _webRoot = Path.Combine(_root, "wwwroot");
        var assets = Path.Combine(_webRoot, "vclient", "assets");
        _archive = new AssetArchive(
            Path.Combine(_root, "data", AssetArchive.FolderName), assets, new AssetArchiveOptions(),
            new AssetArchiveTests.TestClock(DateTimeOffset.UtcNow), NullLogger<AssetArchive>.Instance);

        // An earlier build, archived, then replaced in wwwroot by the current one
        _ = Directory.CreateDirectory(assets);
        File.WriteAllText(Path.Combine(assets, OldBundle), "old bundle");
        File.WriteAllText(Path.Combine(assets, OldBundle + ".br"), "old bundle, brotli");
        File.WriteAllText(Path.Combine(assets, OldBundle + ".gz"), "old bundle, gzip");
        _archive.ArchiveCurrent();
        Directory.Delete(assets, recursive: true);
        _ = Directory.CreateDirectory(assets);
        File.WriteAllText(Path.Combine(assets, CurrentBundle), "current bundle");

        _fileProvider = new PhysicalFileProvider(_webRoot);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _fileProvider.Dispose();
        Directory.Delete(_root, recursive: true);
    }

    private async Task<(HttpResponse Response, string Body, bool PassedThrough)> Invoke(
        string path, string method = "GET", string acceptEncoding = null)
    {
        var environment = new Mock<IWebHostEnvironment>();
        environment.Setup(e => e.WebRootFileProvider).Returns(_fileProvider);

        var passedThrough = false;
        var middleware = new ArchivedAssetMiddleware(_ =>
        {
            passedThrough = true;
            return Task.CompletedTask;
        }, _archive, environment.Object);

        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        if (acceptEncoding != null)
        {
            context.Request.Headers.AcceptEncoding = acceptEncoding;
        }

        var body = new MemoryStream();
        context.Response.Body = body;

        await middleware.InvokeAsync(context);
        return (context.Response, System.Text.Encoding.UTF8.GetString(body.ToArray()), passedThrough);
    }

    [TestMethod]
    public async Task EarlierBuildsBundle_IsServedFromTheArchive()
    {
        var (response, body, passedThrough) = await Invoke($"/vclient/assets/{OldBundle}");

        Assert.IsFalse(passedThrough);
        Assert.AreEqual(StatusCodes.Status200OK, response.StatusCode);
        Assert.AreEqual("text/javascript", response.ContentType);
        Assert.AreEqual("old bundle", body);
        Assert.AreEqual("old bundle".Length, response.ContentLength);
        Assert.AreEqual(0, response.Headers.ContentEncoding.Count);
        Assert.AreEqual("Accept-Encoding", response.Headers.Vary.ToString());
    }

    [TestMethod]
    [DataRow("gzip, deflate, br", "br", "old bundle, brotli")]
    [DataRow("gzip", "gzip", "old bundle, gzip")]
    [DataRow("br;q=0, gzip", "gzip", "old bundle, gzip")]
    public async Task EarlierBuildsBundle_PrefersAPrecompressedCopy(
        string acceptEncoding, string expectedEncoding, string expectedBody)
    {
        var (response, body, _) = await Invoke($"/vclient/assets/{OldBundle}", acceptEncoding: acceptEncoding);

        Assert.AreEqual(expectedEncoding, response.Headers.ContentEncoding.ToString());
        Assert.AreEqual(expectedBody, body);
        Assert.AreEqual("text/javascript", response.ContentType);
    }

    [TestMethod]
    public async Task Head_SendsHeadersWithoutTheBody()
    {
        var (response, body, passedThrough) = await Invoke($"/vclient/assets/{OldBundle}", method: "HEAD");

        Assert.IsFalse(passedThrough);
        Assert.AreEqual(StatusCodes.Status200OK, response.StatusCode);
        Assert.AreEqual("old bundle".Length, response.ContentLength);
        Assert.AreEqual("", body);
    }

    [TestMethod]
    public async Task InThePipeline_ArchivedAndCurrentBundlesAreBothServedImmutable()
    {
        // The production order: cache headers, then the archive, then the static files
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            WebRootPath = _webRoot,
            EnvironmentName = Environments.Production,
        });
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(_archive);
        await using var app = builder.Build();
        app.UseMiddleware<StaticAssetCacheMiddleware>();
        app.UseMiddleware<ArchivedAssetMiddleware>();
        app.UseStaticFiles();
        await app.StartAsync();
        using var client = app.GetTestClient();

        foreach (var (bundle, content) in new[] { (OldBundle, "old bundle"), (CurrentBundle, "current bundle") })
        {
            var response = await client.GetAsync($"/vclient/assets/{bundle}");

            Assert.AreEqual(System.Net.HttpStatusCode.OK, response.StatusCode, bundle);
            Assert.AreEqual(content, await response.Content.ReadAsStringAsync());
            var cacheControl = response.Headers.CacheControl;
            Assert.IsNotNull(cacheControl, bundle);
            Assert.IsTrue(cacheControl.Public, bundle);
            Assert.AreEqual(TimeSpan.FromDays(365), cacheControl.MaxAge, bundle);
            Assert.IsTrue(cacheControl.Extensions.Any(e => e.Name == "immutable"), bundle);
        }

        var missing = await client.GetAsync("/vclient/assets/missing-CCCC3333.js");
        Assert.AreEqual(System.Net.HttpStatusCode.NotFound, missing.StatusCode);
    }

    [TestMethod]
    [DataRow("/vclient/assets/" + CurrentBundle, "GET")] // the current build has it
    [DataRow("/vclient/assets/missing-CCCC3333.js", "GET")] // no build has it: 404 as before
    [DataRow("/vclient/assets/" + OldBundle, "POST")]
    [DataRow("/vclient/assets/../assets/" + OldBundle, "GET")]
    [DataRow("/vclient/assets/sub/" + OldBundle, "GET")]
    [DataRow("/vclient/assets/.archived", "GET")]
    [DataRow("/images/" + OldBundle, "GET")]
    [DataRow("/vclient/assets", "GET")]
    public async Task OtherRequests_PassThrough(string path, string method)
    {
        var (_, body, passedThrough) = await Invoke(path, method);

        Assert.IsTrue(passedThrough);
        Assert.AreEqual("", body);
    }
}
