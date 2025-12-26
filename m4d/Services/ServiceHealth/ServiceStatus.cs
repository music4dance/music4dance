#nullable enable

namespace m4d.Services.ServiceHealth;

/// <summary>
/// Status of a service
/// </summary>
public enum ServiceStatus
{
    /// <summary>
    /// Service status has not been checked yet
    /// </summary>
    Unknown,

    /// <summary>
    /// Service is working correctly
    /// </summary>
    Healthy,

    /// <summary>
    /// Service is slow or partially working
    /// </summary>
    Degraded,

    /// <summary>
    /// Service is down or unavailable
    /// </summary>
    Unavailable
}

/// <summary>
/// Health status information for a service
/// </summary>
public class ServiceHealthStatus
{
    /// <summary>
    /// Name of the service (e.g., "Database", "AzureSearch", "GoogleOAuth")
    /// </summary>
    public string ServiceName { get; set; } = string.Empty;

    /// <summary>
    /// Current status of the service
    /// </summary>
    public ServiceStatus Status { get; set; } = ServiceStatus.Unknown;

    /// <summary>
    /// When this status was last checked
    /// </summary>
    public DateTime LastChecked { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the service was last known to be healthy (null if never healthy)
    /// </summary>
    public DateTime? LastHealthy { get; set; }

    /// <summary>
    /// Error message if service is unavailable or degraded
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Response time of the last health check
    /// </summary>
    public TimeSpan? ResponseTime { get; set; }

    /// <summary>
    /// Number of consecutive failures (reset on success)
    /// </summary>
    public int ConsecutiveFailures { get; set; }

    /// <summary>
    /// Whether an admin notification has been sent for the current failure
    /// </summary>
    public bool NotificationSent { get; set; }
}
