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
            var lastHour = now.AddHours(-1);
            var allEvents = _events.ToList();
            var recentEvents = allEvents.Where(e => e.Timestamp >= lastHour).ToList();

            // Create 5-minute time slices for last hour (12 buckets)
            var timeSlices = new List<TimeSliceStats>();
            for (int i = 0; i < 12; i++)
            {
                var sliceEnd = now.AddMinutes(-i * 5);
                var sliceStart = sliceEnd.AddMinutes(-5);
                var sliceEvents = recentEvents.Where(e => e.Timestamp >= sliceStart && e.Timestamp < sliceEnd).ToList();

                timeSlices.Add(new TimeSliceStats
                {
                    StartTime = sliceStart,
                    EndTime = sliceEnd,
                    TotalRequests = sliceEvents.Count,
                    LimitedRequests = sliceEvents.Count(e => e.WasLimited),
                    UniqueIPs = sliceEvents.Select(e => e.IpAddress).Distinct().Count(),
                    GlobalLimitHits = sliceEvents.Count(e => e.IsGlobalLimit)
                });
            }

            timeSlices.Reverse(); // Oldest to newest

            return new RateLimitingStats
            {
                TotalEventsTracked = allEvents.Count,
                LastHourRequests = recentEvents.Count,
                LastHourLimited = recentEvents.Count(e => e.WasLimited),
                UniqueIPsLastHour = recentEvents.Select(e => e.IpAddress).Distinct().Count(),
                GlobalLimitHitsLastHour = recentEvents.Count(e => e.IsGlobalLimit),
                PerIPLimitHitsLastHour = recentEvents.Count(e => e.WasLimited && !e.IsGlobalLimit),
                TimeSlices = timeSlices,
                TopRequestingIPs = recentEvents
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
    public int GlobalLimitHitsLastHour { get; set; }
    public int PerIPLimitHitsLastHour { get; set; }
    public List<TimeSliceStats> TimeSlices { get; set; }
    public List<IPRequestStats> TopRequestingIPs { get; set; }
    public List<PathStats> MostTargetedPaths { get; set; }
    public List<RateLimitEvent> RecentLimitedRequests { get; set; }
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
