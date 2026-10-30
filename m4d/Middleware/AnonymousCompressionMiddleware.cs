using Microsoft.AspNetCore.Http.Features;

namespace m4d.Middleware;

/// <summary>
/// Opts anonymous HTTPS responses into response compression and keeps authenticated ones out.
/// Response compression is registered with EnableForHttps = false, so over HTTPS nothing is
/// compressed unless a request sets IHttpsCompressionFeature.Mode = Compress, which this does
/// for anonymous users only.
///
/// Authenticated pages stay uncompressed because compressing a secret alongside reflected
/// input over TLS is the BREACH attack surface. Anonymous pages are where the bandwidth goes
/// (crawlers and first-time visitors), and they carry no secret: the antiforgery token is
/// fetched separately (A3). See architecture/plans/bot-load-and-public-page-rendering.md (A1).
///
/// Must run after UseAuthentication(), so context.User is populated, and inside
/// UseResponseCompression(), so the feature is present. Neither is registered while hot
/// reload's BrowserRefreshMiddleware is attached in Development (see UseM4dPipeline).
/// </summary>
public class AnonymousCompressionMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context)
    {
        var feature = context.Features.Get<IHttpsCompressionFeature>();
        if (feature != null)
        {
            feature.Mode = context.User.Identity?.IsAuthenticated == true
                ? HttpsCompressionMode.DoNotCompress
                : HttpsCompressionMode.Compress;
        }

        return next(context);
    }
}
