using System.Net;

using Azure.Identity;

using m4d.Configuration;
using m4d.Middleware;
using m4d.Services;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace m4d.Tests.Middleware;

[TestClass]
public class RequestLogTests
{
    private const string Visitor = "203.0.113.45";
    private const string CloudflareEdge = "172.70.1.20"; // in 172.64.0.0/13
    private const string Container = "https://m4dlogs.blob.core.windows.net/app-request-logs";

    private string _directory = "";

    [TestInitialize]
    public void CreateDirectory()
    {
        _directory = Path.Combine(Path.GetTempPath(), "m4d-request-log-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
    }

    [TestCleanup]
    public void DeleteDirectory()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
            // The logger may still hold the file for a moment; the temp folder gets cleaned anyway.
        }
    }

    private static IConfiguration Config(Dictionary<string, string> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values!).Build();

    private Dictionary<string, string> Enabled() => Enabled(_directory);

    private static Dictionary<string, string> Enabled(string directory) => new()
    {
        { "RequestLog:Enabled", "true" },
        { "RequestLog:Directory", directory },
    };

    // --- Settings ---

    [TestMethod]
    public void Read_Disabled_IsOffWithoutError()
    {
        var settings = RequestLogSetup.Read(Config(new()), out var error);

        Assert.IsNull(settings);
        Assert.IsNull(error);
    }

    [TestMethod]
    public void Read_Valid_UsesDefaults()
    {
        var settings = RequestLogSetup.Read(Config(Enabled()), out var error);

        Assert.IsNull(error);
        Assert.IsNotNull(settings);
        Assert.AreEqual(_directory, settings.Directory);
        Assert.AreEqual(RequestLogSetup.DefaultRetainedFileCountLimit, settings.RetainedFileCountLimit);
        Assert.AreEqual(RequestLogSetup.DefaultFileSizeLimitMB, settings.FileSizeLimitMB);
        Assert.IsNull(settings.UploadContainer);
    }

    [TestMethod]
    public void Read_UploadContainer_IsParsed()
    {
        var values = Enabled();
        values["RequestLog:UploadContainer"] = Container;

        var settings = RequestLogSetup.Read(Config(values), out var error);

        Assert.IsNull(error);
        Assert.AreEqual(new Uri(Container), settings!.UploadContainer);
    }

    [TestMethod]
    [DataRow("app-request-logs")]
    [DataRow("http://m4dlogs.blob.core.windows.net/app-request-logs")]
    public void Read_UploadContainerNotHttps_IsOffWithError(string value)
    {
        var values = Enabled();
        values["RequestLog:UploadContainer"] = value;

        var settings = RequestLogSetup.Read(Config(values), out var error);

        Assert.IsNull(settings);
        StringAssert.Contains(error, "RequestLog:UploadContainer");
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("relative/logs")]
    public void Read_DirectoryNotAbsolute_IsOffWithError(string directory)
    {
        var settings = RequestLogSetup.Read(Config(Enabled(directory)), out var error);

        Assert.IsNull(settings);
        StringAssert.Contains(error, "absolute path");
    }

    [TestMethod]
    public void Read_MissingDirectory_IsFine()
    {
        // The logger creates it on the first write.
        var settings = RequestLogSetup.Read(Config(Enabled(Path.Combine(_directory, "new"))), out var error);

        Assert.IsNull(error);
        Assert.IsNotNull(settings);
    }

    [TestMethod]
    [DataRow("RequestLog:RetainedFileCountLimit", "0")]
    [DataRow("RequestLog:FileSizeLimitMB", "5000")]
    public void Read_LimitOutOfRange_IsOffWithError(string key, string value)
    {
        var values = Enabled();
        values[key] = value;

        var settings = RequestLogSetup.Read(Config(values), out var error);

        Assert.IsNull(settings);
        StringAssert.Contains(error, key);
    }

    [TestMethod]
    public void Configure_NeverLogsCookieValues()
    {
        var options = new W3CLoggerOptions();

        RequestLogSetup.Configure(options, new RequestLogSetup.Settings(_directory, 7, 100, new Uri(Container)));

        Assert.IsFalse(options.LoggingFields.HasFlag(W3CLoggingFields.Cookie));
        Assert.IsFalse(options.AdditionalRequestHeaders.Contains("Cookie"));
        Assert.AreEqual(TimeSpan.FromSeconds(1), options.FlushInterval);
        Assert.AreEqual(100 * 1024 * 1024, options.FileSizeLimit);
    }

    [TestMethod]
    public void Add_WithUploadContainer_RegistersUploader()
    {
        var values = Enabled();
        values["RequestLog:UploadContainer"] = Container;
        var services = new ServiceCollection();

        Assert.IsTrue(services.AddM4dRequestLog(Config(values), new DefaultAzureCredential()));

        Assert.IsTrue(services.Any(d => d.ImplementationType == typeof(RequestLogUploader)));
        Assert.IsTrue(services.Any(d => d.ServiceType == typeof(IRequestLogStore)));
    }

    [TestMethod]
    public void Add_WithoutUploadContainer_LogsOnlyToDisk()
    {
        var services = new ServiceCollection();

        Assert.IsTrue(services.AddM4dRequestLog(Config(Enabled()), new DefaultAzureCredential()));

        Assert.IsFalse(services.Any(d => d.ImplementationType == typeof(RequestLogUploader)));
    }

    // --- RequestLogFieldsMiddleware ---

    private static async Task<HttpContext> SendFieldsAsync(Action<HttpContext> setup)
    {
        var context = new DefaultHttpContext();
        setup(context);
        await new RequestLogFieldsMiddleware(_ => Task.CompletedTask).InvokeAsync(context);
        return context;
    }

    [TestMethod]
    public async Task Fields_ComeFromClientAddresses()
    {
        var context = await SendFieldsAsync(c => c.Features.Set(
            new ClientAddresses(IPAddress.Parse(Visitor), IPAddress.Parse(CloudflareEdge))));

        Assert.AreEqual(Visitor, context.Request.Headers[RequestLogFieldsMiddleware.ClientHeader].ToString());
        Assert.AreEqual(CloudflareEdge, context.Request.Headers[RequestLogFieldsMiddleware.RelayHeader].ToString());
    }

    [TestMethod]
    public async Task Fields_OverwriteValuesTheClientSent()
    {
        var context = await SendFieldsAsync(c =>
        {
            c.Request.Headers[RequestLogFieldsMiddleware.ClientHeader] = "198.51.100.1";
            c.Request.Headers[RequestLogFieldsMiddleware.RelayHeader] = "198.51.100.2";
            c.Request.Headers[RequestLogFieldsMiddleware.CookiesHeader] = "planted=";
        });

        Assert.AreEqual("", context.Request.Headers[RequestLogFieldsMiddleware.ClientHeader].ToString());
        Assert.AreEqual("", context.Request.Headers[RequestLogFieldsMiddleware.RelayHeader].ToString());
        Assert.AreEqual("", context.Request.Headers[RequestLogFieldsMiddleware.CookiesHeader].ToString());
    }

    [TestMethod]
    public async Task Fields_CookieNamesOnly()
    {
        var context = await SendFieldsAsync(c =>
            c.Request.Headers.Cookie = "_ga=GA1.2.secret; music4dance=CfDJ8ticket");

        var cookies = context.Request.Headers[RequestLogFieldsMiddleware.CookiesHeader].ToString();
        Assert.AreEqual("_ga=;music4dance=;", cookies);
    }

    // --- End to end, through the real W3C logger ---

    private async Task<(Dictionary<string, string> Line, string Text)> LogOneRequestAsync(
        string path, Action<HttpContext> setup, Action<WebApplication> endpoints)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string>(Enabled())
        {
            { "CloudflareClientIp:Enabled", "true" },
        }!);
        builder.Logging.ClearProviders();
        builder.WebHost.UseTestServer();
        Assert.IsTrue(builder.Services.AddM4dRequestLog(builder.Configuration, new DefaultAzureCredential()));

        await using var app = builder.Build();
        app.UseMiddleware<CloudflareClientIpMiddleware>();
        app.UseM4dRequestLog();
        app.UseExceptionHandler(e => e.Run(c =>
        {
            c.Response.StatusCode = StatusCodes.Status500InternalServerError;
            return Task.CompletedTask;
        }));
        endpoints(app);
        await app.StartAsync();

        await app.GetTestServer().SendAsync(c =>
        {
            c.Request.Method = "GET";
            c.Request.Path = path;
            setup(c);
        });

        // The logger writes from a background queue about once a second.
        for (var attempt = 0; attempt < 100; attempt++)
        {
            var file = Directory.GetFiles(_directory, RequestLogSetup.FileNamePrefix + "*.txt").SingleOrDefault();
            if (file != null)
            {
                using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                var text = await new StreamReader(stream).ReadToEndAsync();
                var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                var fields = lines.FirstOrDefault(l => l.StartsWith("#Fields:"));
                var record = lines.FirstOrDefault(l => !l.StartsWith('#'));
                if (fields != null && record != null)
                {
                    var names = fields["#Fields:".Length..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    return (names.Zip(record.Split(' ')).ToDictionary(p => p.First, p => p.Second), text);
                }
            }
            await Task.Delay(100);
        }
        Assert.Fail("No request log line was written");
        return default;
    }

    [TestMethod]
    public async Task EndToEnd_RecordsVisitorRelayAndCookieNames()
    {
        var (line, text) = await LogOneRequestAsync("/dances/waltz",
            c =>
            {
                c.Connection.RemoteIpAddress = IPAddress.Parse(CloudflareEdge);
                c.Request.Headers[CloudflareClientIpMiddleware.ConnectingIpHeader] = Visitor;
                c.Request.Headers[RequestLogFieldsMiddleware.ClientHeader] = "198.51.100.1";
                c.Request.Headers.Cookie = "_ga=GA1.2.secret; music4dance=CfDJ8ticket";
                c.Request.Headers.UserAgent = "Mozilla/5.0 (Windows NT 10.0)";
                c.Request.QueryString = new QueryString("?page=2");
            },
            app => app.MapGet("/dances/waltz", () => "waltz"));

        Assert.AreEqual(Visitor, line["cs(X-M4d-Log-Client)"]);
        Assert.AreEqual(CloudflareEdge, line["cs(X-M4d-Log-Relay)"]);
        Assert.AreEqual("_ga=;music4dance=;", line["cs(X-M4d-Log-Cookies)"]);
        Assert.AreEqual("/dances/waltz", line["cs-uri-stem"]);
        Assert.AreEqual("?page=2", line["cs-uri-query"]);
        Assert.AreEqual("200", line["sc-status"]);
        // The logger writes whitespace as '+'.
        Assert.AreEqual("Mozilla/5.0+(Windows+NT+10.0)", line["cs(User-Agent)"]);
        Assert.IsFalse(text.Contains("secret") || text.Contains("ticket"), "cookie values were logged");
    }

    [TestMethod]
    public async Task EndToEnd_UnhandledException_IsLoggedAs500()
    {
        var (line, _) = await LogOneRequestAsync("/boom",
            c => c.Connection.RemoteIpAddress = IPAddress.Parse(Visitor),
            app => app.MapGet("/boom", (Func<string>)(() => throw new InvalidOperationException("boom"))));

        Assert.AreEqual("500", line["sc-status"]);
        Assert.AreEqual(Visitor, line["cs(X-M4d-Log-Client)"]);
    }
}
