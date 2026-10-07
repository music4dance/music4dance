#nullable enable

using System.Net;
using System.Text;

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

    /// <summary>
    /// Whether to send a status email each time the app starts. Set it per environment (an App
    /// Configuration label in Azure, user secrets locally) to choose which instances report in.
    /// </summary>
    public bool StartupStatus { get; set; } = false;
}

public enum HealthNotificationKind
{
    Failure,
    Recovery,
    Status
}

/// <summary>
/// Sends the admin service-health emails: a failure, a recovery, or a status report (sent when
/// an instance starts in Azure).
/// </summary>
public class ServiceHealthNotifier
{
    private readonly IConfiguration _configuration;
    private readonly Func<IEmailSender?> _emailSenderFactory;
    private readonly ILogger<ServiceHealthNotifier> _logger;

    /// <summary>
    /// The options and the email sender are resolved at send time, not here: when App
    /// Configuration fails to load at startup both are missing, and they appear once it recovers
    /// in place (see AppConfigurationRecoveryService).
    /// </summary>
    public ServiceHealthNotifier(
        IConfiguration configuration,
        Func<IEmailSender?> emailSenderFactory,
        ILogger<ServiceHealthNotifier> logger)
    {
        _configuration = configuration;
        _emailSenderFactory = emailSenderFactory;
        _logger = logger;
    }

    public ServiceHealthNotificationOptions GetOptions() =>
        _configuration.GetSection("ServiceHealth:AdminNotifications")
            .Get<ServiceHealthNotificationOptions>() ?? new ServiceHealthNotificationOptions();

    /// <summary>
    /// One line describing the notification settings as the notifier sees them right now, for
    /// the console log: when an email doesn't arrive, this says which setting is missing.
    /// </summary>
    public string DescribeConfiguration()
    {
        var options = GetOptions();
        var emailSender = _emailSenderFactory();
        var email = emailSender == null || emailSender is NullEmailSender ? "missing" : "configured";
        return $"Enabled={options.Enabled}, Recipients={string.Join(";", options.Recipients)}, " +
            $"StartupStatus={options.StartupStatus}, EmailService={email}";
    }

    /// <summary>
    /// Send notification email about service failure
    /// </summary>
    public Task<bool> SendFailureNotificationAsync(
        string serviceName,
        string errorMessage,
        ServiceHealthManager serviceHealth) =>
        SendAsync(HealthNotificationKind.Failure, serviceName, errorMessage, serviceHealth);

    /// <summary>
    /// Send one email of the given kind to every recipient. Returns whether it was sent: false
    /// when notifications are disabled or unconfigured (as they are while App Configuration is
    /// unavailable), so callers only treat an incident as notified when someone was told.
    /// </summary>
    public async Task<bool> SendAsync(
        HealthNotificationKind kind,
        string subject,
        string message,
        ServiceHealthManager serviceHealth)
    {
        var options = GetOptions();
        if (!options.Enabled || !options.Recipients.Any())
        {
            _logger.LogInformation(
                "Service health {Kind} email not sent ({Subject}): notifications are disabled or have no recipients",
                kind, subject);
            return false;
        }

        var emailSender = _emailSenderFactory();
        if (emailSender == null || emailSender is NullEmailSender)
        {
            _logger.LogWarning(
                "Service health {Kind} email not sent ({Subject}): email service is not available",
                kind, subject);
            return false;
        }

        try
        {
            var fullSubject = $"[music4dance.net] {SubjectPrefix(kind)}: {subject}{HostSuffix()}";
            var body = BuildEmailBody(kind, subject, message, serviceHealth);

            foreach (var recipient in options.Recipients)
            {
                await emailSender.SendEmailAsync(recipient, fullSubject, body);
                _logger.LogInformation(
                    "Service health {Kind} email sent to {Recipient}: {Subject}", kind, recipient, subject);
            }
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send service health {Kind} email: {Subject}", kind, subject);
            return false;
        }
    }

    private static string SubjectPrefix(HealthNotificationKind kind) => kind switch
    {
        HealthNotificationKind.Failure => "Service Failure",
        HealthNotificationKind.Recovery => "Service Recovered",
        _ => "Status"
    };

    // App Service sets WEBSITE_SITE_NAME / WEBSITE_INSTANCE_ID, which tell production, staging
    // and test apart, and one instance from the next across restarts
    private string HostSuffix()
    {
        var site = _configuration["WEBSITE_SITE_NAME"];
        return string.IsNullOrEmpty(site) ? "" : $" ({site})";
    }

    private string HostDescription()
    {
        var site = _configuration["WEBSITE_SITE_NAME"];
        var instance = _configuration["WEBSITE_INSTANCE_ID"];
        var environment = _configuration["ASPNETCORE_ENVIRONMENT"];
        var parts = new[]
        {
            string.IsNullOrEmpty(site) ? Environment.MachineName : site,
            string.IsNullOrEmpty(instance) ? null : $"instance {instance[..Math.Min(8, instance.Length)]}",
            string.IsNullOrEmpty(environment) ? null : environment
        };
        return string.Join(", ", parts.Where(p => p != null));
    }

    private string BuildEmailBody(
        HealthNotificationKind kind,
        string subject,
        string message,
        ServiceHealthManager serviceHealth)
    {
        var summary = serviceHealth.GetHealthSummary();
        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC");
        var allHealthy = summary.UnavailableCount == 0 && summary.DegradedCount == 0;

        var (headerColor, title, intro, subjectLabel) = kind switch
        {
            HealthNotificationKind.Failure => ("#dc3545", "🚨 Service Failure Alert",
                "A service has become unavailable on music4dance.net", "Failed Service"),
            HealthNotificationKind.Recovery => ("#28a745", "✅ Service Recovered",
                "A service that was reported unavailable has recovered", "Recovered Service"),
            _ => (allHealthy ? "#28a745" : "#fd7e14", allHealthy ? "✅ Started: Healthy" : "⚠ Started: Degraded",
                "A music4dance.net server instance has started", "Event")
        };

        var footer = kind switch
        {
            HealthNotificationKind.Failure =>
                "You will receive this notification once per failure incident. A recovery email follows when the service recovers.",
            HealthNotificationKind.Recovery =>
                "This closes the failure incident reported earlier for this service.",
            _ => "Sent each time a server instance starts in Azure."
        };

        var rows = new StringBuilder();
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
            var error = string.IsNullOrEmpty(status.ErrorMessage)
                ? ""
                : WebUtility.HtmlEncode(status.ErrorMessage);

            rows.AppendLine($@"
            <tr>
                <td>{status.ServiceName}</td>
                <td class='{statusClass}'>{statusIcon} {status.Status}</td>
                <td>{status.LastChecked:yyyy-MM-dd HH:mm:ss}</td>
                <td>{status.ConsecutiveFailures}</td>
                <td>{error}</td>
            </tr>");
        }

        return $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: 'Segoe UI', Arial, sans-serif; margin: 20px; }}
        .header {{ background: {headerColor}; color: white; padding: 15px; border-radius: 4px; }}
        .content {{ margin: 20px 0; }}
        .message-box {{ background: #f8f9fa; border: 1px solid #ddd; padding: 10px; border-radius: 4px; margin: 10px 0; }}
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
        <h2>{title}</h2>
        <p>{intro}</p>
    </div>

    <div class='content'>
        <p><strong>{subjectLabel}:</strong> {WebUtility.HtmlEncode(subject)}</p>
        <p><strong>Host:</strong> {WebUtility.HtmlEncode(HostDescription())}</p>
        <p><strong>Timestamp:</strong> {timestamp}</p>

        <div class='message-box'>
            <pre>{WebUtility.HtmlEncode(message)}</pre>
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
                <th>Error</th>
            </tr>
{rows}
        </table>

        <hr>
        <p style='color: #666; font-size: 0.9em;'>
            This is an automated notification from music4dance.net service health monitoring.<br>
            {footer}
        </p>
    </div>
</body>
</html>";
    }
}
