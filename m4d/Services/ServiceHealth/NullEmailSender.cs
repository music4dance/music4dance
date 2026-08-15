using m4d.Utilities;

using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Logging;

namespace m4d.Services.ServiceHealth;

/// <summary>
/// Null implementation of IEmailSender for when Azure Communication Services is unavailable.
/// Unlike EmailSender, this never throws on construction or on send - callers that don't
/// check ServiceHealth before sending shouldn't crash the page just because email is down.
/// </summary>
public class NullEmailSender : IEmailSender
{
    private static readonly ILogger Logger = ApplicationLogging.CreateLogger<NullEmailSender>();

    public Task SendEmailAsync(string email, string subject, string message)
    {
        Logger.LogWarning(
            "Email service is unavailable - message to {Email} with subject '{Subject}' was not sent",
            email, subject);
        return Task.CompletedTask;
    }
}
