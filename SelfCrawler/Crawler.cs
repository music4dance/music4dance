using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SelfCrawler
{
    internal class Crawler<T> : IDisposable
    {
        private readonly string _root;
        private const string _altRoot = "https://www.music4dance.net";

        private readonly IWebDriver _driver;

        public Crawler(string root = "https://localhost:5001")
        {
            var chromeOptions = new ChromeOptions();
            chromeOptions.AddArguments("--ignore-certificate-errors");
            chromeOptions.SetLoggingPreference(LogType.Browser, LogLevel.All);
            _driver = new ChromeDriver(chromeOptions);
            _root = root;
        }
        public void Dispose()
        {
            _driver.Quit();
            _driver.Dispose();
        }

        public List<T> CrawlPages(Func<string, string, IWebDriver, T> crawl)
        {
            _driver.Navigate().GoToUrl($"{_root}/home/sitemap");

            var elements = _driver.FindElements(By.CssSelector("a.m4d-content"));
            Assert.IsNotNull(elements);

            var references = new List<string>();

            foreach (var element in elements)
            {
                var reference = element?.GetDomProperty("href");
                if (reference == null)
                {
                    continue;
                }
                Console.WriteLine($"{element?.Text} @ {reference}");

                references.Add(RemoveRoot(reference));
            }

            var pages = references.Select(p => crawl(p, _root, _driver));

            return pages.ToList();
        }

        public T SinglePage(Func<string, string, IWebDriver, T> crawl, string url)
        {
            return crawl(url, _root, _driver);
        }

        public IWebElement? TryFindElement(By by)
        {
            try
            {
                return _driver.FindElement(by);
            }
            catch
            {
                return null;
            }
        }

        public bool IsSelfReference(string url)
        {
            return url.StartsWith(_root, StringComparison.OrdinalIgnoreCase) ||
                url.StartsWith(_altRoot, StringComparison.OrdinalIgnoreCase);
        }

        private string RemoveRoot(string url)
        {
            if (url.StartsWith(_root, StringComparison.OrdinalIgnoreCase))
            {
                return url.Substring(_root.Length);
            }

            if (url.StartsWith(_altRoot, StringComparison.OrdinalIgnoreCase))
            {
                return url.Substring(_altRoot.Length);
            }
            return url;
        }
    }
}
