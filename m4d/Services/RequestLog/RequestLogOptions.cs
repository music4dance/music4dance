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

    /// <summary>
    /// Requests held between flushes; past this, new requests aren't logged. A typical minute is
    /// a few hundred; at about 1 KB a record this caps the queue near 20 MB.
    /// </summary>
    public int MaxQueuedRecords { get; set; } = 20_000;

    /// <summary>
    /// The container's URI, or null with <paramref name="error"/> set when ServiceUri isn't an
    /// absolute http(s) URI or Container is empty. A bad value turns the log off rather than
    /// failing startup.
    /// </summary>
    public Uri ContainerUri(out string error)
    {
        var container = (Container ?? "").Trim().Trim('/');
        if (container.Length == 0)
        {
            error = "RequestLog:Container is empty";
            return null;
        }
        if (!Uri.TryCreate((ServiceUri ?? "").Trim(), UriKind.Absolute, out var service)
            || (service.Scheme != Uri.UriSchemeHttps && service.Scheme != Uri.UriSchemeHttp))
        {
            error = $"RequestLog:ServiceUri '{ServiceUri}' isn't an absolute http(s) URI";
            return null;
        }
        if (!Uri.TryCreate($"{service.AbsoluteUri.TrimEnd('/')}/{container}", UriKind.Absolute, out var uri))
        {
            error = $"RequestLog:Container '{Container}' doesn't make a valid URI";
            return null;
        }

        error = null;
        return uri;
    }

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
