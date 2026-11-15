namespace m4d.Services.RequestLog;

/// <summary>
/// Settings for the app-side request log, from the RequestLog configuration section. Read once
/// at startup, so changing them needs a restart. See
/// architecture/observability/logging-and-diagnostics.md (App request log).
/// </summary>
public class RequestLogOptions
{
    public const string SectionName = "RequestLog";

    /// <summary>Off by default: the app's identity needs write access to the container first.</summary>
    public bool Enabled { get; set; }

    public string ServiceUri { get; set; } = "https://m4dlogs.blob.core.windows.net";

    public string Container { get; set; } = "app-request-logs";

    /// <summary>
    /// First segment of each blob name. Empty means the App Service site name
    /// (WEBSITE_SITE_NAME), or "local" off App Service.
    /// </summary>
    public string Prefix { get; set; } = "";

    public int FlushIntervalSeconds { get; set; } = 60;

    /// <summary>Requests held between flushes; past this, new requests aren't logged.</summary>
    public int MaxQueuedRecords { get; set; } = 50_000;

    public string EffectivePrefix =>
        !string.IsNullOrWhiteSpace(Prefix)
            ? Prefix.Trim('/')
            : Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME") is { Length: > 0 } site
                ? site.ToLowerInvariant()
                : "local";

    public static RequestLogOptions FromConfiguration(IConfiguration configuration)
    {
        return configuration.GetSection(SectionName).Get<RequestLogOptions>()
            ?? new RequestLogOptions();
    }
}
