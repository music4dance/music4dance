namespace m4d.Middleware;

/// <summary>
/// Gives the built-in W3C request logger the three fields it can't produce itself, as request
/// headers it records through <c>AdditionalRequestHeaders</c>:
/// <list type="bullet">
/// <item><see cref="ClientHeader"/>: the visitor, from <see cref="ClientAddresses"/>
/// (CF-Connecting-IP when relayed by Cloudflare, otherwise the connecting address).</item>
/// <item><see cref="RelayHeader"/>: the address that handed the request to App Service (the
/// Cloudflare edge when proxied).</item>
/// <item><see cref="CookiesHeader"/>: cookie <em>names</em> only (<c>_ga=;music4dance=</c>).
/// The logger's own Cookie field would record values, session tickets included.</item>
/// </list>
/// Each header is always overwritten, so a client can't plant its own value in the log. Nothing
/// else reads them. Runs after <see cref="CloudflareClientIpMiddleware"/> and before
/// <c>UseW3CLogging</c>, which reads request headers before calling the rest of the pipeline.
/// See architecture/observability/logging-and-diagnostics.md (App request log).
/// </summary>
public class RequestLogFieldsMiddleware(RequestDelegate next)
{
    public const string ClientHeader = "X-M4d-Log-Client";
    public const string RelayHeader = "X-M4d-Log-Relay";
    public const string CookiesHeader = "X-M4d-Log-Cookies";

    public Task InvokeAsync(HttpContext context)
    {
        var addresses = context.Features.Get<ClientAddresses>();
        var headers = context.Request.Headers;
        headers[ClientHeader] = addresses?.Client?.ToString() ?? "";
        headers[RelayHeader] = addresses?.Relay?.ToString() ?? "";
        headers[CookiesHeader] = string.Concat(context.Request.Cookies.Keys.Select(k => k + "=;"));
        return next(context);
    }
}
