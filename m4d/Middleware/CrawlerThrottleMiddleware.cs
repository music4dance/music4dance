using m4d.Security;
using m4d.Utilities;

using Microsoft.Extensions.Caching.Memory;

namespace m4d.Middleware;

/// <summary>
/// Caps how fast any one crawler user agent can request catalog pages. A crawler (as
/// SpiderManager classifies it, plus anything in CrawlerThrottle:AgentLimits) that goes over
/// its limit on the configured paths gets a 429 with Retry-After until its one-minute window
/// ends. The limit is CrawlerThrottle:MaxRequestsPerMinute unless AgentLimits gives the agent
/// its own. Well-behaved crawlers slow down on a 429; others just stop costing a page render.
///
/// The major search engines (CrawlerThrottle:ExemptFragments) and signed-in users are never
/// throttled. Counting is per user agent, not per IP, because crawler fleets spread across
/// many addresses. A client that rotates user agents, or claims to be an exempt one, gets
/// past it; this is a cap for the honest-but-greedy, not a defense. See
/// architecture/plans/bot-load-and-public-page-rendering.md (A6).
///
/// Must run after UseAuthentication(), so signed-in users can be skipped.
/// </summary>
public class CrawlerThrottleMiddleware(
    RequestDelegate next,
    IMemoryCache cache,
    IConfiguration configuration,
    CrawlerThrottleTracker tracker,
    ILogger<CrawlerThrottleMiddleware> logger)
{
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    private sealed class WindowCount
    {
        public int Count;
        public DateTime Start;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var options = CrawlerThrottleOptions.FromConfiguration(configuration);
        if (options.Enabled && ShouldCount(context, options))
        {
            var agent = context.Request.Headers.UserAgent.ToString().ToLowerInvariant();
            var limit = options.LimitFor(agent, SpiderManager.IsSpider(agent, configuration));
            if (limit != null)
            {
                var retryAfter = Count(agent, limit.Value);
                if (retryAfter > 0)
                {
                    tracker.RecordThrottled(agent);
                    logger.LogInformation(
                        "CrawlerThrottle: 429 for {UserAgent} on {Path}", agent, context.Request.Path);

                    context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                    context.Response.Headers.RetryAfter = retryAfter.ToString();
                    context.Response.Headers.CacheControl = "no-store";
                    context.Response.ContentType = "text/plain";
                    await context.Response.WriteAsync(
                        $"Too many requests. Please slow down and retry after {retryAfter} seconds.");
                    return;
                }
            }
        }

        await next(context);
    }

    private static bool ShouldCount(HttpContext context, CrawlerThrottleOptions options)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            return false;
        }

        var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";
        return options.PathPrefixList.Any(prefix =>
            path == prefix || path.StartsWith(prefix + "/", StringComparison.Ordinal));
    }

    /// <summary>
    /// Counts this request in the agent's current window. Returns 0 while the agent is within
    /// its limit, otherwise the seconds until its window ends.
    /// </summary>
    private int Count(string agent, int limit)
    {
        var now = DateTime.UtcNow;
        var window = cache.GetOrCreate($"crawler-throttle:{agent}", entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = Window;
            return new WindowCount { Start = now };
        })!;

        if (Interlocked.Increment(ref window.Count) <= limit)
        {
            return 0;
        }

        var remaining = window.Start + Window - now;
        return Math.Max(1, (int)Math.Ceiling(remaining.TotalSeconds));
    }
}
