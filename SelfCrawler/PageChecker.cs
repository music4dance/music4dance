using OpenQA.Selenium;

namespace SelfCrawler;


[TestClass]
public abstract class PageChecker : IDisposable
{
    public TestContext? TestContext { get; set; }

    protected Crawler<bool>? Crawler;
    protected readonly HttpClient Client = new();
    protected int ErrorCount;

    protected static readonly string _testUrl = @"https://m4d-linux.azurewebsites.net/";

    public PageChecker()
    {
        Client.DefaultRequestHeaders.Add("User-Agent", "Music4Dance.net Link Checker");
        Client.Timeout = TimeSpan.FromMinutes(5);
    }

    public void Dispose()
    {
        Crawler?.Dispose();
    }

    protected bool CheckSite(Crawler<bool> crawler)
    {
        Crawler = crawler;
        var results = Crawler.CrawlPages(CrawlPage);
        return results.All(r => r);
    }

    protected bool CheckPage(Crawler<bool> crawler)
    {
        Crawler = crawler;
        var url = "/home/readinglist";
        return Crawler.SinglePage(CrawlPage, url);
    }
    internal static void NavigateTo(string relativePath, string root, IWebDriver driver)
    {
        var sep = relativePath.Contains('?') ? "&" : "?";
        driver.Navigate().GoToUrl($"{root}{relativePath}{sep}flat=true");
    }

    protected abstract bool CrawlPage(string relativePath, string root, IWebDriver driver);
}