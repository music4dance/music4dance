using Azure;
using Azure.Core;
using Azure.Core.Pipeline;

namespace m4d.Services.ServiceHealth;

/// <summary>
/// A circuit breaker in the HTTP pipeline of the <c>SearchClient</c>s for one Azure AI Search
/// service, shared by those clients since they share that service's capacity (one per service:
/// see <see cref="SearchCircuitBreakers"/>). A <c>503</c> or <c>429</c> opens it; while it's open
/// every query attempt fails at once with a <see cref="RequestFailedException"/> of status 503,
/// without sending anything. When <see cref="BreakDuration"/> has passed, one request goes
/// through as a probe while the rest keep failing fast: a response below 500 closes the breaker,
/// a 5xx, 429 or exception reopens it. A probe still running after <see cref="ProbeTimeout"/>
/// lets another request probe, so one slow response doesn't hold Search closed.
/// </summary>
/// <remarks>
/// <para>
/// It sits at <see cref="HttpPipelinePosition.PerRetry"/>, ahead of Azure.Core's logging policy,
/// so a short-circuited attempt writes no <c>Error response</c> (and no headers) to the log, and
/// the SDK's own retries of a throttled query stop as soon as the first attempt fails. On
/// 2026-11-18 a throttling burst produced 12,024 logged <c>503</c>s in four minutes (filling the
/// day's 10 MB log file), and every success from a request still in flight marked Search
/// healthy again, which let the next wave of requests through. The 503 it throws is what
/// <c>SongIndex.DoSearch</c> already turns into "Azure Search service is unavailable".
/// </para>
/// <para>
/// Document writes (<c>docs/search.index</c>) pass straight through and keep the SDK's retries:
/// failing a song save fast loses the edit, and a bulk load on a test index shouldn't open the
/// breaker for production queries on the same service.
/// </para>
/// <para>
/// Every attempt is stamped with the breaker's generation, which moves on each open, reopen and
/// close. Only an attempt from the current generation changes the state, so a response from a
/// request sent before the last change (still in flight when the breaker opened, or a superseded
/// probe) is ignored.
/// </para>
/// <para>
/// While it's open the breaker decides <see cref="ServiceHealthManager.IsServiceHealthy"/> for
/// its service, so the health check's own one-minute cooldown doesn't stretch the 30 s break.
/// </para>
/// </remarks>
public sealed class SearchCircuitBreaker : HttpPipelinePolicy
{
    internal const string ServiceName = "SearchService";
    internal static readonly TimeSpan DefaultBreakDuration = TimeSpan.FromSeconds(30);
    internal static readonly TimeSpan DefaultProbeTimeout = TimeSpan.FromSeconds(10);

    private readonly record struct Attempt(long Generation, bool Probe);

    private readonly object _lock = new();
    private readonly ServiceHealthManager _serviceHealth;
    private readonly ILogger<SearchCircuitBreaker> _logger;
    private readonly TimeProvider _time;

    private DateTimeOffset? _openUntil;
    private DateTimeOffset? _probeStarted;
    private long _generation;
    private long _shortCircuited;

    public SearchCircuitBreaker(ServiceHealthManager serviceHealth, ILogger<SearchCircuitBreaker> logger,
        string serviceName = ServiceName, TimeProvider time = null)
    {
        _serviceHealth = serviceHealth;
        _logger = logger;
        _time = time ?? TimeProvider.System;
        HealthServiceName = serviceName;
        _serviceHealth?.SetHealthGate(serviceName, HealthGate);
    }

    /// <summary>The <see cref="ServiceHealthManager"/> name this breaker marks and gates.</summary>
    public string HealthServiceName { get; }

    internal TimeSpan BreakDuration { get; set; } = DefaultBreakDuration;
    internal TimeSpan ProbeTimeout { get; set; } = DefaultProbeTimeout;

    /// <summary>True from the first throttled response until a probe succeeds.</summary>
    public bool IsOpen
    {
        get
        {
            lock (_lock)
            {
                return _openUntil.HasValue;
            }
        }
    }

    /// <summary>Attempts failed without being sent since the breaker last opened.</summary>
    internal long ShortCircuited => Interlocked.Read(ref _shortCircuited);

    public override void Process(HttpMessage message, ReadOnlyMemory<HttpPipelinePolicy> pipeline)
    {
        if (IsDocumentWrite(message))
        {
            ProcessNext(message, pipeline);
            return;
        }

        var attempt = Enter();
        try
        {
            ProcessNext(message, pipeline);
        }
        catch (Exception ex) when (attempt.Probe)
        {
            ProbeFailed(attempt, message, ex);
            throw;
        }
        Record(attempt, message.Response.Status);
    }

    public override async ValueTask ProcessAsync(HttpMessage message, ReadOnlyMemory<HttpPipelinePolicy> pipeline)
    {
        if (IsDocumentWrite(message))
        {
            await ProcessNextAsync(message, pipeline).ConfigureAwait(false);
            return;
        }

        var attempt = Enter();
        try
        {
            await ProcessNextAsync(message, pipeline).ConfigureAwait(false);
        }
        catch (Exception ex) when (attempt.Probe)
        {
            ProbeFailed(attempt, message, ex);
            throw;
        }
        Record(attempt, message.Response.Status);
    }

    private static bool IsThrottled(int status) => status == 503 || status == 429;

    // IndexDocuments: POST /indexes('name')/docs/search.index
    private static bool IsDocumentWrite(HttpMessage message) =>
        message.Request.Uri.Path.EndsWith("/docs/search.index", StringComparison.OrdinalIgnoreCase);

    // Whether an attempt would be sent now: null while closed (no opinion), else whether the
    // probe slot is free
    private bool? HealthGate()
    {
        lock (_lock)
        {
            return _openUntil.HasValue ? CanProbe(_time.GetUtcNow()) : null;
        }
    }

    private bool CanProbe(DateTimeOffset now) =>
        now >= _openUntil.Value && (!_probeStarted.HasValue || now >= _probeStarted.Value + ProbeTimeout);

    // Throws if the breaker is open; otherwise returns the attempt's generation and whether it's
    // the half-open probe
    private Attempt Enter()
    {
        lock (_lock)
        {
            if (!_openUntil.HasValue)
            {
                return new Attempt(_generation, Probe: false);
            }

            var now = _time.GetUtcNow();
            if (!CanProbe(now))
            {
                _ = Interlocked.Increment(ref _shortCircuited);
                throw new RequestFailedException(503,
                    "Azure Search circuit breaker is open: the service was throttling, so this request wasn't sent");
            }

            _probeStarted = now;
            return new Attempt(_generation, Probe: true);
        }
    }

    private void Record(Attempt attempt, int status)
    {
        if (attempt.Probe)
        {
            if (status >= 500 || status == 429)
            {
                Reopen(attempt, $"the probe got {status}");
            }
            else
            {
                Close(attempt);
            }
        }
        else if (IsThrottled(status))
        {
            Open(attempt, status);
        }
        // Any other response from a request sent while closed changes nothing: in particular a
        // success from one sent before the breaker opened doesn't close it, only a probe does
    }

    private void ProbeFailed(Attempt attempt, HttpMessage message, Exception ex)
    {
        if (ex is OperationCanceledException && message.CancellationToken.IsCancellationRequested)
        {
            // The caller gave up (the user left the page), which says nothing about Search:
            // free the probe slot for the next request
            lock (_lock)
            {
                if (attempt.Generation == _generation)
                {
                    _probeStarted = null;
                }
            }
            return;
        }
        Reopen(attempt, $"the probe request failed ({ex.GetType().Name})");
    }

    private void Open(Attempt attempt, int status)
    {
        lock (_lock)
        {
            if (_openUntil.HasValue || attempt.Generation != _generation)
            {
                return; // Sent before the breaker last opened or closed
            }
            _generation++;
            _openUntil = _time.GetUtcNow() + BreakDuration;
            _probeStarted = null;
            _ = Interlocked.Exchange(ref _shortCircuited, 0);
        }

        _logger.LogWarning(
            "Search circuit breaker opened for {Service}: Azure Search returned {Status}; failing requests fast for {Seconds}s",
            HealthServiceName, status, BreakDuration.TotalSeconds);
        _serviceHealth?.MarkUnavailable(HealthServiceName, $"Azure Search returned {status}; circuit breaker open");
    }

    private void Reopen(Attempt attempt, string reason)
    {
        lock (_lock)
        {
            if (attempt.Generation != _generation)
            {
                return; // A superseded probe
            }
            _generation++;
            _openUntil = _time.GetUtcNow() + BreakDuration;
            _probeStarted = null;
        }

        _logger.LogWarning(
            "Search circuit breaker for {Service} stays open: {Reason}; {ShortCircuited} requests failed fast so far, next probe in {Seconds}s",
            HealthServiceName, reason, ShortCircuited, BreakDuration.TotalSeconds);
        _serviceHealth?.MarkUnavailable(HealthServiceName,
            $"Azure Search still throttling ({reason}); circuit breaker open");
    }

    private void Close(Attempt attempt)
    {
        long shortCircuited;
        lock (_lock)
        {
            if (attempt.Generation != _generation)
            {
                return; // A superseded probe
            }
            _generation++;
            _openUntil = null;
            _probeStarted = null;
            shortCircuited = Interlocked.Exchange(ref _shortCircuited, 0);
        }

        _logger.LogWarning(
            "Search circuit breaker for {Service} closed: a probe succeeded after {ShortCircuited} requests failed fast",
            HealthServiceName, shortCircuited);
        _serviceHealth?.MarkHealthy(HealthServiceName);
    }
}
