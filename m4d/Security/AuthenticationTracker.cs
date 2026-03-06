using System.Collections.Concurrent;

namespace m4d.Security;

/// <summary>
/// Tracks authentication attempts in memory for security monitoring
/// Retains last 1000 attempts or 24 hours, whichever is less
/// </summary>
public class AuthenticationTracker
{
    private readonly ConcurrentDictionary<string, List<AuthAttempt>> _attemptsByUsername
        = new ConcurrentDictionary<string, List<AuthAttempt>>();

    private readonly ConcurrentDictionary<string, List<AuthAttempt>> _attemptsByIp
        = new ConcurrentDictionary<string, List<AuthAttempt>>();

    private readonly List<AuthAttempt> _recentAttempts = new List<AuthAttempt>();
    private readonly object _lock = new object();
    private readonly List<SuspiciousActivityEvent> _suspiciousActivity = new List<SuspiciousActivityEvent>();

    // Keep last 1000 attempts or 24 hours, whichever is less
    private const int MAX_TRACKED_ATTEMPTS = 1000;
    private static readonly TimeSpan RETENTION_PERIOD = TimeSpan.FromHours(24);

    public void RecordAttempt(string username, string ipAddress, bool success, string failureReason = null)
    {
        var attempt = new AuthAttempt
        {
            Username = username ?? "unknown",
            IpAddress = ipAddress ?? "unknown",
            Timestamp = DateTime.UtcNow,
            Success = success,
            FailureReason = failureReason
        };

        lock (_lock)
        {
            // Add to recent attempts
            _recentAttempts.Add(attempt);

            // Trim old entries
            if (_recentAttempts.Count > MAX_TRACKED_ATTEMPTS)
            {
                _recentAttempts.RemoveAt(0);
            }

            // Remove entries older than retention period
            var cutoff = DateTime.UtcNow - RETENTION_PERIOD;
            _recentAttempts.RemoveAll(a => a.Timestamp < cutoff);
        }

        // Track by username
        _attemptsByUsername.AddOrUpdate(
            username ?? "unknown",
            _ => new List<AuthAttempt> { attempt },
            (_, list) =>
            {
                lock (list)
                {
                    list.Add(attempt);
                    return list;
                }
            });

        // Track by IP
        _attemptsByIp.AddOrUpdate(
            ipAddress ?? "unknown",
            _ => new List<AuthAttempt> { attempt },
            (_, list) =>
            {
                lock (list)
                {
                    list.Add(attempt);
                    return list;
                }
            });
    }

    /// <summary>
    /// Record suspicious activity detected before authentication (nested returnUrls, non-local URLs, etc.)
    /// </summary>
    /// <param name="ipAddress">IP address of the suspicious request (currently not stored to minimize memory)</param>
    /// <param name="activity">Type of suspicious activity (e.g., "Suspicious returnUrl", "Non-local returnUrl")</param>
    public void RecordSuspiciousActivity(string ipAddress, string activity)
    {
        var evt = new SuspiciousActivityEvent
        {
            Timestamp = DateTime.UtcNow,
            Activity = activity ?? "Unknown"
        };

        lock (_lock)
        {
            _suspiciousActivity.Add(evt);

            // Trim old entries (same retention as auth attempts)
            if (_suspiciousActivity.Count > MAX_TRACKED_ATTEMPTS)
            {
                _suspiciousActivity.RemoveAt(0);
            }

            // Remove entries older than retention period
            var cutoff = DateTime.UtcNow - RETENTION_PERIOD;
            _suspiciousActivity.RemoveAll(a => a.Timestamp < cutoff);
        }

        // Note: ipAddress is accepted for API consistency and potential future use,
        // but not currently stored to keep memory footprint low
    }

    public AuthenticationStats GetStats()
    {
        lock (_lock)
        {
            var now = DateTime.UtcNow;
            var cutoff = now - TimeSpan.FromMinutes(60);
            var recentAttempts = _recentAttempts.Where(a => a.Timestamp >= cutoff).ToList();
            var recentSuspicious = _suspiciousActivity.Where(a => a.Timestamp >= cutoff).ToList();

            // Calculate uptime for rate calculation
            var uptimeHours = m4d.Utilities.GlobalState.UpTime.TotalHours;
            var suspiciousPerHour = uptimeHours > 0 ? _suspiciousActivity.Count / uptimeHours : 0;

            // Build activity type breakdown
            var activityByType = _suspiciousActivity
                .GroupBy(a => a.Activity)
                .ToDictionary(g => g.Key, g => g.Count());

            // Generate hourly statistics (up to 48 hours)
            var hourlyStats = new List<HourlyAuthStats>();
            if (_recentAttempts.Any())
            {
                var oldestEvent = _recentAttempts.Min(a => a.Timestamp);
                var hoursOfData = (int)Math.Ceiling((now - oldestEvent).TotalHours);
                var hoursToReport = Math.Min(hoursOfData, 48);

                for (int i = 0; i < hoursToReport; i++)
                {
                    var hourEnd = now.AddHours(-i);
                    var hourStart = hourEnd.AddHours(-1);
                    var hourAttempts = _recentAttempts.Where(a => a.Timestamp >= hourStart && a.Timestamp < hourEnd).ToList();
                    var hourSuspicious = _suspiciousActivity.Where(a => a.Timestamp >= hourStart && a.Timestamp < hourEnd).ToList();

                    // Only add hours that have events
                    if (hourAttempts.Any() || hourSuspicious.Any())
                    {
                        hourlyStats.Add(new HourlyAuthStats
                        {
                            HourStart = hourStart,
                            HourEnd = hourEnd,
                            TotalAttempts = hourAttempts.Count,
                            FailedAttempts = hourAttempts.Count(a => !a.Success),
                            SuspiciousActivity = hourSuspicious.Count,
                            UniqueIPs = hourAttempts.Select(a => a.IpAddress).Distinct().Count(),
                            UniqueUsernames = hourAttempts.Select(a => a.Username).Distinct().Count()
                        });
                    }
                }
            }

            return new AuthenticationStats
            {
                TotalAttempts = _recentAttempts.Count,
                LastHourAttempts = recentAttempts.Count,
                FailedAttempts = recentAttempts.Count(a => !a.Success),
                SuspiciousActivityCount = _suspiciousActivity.Count,
                SuspiciousActivityLastHour = recentSuspicious.Count,
                SuspiciousActivityPerHour = suspiciousPerHour,
                SuspiciousActivityByType = activityByType,
                UptimeHours = uptimeHours,
                UniqueIPs = recentAttempts.Select(a => a.IpAddress).Distinct().Count(),
                UniqueUsernames = recentAttempts.Select(a => a.Username).Distinct().Count(),
                UniqueIPsAllTime = _recentAttempts.Select(a => a.IpAddress).Distinct().Count(),
                UniqueUsernamesAllTime = _recentAttempts.Select(a => a.Username).Distinct().Count(),
                HourlyAuthStats = hourlyStats,
                TopTargetedUsernames = recentAttempts
                    .Where(a => !a.Success)
                    .GroupBy(a => a.Username)
                    .OrderByDescending(g => g.Count())
                    .Take(10)
                    .Select(g => new UsernameStats
                    {
                        Username = g.Key,
                        FailedAttempts = g.Count(),
                        DistinctIPs = g.Select(a => a.IpAddress).Distinct().Count()
                    })
                    .ToList(),
                TopAttackingIPs = recentAttempts
                    .Where(a => !a.Success)
                    .GroupBy(a => a.IpAddress)
                    .OrderByDescending(g => g.Count())
                    .Take(10)
                    .Select(g => new IPStats
                    {
                        IpAddress = g.Key,
                        FailedAttempts = g.Count(),
                        TargetedUsernames = g.Select(a => a.Username).Distinct().Count()
                    })
                    .ToList(),
                RecentAttempts = recentAttempts
                    .OrderByDescending(a => a.Timestamp)
                    .Take(50)
                    .ToList()
            };
        }
    }

    /// <summary>
    /// Get failed attempts for a specific IP in the given time window
    /// </summary>
    public int GetFailedAttemptsForIP(string ipAddress, TimeSpan window)
    {
        if (string.IsNullOrEmpty(ipAddress))
        {
            return 0;
        }

        if (_attemptsByIp.TryGetValue(ipAddress, out var attempts))
        {
            var cutoff = DateTime.UtcNow - window;
            lock (attempts)
            {
                return attempts.Count(a => !a.Success && a.Timestamp >= cutoff);
            }
        }

        return 0;
    }
}

public class AuthAttempt
{
    public string Username { get; set; }
    public string IpAddress { get; set; }
    public DateTime Timestamp { get; set; }
    public bool Success { get; set; }
    public string FailureReason { get; set; }
}

public class SuspiciousActivityEvent
{
    public DateTime Timestamp { get; set; }
    public string Activity { get; set; }
}

public class AuthenticationStats
{
    public int TotalAttempts { get; set; }
    public int LastHourAttempts { get; set; }
    public int FailedAttempts { get; set; }
    public int SuspiciousActivityCount { get; set; }
    public int SuspiciousActivityLastHour { get; set; }
    public double SuspiciousActivityPerHour { get; set; }
    public double UptimeHours { get; set; }
    public Dictionary<string, int> SuspiciousActivityByType { get; set; }
    public int UniqueIPs { get; set; }
    public int UniqueUsernames { get; set; }
    public int UniqueIPsAllTime { get; set; }
    public int UniqueUsernamesAllTime { get; set; }
    public List<HourlyAuthStats> HourlyAuthStats { get; set; }
    public List<UsernameStats> TopTargetedUsernames { get; set; }
    public List<IPStats> TopAttackingIPs { get; set; }
    public List<AuthAttempt> RecentAttempts { get; set; }
}

public class HourlyAuthStats
{
    public DateTime HourStart { get; set; }
    public DateTime HourEnd { get; set; }
    public int TotalAttempts { get; set; }
    public int FailedAttempts { get; set; }
    public int SuspiciousActivity { get; set; }
    public int UniqueIPs { get; set; }
    public int UniqueUsernames { get; set; }
}

public class UsernameStats
{
    public string Username { get; set; }
    public int FailedAttempts { get; set; }
    public int DistinctIPs { get; set; }
}

public class IPStats
{
    public string IpAddress { get; set; }
    public int FailedAttempts { get; set; }
    public int TargetedUsernames { get; set; }
}
