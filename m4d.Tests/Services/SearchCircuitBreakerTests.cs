#nullable disable

using Azure;
using Azure.Core;
using Azure.Core.Pipeline;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;

using m4d.Services.ServiceHealth;

using Microsoft.Extensions.Logging.Abstractions;

using System.Net;
using System.Text;

namespace m4d.Tests.Services;

// The breaker runs inside a real SearchClient pipeline (with the SDK's default retries on), so
// these also pin down how Azure.Core treats the 503 it throws: not retried, not wrapped
[TestClass]
public class SearchCircuitBreakerTests
{
    private sealed class FakeTime : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 11, 19, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class FakeSearch : HttpMessageHandler
    {
        private readonly List<TaskCompletionSource<HttpStatusCode>> _held = [];

        public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;
        public int Calls;

        // While set, requests wait for Release to choose their response
        public bool Hold { get; set; }

        public void Release(HttpStatusCode status)
        {
            lock (_held)
            {
                _held.ForEach(held => held.SetResult(status));
                _held.Clear();
            }
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _ = Interlocked.Increment(ref Calls);
            var status = Status;
            if (Hold)
            {
                var held = new TaskCompletionSource<HttpStatusCode>(TaskCreationOptions.RunContinuationsAsynchronously);
                lock (_held)
                {
                    _held.Add(held);
                }
                status = await held.Task.WaitAsync(cancellationToken);
            }
            var ok = status == HttpStatusCode.OK
                ? request.RequestUri.AbsolutePath.EndsWith("/docs/search.index") ? """{"value":[]}""" : """{"id":"1"}"""
                : "{}";
            return new HttpResponseMessage(status)
            {
                Content = new StringContent(ok, Encoding.UTF8, "application/json")
            };
        }
    }

    private async Task WaitForCalls(int calls)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (Volatile.Read(ref _search.Calls) < calls)
        {
            Assert.IsTrue(DateTime.UtcNow < deadline, $"Timed out waiting for {calls} request(s)");
            await Task.Delay(5);
        }
    }

    private readonly FakeTime _time = new();
    private readonly FakeSearch _search = new();
    private ServiceHealthManager _health;
    private SearchCircuitBreaker _breaker;
    private SearchClient _client;

    [TestInitialize]
    public void Setup()
    {
        _health = new ServiceHealthManager(NullLogger<ServiceHealthManager>.Instance);
        _breaker = new SearchCircuitBreaker(_health, NullLogger<SearchCircuitBreaker>.Instance, time: _time);
        var options = new SearchClientOptions { Transport = new HttpClientTransport(new HttpClient(_search)) };
        options.Retry.Delay = TimeSpan.FromMilliseconds(1);
        options.Retry.MaxDelay = TimeSpan.FromMilliseconds(5);
        options.AddPolicy(_breaker, HttpPipelinePosition.PerRetry);
        _client = new SearchClient(new Uri("https://fake.search.windows.net"), "songs",
            new AzureKeyCredential("key"), options);
    }

    private Task<Response<SearchDocument>> Get(CancellationToken cancellationToken = default) =>
        _client.GetDocumentAsync<SearchDocument>("1", cancellationToken: cancellationToken);

    private Task<Response<IndexDocumentsResult>> Write() =>
        _client.IndexDocumentsAsync(IndexDocumentsBatch.Upload([new SearchDocument { ["id"] = "1" }]));

    private async Task<RequestFailedException> GetFails()
    {
        try
        {
            _ = await Get();
        }
        catch (RequestFailedException ex)
        {
            return ex;
        }
        Assert.Fail("Expected a RequestFailedException");
        return null;
    }

    private async Task Open()
    {
        _search.Status = HttpStatusCode.ServiceUnavailable;
        _ = await GetFails();
        _search.Calls = 0;
    }

    [TestMethod]
    public async Task Closed_PassesRequestsThrough()
    {
        _ = await Get();
        _ = await Get();

        Assert.AreEqual(2, _search.Calls);
        Assert.IsFalse(_breaker.IsOpen);
    }

    [TestMethod]
    [DataRow(HttpStatusCode.ServiceUnavailable)]
    [DataRow(HttpStatusCode.TooManyRequests)]
    public async Task ThrottledResponse_OpensAndStopsTheSdkRetries(HttpStatusCode status)
    {
        _search.Status = status;

        var ex = await GetFails();

        // Without the breaker the SDK would send this 4 times (3 retries). Its first retry fails
        // fast, and it doesn't retry that
        Assert.AreEqual(1, _search.Calls);
        Assert.AreEqual(1, _breaker.ShortCircuited);
        Assert.AreEqual(503, ex.Status);
        Assert.IsTrue(_breaker.IsOpen);
        Assert.IsFalse(_health.IsServiceHealthy(SearchCircuitBreaker.ServiceName));
    }

    [TestMethod]
    [DataRow(HttpStatusCode.NotFound)]
    [DataRow(HttpStatusCode.BadRequest)]
    public async Task RequestErrors_DoNotOpen(HttpStatusCode status)
    {
        _search.Status = status;

        _ = await GetFails();

        Assert.IsFalse(_breaker.IsOpen);
    }

    [TestMethod]
    public async Task Open_FailsFastWithoutSending()
    {
        await Open();
        _search.Status = HttpStatusCode.OK;
        var before = _breaker.ShortCircuited;

        var ex = await GetFails();

        Assert.AreEqual(0, _search.Calls);
        Assert.AreEqual(503, ex.Status); // SongIndex.DoSearch maps this to "service is unavailable"
        // One attempt: the SDK doesn't retry the breaker's 503
        Assert.AreEqual(1, _breaker.ShortCircuited - before);
    }

    [TestMethod]
    public async Task SuccessfulProbe_Closes()
    {
        await Open();
        _search.Status = HttpStatusCode.OK;
        _time.Now += SearchCircuitBreaker.DefaultBreakDuration;

        _ = await Get();
        _ = await Get();

        Assert.AreEqual(2, _search.Calls);
        Assert.IsFalse(_breaker.IsOpen);
        Assert.IsTrue(_health.IsServiceHealthy(SearchCircuitBreaker.ServiceName));
    }

    [TestMethod]
    public async Task ThrottledProbe_ReopensForAnotherBreak()
    {
        await Open();
        _time.Now += SearchCircuitBreaker.DefaultBreakDuration;

        _ = await GetFails();
        Assert.AreEqual(1, _search.Calls);
        Assert.IsTrue(_breaker.IsOpen);

        _search.Status = HttpStatusCode.OK;
        _time.Now += SearchCircuitBreaker.DefaultBreakDuration / 2;
        _ = await GetFails();
        Assert.AreEqual(1, _search.Calls);
    }

    [TestMethod]
    public async Task HalfOpen_LetsOnlyOneProbeThrough()
    {
        await Open();
        _time.Now += SearchCircuitBreaker.DefaultBreakDuration;
        _search.Hold = true;

        var probe = Get();
        await WaitForCalls(1);
        var other = await GetFails();
        _search.Release(HttpStatusCode.OK);
        _ = await probe;

        Assert.AreEqual(503, other.Status);
        Assert.AreEqual(1, _search.Calls);
        Assert.IsFalse(_breaker.IsOpen);
    }

    [TestMethod]
    public async Task InFlightSuccessWhileOpen_DoesNotClose()
    {
        // A request sent before the breaker opened that succeeds after it did
        _search.Hold = true;
        var inFlight = Get();
        await WaitForCalls(1);
        _search.Hold = false;
        await Open();

        _search.Release(HttpStatusCode.OK);
        _ = await inFlight;

        Assert.IsTrue(_breaker.IsOpen);
    }

    [TestMethod]
    public async Task InFlightThrottledWhileOpen_DoesNotExtendTheBreak()
    {
        _search.Hold = true;
        var inFlight = Get();
        await WaitForCalls(1);
        _search.Hold = false;
        await Open();

        _time.Now += SearchCircuitBreaker.DefaultBreakDuration / 2;
        _search.Release(HttpStatusCode.ServiceUnavailable);
        _ = await Assert.ThrowsExactlyAsync<RequestFailedException>(() => inFlight);

        // Still due to probe at the original time
        _time.Now += SearchCircuitBreaker.DefaultBreakDuration / 2;
        _search.Status = HttpStatusCode.OK;
        _search.Calls = 0;
        _ = await Get();
        Assert.AreEqual(1, _search.Calls);
        Assert.IsFalse(_breaker.IsOpen);
    }

    [TestMethod]
    public async Task StaleThrottledAfterClose_DoesNotReopen()
    {
        // Sent before the breaker opened, answered 503 after a probe closed it
        _search.Hold = true;
        var stale = Get();
        await WaitForCalls(1);
        _search.Hold = false;
        await Open();
        _search.Status = HttpStatusCode.OK;
        _time.Now += SearchCircuitBreaker.DefaultBreakDuration;
        _ = await Get();
        Assert.IsFalse(_breaker.IsOpen);

        _search.Release(HttpStatusCode.ServiceUnavailable);
        _ = await stale; // The SDK's retry of it goes out as a new attempt, and succeeds

        Assert.IsFalse(_breaker.IsOpen);
    }

    [TestMethod]
    [DataRow(HttpStatusCode.InternalServerError)]
    [DataRow(HttpStatusCode.BadGateway)]
    [DataRow(HttpStatusCode.GatewayTimeout)]
    public async Task ProbeServerError_Reopens(HttpStatusCode status)
    {
        await Open();
        _time.Now += SearchCircuitBreaker.DefaultBreakDuration;
        _search.Status = status;

        _ = await GetFails();

        Assert.AreEqual(1, _search.Calls);
        Assert.IsTrue(_breaker.IsOpen);
    }

    [TestMethod]
    public async Task ProbeRequestError_Closes()
    {
        await Open();
        _time.Now += SearchCircuitBreaker.DefaultBreakDuration;
        _search.Status = HttpStatusCode.NotFound; // Search answered

        _ = await GetFails();

        Assert.IsFalse(_breaker.IsOpen);
    }

    [TestMethod]
    public async Task SlowProbe_LetsAnotherProbeAfterTheProbeTimeout()
    {
        await Open();
        _time.Now += SearchCircuitBreaker.DefaultBreakDuration;
        _search.Hold = true;
        var slow = Get();
        await WaitForCalls(1);
        _search.Hold = false;
        _search.Status = HttpStatusCode.OK;

        _ = await GetFails();
        Assert.AreEqual(1, _search.Calls);

        _time.Now += SearchCircuitBreaker.DefaultProbeTimeout;
        _ = await Get();
        Assert.AreEqual(2, _search.Calls);
        Assert.IsFalse(_breaker.IsOpen);

        // The superseded probe's answer doesn't reopen it
        _search.Release(HttpStatusCode.ServiceUnavailable);
        _ = await slow;
        Assert.IsFalse(_breaker.IsOpen);
    }

    [TestMethod]
    public async Task CancelledProbe_FreesTheProbeSlot()
    {
        await Open();
        _time.Now += SearchCircuitBreaker.DefaultBreakDuration;
        _search.Hold = true;
        using var cancel = new CancellationTokenSource();
        var probe = Get(cancel.Token);
        await WaitForCalls(1);
        _search.Hold = false;
        _search.Status = HttpStatusCode.OK;

        await cancel.CancelAsync();
        _ = await Assert.ThrowsAsync<OperationCanceledException>(() => probe);
        Assert.IsTrue(_breaker.IsOpen);

        _ = await Get();
        Assert.AreEqual(2, _search.Calls);
        Assert.IsFalse(_breaker.IsOpen);
    }

    [TestMethod]
    public async Task DocumentWrites_BypassTheBreaker()
    {
        _search.Status = HttpStatusCode.ServiceUnavailable;

        _ = await Assert.ThrowsExactlyAsync<RequestFailedException>(Write);

        // The SDK's 3 retries all go out, and a throttled write doesn't open the breaker
        Assert.AreEqual(4, _search.Calls);
        Assert.IsFalse(_breaker.IsOpen);

        await Open();
        _search.Status = HttpStatusCode.OK;
        _ = await Write();
        Assert.AreEqual(1, _search.Calls);
        Assert.IsTrue(_breaker.IsOpen);
    }

    [TestMethod]
    public async Task WhileOpen_TheBreakerDecidesServiceHealth()
    {
        const string name = SearchCircuitBreaker.ServiceName;
        await Open();
        Assert.IsFalse(_health.IsServiceHealthy(name));

        // A controller that caught a fast-fail marks it again; that doesn't stretch the break
        // to the health check's one-minute cooldown
        _health.MarkUnavailable(name, "test: caught the breaker's 503");
        _time.Now += SearchCircuitBreaker.DefaultBreakDuration;
        Assert.IsTrue(_health.IsServiceHealthy(name), "Due a probe");

        _search.Status = HttpStatusCode.OK;
        _search.Hold = true;
        var probe = Get();
        await WaitForCalls(1);
        Assert.IsFalse(_health.IsServiceHealthy(name), "Probe in flight");

        _search.Release(HttpStatusCode.OK);
        _ = await probe;
        Assert.IsTrue(_health.IsServiceHealthy(name));
    }

    [TestMethod]
    public void AssignServiceNames_OneNamePerSearchService()
    {
        // The appsettings.json layout
        var names = SearchCircuitBreakers.AssignServiceNames([
            ("SongIndexProd-4", "https://music4dance.search.windows.net"),
            ("SongIndexTest-4", "https://music4dance.search.windows.net"),
            ("SongIndexExperimental", "https://music4dance.search.windows.net"),
            ("PageIndex", "https://m4d.search.windows.net"),
            ("OtherIndex", "https://other.search.windows.net/"),
        ]);

        Assert.AreEqual(SearchCircuitBreaker.ServiceName, names["SongIndexProd-4"]);
        Assert.AreEqual(SearchCircuitBreaker.ServiceName, names["SongIndexExperimental"]);
        Assert.AreEqual(SearchCircuitBreakers.PageServiceName, names["PageIndex"]);
        Assert.AreEqual("SearchService (other.search.windows.net)", names["OtherIndex"]);
    }

    [TestMethod]
    public void Breakers_AreSeparatePerService()
    {
        var breakers = new SearchCircuitBreakers(_health, NullLoggerFactory.Instance);

        var songs = breakers.For(SearchCircuitBreaker.ServiceName);

        Assert.AreSame(songs, breakers.For(SearchCircuitBreaker.ServiceName));
        Assert.AreNotSame(songs, breakers.For(SearchCircuitBreakers.PageServiceName));
        Assert.AreEqual(SearchCircuitBreakers.PageServiceName,
            breakers.For(SearchCircuitBreakers.PageServiceName).HealthServiceName);
    }
}
