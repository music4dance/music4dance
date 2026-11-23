#nullable disable

using Azure;
using Azure.Core.Pipeline;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;

using Microsoft.Extensions.Azure;

using Moq;

using System.Net;
using System.Text;

namespace m4dModels.Tests;

// SearchServiceInfo.HasFieldAsync: a failed schema read reports "no such field", and an upload
// built from that drops the field (the individual-artists §7.4 trap), so how long a failure is
// trusted matters
[TestClass]
public class SchemaCacheTests
{
    private sealed class FakeIndexService : HttpMessageHandler
    {
        public HttpStatusCode Status { get; set; }
        public int Calls;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            var body = Status == HttpStatusCode.OK
                ? """
                  {"name":"songs-test-4","fields":[
                    {"name":"SongId","type":"Edm.String","key":true},
                    {"name":"Artists","type":"Collection(Edm.String)"}]}
                  """
                : "{}";
            return Task.FromResult(new HttpResponseMessage(Status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }

    private readonly FakeIndexService _service = new();

    private SearchServiceInfo Info()
    {
        var options = new SearchClientOptions { Transport = new HttpClientTransport(new HttpClient(_service)) };
        options.Retry.MaxRetries = 0;
        var indexClient = new SearchIndexClient(new Uri("https://fake.search.windows.net"),
            new AzureKeyCredential("key"), options);
        var indexFactory = new Mock<IAzureClientFactory<SearchIndexClient>>();
        _ = indexFactory.Setup(f => f.CreateClient("SongIndex")).Returns(indexClient);
        var manager = new Mock<ISearchServiceManager>();
        _ = manager.SetupGet(m => m.ConfigVersion).Returns(4);
        return new SearchServiceInfo("SongIndexTest", 4, "songs-test-4",
            Mock.Of<IAzureClientFactory<SearchClient>>(), indexFactory.Object, manager.Object);
    }

    [TestMethod]
    [DataRow(HttpStatusCode.ServiceUnavailable)]
    [DataRow(HttpStatusCode.TooManyRequests)]
    public async Task ThrottledRead_IsNotCached(HttpStatusCode status)
    {
        var info = Info();
        _service.Status = status;
        Assert.IsFalse(await info.HasFieldAsync(Song.ArtistsField, isNext: false));

        _service.Status = HttpStatusCode.OK;
        Assert.IsTrue(await info.HasFieldAsync(Song.ArtistsField, isNext: false));
        Assert.AreEqual(2, _service.Calls);
    }

    [TestMethod]
    public async Task OtherFailure_IsCachedBriefly()
    {
        var info = Info();
        _service.Status = HttpStatusCode.Forbidden;
        Assert.IsFalse(await info.HasFieldAsync(Song.ArtistsField, isNext: false));

        _service.Status = HttpStatusCode.OK;
        Assert.IsFalse(await info.HasFieldAsync(Song.ArtistsField, isNext: false));
        Assert.AreEqual(1, _service.Calls);
    }
}
