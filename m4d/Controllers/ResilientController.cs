#nullable enable

using Microsoft.AspNetCore.Mvc;
using m4d.Services.ServiceHealth;

namespace m4d.Controllers;

/// <summary>
/// Base controller with service health checking capabilities
/// </summary>
public abstract class ResilientController : Controller
{
    protected readonly ServiceHealthManager ServiceHealth;
    protected readonly ILogger Logger;

    protected ResilientController(ServiceHealthManager serviceHealth, ILogger logger)
    {
        ServiceHealth = serviceHealth;
        Logger = logger;
    }

    /// <summary>
    /// Check if a service is healthy and set ViewData accordingly
    /// </summary>
    /// <param name="serviceName">Name of the service to check</param>
    /// <returns>True if service is healthy, false otherwise</returns>
    protected bool IsServiceHealthy(string serviceName)
    {
        var isHealthy = ServiceHealth.IsServiceHealthy(serviceName);

        if (!isHealthy)
        {
            var status = ServiceHealth.GetServiceStatus(serviceName);
            Logger.LogWarning("Service {ServiceName} is {Status}: {ErrorMessage}",
                serviceName, status?.Status, status?.ErrorMessage);
        }

        return isHealthy;
    }

    /// <summary>
    /// Check if database is available. Sets ViewData flags for degraded UI.
    /// </summary>
    /// <returns>True if database is healthy</returns>
    protected bool IsDatabaseAvailable()
    {
        var isHealthy = IsServiceHealthy("Database");
        ViewData["DatabaseAvailable"] = isHealthy;

        if (!isHealthy)
        {
            ViewData["ShowDatabaseUnavailableNotice"] = true;
        }

        return isHealthy;
    }

    /// <summary>
    /// Check if search service is available. Sets ViewData flags for degraded UI.
    /// </summary>
    /// <returns>True if search service is healthy</returns>
    protected bool IsSearchAvailable()
    {
        var isHealthy = IsServiceHealthy("SearchService");
        ViewData["SearchAvailable"] = isHealthy;

        if (!isHealthy)
        {
            ViewData["ShowSearchUnavailableNotice"] = true;
        }

        return isHealthy;
    }

    /// <summary>
    /// Check if an OAuth provider is available. Sets ViewData flags for degraded UI.
    /// </summary>
    /// <param name="provider">Provider name (Google, Facebook, Spotify)</param>
    /// <returns>True if provider is healthy</returns>
    protected bool IsAuthProviderAvailable(string provider)
    {
        var serviceName = $"{provider}OAuth";
        var isHealthy = IsServiceHealthy(serviceName);
        ViewData[$"{provider}Available"] = isHealthy;

        if (!isHealthy)
        {
            ViewData[$"Show{provider}UnavailableNotice"] = true;
        }

        return isHealthy;
    }

    /// <summary>
    /// Check if email service is available. Sets ViewData flags for degraded UI.
    /// </summary>
    /// <returns>True if email service is healthy</returns>
    protected bool IsEmailAvailable()
    {
        var isHealthy = IsServiceHealthy("EmailService");
        ViewData["EmailAvailable"] = isHealthy;

        if (!isHealthy)
        {
            ViewData["ShowEmailUnavailableNotice"] = true;
        }

        return isHealthy;
    }

    /// <summary>
    /// Set ViewData with all service statuses for comprehensive UI feedback
    /// </summary>
    protected void SetAllServiceStatuses()
    {
        IsDatabaseAvailable();
        IsSearchAvailable();
        IsAuthProviderAvailable("Google");
        IsAuthProviderAvailable("Facebook");
        IsAuthProviderAvailable("Spotify");
        IsEmailAvailable();

        var summary = ServiceHealth.GetHealthSummary();
        ViewData["AnyServiceUnavailable"] = summary.UnavailableCount > 0;
        ViewData["AnyServiceDegraded"] = summary.DegradedCount > 0;
        ViewData["AllServicesHealthy"] = summary.IsFullyHealthy;
    }

    /// <summary>
    /// Return a view with service unavailable message if a required service is down
    /// </summary>
    /// <param name="serviceName">Name of the required service</param>
    /// <param name="fallbackViewName">Optional view name for service unavailable page</param>
    /// <returns>ViewResult with unavailable notice</returns>
    protected IActionResult ServiceUnavailableView(string serviceName, string? fallbackViewName = null)
    {
        ViewData["ServiceName"] = serviceName;
        var status = ServiceHealth.GetServiceStatus(serviceName);
        ViewData["ServiceStatus"] = status;
        ViewData["ErrorMessage"] = status?.ErrorMessage ?? "Service is currently unavailable";

        return View(fallbackViewName ?? "ServiceUnavailable");
    }
}
