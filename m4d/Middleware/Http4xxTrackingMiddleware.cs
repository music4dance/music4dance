using Microsoft.AspNetCore.Diagnostics;

using m4d.Security;

namespace m4d.Middleware;

/// <summary>
/// Records 4xx HTTP responses (mostly 400 and 404) by URL so admins can distinguish
/// legitimate crawlers hitting stale links from malicious probing at a glance, without
/// cluttering the default request log with a URL on every line.
/// Runs after routing so it observes the final status code for every request, including
/// ones that never match a route. 429s are excluded — those are already covered in detail
/// by <see cref="RateLimitingTracker"/> and would just be noise here.
/// </summary>
public class Http4xxTrackingMiddleware(RequestDelegate next, Http4xxTracker tracker)
{
    private const int MaxUrlLength = 512;

    public async Task InvokeAsync(HttpContext context)
    {
        await next(context);

        // UseStatusCodePagesWithReExecute re-invokes the entire downstream pipeline
        // (including this middleware) a second time with the path rewritten to
        // /Error/{status} whenever the first pass produced an empty 4xx response. That
        // first pass already recorded the real request URL, so skip the re-executed
        // pass here to avoid logging every 404 a second time under "/Error/404".
        if (context.Features.Get<IStatusCodeReExecuteFeature>() != null)
        {
            return;
        }

        var statusCode = context.Response.StatusCode;
        if (statusCode is >= 400 and < 500 and not 429)
        {
            tracker.RecordEvent(BuildUrl(context), statusCode);
        }
    }

    private static string BuildUrl(HttpContext context)
    {
        var url = context.Request.Path.Value ?? "/";
        var query = context.Request.QueryString.Value;
        if (!string.IsNullOrEmpty(query))
        {
            url += query;
        }

        return url.Length <= MaxUrlLength ? url : url[..MaxUrlLength];
    }
}
