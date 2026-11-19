using Azure;
using Azure.Core;
using Azure.Core.Pipeline;

namespace m4d.Services.ServiceHealth;

/// <summary>
/// A circuit breaker in every Azure AI Search client's HTTP pipeline, shared by all of them since
/// they share one service's capacity. A <c>503</c> or <c>429</c> opens it; while it's open every
/// attempt fails at once with a <see cref="RequestFailedException"/> of status 503, without
/// sending anything. When <see cref="BreakDuration"/> has passed, one request goes through as a
/// probe while the rest keep failing fast: success closes the breaker, failure reopens it.
/// </summary>
/// <remarks>
/// It sits at <see cref="HttpPipelinePosition.PerRetry"/>, ahead of Azure.Core's logging policy,
/// so a short-circuited attempt writes no <c>Error response</c> (and no headers) to the log, and
/// the SDK's own retries of a throttled request stop as soon as the first attempt fails. On
/// 2026-11-18 a throttling burst produced 12,024 logged <c>503</c>s in four minutes (filling the
/// day's 10 MB log file), and every success from a request still in flight marked Search
/// healthy again, which let the next wave of requests through. The 503 it throws is what
/// <c>SongIndex.DoSearch</c> already turns into "Azure Search service is unavailable".
/// </remarks>
public sealed class SearchCircuitBreaker : HttpPipelinePolicy
{
    internal const string ServiceName = "SearchService";
    internal static readonly TimeSpan DefaultBreakDuration = TimeSpan.FromSeconds(30);

    private readonly object _lock = new();
    private readonly ServiceHealthManager _serviceHealth;
    private readonly ILogger<SearchCircuitBreaker> _logger;
    private readonly TimeProvider _time;

    private DateTimeOffset? _openUntil;
    private bool _probing;
    private long _shortCircuited;

    public SearchCircuitBreaker(ServiceHealthManager serviceHealth, ILogger<SearchCircuitBreaker> logger,
        TimeProvider time = null)
    {
        _serviceHealth = serviceHealth;
        _logger = logger;
        _time = time ?? TimeProvider.System;
    }

    internal TimeSpan BreakDuration { get; set; } = DefaultBreakDuration;

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
        var probe = Enter();
        try
        {
            ProcessNext(message, pipeline);
        }
        catch when (probe)
        {
            Reopen("the probe request failed");
            throw;
        }
        Record(message.Response.Status, probe);
    }

    public override async ValueTask ProcessAsync(HttpMessage message, ReadOnlyMemory<HttpPipelinePolicy> pipeline)
    {
        var probe = Enter();
        try
        {
            await ProcessNextAsync(message, pipeline).ConfigureAwait(false);
        }
        catch when (probe)
        {
            Reopen("the probe request failed");
            throw;
        }
        Record(message.Response.Status, probe);
    }

    private static bool IsThrottled(int status) => status == 503 || status == 429;

    // Returns whether this attempt is the half-open probe; throws if the breaker is open
    private bool Enter()
    {
        lock (_lock)
        {
            if (!_openUntil.HasValue)
            {
                return false;
            }

            if (_probing || _time.GetUtcNow() < _openUntil.Value)
            {
                _ = Interlocked.Increment(ref _shortCircuited);
                throw new RequestFailedException(503,
                    "Azure Search circuit breaker is open: the service was throttling, so this request wasn't sent");
            }

            _probing = true;
            return true;
        }
    }

    private void Record(int status, bool probe)
    {
        if (IsThrottled(status))
        {
            if (probe)
            {
                Reopen($"the probe got {status}");
            }
            else
            {
                Open(status);
            }
        }
        else if (probe)
        {
            Close();
        }
        // A non-throttled response from a request sent before the breaker opened doesn't close
        // it: only the probe decides
    }

    private void Open(int status)
    {
        lock (_lock)
        {
            if (_openUntil.HasValue)
            {
                return; // Requests already in flight when it opened, coming back throttled
            }
            _openUntil = _time.GetUtcNow() + BreakDuration;
            _ = Interlocked.Exchange(ref _shortCircuited, 0);
        }

        _logger.LogWarning(
            "Search circuit breaker opened: Azure Search returned {Status}; failing requests fast for {Seconds}s",
            status, BreakDuration.TotalSeconds);
        _serviceHealth?.MarkUnavailable(ServiceName, $"Azure Search returned {status}; circuit breaker open");
    }

    private void Reopen(string reason)
    {
        lock (_lock)
        {
            _openUntil = _time.GetUtcNow() + BreakDuration;
            _probing = false;
        }

        _logger.LogWarning(
            "Search circuit breaker stays open: {Reason}; {ShortCircuited} requests failed fast so far, next probe in {Seconds}s",
            reason, ShortCircuited, BreakDuration.TotalSeconds);
        _serviceHealth?.MarkUnavailable(ServiceName, $"Azure Search still throttling ({reason}); circuit breaker open");
    }

    private void Close()
    {
        long shortCircuited;
        lock (_lock)
        {
            _openUntil = null;
            _probing = false;
            shortCircuited = Interlocked.Exchange(ref _shortCircuited, 0);
        }

        _logger.LogWarning("Search circuit breaker closed: a probe succeeded after {ShortCircuited} requests failed fast",
            shortCircuited);
        _serviceHealth?.MarkHealthy(ServiceName);
    }
}
