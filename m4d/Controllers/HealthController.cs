#nullable enable

using Microsoft.AspNetCore.Mvc;
using m4d.Services.ServiceHealth;
using System.Text;

namespace m4d.Controllers;

/// <summary>
/// Health check endpoint for monitoring and diagnostics
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly ServiceHealthManager _serviceHealth;
    private readonly ILogger<HealthController> _logger;

    public HealthController(ServiceHealthManager serviceHealth, ILogger<HealthController> logger)
    {
        _serviceHealth = serviceHealth;
        _logger = logger;
    }

    /// <summary>
    /// Get health status of all services
    /// </summary>
    /// <returns>JSON object with service health information</returns>
    [HttpGet("status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public IActionResult GetStatus()
    {
        var allStatuses = _serviceHealth.GetAllStatuses();
        var summary = _serviceHealth.GetHealthSummary();

        var response = new
        {
            timestamp = DateTime.UtcNow,
            overallStatus = summary.IsFullyHealthy ? "healthy" : summary.HasCriticalFailures ? "unavailable" : "degraded",
            summary = new
            {
                healthy = summary.HealthyCount,
                degraded = summary.DegradedCount,
                unavailable = summary.UnavailableCount,
                unknown = summary.UnknownCount
            },
            services = allStatuses.OrderBy(s => s.ServiceName).Select(s => new
            {
                name = s.ServiceName,
                status = s.Status.ToString().ToLowerInvariant(),
                lastChecked = s.LastChecked,
                lastHealthy = s.LastHealthy,
                errorMessage = s.ErrorMessage,
                responseTime = s.ResponseTime?.TotalMilliseconds,
                consecutiveFailures = s.ConsecutiveFailures
            })
        };

        // Return 503 if any critical services are unavailable
        if (summary.HasCriticalFailures)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, response);
        }

        return Ok(response);
    }

    /// <summary>
    /// Simple health check endpoint for load balancers
    /// Returns 200 if application is running, 503 if critical services are down
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public IActionResult GetHealth()
    {
        var summary = _serviceHealth.GetHealthSummary();

        // Application is "up" if it's running, even with degraded services
        // Only return 503 if database is unavailable (critical service)
        var dbStatus = _serviceHealth.GetServiceStatus("Database");
        if (dbStatus?.Status == ServiceStatus.Unavailable)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { status = "unavailable", message = "Critical services unavailable" });
        }

        return Ok(new { status = summary.IsFullyHealthy ? "healthy" : "degraded" });
    }

    /// <summary>
    /// Human-readable health report (HTML)
    /// </summary>
    [HttpGet("report")]
    [Produces("text/html")]
    public IActionResult GetReport()
    {
        var allStatuses = _serviceHealth.GetAllStatuses();
        var summary = _serviceHealth.GetHealthSummary();

        var html = new StringBuilder();
        html.AppendLine("<!DOCTYPE html>");
        html.AppendLine("<html><head>");
        html.AppendLine("<meta charset=\"UTF-8\">");
        html.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        html.AppendLine("<title>music4dance.net Health Report</title>");
        html.AppendLine("<style>");
        html.AppendLine("body { font-family: 'Segoe UI', Arial, sans-serif; margin: 40px; background: #f5f5f5; }");
        html.AppendLine("h1 { color: #333; }");
        html.AppendLine(".healthy { color: #28a745; font-weight: bold; }");
        html.AppendLine(".degraded { color: #ffc107; font-weight: bold; }");
        html.AppendLine(".unavailable { color: #dc3545; font-weight: bold; }");
        html.AppendLine(".unknown { color: #6c757d; font-weight: bold; }");
        html.AppendLine("table { border-collapse: collapse; width: 100%; max-width: 900px; background: white; box-shadow: 0 2px 4px rgba(0,0,0,0.1); }");
        html.AppendLine("th, td { padding: 12px; text-align: left; border-bottom: 1px solid #ddd; }");
        html.AppendLine("th { background: #007bff; color: white; }");
        html.AppendLine("tr:hover { background: #f8f9fa; }");
        html.AppendLine(".summary { background: white; padding: 20px; margin-bottom: 20px; border-radius: 4px; box-shadow: 0 2px 4px rgba(0,0,0,0.1); max-width: 900px; }");
        html.AppendLine("</style></head><body>");

        html.AppendLine("<h1>music4dance.net Service Health Report</h1>");
        html.AppendLine($"<p>Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC</p>");

        html.AppendLine("<div class='summary'>");
        html.AppendLine($"<h2>Overall Status: <span class='{(summary.IsFullyHealthy ? "healthy" : summary.HasCriticalFailures ? "unavailable" : "degraded")}'>{(summary.IsFullyHealthy ? "HEALTHY" : summary.HasCriticalFailures ? "UNAVAILABLE" : "DEGRADED")}</span></h2>");
        html.AppendLine($"<p>✓ Healthy: {summary.HealthyCount} | ⚠ Degraded: {summary.DegradedCount} | ✗ Unavailable: {summary.UnavailableCount} | ? Unknown: {summary.UnknownCount}</p>");
        html.AppendLine("</div>");

        html.AppendLine("<table>");
        html.AppendLine("<tr><th>Service</th><th>Status</th><th>Last Checked</th><th>Last Healthy</th><th>Response Time</th><th>Failures</th><th>Error</th></tr>");

        foreach (var service in allStatuses.OrderBy(s => s.ServiceName))
        {
            var statusClass = service.Status.ToString().ToLowerInvariant();
            var statusIcon = service.Status switch
            {
                ServiceStatus.Healthy => "✓",
                ServiceStatus.Degraded => "⚠",
                ServiceStatus.Unavailable => "✗",
                _ => "?"
            };

            html.AppendLine("<tr>");
            html.AppendLine($"<td>{service.ServiceName}</td>");
            html.AppendLine($"<td class='{statusClass}'>{statusIcon} {service.Status}</td>");
            html.AppendLine($"<td>{service.LastChecked:yyyy-MM-dd HH:mm:ss}</td>");
            html.AppendLine($"<td>{(service.LastHealthy.HasValue ? service.LastHealthy.Value.ToString("yyyy-MM-dd HH:mm:ss") : "Never")}</td>");
            html.AppendLine($"<td>{(service.ResponseTime.HasValue ? $"{service.ResponseTime.Value.TotalMilliseconds:F0} ms" : "-")}</td>");
            html.AppendLine($"<td>{service.ConsecutiveFailures}</td>");
            html.AppendLine($"<td>{(string.IsNullOrEmpty(service.ErrorMessage) ? "-" : System.Net.WebUtility.HtmlEncode(service.ErrorMessage))}</td>");
            html.AppendLine("</tr>");
        }

        html.AppendLine("</table>");
        html.AppendLine("</body></html>");

        return Content(html.ToString(), "text/html");
    }
}
