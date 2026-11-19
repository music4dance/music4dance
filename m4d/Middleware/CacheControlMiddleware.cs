using m4d.Services.ServiceHealth;
using m4d.Utilities;

namespace m4d.Middleware;

/// <summary>
/// Sets Cache-Control on HTML pages, and tells Cloudflare which anonymous pages it may keep.
///
/// - Signed-in pages: no-store, so no browser or cache ever keeps a personalized page.
/// - Anonymous pages: no-cache for browsers and every other cache, including the in-process
///   UseResponseCaching (which doesn't look at cookies, so anything "public" would be handed to
///   signed-in users). When EdgeCache:Enabled is on, a page that's the same for every signed-out
///   human visitor also gets Cloudflare-CDN-Cache-Control: max-age=EdgeCache:MaxAgeSeconds.
///   Only Cloudflare reads that header, and it doesn't forward it.
///
/// The edge header is the origin's half of the edge cache. Cloudflare's half is a Cache Rule that
/// makes HTML eligible only for requests without the sign-in cookie and takes its TTL from this
/// header. Cached pages point at hashed bundles that a deploy removes from wwwroot; the app keeps
/// recent builds' bundles and serves them (AssetArchive, ArchivedAssetMiddleware), so deploys
/// don't purge the cache. See architecture/plans/bot-load-and-public-page-rendering.md (Phase 2).
///
/// A page gets no edge header when it differs by requester or would freeze a transient state:
/// - Googlebot: _head.cshtml leaves the AdSense script out for it.
/// - Anything SpiderManager calls a spider: it can get BotWarning or the bot stub instead.
/// - Search, the database or App Configuration not healthy, as the page was rendered or when it's
///   sent: menuContext bakes their health into every page, and a cached "search is unavailable"
///   banner would outlive the outage.
/// - /admin and /payment, and any response that already sent no-store, private or
///   Pragma: no-cache (antiforgery does, for pages with a form token).
///
/// Registered on its own response-starting callback; needs context.User, so it must run after
/// UseAuthentication().
/// </summary>
public class CacheControlMiddleware(
    RequestDelegate next, IConfiguration configuration, ServiceHealthManager serviceHealth)
{
    public const string EdgeCacheHeader = "Cloudflare-CDN-Cache-Control";
    public const string NoStore = "no-store, no-cache, must-revalidate";

    // The services whose health _head.cshtml writes into menuContext.
    internal static readonly string[] PageHealthServices = ["SearchService", "Database", "AppConfiguration"];

    // HttpContext.Items key where _head.cshtml records whether all of them were healthy as the
    // page was rendered.
    public const string RenderedHealthKey = "m4d.RenderedPageHealthy";

    // Never edge-cached: /admin is per-user, and /payment's Stripe return pages are per checkout
    // (Success and Cancel read session_id, have side effects and show one donor's confirmation).
    internal static readonly string[] EdgeExcludedPrefixes = ["/admin", "/payment"];

    public Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            Apply(context);
            return Task.CompletedTask;
        });

        return next(context);
    }

    private void Apply(HttpContext context)
    {
        if (!HttpMethods.IsGet(context.Request.Method))
        {
            return;
        }

        var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";
        var status = context.Response.StatusCode;

        // Prevent caching of redirect responses to identity paths.
        // Meta crawlers and proxies caching 302→/login amplifies redirect loops.
        if (status >= 300 && status < 400)
        {
            if (path.StartsWith("/identity/"))
            {
                context.Response.Headers.CacheControl = NoStore;
                context.Response.Headers.Pragma = "no-cache";
            }
            return;
        }

        // Only successful responses (200-299) get a policy.
        if (status < 200 || status >= 300)
        {
            return;
        }

        // Never cache Identity responses regardless of auth state or Set-Cookie presence.
        // The Meta crawler short-circuit returns 200 without Set-Cookie on /identity/,
        // which would otherwise be treated as cacheable for anonymous users.
        // API endpoints are dynamic data and set their own headers.
        // RawSearchForm has a form with an anti-forgery token for anonymous users.
        if (path.StartsWith("/identity/") || path.StartsWith("/api/") || path.StartsWith("/song/rawsearchform"))
        {
            return;
        }

        // Don't touch responses that set cookies (antiforgery, auth, etc.)
        if (context.Response.Headers.ContainsKey("Set-Cookie"))
        {
            return;
        }

        // Nor ones that already opted out of caching. Antiforgery sends no-cache, no-store and
        // Pragma: no-cache whenever it issues a form token, including when it reuses the visitor's
        // existing cookie and so sets none: a page with someone's request token must never be
        // shared (/home/contribute, any form tag helper with method="post").
        if (OptedOutOfCaching(context.Response))
        {
            return;
        }

        // Only HTML pages.
        var contentType = context.Response.ContentType?.ToLowerInvariant() ?? "";
        if (!contentType.Contains("text/html"))
        {
            return;
        }

        if (context.User.Identity?.IsAuthenticated == true)
        {
            context.Response.Headers.CacheControl = NoStore;
            context.Response.Headers.Pragma = "no-cache";
            return;
        }

        // Anonymous: browsers revalidate every time, so a visitor who signs in sees the dynamic
        // page straight away; only Cloudflare keeps a copy, and only when the page is the same
        // for every signed-out human.
        context.Response.Headers.Remove("Pragma");
        context.Response.Headers.CacheControl = "no-cache";

        var edge = EdgePolicy(context, path);
        if (edge != null)
        {
            context.Response.Headers[EdgeCacheHeader] = edge;
        }
    }

    internal static bool OptedOutOfCaching(HttpResponse response)
    {
        var directives = response.Headers.CacheControl.ToString();
        return directives.Contains("no-store", StringComparison.OrdinalIgnoreCase)
            || directives.Contains("private", StringComparison.OrdinalIgnoreCase)
            || response.Headers.Pragma.ToString().Contains("no-cache", StringComparison.OrdinalIgnoreCase);
    }

    internal string EdgePolicy(HttpContext context, string path)
    {
        var options = EdgeCacheOptions.FromConfiguration(configuration);
        if (!options.Enabled || EdgeExcludedPrefixes.Any(p => path.StartsWith(p, StringComparison.Ordinal)))
        {
            return null;
        }

        var agent = context.Request.Headers.UserAgent.ToString();
        if (agent.Contains("googlebot", StringComparison.OrdinalIgnoreCase) ||
            SpiderManager.IsSpider(agent, configuration))
        {
            return null;
        }

        // Health as the page showed it (see _head.cshtml), and as it is now. The current value
        // alone could turn healthy between render and the response starting (a cooldown ending)
        // and freeze an outage banner; pages without _head fall back to the current value.
        var renderedHealthy = context.Items.TryGetValue(RenderedHealthKey, out var rendered)
            ? rendered as bool? ?? false
            : true;
        return renderedHealthy && PageHealthServices.All(serviceHealth.IsServiceHealthy)
            ? $"max-age={options.MaxAgeSeconds}"
            : null;
    }
}

/// <summary>
/// EdgeCache settings: whether anonymous pages tell Cloudflare it may cache them, and for how long.
/// </summary>
public sealed record EdgeCacheOptions(bool Enabled, int MaxAgeSeconds)
{
    public const string SectionName = "EdgeCache";
    public const int DefaultMaxAgeSeconds = 86400;

    public static EdgeCacheOptions FromConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection(SectionName);
        var maxAge = section.GetValue("MaxAgeSeconds", DefaultMaxAgeSeconds);
        return new EdgeCacheOptions(
            section.GetValue("Enabled", false),
            maxAge > 0 ? maxAge : DefaultMaxAgeSeconds);
    }
}
