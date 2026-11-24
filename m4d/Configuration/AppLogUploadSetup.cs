using Azure.Core;
using Azure.Storage.Blobs;

using m4d.Services;

namespace m4d.Configuration;

/// <summary>
/// Copies the App Service file logger's app log (<c>LogFiles/Application/diagnostics-YYYYMMDD.txt</c>,
/// Warning and above) to blob storage every hour with <see cref="LogFileUploader"/>, so it outlives
/// the file logger's retention on /home (<see cref="M4dApplicationExtensions.AzureFileLogRetainedFiles"/>
/// files). App Service's own "Application Logging (Blob)" isn't offered on Linux. Off unless
/// AppLog:UploadContainer is set. See architecture/observability/logging-and-diagnostics.md
/// (App log upload).
/// </summary>
public static class AppLogUploadSetup
{
    public const string FileNamePrefix = "diagnostics-";

    /// <param name="Directory">Where the file logger writes: AppLog:Directory, else
    /// <c>$HOME/LogFiles/Application</c> (the path the App Service file logger uses).</param>
    public sealed record Settings(string Directory, Uri UploadContainer);

    /// <summary>
    /// The upload settings, or null when it's off or misconfigured (then <paramref name="error"/>
    /// says why). A bad setting turns the upload off rather than stopping the site.
    /// </summary>
    public static Settings Read(IConfiguration configuration, out string error)
    {
        error = null;
        var section = configuration.GetSection("AppLog");
        var uploadValue = section["UploadContainer"];
        if (string.IsNullOrWhiteSpace(uploadValue))
        {
            return null;
        }
        if (!Uri.TryCreate(uploadValue, UriKind.Absolute, out var upload) || upload.Scheme != Uri.UriSchemeHttps)
        {
            error = "AppLog:UploadContainer must be an https container URL";
            return null;
        }

        var directory = section["Directory"];
        if (string.IsNullOrWhiteSpace(directory))
        {
            var home = configuration["HOME"];
            if (string.IsNullOrWhiteSpace(home))
            {
                error = "AppLog:Directory isn't set and there's no HOME to find LogFiles/Application under";
                return null;
            }
            directory = Path.Combine(home, "LogFiles", "Application");
        }
        if (!Path.IsPathRooted(directory))
        {
            error = "AppLog:Directory must be an absolute path";
            return null;
        }
        return new Settings(directory, upload);
    }

    /// <summary>
    /// Registers the hourly app log uploader when AppLog:UploadContainer is set, signing in with
    /// <paramref name="credential"/> (the app's managed identity in production). The file logger
    /// deletes its own old files, so the uploader never does.
    /// </summary>
    public static IServiceCollection AddM4dAppLogUpload(this IServiceCollection services,
        IConfiguration configuration, TokenCredential credential)
    {
        var settings = Read(configuration, out var error);
        if (settings == null)
        {
            if (error != null)
            {
                Console.WriteLine($"[AppLog] WARNING: upload disabled, {error}");
            }
            return services;
        }

        services.AddLogFileUploader(
            new LogUploadSource("AppLog", settings.Directory, FileNamePrefix, DeleteUploaded: false),
            settings.UploadContainer, credential);
        Console.WriteLine($"[AppLog] {Path.Combine(settings.Directory, FileNamePrefix)}* copied hourly to "
            + settings.UploadContainer);
        return services;
    }

    /// <summary>
    /// Registers a <see cref="LogFileUploader"/> for <paramref name="source"/>, copying to
    /// <paramref name="container"/>. Each log gets its own uploader and store, so this registers
    /// an instance rather than a type (AddHostedService would keep only the first of one type).
    /// </summary>
    internal static IServiceCollection AddLogFileUploader(this IServiceCollection services,
        LogUploadSource source, Uri container, TokenCredential credential)
    {
        var store = new BlobLogFileStore(new BlobContainerClient(container, credential));
        services.AddSingleton<IHostedService>(provider => new LogFileUploader(
            source, store, provider.GetRequiredService<ILogger<LogFileUploader>>()));
        return services;
    }
}
