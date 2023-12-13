using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using m4dModels;

namespace SelfCrawler
{

    [TestClass]
    public class LinkChecker : IDisposable
    {
        public TestContext? TestContext { get; set; }

        private Crawler<bool>? _crawler;
        private readonly HttpClient _client = new ();
        private readonly HashSet<string> _knownGood = new();
        private readonly HashSet<string> _manualTest = new();
        private int _errorCount;

        private static readonly string _testUrl = @"https://m4d-linux.azurewebsites.net/";

        private readonly string[] _knownLogs = 
        {
            @"""Ads are paused""",
            @"""Download the Vue Devtools extension for a better development experience",
            @"""You are running Vue in development mode.",
            @"""[BootstrapVue warn]: tooltip - The provided target is no valid HTML element.""",
            @"https://ir-na.amazon-adsystem.com",
            "[vite] connecting",
            "[vite] connected",
        };

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


        public void Dispose()
        {
            _crawler?.Dispose();
        }

        [TestMethod]
        public void CheckLocalSiteForBrokenLinks()
        {
            CheckSiteForBrokenLinks(new Crawler<bool>());
        }

        [TestMethod]
        public void CheckLocalPageForBrokenLinks()
        {
            CheckPageForBrokenLinks(new Crawler<bool>());
        }

        [TestMethod]
        public void CheckTestSiteForBrokenLinks()
        {
            CheckSiteForBrokenLinks(new Crawler<bool>(_testUrl));
        }

        [TestMethod]
        public void CheckTestPageForBrokenLinks()
        {
            CheckPageForBrokenLinks(new Crawler<bool>(_testUrl));
        }

        private void CheckSiteForBrokenLinks(Crawler<bool> crawler)
        {
            _crawler = crawler;
            var results = _crawler.CrawlPages(CrawlPage);
            ShowManualTests();
            Assert.IsTrue(results.All(r => r));
        }

        private void CheckPageForBrokenLinks(Crawler<bool> crawler)
        {
            _crawler = crawler;
            var url = "/home/tempi";
            var result = _crawler.SinglePage(CrawlPage, url);
            ShowManualTests();
            Assert.IsTrue(result);
        }

        private bool CrawlPage(string relativePath, string root, IWebDriver driver)
        {
            driver.Navigate().GoToUrl($"{root}{relativePath}?flat=true");

            return CheckLogs(relativePath, driver) && CheckLinks(relativePath, driver);
        }

        private bool CheckLogs(string relativePath, IWebDriver driver)
        {
            var logs = driver.Manage().Logs;
            var logEntries = logs.GetLog(LogType.Browser).Where(l => !IsKnownLog(l.Message)).ToList();

            if (logEntries.Any())
            {
                TestContext?.WriteLine($"FAILED (LOGS): {relativePath}");
                foreach (var logEntry in logEntries)
                {
                    TestContext?.WriteLine($"{logEntry.Level}: {logEntry.Message}");
                }

                _errorCount += logEntries.Count;
                if (_errorCount > 100)
                {
                    throw new Exception(
                        $"Something is very wrong, stopping at {_errorCount} errors");
                }
                return false;
            }

            return true;
        }

        private bool IsKnownLog(string log)
        {
            return _knownLogs.Any(log.Contains);
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

                _errorCount += errors.Count();
                if (_errorCount > 100)
                {
                    throw new Exception(
                        $"Something is very wrong, stopping at {_errorCount} errors");
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
                Debug.Assert(_crawler != null, nameof(_crawler) + " != null");
                if (string.IsNullOrWhiteSpace(reference) || _crawler.IsSelfReference(reference)
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
                    var result = _client.Send(new HttpRequestMessage(HttpMethod.Head, reference));

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
}