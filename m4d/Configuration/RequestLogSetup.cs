using m4d.Middleware;

using Microsoft.AspNetCore.HttpLogging;

namespace m4d.Configuration;

/// <summary>
/// The app request log: one line per request, written by ASP.NET Core's built-in W3C logger,
/// with the visitor's IP (App Service's own HTTP log only sees Cloudflare's edge). Off unless
/// RequestLog:Enabled. See architecture/observability/logging-and-diagnostics.md (App request log).
/// </summary>
public static class RequestLogSetup
{
    public const string FileNamePrefix = "requests-";
    public const int DefaultRetainedFileCountLimit = 14;
    public const int DefaultFileSizeLimitMB = 200;

    public sealed record Settings(string Directory, int RetainedFileCountLimit, int FileSizeLimitMB);

    /// <summary>
    /// The request log settings, or null when it's off or misconfigured (then
    /// <paramref name="error"/> says why). A bad setting turns the log off rather than stopping
    /// the site.
    /// </summary>
    public static Settings Read(IConfiguration configuration, out string error)
    {
        error = null;
        var section = configuration.GetSection("RequestLog");
        if (!section.GetValue<bool>("Enabled"))
        {
            return null;
        }

        var directory = section["Directory"];
        var retained = section.GetValue("RetainedFileCountLimit", DefaultRetainedFileCountLimit);
        var sizeMB = section.GetValue("FileSizeLimitMB", DefaultFileSizeLimitMB);
        if (string.IsNullOrWhiteSpace(directory) || !Path.IsPathRooted(directory))
        {
            error = "RequestLog:Directory must be an absolute path";
        }
        // The logger would create a missing directory, so an absent storage mount would quietly
        // become a folder on the container's own disk. Require it to exist instead.
        else if (!System.IO.Directory.Exists(directory))
        {
            error = $"RequestLog:Directory {directory} doesn't exist (is the storage mount missing?)";
        }
        else if (retained is < 1 or > 1000)
        {
            error = "RequestLog:RetainedFileCountLimit must be 1 to 1000";
        }
        else if (sizeMB is < 1 or > 2000)
        {
            error = "RequestLog:FileSizeLimitMB must be 1 to 2000";
        }

        return error == null ? new Settings(directory, retained, sizeMB) : null;
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
        options.FileName = FileNamePrefix;
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

    /// <summary>Registers the logger when RequestLog:Enabled and the settings are valid.</summary>
    public static bool AddM4dRequestLog(this IServiceCollection services, IConfiguration configuration)
    {
        var settings = Read(configuration, out var error);
        if (settings == null)
        {
            if (error != null)
            {
                Console.WriteLine($"[RequestLog] WARNING: disabled, {error}");
            }
            return false;
        }

        services.AddSingleton(settings);
        services.AddW3CLogging(o => Configure(o, settings));
        Console.WriteLine($"[RequestLog] Enabled: {settings.Directory}, keeping "
            + $"{settings.RetainedFileCountLimit} files of up to {settings.FileSizeLimitMB} MB");
        return true;
    }

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
