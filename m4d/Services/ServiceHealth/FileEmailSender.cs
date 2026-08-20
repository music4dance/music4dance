using m4d.Utilities;

using Microsoft.AspNetCore.Identity.UI.Services;

namespace m4d.Services.ServiceHealth;

/// <summary>
/// IEmailSender for the no-external-service m4d.Sandbox host - writes each message to a .eml
/// file under local/mail/ instead of sending it, so self-registration and password-reset flows
/// are testable for accounts a contributor creates beyond the seeded ones. See
/// architecture/contributor-test-environments.md, L1e. local/ is gitignored per CLAUDE.md.
/// </summary>
public class FileEmailSender(string outputDirectory) : IEmailSender
{
    private static readonly ILogger Logger = ApplicationLogging.CreateLogger<FileEmailSender>();

    public async Task SendEmailAsync(string email, string subject, string message)
    {
        Directory.CreateDirectory(outputDirectory);

        var safeEmail = string.Join("_", email.Split(Path.GetInvalidFileNameChars()));
        var fileName = $"{DateTime.UtcNow:yyyy-MM-dd_HH-mm-ss-fff}_{safeEmail}.eml";
        var path = Path.Combine(outputDirectory, fileName);

        var contents =
            $"""
            From: sandbox@music4dance.net
            To: {email}
            Subject: {subject}
            Content-Type: text/html; charset=utf-8

            {message}
            """;

        await File.WriteAllTextAsync(path, contents);

        Logger.LogInformation(
            "[Sandbox] Email to {Email} with subject '{Subject}' written to {Path}",
            email, subject, path);
    }
}
