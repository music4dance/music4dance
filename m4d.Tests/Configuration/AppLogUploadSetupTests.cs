using Azure.Identity;

using m4d.Configuration;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace m4d.Tests.Configuration;

[TestClass]
public class AppLogUploadSetupTests
{
    private const string Container = "https://m4dlogs.blob.core.windows.net/app-diagnostic-logs";

    private static readonly string Home = Path.Combine(Path.GetTempPath(), "m4d-home");

    private static IConfiguration Config(Dictionary<string, string> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values!).Build();

    [TestMethod]
    public void Read_NoUploadContainer_IsOff()
    {
        var settings = AppLogUploadSetup.Read(Config(new() { ["HOME"] = Home }), out var error);

        Assert.IsNull(settings);
        Assert.IsNull(error);
    }

    [TestMethod]
    public void Read_DefaultsToTheFileLoggersDirectoryUnderHome()
    {
        var settings = AppLogUploadSetup.Read(
            Config(new() { ["HOME"] = Home, ["AppLog:UploadContainer"] = Container }), out var error);

        Assert.IsNull(error);
        Assert.AreEqual(Path.Combine(Home, "LogFiles", "Application"), settings.Directory);
        Assert.AreEqual(new Uri(Container), settings.UploadContainer);
    }

    [TestMethod]
    public void Read_DirectoryOverridesHome()
    {
        var directory = Path.Combine(Home, "elsewhere");
        var settings = AppLogUploadSetup.Read(Config(new()
        {
            ["HOME"] = Home, ["AppLog:UploadContainer"] = Container, ["AppLog:Directory"] = directory,
        }), out _);

        Assert.AreEqual(directory, settings.Directory);
    }

    [TestMethod]
    [DataRow("http://m4dlogs.blob.core.windows.net/c", null)]
    [DataRow("not a url", null)]
    [DataRow(Container, "relative/path")]
    public void Read_BadSettings_TurnTheUploadOffWithAReason(string container, string directory)
    {
        var values = new Dictionary<string, string> { ["HOME"] = Home, ["AppLog:UploadContainer"] = container };
        if (directory != null)
        {
            values["AppLog:Directory"] = directory;
        }

        var settings = AppLogUploadSetup.Read(Config(values), out var error);

        Assert.IsNull(settings);
        Assert.IsNotNull(error);
    }

    [TestMethod]
    public void Add_WithUploadContainer_RegistersAnUploader()
    {
        var services = new ServiceCollection();

        services.AddM4dAppLogUpload(
            Config(new() { ["HOME"] = Home, ["AppLog:UploadContainer"] = Container }), new DefaultAzureCredential());

        Assert.AreEqual(1, services.Count(d => d.ServiceType == typeof(IHostedService)));
    }

    [TestMethod]
    public void Add_WithBothLogs_RegistersTwoUploaders()
    {
        // AddHostedService<T> would keep only one of a type; each log needs its own.
        var directory = Path.Combine(Home, "requests");
        var configuration = Config(new()
        {
            ["HOME"] = Home,
            ["AppLog:UploadContainer"] = Container,
            ["RequestLog:Enabled"] = "true",
            ["RequestLog:Directory"] = directory,
            ["RequestLog:UploadContainer"] = "https://m4dlogs.blob.core.windows.net/app-request-logs",
        });
        var services = new ServiceCollection();

        services.AddM4dRequestLog(configuration, new DefaultAzureCredential());
        services.AddM4dAppLogUpload(configuration, new DefaultAzureCredential());

        Assert.AreEqual(2, services.Count(d => d.ServiceType == typeof(IHostedService)));
    }

    [TestMethod]
    public void Add_WithoutUploadContainer_RegistersNothing()
    {
        var services = new ServiceCollection();

        services.AddM4dAppLogUpload(Config(new() { ["HOME"] = Home }), new DefaultAzureCredential());

        Assert.AreEqual(0, services.Count);
    }
}
