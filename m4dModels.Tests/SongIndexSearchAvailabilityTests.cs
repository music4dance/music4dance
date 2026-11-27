using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Moq;

using System.Threading;

namespace m4dModels.Tests;

// Exercises SongIndex.DoSearch's handling of a throttled/overloaded Azure Search service
// (RequestFailedException with Status 503 or 429, e.g. throttle-reason: capacityOverloaded).
// Before this fix, DoSearch's generic catch block treated that the same as an unrelated
// field-selection error: it retried immediately with no backoff against the still-overloaded
// service, and on a second failure wrapped both exceptions in an AggregateException that none
// of the callers' `IsSearchServiceError` checks recognized - so it surfaced as an unhandled
// exception instead of the graceful "service unavailable" degradation those checks exist for.
[TestClass]
public class SongIndexSearchAvailabilityTests
{
    private class FaultInjectingSongIndex(SearchClient client, ISearchServiceManager manager) : SongIndex
    {
        protected override SearchClient Client => client;
        protected override ISearchServiceManager Manager => manager;
    }

    private static Mock<SearchClient> CreateThrowingClient(int status)
    {
        var mock = new Mock<SearchClient>();
        mock.Setup(c => c.SearchAsync<SearchDocument>(
                It.IsAny<string>(), It.IsAny<SearchOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException(status, "Service Unavailable"));
        return mock;
    }

    [TestMethod]
    [DataRow(503)]
    [DataRow(429)]
    public async Task Search_ServiceThrottled_ThrowsRecognizedUnavailableException(int status)
    {
        var client = CreateThrowingClient(status);
        var manager = new Mock<ISearchServiceManager>();
        var index = new FaultInjectingSongIndex(client.Object, manager.Object);

        var ex = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => index.Search("test", new SearchOptions()));

        Assert.IsTrue(ex.Message.Contains("Azure Search service is unavailable"));
        Assert.IsInstanceOfType<RequestFailedException>(ex.InnerException);
        Assert.AreEqual(status, ((RequestFailedException)ex.InnerException).Status);
        manager.Verify(m => m.ReportSearchSuccess(), Times.Never);
    }

    [TestMethod]
    public async Task Search_Succeeds_ReportsSuccessToSearchServiceManager()
    {
        var mock = new Mock<SearchClient>();
        mock.Setup(c => c.SearchAsync<SearchDocument>(
                It.IsAny<string>(), It.IsAny<SearchOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(
                SearchModelFactory.SearchResults<SearchDocument>([], null, null, null, null),
                Mock.Of<Response>()));
        var manager = new Mock<ISearchServiceManager>();
        var index = new FaultInjectingSongIndex(mock.Object, manager.Object);

        _ = await index.Search("test", new SearchOptions());

        manager.Verify(m => m.ReportSearchSuccess(), Times.Once);
    }

    // What Azure.Core throws once a request has run past ClientOptions.Retry.NetworkTimeout on
    // every try (seen on 2026-11-27 from SongIndex.FindSong via SongController.Details)
    // The first try timed out; the SDK's retry was refused by the open SearchCircuitBreaker
    private static AggregateException NetworkTimeout() =>
        new("Retry failed after 2 tries.",
            new TaskCanceledException("The operation was cancelled because it exceeded the configured timeout of 0:01:40."),
            new RequestFailedException(503, "Azure Search circuit breaker is open (a retried query timed out), so this request wasn't sent"));

    [TestMethod]
    public async Task Search_NetworkTimeout_ThrowsRecognizedUnavailableExceptionWithoutRetrying()
    {
        var client = new Mock<SearchClient>();
        client.Setup(c => c.SearchAsync<SearchDocument>(
                It.IsAny<string>(), It.IsAny<SearchOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(NetworkTimeout());
        var index = new FaultInjectingSongIndex(client.Object, Mock.Of<ISearchServiceManager>());

        var ex = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => index.Search("test", new SearchOptions()));

        Assert.IsTrue(ex.Message.Contains("Azure Search service is unavailable"));
        client.Verify(
            c => c.SearchAsync<SearchDocument>(
                It.IsAny<string>(), It.IsAny<SearchOptions>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task FindSong_NetworkTimeout_ThrowsRecognizedUnavailableException()
    {
        var client = new Mock<SearchClient>();
        client.Setup(c => c.GetDocumentAsync<SearchDocument>(
                It.IsAny<string>(), It.IsAny<GetDocumentOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(NetworkTimeout());
        var index = new FaultInjectingSongIndex(client.Object, Mock.Of<ISearchServiceManager>());

        var ex = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => index.FindSong(Guid.NewGuid()));

        Assert.IsTrue(ex.Message.Contains("Azure Search service is unavailable"));
        Assert.IsInstanceOfType<AggregateException>(ex.InnerException);
    }

    [TestMethod]
    public async Task FindSong_NotFound_StillReturnsNull()
    {
        var client = new Mock<SearchClient>();
        client.Setup(c => c.GetDocumentAsync<SearchDocument>(
                It.IsAny<string>(), It.IsAny<GetDocumentOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException(404, "Not Found"));
        var index = new FaultInjectingSongIndex(client.Object, Mock.Of<ISearchServiceManager>());

        Assert.IsNull(await index.FindSong(Guid.NewGuid()));
    }

    [TestMethod]
    public void IsSearchTimeout_OnlyMatchesCancellations()
    {
        Assert.IsTrue(SongIndex.IsSearchTimeout(NetworkTimeout()));
        Assert.IsTrue(SongIndex.IsSearchTimeout(new TaskCanceledException()));
        Assert.IsTrue(SongIndex.IsSearchTimeout(
            new AggregateException(new TaskCanceledException(), new TaskCanceledException())));
        Assert.IsFalse(SongIndex.IsSearchTimeout(new RequestFailedException(400, "Bad Request")));
        Assert.IsFalse(SongIndex.IsSearchTimeout(new RequestFailedException(503, "Service Unavailable")));
        Assert.IsFalse(SongIndex.IsSearchTimeout(
            new AggregateException(new TaskCanceledException(), new RequestFailedException(400, "Bad Request"))));
        Assert.IsFalse(SongIndex.IsSearchTimeout(new AggregateException()));
    }

    [TestMethod]
    public void IsSearchTimeout_CallerCancellationIsNotATimeout()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.IsFalse(SongIndex.IsSearchTimeout(new TaskCanceledException(), cts.Token));
        Assert.IsTrue(SongIndex.IsSearchTimeout(new TaskCanceledException(), CancellationToken.None));
    }

    [TestMethod]
    public void SearchBulkRead_IsPagesOfHundredsOfDocuments()
    {
        Assert.IsFalse(SearchBulkRead.IsBulk(new SearchOptions()));
        Assert.IsFalse(SearchBulkRead.IsBulk(new SearchOptions { Size = 100 }));
        Assert.IsTrue(SearchBulkRead.IsBulk(new SearchOptions { Size = 250 }));
        Assert.IsTrue(SearchBulkRead.IsBulk(new SearchOptions { Size = 1000 }));
    }

    [TestMethod]
    public async Task Search_OtherClientError_StillUsesRetryWithoutSelectFallback()
    {
        // A non-throttling failure (e.g. a genuine bad-request/schema error) should still fall
        // into the pre-existing retry-without-select path - which retries, fails again against
        // the same mock, and is swallowed by Search's generic catch-all into an empty result -
        // rather than being reclassified as "service unavailable". Confirms the new catch only
        // special-cases 503/429 instead of widening what counts as "unavailable".
        var client = CreateThrowingClient(400);
        var manager = new Mock<ISearchServiceManager>();
        var index = new FaultInjectingSongIndex(client.Object, manager.Object);

        var results = await index.Search("test", new SearchOptions());

        Assert.AreEqual(0, results.Count);
        client.Verify(
            c => c.SearchAsync<SearchDocument>(
                It.IsAny<string>(), It.IsAny<SearchOptions>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
        manager.Verify(m => m.ReportSearchSuccess(), Times.Never);
    }
}
