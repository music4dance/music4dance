namespace m4d.Security;

/// <summary>
/// Tracks 4xx HTTP responses (mostly 400/404) by URL for admin visibility into broken
/// links, scraper probing, and malicious request patterns, without needing per-request
/// URL logging in the default ASP.NET Core log output.
/// Uses a circular buffer to store the last 10,000 events.
/// </summary>
public class Http4xxTracker
{
    private readonly CircularBuffer<Http4xxEvent> _events = new(10000);
    private readonly object _lock = new();

    // Path prefixes for well-known scanner/exploit probes that show up constantly in the
    // 4xx log and aren't actionable bugs in our own code (WordPress/Joomla probing, secret
    // scanning, etc). Matched against the path only (query string stripped). ".php" is
    // checked separately since it can appear anywhere in the probed path, not just as a
    // prefix.
    private static readonly string[] KnownAttackPathPrefixes =
    [
        "/wp-",
        "/wp/",
        "/administrator",
        "/.env",
    ];

    public static bool IsKnownAttackUrl(string url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return false;
        }

        var path = url.Split('?', 2)[0];

        return path.EndsWith(".php", StringComparison.OrdinalIgnoreCase)
            || KnownAttackPathPrefixes.Any(
                prefix => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    public void RecordEvent(string url, int statusCode)
    {
        var evt = new Http4xxEvent
        {
            Timestamp = DateTime.UtcNow,
            Url = string.IsNullOrEmpty(url) ? "/" : url,
            StatusCode = statusCode
        };

        lock (_lock)
        {
            _events.Add(evt);
        }
    }

    /// <summary>
    /// Aggregates tracked events by URL+status. Pass <paramref name="topN"/> as null to
    /// return every distinct URL (used for the CSV export) instead of capping the list.
    /// </summary>
    public Http4xxStats GetStats(int? topN = 100, Http4xxUrlFilter filter = Http4xxUrlFilter.All)
    {
        lock (_lock)
        {
            var allEvents = _events.ToList();
            var filteredEvents = filter switch
            {
                Http4xxUrlFilter.KnownAttacksOnly => allEvents.Where(e => IsKnownAttackUrl(e.Url)).ToList(),
                Http4xxUrlFilter.ExcludeKnownAttacks => allEvents.Where(e => !IsKnownAttackUrl(e.Url)).ToList(),
                _ => allEvents
            };

            if (!filteredEvents.Any())
            {
                return new Http4xxStats
                {
                    TotalEventsTracked = 0,
                    TopUrls = new List<Http4xxUrlStats>()
                };
            }

            var lastHour = DateTime.UtcNow.AddHours(-1);

            var topUrls = filteredEvents
                .GroupBy(e => new { e.Url, e.StatusCode })
                .OrderByDescending(g => g.Count())
                .Select(g => new Http4xxUrlStats
                {
                    Url = g.Key.Url,
                    StatusCode = g.Key.StatusCode,
                    Count = g.Count(),
                    LastSeen = g.Max(e => e.Timestamp),
                    IsKnownAttack = IsKnownAttackUrl(g.Key.Url)
                });

            if (topN.HasValue)
            {
                topUrls = topUrls.Take(topN.Value);
            }

            return new Http4xxStats
            {
                TotalEventsTracked = filteredEvents.Count,
                LastHourCount = filteredEvents.Count(e => e.Timestamp >= lastHour),
                OldestEventTime = filteredEvents.Min(e => e.Timestamp),
                TopUrls = topUrls.ToList()
            };
        }
    }
}

public enum Http4xxUrlFilter
{
    All,
    KnownAttacksOnly,
    ExcludeKnownAttacks
}

public class Http4xxEvent
{
    public DateTime Timestamp { get; set; }
    public string Url { get; set; }
    public int StatusCode { get; set; }
}

public class Http4xxStats
{
    public int TotalEventsTracked { get; set; }
    public int LastHourCount { get; set; }
    public DateTime OldestEventTime { get; set; }
    public List<Http4xxUrlStats> TopUrls { get; set; }
}

public class Http4xxUrlStats
{
    public string Url { get; set; }
    public int StatusCode { get; set; }
    public int Count { get; set; }
    public DateTime LastSeen { get; set; }
    public bool IsKnownAttack { get; set; }
}
