using System.Collections.Concurrent;

namespace m4d.Services.ServiceHealth;

/// <summary>
/// Central manager for tracking service health across the application
/// </summary>
public class ServiceHealthManager
{
    private readonly ConcurrentDictionary<string, ServiceHealthStatus> _serviceStatuses = new();
    private readonly ILogger<ServiceHealthManager> _logger;

    public ServiceHealthManager(ILogger<ServiceHealthManager> logger)
    {
        _logger = logger;
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

        status.Status = ServiceStatus.Unavailable;
        status.LastChecked = DateTime.UtcNow;
        status.ErrorMessage = errorMessage;
        status.ConsecutiveFailures++;

        if (wasHealthy)
        {
            _logger.LogError("Service '{ServiceName}' is now unavailable: {ErrorMessage}",
                serviceName, errorMessage);
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
    /// Check if a service is healthy
    /// </summary>
    public bool IsServiceHealthy(string serviceName)
    {
        return _serviceStatuses.TryGetValue(serviceName, out var status)
            && status.Status == ServiceStatus.Healthy;
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
    public (int healthy, int degraded, int unavailable, int unknown) GetHealthSummary()
    {
        var statuses = _serviceStatuses.Values.ToList();
        return (
            healthy: statuses.Count(s => s.Status == ServiceStatus.Healthy),
            degraded: statuses.Count(s => s.Status == ServiceStatus.Degraded),
            unavailable: statuses.Count(s => s.Status == ServiceStatus.Unavailable),
            unknown: statuses.Count(s => s.Status == ServiceStatus.Unknown)
        );
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

        if (summary.unavailable > 0)
        {
            report.AppendLine($"Overall Status: DEGRADED ({summary.unavailable} service(s) unavailable)");
            report.AppendLine("Application started in degraded mode.");
        }
        else if (summary.degraded > 0)
        {
            report.AppendLine($"Overall Status: DEGRADED ({summary.degraded} service(s) degraded)");
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
