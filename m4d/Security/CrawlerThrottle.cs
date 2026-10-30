namespace m4d.Security;

/// <summary>
/// Settings for CrawlerThrottleMiddleware, from the CrawlerThrottle configuration section.
/// Read per request, so a change in App Configuration applies without a restart.
/// </summary>
public class CrawlerThrottleOptions
{
    public const string SectionName = "CrawlerThrottle";

    public bool Enabled { get; set; } = true;

    /// <summary>Requests one crawler user agent may make per minute to the throttled paths.</summary>
    public int MaxRequestsPerMinute { get; set; } = 20;

    /// <summary>
    /// ';'-separated user-agent fragments that are never throttled: the search engines whose
    /// crawl rate we want to leave to them. A user agent can claim to be any of these.
    /// </summary>
    public string ExemptFragments { get; set; } =
        "googlebot;google-inspectiontool;adsbot-google;mediapartners-google;storebot-google;" +
        "bingbot;applebot;duckduckbot";

    /// <summary>';'-separated path prefixes the throttle applies to: the catalog pages.</summary>
    public string PathPrefixes { get; set; } = "/song;/customsearch;/dances";

    public IReadOnlyList<string> ExemptFragmentList => ListFromString(ExemptFragments);
    public IReadOnlyList<string> PathPrefixList => ListFromString(PathPrefixes);

    public static CrawlerThrottleOptions FromConfiguration(IConfiguration configuration)
    {
        return configuration.GetSection(SectionName).Get<CrawlerThrottleOptions>()
            ?? new CrawlerThrottleOptions();
    }

    private static IReadOnlyList<string> ListFromString(string value) =>
        value?.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(v => v.ToLowerInvariant()).ToList()
        ?? [];
}

public record CrawlerThrottleStats(string UserAgent, long ThrottledCount, DateTime LastThrottled);

/// <summary>
/// In-memory count of throttled requests per crawler user agent, for /Admin/Diagnostics.
/// Resets on restart, like the other trackers. Holds at most <see cref="MaxAgents"/> agents,
/// so a client rotating user agents can't grow it without bound.
/// </summary>
public class CrawlerThrottleTracker
{
    public const int MaxAgents = 500;

    private readonly Dictionary<string, (long Count, DateTime Last)> _throttled = [];
    private readonly object _lock = new();

    public void RecordThrottled(string userAgent)
    {
        lock (_lock)
        {
            if (_throttled.TryGetValue(userAgent, out var entry))
            {
                _throttled[userAgent] = (entry.Count + 1, DateTime.UtcNow);
            }
            else if (_throttled.Count < MaxAgents)
            {
                _throttled[userAgent] = (1, DateTime.UtcNow);
            }
        }
    }

    public IReadOnlyList<CrawlerThrottleStats> GetStats()
    {
        lock (_lock)
        {
            return [.. _throttled
                .Select(kv => new CrawlerThrottleStats(kv.Key, kv.Value.Count, kv.Value.Last))
                .OrderByDescending(s => s.ThrottledCount)];
        }
    }
}
