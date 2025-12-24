#nullable enable

using Microsoft.AspNetCore.Identity.UI.Services;

namespace m4d.Services.ServiceHealth;

/// <summary>
/// Configuration for service health notifications
/// </summary>
public class ServiceHealthNotificationOptions
{
    /// <summary>
    /// Whether notifications are enabled
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Admin email addresses to notify
    /// </summary>
    public List<string> Recipients { get; set; } = new();

    /// <summary>
    /// Whether to include stack trace in notifications
    /// </summary>
    public bool IncludeStackTrace { get; set; } = false;

    /// <summary>
    /// Email sender address (from field)
    /// </summary>
    public string SenderAddress { get; set; } = "donotreply@music4dance.net";
}

/// <summary>
/// Service for sending email notifications when services fail
/// </summary>
public class ServiceHealthNotifier
{
    private readonly ServiceHealthNotificationOptions _options;
    private readonly IEmailSender? _emailSender;
    private readonly ILogger<ServiceHealthNotifier> _logger;

    public ServiceHealthNotifier(
        IConfiguration configuration,
        IEmailSender? emailSender,
        ILogger<ServiceHealthNotifier> logger)
    {
        _logger = logger;
        _emailSender = emailSender;
        _options = configuration.GetSection("ServiceHealth:AdminNotifications")
            .Get<ServiceHealthNotificationOptions>() ?? new ServiceHealthNotificationOptions();

        if (_options.Enabled)
        {
            if (_emailSender != null)
            {
                _logger.LogInformation("Service health email notifications enabled for {Count} recipients",
                    _options.Recipients.Count);
            }
            else
            {
                _logger.LogWarning("Service health notifications enabled but email service is not available");
            }
        }
    }

    /// <summary>
    /// Send notification email about service failure
    /// </summary>
    public async Task SendFailureNotificationAsync(
        string serviceName,
        string errorMessage,
        ServiceHealthManager serviceHealth)
    {
        if (!_options.Enabled || _emailSender == null || !_options.Recipients.Any())
        {
            return;
        }

        try
        {
            var subject = $"[music4dance.net] Service Failure: {serviceName}";
            var body = BuildFailureEmailBody(serviceName, errorMessage, serviceHealth);

            foreach (var recipient in _options.Recipients)
            {
                await _emailSender.SendEmailAsync(recipient, subject, body);
                _logger.LogInformation(
                    "Service failure notification sent to {Recipient} for {ServiceName}",
                    recipient, serviceName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send service failure notification for {ServiceName}", serviceName);
        }
    }

    private string BuildFailureEmailBody(
        string serviceName,
        string errorMessage,
        ServiceHealthManager serviceHealth)
    {
        var summary = serviceHealth.GetHealthSummary();
        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC");

        var html = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: 'Segoe UI', Arial, sans-serif; margin: 20px; }}
        .header {{ background: #dc3545; color: white; padding: 15px; border-radius: 4px; }}
        .content {{ margin: 20px 0; }}
        .error-box {{ background: #f8d7da; border: 1px solid #f5c6cb; padding: 10px; border-radius: 4px; margin: 10px 0; }}
        .status-table {{ border-collapse: collapse; width: 100%; margin: 20px 0; }}
        .status-table th, .status-table td {{ padding: 8px; text-align: left; border: 1px solid #ddd; }}
        .status-table th {{ background: #f8f9fa; }}
        .healthy {{ color: #28a745; }}
        .degraded {{ color: #ffc107; }}
        .unavailable {{ color: #dc3545; }}
    </style>
</head>
<body>
    <div class='header'>
        <h2>🚨 Service Failure Alert</h2>
        <p>A critical service has become unavailable on music4dance.net</p>
    </div>

    <div class='content'>
        <p><strong>Failed Service:</strong> {serviceName}</p>
        <p><strong>Timestamp:</strong> {timestamp}</p>

        <div class='error-box'>
            <p><strong>Error Message:</strong></p>
            <pre>{System.Net.WebUtility.HtmlEncode(errorMessage)}</pre>
        </div>

        <h3>Current Service Status Summary</h3>
        <ul>
            <li>✓ Healthy: {summary.HealthyCount}</li>
            <li>⚠ Degraded: {summary.DegradedCount}</li>
            <li>✗ Unavailable: {summary.UnavailableCount}</li>
        </ul>

        <h3>All Services Status</h3>
        <table class='status-table'>
            <tr>
                <th>Service</th>
                <th>Status</th>
                <th>Last Checked</th>
                <th>Failures</th>
            </tr>";

        var tableRows = new System.Text.StringBuilder();
        foreach (var status in serviceHealth.GetAllStatuses().OrderBy(s => s.ServiceName))
        {
            var statusClass = status.Status.ToString().ToLowerInvariant();
            var statusIcon = status.Status switch
            {
                ServiceStatus.Healthy => "✓",
                ServiceStatus.Degraded => "⚠",
                ServiceStatus.Unavailable => "✗",
                _ => "?"
            };

            tableRows.AppendLine($@"
            <tr>
                <td>{status.ServiceName}</td>
                <td class='{statusClass}'>{statusIcon} {status.Status}</td>
                <td>{status.LastChecked:yyyy-MM-dd HH:mm:ss}</td>
                <td>{status.ConsecutiveFailures}</td>
            </tr>");
        }

        html += tableRows.ToString();
        html += @"
        </table>

        <h3>Impact Assessment</h3>
        <p>Users may experience degraded functionality. The application will serve cached content where possible and continue operating with reduced features.</p>

        <p><strong>Action Required:</strong> Please investigate the cause of the failure. The system will attempt automatic recovery, but manual intervention may be required.</p>

        <hr>
        <p style='color: #666; font-size: 0.9em;'>
            This is an automated notification from music4dance.net service health monitoring.<br>
            You will receive this notification only once per service failure incident.<br>
            No additional notifications will be sent until the service recovers and fails again.
        </p>
    </div>
</body>
</html>";

        return html;
    }
}
