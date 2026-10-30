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

    /// <summary>
    /// ';'-separated <c>fragment</c> or <c>fragment=limit</c> entries for crawlers that get
    /// their own treatment. A matching user agent is throttled even when the BotFilter lists
    /// don't count it as a crawler (so it still gets the real page, not the bot stub), at its
    /// own per-minute limit if one is given and MaxRequestsPerMinute otherwise. Exempt
    /// fragments still win. Reflectionbot crawls steadily at 5-6 pages a minute, under the
    /// default limit, so it needs a lower one to be slowed at all; Barkrowler and
    /// Meta's AI crawlers (meta-externalagent, meta-webindexer) don't say "bot" or "spider" in their
    /// user agents. Meta's link previews (facebookexternalhit) and user-triggered fetchers
    /// (meta-externalfetcher, ChatGPT-User, Claude-User) are left alone.
    /// </summary>
    public string AgentLimits { get; set; } = "reflectionbot=3;barkrowler;meta-externalagent;meta-webindexer";

    public IReadOnlyList<string> ExemptFragmentList => ListFromString(ExemptFragments);
    public IReadOnlyList<string> PathPrefixList => ListFromString(PathPrefixes);

    public IReadOnlyList<(string Fragment, int? Limit)> AgentLimitList =>
        [.. ListFromString(AgentLimits).Select(entry =>
        {
            var parts = entry.Split('=', 2, StringSplitOptions.TrimEntries);
            return (parts[0], parts.Length == 2 && int.TryParse(parts[1], out var limit)
                ? limit
                : (int?)null);
        })];

    /// <summary>
    /// The per-minute limit for a lower-cased user agent, or null when it isn't throttled.
    /// <paramref name="isSpider"/> is SpiderManager.IsSpider for the agent.
    /// </summary>
    public int? LimitFor(string agent, bool isSpider)
    {
        if (ExemptFragmentList.Any(agent.Contains))
        {
            return null;
        }

        var match = AgentLimitList.FirstOrDefault(a => agent.Contains(a.Fragment));
        if (match.Fragment != null)
        {
            return match.Limit ?? MaxRequestsPerMinute;
        }

        return isSpider ? MaxRequestsPerMinute : null;
    }

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
