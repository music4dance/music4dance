namespace m4d.Security;

/// <summary>
/// Tracks rate limiting middleware activity for real-time monitoring
/// Uses a circular buffer to store the last 10,000 events
/// </summary>
public class RateLimitingTracker
{
    private readonly CircularBuffer<RateLimitEvent> _events = new CircularBuffer<RateLimitEvent>(10000);
    private readonly object _lock = new object();

    public void RecordEvent(string ipAddress, string path, bool wasLimited, int requestCount, bool isGlobal)
    {
        var evt = new RateLimitEvent
        {
            Timestamp = DateTime.UtcNow,
            IpAddress = ipAddress ?? "unknown",
            Path = path ?? "/",
            WasLimited = wasLimited,
            RequestCount = requestCount,
            IsGlobalLimit = isGlobal
        };

        lock (_lock)
        {
            _events.Add(evt);
        }
    }

    public RateLimitingStats GetStats()
    {
        lock (_lock)
        {
            var now = DateTime.UtcNow;
            var allEvents = _events.ToList();

            if (!allEvents.Any())
            {
                return new RateLimitingStats
                {
                    TotalEventsTracked = 0,
                    HourlyStats = new List<HourlyStats>(),
                    TopRequestingIPs = new List<IPRequestStats>(),
                    MostTargetedPaths = new List<PathStats>(),
                    RecentLimitedRequests = new List<RateLimitEvent>()
                };
            }

            // Find oldest event to determine how many hours of data we have
            var oldestEvent = allEvents.Min(e => e.Timestamp);
            var hoursOfData = (int)Math.Ceiling((now - oldestEvent).TotalHours);
            var hoursToReport = Math.Min(hoursOfData, 48);

            // Create hourly stats (most recent first)
            var hourlyStats = new List<HourlyStats>();
            for (int i = 0; i < hoursToReport; i++)
            {
                var hourEnd = now.AddHours(-i);
                var hourStart = hourEnd.AddHours(-1);
                var hourEvents = allEvents.Where(e => e.Timestamp >= hourStart && e.Timestamp < hourEnd).ToList();

                // Only add hours that have events
                if (hourEvents.Any())
                {
                    hourlyStats.Add(new HourlyStats
                    {
                        HourStart = hourStart,
                        HourEnd = hourEnd,
                        TotalRequests = hourEvents.Count,
                        LimitedRequests = hourEvents.Count(e => e.WasLimited),
                        UniqueIPs = hourEvents.Select(e => e.IpAddress).Distinct().Count(),
                        GlobalLimitHits = hourEvents.Count(e => e.IsGlobalLimit),
                        PerIPLimitHits = hourEvents.Count(e => e.WasLimited && !e.IsGlobalLimit)
                    });
                }
            }

            var lastHour = now.AddHours(-1);
            var recentEvents = allEvents.Where(e => e.Timestamp >= lastHour).ToList();

            return new RateLimitingStats
            {
                TotalEventsTracked = allEvents.Count,
                LastHourRequests = recentEvents.Count,
                LastHourLimited = recentEvents.Count(e => e.WasLimited),
                UniqueIPsLastHour = recentEvents.Select(e => e.IpAddress).Distinct().Count(),
                UniqueIPsAllTime = allEvents.Select(e => e.IpAddress).Distinct().Count(),
                GlobalLimitHitsLastHour = recentEvents.Count(e => e.IsGlobalLimit),
                PerIPLimitHitsLastHour = recentEvents.Count(e => e.WasLimited && !e.IsGlobalLimit),
                HourlyStats = hourlyStats,
                TopRequestingIPs = allEvents
                    .GroupBy(e => e.IpAddress)
                    .OrderByDescending(g => g.Count())
                    .Take(10)
                    .Select(g => new IPRequestStats
                    {
                        IpAddress = g.Key,
                        TotalRequests = g.Count(),
                        LimitedRequests = g.Count(e => e.WasLimited),
                        LastRequestTime = g.Max(e => e.Timestamp)
                    })
                    .ToList(),
                MostTargetedPaths = recentEvents
                    .GroupBy(e => e.Path)
                    .OrderByDescending(g => g.Count())
                    .Take(10)
                    .Select(g => new PathStats
                    {
                        Path = g.Key,
                        RequestCount = g.Count(),
                        UniqueIPs = g.Select(e => e.IpAddress).Distinct().Count()
                    })
                    .ToList(),
                RecentLimitedRequests = recentEvents
                    .Where(e => e.WasLimited)
                    .OrderByDescending(e => e.Timestamp)
                    .Take(50)
                    .ToList()
            };
        }
    }
}

/// <summary>
/// Simple circular buffer implementation for efficient memory usage
/// </summary>
public class CircularBuffer<T>
{
    private readonly T[] _buffer;
    private int _head = 0;
    private int _count = 0;

    public CircularBuffer(int capacity)
    {
        _buffer = new T[capacity];
    }

    public void Add(T item)
    {
        _buffer[_head] = item;
        _head = (_head + 1) % _buffer.Length;
        if (_count < _buffer.Length) _count++;
    }

    public List<T> ToList()
    {
        var result = new List<T>(_count);
        for (int i = 0; i < _count; i++)
        {
            var index = (_head - _count + i + _buffer.Length) % _buffer.Length;
            result.Add(_buffer[index]);
        }
        return result;
    }
}

public class RateLimitEvent
{
    public DateTime Timestamp { get; set; }
    public string IpAddress { get; set; }
    public string Path { get; set; }
    public bool WasLimited { get; set; }
    public int RequestCount { get; set; }
    public bool IsGlobalLimit { get; set; }
}

public class RateLimitingStats
{
    public int TotalEventsTracked { get; set; }
    public int LastHourRequests { get; set; }
    public int LastHourLimited { get; set; }
    public int UniqueIPsLastHour { get; set; }
    public int UniqueIPsAllTime { get; set; }
    public int GlobalLimitHitsLastHour { get; set; }
    public int PerIPLimitHitsLastHour { get; set; }
    public List<HourlyStats> HourlyStats { get; set; }
    public List<IPRequestStats> TopRequestingIPs { get; set; }
    public List<PathStats> MostTargetedPaths { get; set; }
    public List<RateLimitEvent> RecentLimitedRequests { get; set; }
}

public class HourlyStats
{
    public DateTime HourStart { get; set; }
    public DateTime HourEnd { get; set; }
    public int TotalRequests { get; set; }
    public int LimitedRequests { get; set; }
    public int UniqueIPs { get; set; }
    public int GlobalLimitHits { get; set; }
    public int PerIPLimitHits { get; set; }
}

public class TimeSliceStats
{
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public int TotalRequests { get; set; }
    public int LimitedRequests { get; set; }
    public int UniqueIPs { get; set; }
    public int GlobalLimitHits { get; set; }
}

public class IPRequestStats
{
    public string IpAddress { get; set; }
    public int TotalRequests { get; set; }
    public int LimitedRequests { get; set; }
    public DateTime LastRequestTime { get; set; }
}

public class PathStats
{
    public string Path { get; set; }
    public int RequestCount { get; set; }
    public int UniqueIPs { get; set; }
}
