namespace m4d.Services.ServiceHealth;

/// <summary>
/// Interface for performing health checks on a service
/// </summary>
public interface IServiceHealthCheck
{
    /// <summary>
    /// Name of the service being checked
    /// </summary>
    string ServiceName { get; }

    /// <summary>
    /// Perform a health check on the service
    /// </summary>
    /// <returns>The health status of the service</returns>
    Task<ServiceHealthStatus> CheckHealthAsync();
}
