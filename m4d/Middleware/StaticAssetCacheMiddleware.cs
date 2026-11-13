namespace m4d.Middleware;

/// <summary>
/// Sets Cache-Control on static files so browsers and Cloudflare can keep them.
/// MapStaticAssets only treats files it fingerprinted itself as immutable; everything else,
/// including Vite's content-hashed bundles under /vclient/assets, goes out as no-cache. Behind
/// Cloudflare that turns every bundle load into an origin revalidation (cf-cache-status
/// REVALIDATED): about 1.3M 304s and half the server time over 2026-11-08..12.
///
/// - /vclient/assets/*: Vite puts a content hash in every file name there, so a year and
///   immutable. A deploy produces new names, never new content under an old name.
/// - /images, /css, /vendor and /favicon.ico: not hashed, so one day.
///
/// Only GET/HEAD responses with 200 or 304 are changed; 404s keep their headers so a missing
/// bundle isn't pinned at the edge. Registered ahead of the static-file middleware.
/// </summary>
public class StaticAssetCacheMiddleware(RequestDelegate next)
{
    public const string ImmutableCacheControl = "public, max-age=31536000, immutable";
    public const string DailyCacheControl = "public, max-age=86400";

    private static readonly PathString HashedAssets = new("/vclient/assets");

    private static readonly PathString[] DailyPaths =
    [
        new("/images"),
        new("/css"),
        new("/vendor"),
        new("/favicon.ico"),
    ];

    public Task InvokeAsync(HttpContext context)
    {
        var cacheControl = PolicyFor(context.Request);
        if (cacheControl != null)
        {
            context.Response.OnStarting(() =>
            {
                var status = context.Response.StatusCode;
                if (status == StatusCodes.Status200OK || status == StatusCodes.Status304NotModified)
                {
                    context.Response.Headers.CacheControl = cacheControl;
                    context.Response.Headers.Remove("Pragma");
                }
                return Task.CompletedTask;
            });
        }

        return next(context);
    }

    internal static string PolicyFor(HttpRequest request)
    {
        if (!HttpMethods.IsGet(request.Method) && !HttpMethods.IsHead(request.Method))
        {
            return null;
        }

        var path = request.Path;
        if (path.StartsWithSegments(HashedAssets, StringComparison.OrdinalIgnoreCase))
        {
            return ImmutableCacheControl;
        }

        return DailyPaths.Any(p => path.StartsWithSegments(p, StringComparison.OrdinalIgnoreCase))
            ? DailyCacheControl
            : null;
    }
}
