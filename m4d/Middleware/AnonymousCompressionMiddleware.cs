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
/// (crawlers and first-time visitors), and the only secret they carry is the antiforgery
/// request token, which is re-encrypted with a fresh IV on every response. See
/// architecture/plans/bot-load-and-public-page-rendering.md (A1).
///
/// Must run after UseAuthentication(), so context.User is populated, and inside
/// UseResponseCompression(), so the feature is present.
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
