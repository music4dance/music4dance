#nullable enable

using System.Collections.Concurrent;

namespace m4d.Services.ServiceHealth;

/// <summary>
/// Central manager for tracking service health across the application
/// </summary>
public class ServiceHealthManager
{
    private static readonly TimeSpan DefaultUnavailableCooldown = TimeSpan.FromMinutes(1);

    private readonly ConcurrentDictionary<string, ServiceHealthStatus> _serviceStatuses = new();
    private readonly ILogger<ServiceHealthManager> _logger;
    private ServiceHealthNotifier? _notifier;

    // How long a service stays reported Unavailable before IsServiceHealthy optimistically
    // lets the next caller retry it. Nothing in production ever calls MarkHealthy for most
    // services (e.g. SearchService), so without this a transient failure - a brief Azure
    // Search throttling spike, say - would otherwise wedge the whole app in degraded mode
    // until the process restarts, long after the underlying service recovered on its own.
    // Internal setter so tests can use a short cooldown instead of sleeping for real.
    internal TimeSpan UnavailableCooldown { get; set; } = DefaultUnavailableCooldown;

    public ServiceHealthManager(ILogger<ServiceHealthManager> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Set the notifier (called after service registration)
    /// </summary>
    internal void SetNotifier(ServiceHealthNotifier notifier)
    {
        _notifier = notifier;
    }

    /// <summary>
    /// Send consolidated startup failure notification if any services are unhealthy
    /// </summary>
    public async Task SendStartupFailureNotificationAsync()
    {
        if (_notifier == null)
        {
            return;
        }

        var failedServices = GetAllStatuses()
            .Where(s => s.Status == ServiceStatus.Unavailable || s.Status == ServiceStatus.Degraded)
            .ToList();

        if (failedServices.Any())
        {
            var errorSummary = string.Join("; ", failedServices.Select(s => $"{s.ServiceName}: {s.ErrorMessage}"));
            await _notifier.SendFailureNotificationAsync(
                $"Startup Failures ({failedServices.Count} services)",
                errorSummary,
                this);

            _logger.LogInformation("Startup failure notification sent for {Count} service(s)", failedServices.Count);
        }
    }

    /// <summary>
    /// Mark a service as healthy
    /// </summary>
    public void MarkHealthy(string serviceName, TimeSpan? responseTime = null)
    {
        var status = _serviceStatuses.GetOrAdd(serviceName, _ => new ServiceHealthStatus { ServiceName = serviceName });

        var wasUnhealthy = status.Status != ServiceStatus.Healthy;

        status.Status = ServiceStatus.Healthy;
        status.LastChecked = DateTime.UtcNow;
        status.LastHealthy = DateTime.UtcNow;
        status.ErrorMessage = null;
        status.ResponseTime = responseTime;
        status.ConsecutiveFailures = 0;

        if (wasUnhealthy && status.NotificationSent)
        {
            _logger.LogInformation("Service '{ServiceName}' has recovered", serviceName);
            status.NotificationSent = false; // Reset for next failure
        }
    }

    /// <summary>
    /// Mark a service as unavailable
    /// </summary>
    public void MarkUnavailable(string serviceName, string errorMessage)
    {
        var status = _serviceStatuses.GetOrAdd(serviceName, _ => new ServiceHealthStatus { ServiceName = serviceName });

        var wasHealthy = status.Status == ServiceStatus.Healthy || status.Status == ServiceStatus.Unknown;
        var isFirstFailure = wasHealthy && !status.NotificationSent;

        status.Status = ServiceStatus.Unavailable;
        status.LastChecked = DateTime.UtcNow;
        status.ErrorMessage = errorMessage;
        status.ConsecutiveFailures++;

        if (wasHealthy)
        {
            _logger.LogError("Service '{ServiceName}' is now unavailable: {ErrorMessage}",
                serviceName, errorMessage);

            // Send notification on first failure only
            if (isFirstFailure && _notifier != null)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _notifier.SendFailureNotificationAsync(serviceName, errorMessage, this);
                        status.NotificationSent = true;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to send failure notification for {ServiceName}", serviceName);
                    }
                });
            }
        }
        else
        {
            _logger.LogWarning("Service '{ServiceName}' remains unavailable (failure #{FailureCount}): {ErrorMessage}",
                serviceName, status.ConsecutiveFailures, errorMessage);
        }
    }

    /// <summary>
    /// Mark a service as degraded
    /// </summary>
    public void MarkDegraded(string serviceName, string reason, TimeSpan? responseTime = null)
    {
        var status = _serviceStatuses.GetOrAdd(serviceName, _ => new ServiceHealthStatus { ServiceName = serviceName });

        status.Status = ServiceStatus.Degraded;
        status.LastChecked = DateTime.UtcNow;
        status.ErrorMessage = reason;
        status.ResponseTime = responseTime;

        _logger.LogWarning("Service '{ServiceName}' is degraded: {Reason}", serviceName, reason);
    }

    /// <summary>
    /// Get the current status of a service
    /// </summary>
    public ServiceHealthStatus GetServiceStatus(string serviceName)
    {
        return _serviceStatuses.TryGetValue(serviceName, out var status)
            ? status
            : new ServiceHealthStatus { ServiceName = serviceName, Status = ServiceStatus.Unknown };
    }

    /// <summary>
    /// Get all service statuses
    /// </summary>
    public IEnumerable<ServiceHealthStatus> GetAllStatuses()
    {
        return _serviceStatuses.Values.ToList();
    }

    /// <summary>
    /// Check if a service is healthy (or unknown - optimistic assumption).
    /// Returns false if the service is marked Unavailable and the cooldown since that failure
    /// hasn't elapsed yet; once it has, optimistically returns true so the next caller retries
    /// the real operation instead of every request short-circuiting forever (see
    /// <see cref="UnavailableCooldown"/>). A renewed failure calls <see cref="MarkUnavailable"/>
    /// again, which resets <see cref="ServiceHealthStatus.LastChecked"/> and restarts the
    /// cooldown - so a sustained outage still gets retried at roughly that interval rather than
    /// on every single request.
    /// </summary>
    public bool IsServiceHealthy(string serviceName)
    {
        if (!_serviceStatuses.TryGetValue(serviceName, out var status))
        {
            // Unknown service - assume healthy until proven otherwise (optimistic)
            return true;
        }

        return status.Status != ServiceStatus.Unavailable
            || DateTime.UtcNow - status.LastChecked >= UnavailableCooldown;
    }

    /// <summary>
    /// Check if a service is available (healthy or degraded, but not unavailable)
    /// </summary>
    public bool IsServiceAvailable(string serviceName)
    {
        if (!_serviceStatuses.TryGetValue(serviceName, out var status))
        {
            return false;
        }

        return status.Status == ServiceStatus.Healthy || status.Status == ServiceStatus.Degraded;
    }

    /// <summary>
    /// Get a summary of overall system health
    /// </summary>
    public HealthSummary GetHealthSummary()
    {
        var statuses = _serviceStatuses.Values.ToList();
        var healthyCount = statuses.Count(s => s.Status == ServiceStatus.Healthy);
        var degradedCount = statuses.Count(s => s.Status == ServiceStatus.Degraded);
        var unavailableCount = statuses.Count(s => s.Status == ServiceStatus.Unavailable);
        var unknownCount = statuses.Count(s => s.Status == ServiceStatus.Unknown);

        return new HealthSummary
        {
            HealthyCount = healthyCount,
            DegradedCount = degradedCount,
            UnavailableCount = unavailableCount,
            UnknownCount = unknownCount,
            IsFullyHealthy = unavailableCount == 0 && degradedCount == 0 && unknownCount == 0,
            HasCriticalFailures = unavailableCount > 0
        };
    }

    /// <summary>
    /// Summary of system health status
    /// </summary>
    public class HealthSummary
    {
        public int HealthyCount { get; set; }
        public int DegradedCount { get; set; }
        public int UnavailableCount { get; set; }
        public int UnknownCount { get; set; }
        public bool IsFullyHealthy { get; set; }
        public bool HasCriticalFailures { get; set; }
    }

    /// <summary>
    /// Mark that a notification has been sent for a service failure
    /// </summary>
    public void MarkNotificationSent(string serviceName)
    {
        if (_serviceStatuses.TryGetValue(serviceName, out var status))
        {
            status.NotificationSent = true;
        }
    }

    /// <summary>
    /// Generate a startup report showing all service statuses
    /// </summary>
    public string GenerateStartupReport()
    {
        var statuses = _serviceStatuses.Values.OrderBy(s => s.ServiceName).ToList();
        var report = new System.Text.StringBuilder();

        report.AppendLine("=== music4dance.net Service Health Report ===");

        foreach (var status in statuses)
        {
            var icon = status.Status switch
            {
                ServiceStatus.Healthy => "✓",
                ServiceStatus.Degraded => "⚠",
                ServiceStatus.Unavailable => "✗",
                _ => "?"
            };

            report.AppendLine($"{icon} {status.ServiceName}: {status.Status}");

            if (!string.IsNullOrEmpty(status.ErrorMessage))
            {
                report.AppendLine($"  └─ {status.ErrorMessage}");
            }
        }

        var summary = GetHealthSummary();
        report.AppendLine();

        if (summary.UnavailableCount > 0)
        {
            report.AppendLine($"Overall Status: DEGRADED ({summary.UnavailableCount} service(s) unavailable)");
            report.AppendLine("Application started in degraded mode.");
        }
        else if (summary.DegradedCount > 0)
        {
            report.AppendLine($"Overall Status: DEGRADED ({summary.DegradedCount} service(s) degraded)");
            report.AppendLine("Application started with degraded services.");
        }
        else
        {
            report.AppendLine("Overall Status: HEALTHY");
            report.AppendLine("All services started successfully.");
        }

        return report.ToString();
    }
}
