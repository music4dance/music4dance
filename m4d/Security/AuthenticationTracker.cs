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

    public AuthenticationStats GetStats()
    {
        lock (_lock)
        {
            var cutoff = DateTime.UtcNow - TimeSpan.FromMinutes(60);
            var recentAttempts = _recentAttempts.Where(a => a.Timestamp >= cutoff).ToList();

            return new AuthenticationStats
            {
                TotalAttempts = _recentAttempts.Count,
                LastHourAttempts = recentAttempts.Count,
                FailedAttempts = recentAttempts.Count(a => !a.Success),
                UniqueIPs = recentAttempts.Select(a => a.IpAddress).Distinct().Count(),
                UniqueUsernames = recentAttempts.Select(a => a.Username).Distinct().Count(),
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

public class AuthenticationStats
{
    public int TotalAttempts { get; set; }
    public int LastHourAttempts { get; set; }
    public int FailedAttempts { get; set; }
    public int UniqueIPs { get; set; }
    public int UniqueUsernames { get; set; }
    public List<UsernameStats> TopTargetedUsernames { get; set; }
    public List<IPStats> TopAttackingIPs { get; set; }
    public List<AuthAttempt> RecentAttempts { get; set; }
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
