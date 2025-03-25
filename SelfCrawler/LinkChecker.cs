using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using m4dModels;

namespace SelfCrawler;

[TestClass]
public class LinkChecker : PageChecker, IDisposable
{
    private readonly HashSet<string> _knownGood = new();
    private readonly HashSet<string> _manualTest = new();

    private static readonly string _testUrl = @"https://m4d-linux.azurewebsites.net/";

    // TODO: Rather than adding manual tests, see if we can get selenium to test if the
    //  URL is live - https://stackoverflow.com/questions/6509628/how-to-get-http-response-code-using-selenium-webdriver
    private readonly string[] _hardenedSites =
    {
        @"www.ehow.com",
        @"duetdancestudio.com",
        @"www.hulu.com",
        @"www.olympic.org",
        @"stripe.com",
        @"www.dreamstime.com",
        @"www.linkedin.com",
        @"www.abscdj.com",
        @"usadance.org"

    };

    [TestMethod]
    public void CheckLocalSiteForBrokenLinks()
    {
        var ret = CheckSite(new Crawler<bool>());
        ShowManualTests();
        Assert.IsTrue(ret);
    }

    [TestMethod]
    public void CheckLocalPageForBrokenLinks()
    {
        var ret = CheckPage(new Crawler<bool>());
        ShowManualTests();
        Assert.IsTrue(ret);
    }

    [TestMethod]
    public void CheckTestSiteForBrokenLinks()
    {
        var ret = CheckSite(new Crawler<bool>(_testUrl));
        ShowManualTests();
        Assert.IsTrue(ret);
    }

    [TestMethod]
    public void CheckTestPageForBrokenLinks()
    {
        var ret = CheckPage(new Crawler<bool>(_testUrl));
        ShowManualTests();
        Assert.IsTrue(ret);
    }

    protected override bool CrawlPage(string relativePath, string root, IWebDriver driver)
    {
        NavigateTo(relativePath, root, driver);
        return CheckLinks(relativePath, driver);
    }

    private bool CheckLinks(string relativePath, IWebDriver driver)
    {
        var references = driver.FindElements(By.TagName("a"));
        Assert.IsNotNull(references);
        var errors = CheckElements(references, "href");

        var images = driver.FindElements(By.TagName("img"));
        Assert.IsNotNull(images);
        errors = errors.Concat(CheckElements(images, "src")).ToList();

        if (errors.Any())
        {
            TestContext?.WriteLine($"FAILED (LINKS): {relativePath}");
            foreach (var error in errors)
            {
                TestContext?.WriteLine(error);
            }

            ErrorCount += errors.Count();
            if (ErrorCount > 100)
            {
                throw new Exception(
                    $"Something is very wrong, stopping at {ErrorCount} errors");
            }
            return false;
        }

        return true;
    }

    private IEnumerable<string> CheckElements(IReadOnlyCollection<IWebElement> elements, string attribute)
    {
        var errors = new List<string>();
        foreach (var element in elements)
        {
            var reference = element.GetDomProperty(attribute);
            Debug.Assert(Crawler != null, nameof(Crawler) + " != null");
            if (string.IsNullOrWhiteSpace(reference) || Crawler.IsSelfReference(reference)
                || IsServiceReference(reference) || _knownGood.Contains(reference) ||
                reference.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (_hardenedSites.Any(
                    s => reference.Contains(s, StringComparison.OrdinalIgnoreCase)))
            {
                _manualTest.Add(reference);
                continue;
            }

            try
            {
                var result = Client.Send(new HttpRequestMessage(HttpMethod.Head, reference));

                Trace.WriteLine($"{result.StatusCode}: {reference}");
                if (result.IsSuccessStatusCode)
                {
                    _knownGood.Add(reference);
                }
                else
                {
                    errors.Add($"{reference}: {result.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                errors.Add($"{reference}: {ex.Message}");
            }
        }

        return errors;
    }

    private bool IsServiceReference(string reference)
    {
        foreach (var service in MusicService.GetServices())
        {
            var domain = service.Domain;
            if (domain != null && reference.Contains(service.Domain, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        if (reference.Contains("-na.amazon-adsystem.com", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private void ShowManualTests()
    {
        TestContext?.WriteLine("----- MANUAL TESTS -----");
        foreach (var url in _manualTest)
        {
            TestContext?.WriteLine(url);
        }
    }

    // https://stackoverflow.com/questions/36303770/how-to-read-the-web-browser-console-in-selenium
    // https://irzu.org/research/how-to-get-browser-console-error-messages-using-selenium-webdriver-c/#:~:text=How%20to%20get%20browser%20console%20error%20messages%20using,the%20AddBorwserLogs%20%28%29%20function%20before%20test%20teardown.%20
}