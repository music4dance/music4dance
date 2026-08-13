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

    public Http4xxStats GetStats(int topN = 100)
    {
        lock (_lock)
        {
            var allEvents = _events.ToList();

            if (!allEvents.Any())
            {
                return new Http4xxStats
                {
                    TotalEventsTracked = 0,
                    TopUrls = new List<Http4xxUrlStats>()
                };
            }

            var lastHour = DateTime.UtcNow.AddHours(-1);

            return new Http4xxStats
            {
                TotalEventsTracked = allEvents.Count,
                LastHourCount = allEvents.Count(e => e.Timestamp >= lastHour),
                OldestEventTime = allEvents.Min(e => e.Timestamp),
                TopUrls = allEvents
                    .GroupBy(e => new { e.Url, e.StatusCode })
                    .OrderByDescending(g => g.Count())
                    .Take(topN)
                    .Select(g => new Http4xxUrlStats
                    {
                        Url = g.Key.Url,
                        StatusCode = g.Key.StatusCode,
                        Count = g.Count(),
                        LastSeen = g.Max(e => e.Timestamp)
                    })
                    .ToList()
            };
        }
    }
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
}
