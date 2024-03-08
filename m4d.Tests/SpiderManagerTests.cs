using m4d.Utilities;
using Microsoft.Extensions.Configuration;

namespace m4d.Tests;

[TestClass]
public class SpiderManagerTests
{
    private static Dictionary<string, string> _botFilter = new()
    {
        {"Configuration:BotFilter:ExcludeTokens", "alwayson;mediapartners-google"},
        {"Configuration:BotFilter:ExcludeFragments", "spider;bot"},
        {"Configuration:BotFilter:BadFragments", "baiduspider"},
    };

    private static IConfiguration _config = 
        new ConfigurationBuilder()
        .AddInMemoryCollection(initialData: _botFilter!)
        .Build();

    [TestMethod]
    public void CheckSpiders_Returns_False_OnBrowserAgent()
    {
        var agent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/58.0.3029.110 Safari/537.3";
        var result = SpiderManager.CheckAnySpiders(agent, _config);
        Assert.IsFalse(result); 
    }

    [TestMethod]
    public void CheckSpiders_Returns_True_OnGoogleBot()
    {
        var agent = "Mozilla/5.0 AppleWebKit/537.36 (KHTML, like Gecko; compatible; Googlebot/2.1; +http://www.google.com/bot.html) Chrome/W.X.Y.Z Safari/537.36";
        var result = SpiderManager.CheckAnySpiders(agent, _config);
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void CheckSpiders_Returns_True_OnMediaPartnersBot()
    {
        var agent = "Mediapartners-Google";
        var result = SpiderManager.CheckAnySpiders(agent, _config);
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void CheckBadSpiders_Returns_False_OnGoogleBot()
    {
        var agent = "Mozilla/5.0 AppleWebKit/537.36 (KHTML, like Gecko; compatible; Googlebot/2.1; +http://www.google.com/bot.html) Chrome/W.X.Y.Z Safari/537.36";
        var result = SpiderManager.CheckBadSpiders(agent, _config);
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void CheckBadSpiders_Returns_True_OnBaiduSpider()
    {
        var agent = "Baiduspider+(+http://www.baidu.com/search/spider.htm);google|baiduspider|baidu|spider|sogou|bing|yahoo|soso|sosospider|360spider|youdao|jikeSpider;)";
        var result = SpiderManager.CheckBadSpiders(agent, _config);
        Assert.IsTrue(result);
    }

}