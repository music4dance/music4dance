#nullable disable

using m4d.Configuration;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.AzureAppServices;
using Microsoft.Extensions.Options;

namespace m4d.Tests.Configuration;

[TestClass]
public class LoggingSetupTests
{
    [TestMethod]
    public void AddM4dLogging_RaisesTheFileLoggerSizeLimit()
    {
        var services = new ServiceCollection();
        _ = services.AddLogging(M4dApplicationExtensions.AddM4dLogging);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<AzureFileLoggerOptions>>().Value;

        // The 10 MB default filled during a Search throttling burst and dropped the rest of the day
        Assert.AreEqual(M4dApplicationExtensions.AzureFileLogSizeLimitBytes, options.FileSizeLimit);
        Assert.IsTrue(options.FileSizeLimit > 10 * 1024 * 1024);
        // The default of 2 files kept only about a day and a half of the app log
        Assert.AreEqual(M4dApplicationExtensions.AzureFileLogRetainedFiles, options.RetainedFileCountLimit);
        Assert.IsTrue(options.RetainedFileCountLimit > 2);
    }
}
