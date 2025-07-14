using Azure;
using Azure.Communication.Email;
using m4d.Utilities;
using Microsoft.AspNetCore.Identity.UI.Services;

namespace m4d.Services;

public class EmailSender(string connectionString) : IEmailSender
{
    protected static readonly ILogger Logger = ApplicationLogging.CreateLogger<EmailSender>();

    private readonly EmailClient _emailClient = new(connectionString);

    public async Task SendEmailAsync(string email, string subject, string message)
    {
        var emailMessage = new EmailMessage(
            senderAddress: "donotreply@music4dance.net",
            content: new EmailContent(subject)
            {
                Html = message
            },
            recipients: new EmailRecipients([new EmailAddress(email)])
        );

        try
        {
            var response = await _emailClient.SendAsync(WaitUntil.Completed, emailMessage);
            Logger.LogInformation("Email sent. Message ID: {0}", response.Id);
        }
        catch (RequestFailedException ex)
        {
            Logger.LogError(ex, "Failed to send email: {0}", ex.Message);
            throw;
        }
    }
}