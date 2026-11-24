using System.Globalization;

using Azure.Core;
using Azure.Storage.Blobs;

using m4d.Middleware;
using m4d.Services;

using Microsoft.AspNetCore.HttpLogging;

namespace m4d.Configuration;

/// <summary>
/// The app request log: one line per request, written by ASP.NET Core's built-in W3C logger,
/// with the visitor's IP (App Service's own HTTP log only sees Cloudflare's edge), and copied to
/// blob storage every hour by <see cref="LogFileUploader"/>. Off unless RequestLog:Enabled.
/// See architecture/observability/logging-and-diagnostics.md (App request log).
/// </summary>
public static class RequestLogSetup
{
    public const string FileNamePrefix = "requests-";

    /// <summary>
    /// Files, not days: the logger starts a new file at every restart and at
    /// <see cref="DefaultFileSizeLimitMB"/>. With uploads working, the uploader deletes copied
    /// files after <see cref="LogFileUploader.KeepAfterUpload"/>, so this is only the cap
    /// while uploads fail: at most 30 x 100 MB on /home.
    /// </summary>
    public const int DefaultRetainedFileCountLimit = 30;
    public const int DefaultFileSizeLimitMB = 100;

    /// <param name="FileName">The logger's file name prefix, <see cref="FileNamePrefix"/> plus
    /// this instance's tag (<c>requests-1a2b3c4d-</c>), so instances sharing /home never write
    /// the same file.</param>
    /// <param name="UploadContainer">The blob container the files are copied to, or null to
    /// keep them only in <paramref name="Directory"/>.</param>
    public sealed record Settings(string Directory, string FileName, int RetainedFileCountLimit,
        int FileSizeLimitMB, Uri UploadContainer);

    /// <summary>
    /// The request log settings, or null when it's off or misconfigured (then
    /// <paramref name="error"/> says why). A bad setting turns the log off rather than stopping
    /// the site, so values are parsed here rather than bound (binding throws on a bad value).
    /// </summary>
    public static Settings Read(IConfiguration configuration, out string error)
    {
        error = null;
        var section = configuration.GetSection("RequestLog");
        var enabled = section["Enabled"];
        if (string.IsNullOrWhiteSpace(enabled))
        {
            return null;
        }
        if (!bool.TryParse(enabled, out var isEnabled))
        {
            error = "RequestLog:Enabled must be true or false";
            return null;
        }
        if (!isEnabled)
        {
            return null;
        }

        var directory = section["Directory"];
        var uploadValue = section["UploadContainer"];
        Uri upload = null;
        if (string.IsNullOrWhiteSpace(directory) || !Path.IsPathRooted(directory))
        {
            error = "RequestLog:Directory must be an absolute path";
        }
        else if (!TryReadInt(section, "RetainedFileCountLimit", DefaultRetainedFileCountLimit, out var retained)
            || retained is < 1 or > 1000)
        {
            error = "RequestLog:RetainedFileCountLimit must be a number from 1 to 1000";
        }
        else if (!TryReadInt(section, "FileSizeLimitMB", DefaultFileSizeLimitMB, out var sizeMB)
            || sizeMB is < 1 or > 2000)
        {
            error = "RequestLog:FileSizeLimitMB must be a number from 1 to 2000";
        }
        else if (!string.IsNullOrWhiteSpace(uploadValue)
            && (!Uri.TryCreate(uploadValue, UriKind.Absolute, out upload) || upload.Scheme != Uri.UriSchemeHttps))
        {
            error = "RequestLog:UploadContainer must be an https container URL";
        }
        else
        {
            return new Settings(directory, $"{FileNamePrefix}{InstanceTag(configuration)}-",
                retained, sizeMB, upload);
        }
        return null;
    }

    private static bool TryReadInt(IConfigurationSection section, string key, int fallback, out int value)
    {
        var raw = section[key];
        value = fallback;
        return string.IsNullOrWhiteSpace(raw)
            || int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    /// <summary>
    /// A short name for this instance: App Service's WEBSITE_INSTANCE_ID (the VM), else the
    /// machine name, as up to 8 lowercase letters and digits.
    /// </summary>
    public static string InstanceTag(IConfiguration configuration)
    {
        var id = configuration["WEBSITE_INSTANCE_ID"];
        if (string.IsNullOrWhiteSpace(id))
        {
            id = Environment.MachineName;
        }
        var tag = new string(id.ToLowerInvariant().Where(char.IsAsciiLetterOrDigit).Take(8).ToArray());
        return tag.Length > 0 ? tag : "local";
    }

    /// <summary>
    /// The W3C logger options for <paramref name="settings"/>. FlushInterval stays at its
    /// 1-second default on purpose: the logger's queue holds 1,024 lines and blocks the request
    /// thread when full, and it only drains between intervals, so a long interval would stall
    /// requests during any burst.
    /// </summary>
    public static void Configure(W3CLoggerOptions options, Settings settings)
    {
        options.LogDirectory = settings.Directory;
        options.FileName = settings.FileName;
        options.RetainedFileCountLimit = settings.RetainedFileCountLimit;
        options.FileSizeLimit = settings.FileSizeLimitMB * 1024 * 1024;
        // Not ClientIpAddress (RequestLogFieldsMiddleware's client field is normalized and falls
        // back to the relay) and never Cookie (it records values).
        options.LoggingFields = W3CLoggingFields.Date | W3CLoggingFields.Time
            | W3CLoggingFields.Method | W3CLoggingFields.UriStem | W3CLoggingFields.UriQuery
            | W3CLoggingFields.ProtocolStatus | W3CLoggingFields.TimeTaken
            | W3CLoggingFields.ProtocolVersion | W3CLoggingFields.Host
            | W3CLoggingFields.UserAgent | W3CLoggingFields.Referer;
        options.AdditionalRequestHeaders.Add(RequestLogFieldsMiddleware.ClientHeader);
        options.AdditionalRequestHeaders.Add(RequestLogFieldsMiddleware.RelayHeader);
        options.AdditionalRequestHeaders.Add(RequestLogFieldsMiddleware.CookiesHeader);
    }

    /// <summary>
    /// Registers the logger when RequestLog:Enabled and the settings are valid, and the hourly
    /// uploader when RequestLog:UploadContainer is set (signing in with
    /// <paramref name="credential"/>, the app's managed identity in production). The settings
    /// are read once, here; <see cref="UseM4dRequestLog"/> checks for the registered
    /// <see cref="Settings"/>.
    /// </summary>
    public static IServiceCollection AddM4dRequestLog(this IServiceCollection services,
        IConfiguration configuration, TokenCredential credential)
    {
        var settings = Read(configuration, out var error);
        if (settings == null)
        {
            if (error != null)
            {
                Console.WriteLine($"[RequestLog] WARNING: disabled, {error}");
            }
            else if (AppConfigurationStartup.HasProvider(configuration)
                && !AppConfigurationStartup.HasLoaded(configuration))
            {
                // AppConfigurationRecoveryService says so again if it recovers with the log on.
                Console.WriteLine("[RequestLog] WARNING: off, because App Configuration didn't load at startup. "
                    + "If RequestLog:Enabled is set there, restart once it's back to turn the log on");
            }
            return services;
        }

        services.AddSingleton(settings);
        services.AddW3CLogging(o => Configure(o, settings));
        if (settings.UploadContainer != null)
        {
            services.AddLogFileUploader(
                new LogUploadSource("RequestLog", settings.Directory, FileNamePrefix, DeleteUploaded: true),
                settings.UploadContainer, credential);
        }
        Console.WriteLine($"[RequestLog] Enabled: {Path.Combine(settings.Directory, settings.FileName)}*, "
            + $"keeping up to {settings.RetainedFileCountLimit} files of up to {settings.FileSizeLimitMB} MB, "
            + (settings.UploadContainer != null
                ? $"copied hourly to {settings.UploadContainer}"
                : "not copied to blob storage"));
        return services;
    }

    /// <summary>
    /// For AppConfigurationRecoveryService: what to tell the admins when App Configuration
    /// recovers after a failed startup load with the request log turned on, or null. The settings
    /// are read once, at startup, which saw none of them, so the log is off until a restart.
    /// </summary>
    public static string RestartNote(IConfiguration configuration) =>
        Read(configuration, out _) != null
            ? "The app request log (RequestLog:Enabled) only starts at startup: restart to turn it on."
            : null;

    /// <summary>
    /// Adds the request log to the pipeline when <see cref="AddM4dRequestLog"/> registered it.
    /// Call right after CloudflareClientIpMiddleware, so it sees every request (static files
    /// included) and the status the exception handler settles on.
    /// </summary>
    public static void UseM4dRequestLog(this IApplicationBuilder app)
    {
        if (app.ApplicationServices.GetService<Settings>() == null)
        {
            return;
        }
        app.UseMiddleware<RequestLogFieldsMiddleware>();
        app.UseW3CLogging();
    }
}
