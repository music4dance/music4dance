using m4d.Utilities;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Options;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace m4d.Services;

public class AuthMessageSenderOptions
{
    public string User { get; set; }
    public string Key { get; set; }
}

public class EmailSender : IEmailSender
{
    protected static readonly ILogger Logger = ApplicationLogging.CreateLogger<EmailSender>();
    public EmailSender(IOptions<AuthMessageSenderOptions> optionsAccessor)
    {
        Options = optionsAccessor.Value;
    }

    public AuthMessageSenderOptions Options { get; } //set only via Secret Manager

    public async Task SendEmailAsync(string email, string subject, string message)
    {
        await Execute(Options.Key, subject, message, email);
    }

    public async Task Execute(string apiKey, string subject, string message, string email)
    {
        //var client = new SendGridClient(apiKey);
        //var msg = new SendGridMessage
        //{
        //    From = new EmailAddress("info@music4dance.com", Options.User),
        //    Subject = subject,
        //    PlainTextContent = message,
        //    HtmlContent = message
        //};
        //msg.AddTo(new EmailAddress(email));

        //// Disable click tracking.
        //// See https://sendgrid.com/docs/User_Guide/Settings/tracking.html
        //


        var client = new SendGridClient(apiKey);
        var from = new EmailAddress("support@music4dance.net", "music4dance support");
        var to = new EmailAddress(email);
        var plainTextContent = message;
        var htmlContent = message;
        var msg = MailHelper.CreateSingleEmail(from, to, subject, plainTextContent, htmlContent);
        msg.SetClickTracking(false, false);

        var response = await client.SendEmailAsync(msg);
        if (!response.IsSuccessStatusCode)
        {
            Logger.LogError($"Failed to Send Email: {response.StatusCode})");
            var body = await response.DeserializeResponseBodyAsync();
            foreach (var item in body)
            {
                Logger.LogInformation($"{item.Key}: {item.Value.ToString()}");
            }
        }
    }
}