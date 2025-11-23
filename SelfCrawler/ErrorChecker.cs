using OpenQA.Selenium;

namespace SelfCrawler;


[TestClass]
public class ErrorChecker : PageChecker, IDisposable
{
    private readonly string[] _knownLogs =
    [
        @"""Ads are paused""",
        @"""Download the Vue Devtools extension for a better development experience",
        @"""You are running Vue in development mode.",
        @"""[BootstrapVue warn]: tooltip - The provided target is no valid HTML element.""",
        @"https://ir-na.amazon-adsystem.com",
        @"[vite] connecting",
        @"[vite] connected",
        @"Third-party cookie will be blocked",
        @"CSS Hot Reload ignoring",
        @"Ads are running",
    ];

    [TestMethod]
    public void CheckLocalSiteForErrors()
    {
        Assert.IsTrue(CheckSite(new Crawler<bool>()));
    }

    [TestMethod]
    public void CheckLocalPageForErrors()
    {
        Assert.IsTrue(CheckPage(new Crawler<bool>()));
    }

    [TestMethod]
    public void CheckTestSiteForErrors()
    {
        Assert.IsTrue(CheckSite(new Crawler<bool>(_testUrl)));
    }

    [TestMethod]
    public void CheckTestPageForErrors()
    {
        Assert.IsTrue(CheckPage(new Crawler<bool>(_testUrl)));
    }

    protected override bool CrawlPage(string relativePath, string root, IWebDriver driver)
    {
        NavigateTo(relativePath, root, driver);
        return CheckLogs(relativePath, driver);
    }

    // TODO: Do we wnat to make LinkChecker also check logs?
    private bool CheckLogs(string relativePath, IWebDriver driver)
    {
        var logs = driver.Manage().Logs;
        var logEntries = logs.GetLog(LogType.Browser).Where(l => !IsKnownLog(l.Message)).ToList();

        if (logEntries.Count != 0)
        {
            TestContext?.WriteLine($"FAILED (LOGS): {relativePath}");
            foreach (var logEntry in logEntries)
            {
                TestContext?.WriteLine($"{logEntry.Level}: {logEntry.Message}");
            }

            ErrorCount += logEntries.Count;
            if (ErrorCount > 100)
            {
                throw new Exception(
                    $"Something is very wrong, stopping at {ErrorCount} errors");
            }
            return false;
        }

        return true;
    }

    private bool IsKnownLog(string log)
    {
        return _knownLogs.Any(log.Contains);
    }
}