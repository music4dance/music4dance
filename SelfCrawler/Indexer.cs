using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Azure.Identity;
using Azure.Search.Documents;

using m4dModels;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

using OpenQA.Selenium;

namespace SelfCrawler
{

    [TestClass]
    public class Indexer : IDisposable
    {
        private readonly Crawler<PageSearch> _crawler;

        public Indexer()
        {
            _crawler = new Crawler<PageSearch>();
            //_crawler = new Crawler<PageSearch>("https://m4d-linux.azurewebsites.net/");
        }

        public void Dispose()
        {
            _crawler.Dispose();
        }

        [TestMethod]
        public void CrawlAndStore()
        {
            var pages = _crawler.CrawlPages(CrawlPage);
            SavePages(pages);
            UploadPages(pages);
        }

        [TestMethod]
        public void Crawl()
        {
            var pages = _crawler.CrawlPages(CrawlPage);
            SavePages(pages);
        }

        [TestMethod]
        public void LoadAndStore()
        {
            var pages = LoadPages();
            UploadPages(pages);
        }

        [TestMethod]
        public void SinglePage()
        {
            var url = "/home/readinglist";
            var page = _crawler.SinglePage(CrawlPage, url);
            var pages = new List<PageSearch>() { page.GetEncoded() };
            var client = CreateSearchClient();
            var indexResults = client.UploadDocuments(pages);
            Console.WriteLine(indexResults.Value);
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
            return JsonConvert.DeserializeObject<List<PageSearch>>(text) 
                ?? []; // Ensure a non-null return
        }

        private PageSearch CrawlPage(string relativePath, string root, IWebDriver driver)
        {
            _crawler.NavigateTo(relativePath, root);
            var description = string.Empty;
            var descriptionElement = TryFindElement(By.CssSelector("meta[name='description']"));
            if (descriptionElement != null)
            {
                description = descriptionElement.GetDomAttribute("content");
            }
            var title = driver.Title.Replace(@" - Music4Dance: Shall we dance...to music?", "");
            var content = TryFindElement(By.Id("body-content"));
            if (content == null)
            {
                Console.WriteLine($"{relativePath} has no body-content");
                content = TryFindElement(By.TagName("body"));
            }
            var body = content?.Text.Replace(@"\r\n", " ").Replace("\r\n", " ");

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

        protected static readonly JsonSerializerSettings CamelCaseSerializerSettings = new()
        {
            NullValueHandling = NullValueHandling.Ignore,
            DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate,
            ContractResolver = new DefaultContractResolver
            {
                NamingStrategy = new CamelCaseNamingStrategy()
            }
        };

        protected IWebElement? TryFindElement(By by)
        {
            return _crawler.TryFindElement(by);
        }

        private SearchClient CreateSearchClient(string id = "pages")
        {
            var endpoint = new Uri(@"https://m4d.search.windows.net");
            var credential = new DefaultAzureCredential();

            // Create a client
            return new SearchClient(endpoint, id, credential);
        }
    }
}