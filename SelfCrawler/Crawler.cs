using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using m4dModels;
using Azure.Search.Documents;
using Azure;

namespace SelfCrawler
{

    [TestClass]
    public class Crawler : IDisposable
    {
        private const string _root = "https://localhost:5001";
        private const string _altRoot = "https://www.music4dance.net";

        private readonly IWebDriver _driver;
        private readonly ISearchServiceManager _searchServiceManager;

        public Crawler()
        {
            _driver = new ChromeDriver();
            _searchServiceManager = new SearchServiceManager(GetIConfiguration());
        }

        public void Dispose()
        {
            _driver.Quit();
            _driver.Dispose();
        }

        [TestMethod]
        public void CrawlAndStore()
        {
            var pages = CrawlPages();
            SavePages(pages);
            UploadPages(pages);
        }

        [TestMethod]
        public void Crawl()
        {
            var pages = CrawlPages();
            SavePages(pages);
        }

        [TestMethod]
        public void LoadAndStore()
        {
            var pages = LoadPages();
            UploadPages(pages);
        }

        private List<PageSearch> CrawlPages()
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

            var pages = references.Select(r => CrawlPage(r));

            return pages.ToList();
        }

        private void UploadPages(List<PageSearch> pages)
        {
            try
            {
                var client = CreateSearchClient();
                var indexResults = client.UploadDocuments(pages.Select(p => p.GetEncoded()));
                Console.WriteLine(indexResults.Value);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        private void SavePages(List<PageSearch> pages)
        {
            var results = JsonConvert.SerializeObject(pages, Formatting.Indented, CamelCaseSerializerSettings);
            File.WriteAllText(@"C:\Temp\SearchIndex.json", results);
        }

        private List<PageSearch> LoadPages()
        {
            var text = File.ReadAllText(@"C:\Temp\SearchIndex.json");
            var pages = JsonConvert.DeserializeObject<List<PageSearch>>(text);
            return pages;
        }

        // TODONEXT:
        //  Figure out how to push this to azure search
        //  Play with search
        //  Build a search control for the header
        //  Add in wordpress search
        private PageSearch CrawlPage(string relativePath)
        {
            _driver.Navigate().GoToUrl($"{_root}{relativePath}");
            string? description = null;
            var descriptionElement = TryFindElement(By.CssSelector("meta[name='description']"));
            if (descriptionElement != null)
            {
               description = descriptionElement.GetDomAttribute("content");
            }
            var title = _driver.Title.Replace(@" - Music4Dance: Shall we dance...to music?", "");
            var content = TryFindElement(By.Id("body-content"));
            if (content == null)
            {
                Console.WriteLine($"{relativePath} has no body-content");
                content = TryFindElement(By.TagName("body"));
            }
            var body = content?.Text.Replace(@"\r\n", " ").Replace("\r\n"," ");

            if (relativePath == "/")
            {
                relativePath = "/home";
            }

            return new PageSearch
            {
                Url = relativePath.Trim('/'),
                Title = title,
                Description = description,
                Content = body
            };
        }

        private string RemoveRoot(string url)
        {
            if (url.StartsWith(_root, StringComparison.OrdinalIgnoreCase))
            {
                return url.Substring(_root.Length);
            }
            else if (url.StartsWith(_altRoot, StringComparison.OrdinalIgnoreCase))
            {
                return url.Substring(_altRoot.Length);
            }
            else
            {
                return url;
            }
        }

        protected static readonly JsonSerializerSettings CamelCaseSerializerSettings = new()
        {
            NullValueHandling = NullValueHandling.Ignore,
            DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate,
            ContractResolver = new DefaultContractResolver
            {
                NamingStrategy = new CamelCaseNamingStrategy()
            }
        };

        protected IWebElement TryFindElement(By by)
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

        public static IConfiguration GetIConfiguration()
        {
            return new ConfigurationBuilder()
                .AddUserSecrets("60050f39-d7c1-4b33-8b65-1e6cbb538661")
                .AddEnvironmentVariables()
                .Build();
        }

        private SearchClient CreateSearchClient(string id = "freep")
        {
            var info = _searchServiceManager.GetInfo(id);
            var endpoint = new Uri($"https://{info.Name}.search.windows.net");
            var credentials = new AzureKeyCredential(info.AdminKey);
            return new SearchClient(endpoint, info.Index, credentials);
        }
    }
}