using System.Globalization;
using System.Threading.RateLimiting;

using Microsoft.AspNetCore.RateLimiting;

namespace m4d.PublicApi;

public static class PublicApiProtection
{
    internal static IServiceCollection AddPublicApiRateLimiting(
        this IServiceCollection services, int requestsPerMinute) =>
        services.AddRateLimiter(options =>
        {
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                context.Request.Path.StartsWithSegments("/connect", StringComparison.OrdinalIgnoreCase)
                    ? RateLimitPartition.GetFixedWindowLimiter(
                        context.Connection.RemoteIpAddress?.MapToIPv6().ToString() ?? "unknown",
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = requestsPerMinute,
                            Window = TimeSpan.FromMinutes(1),
                            QueueLimit = 0
                        })
                    : RateLimitPartition.GetNoLimiter("site"));
            options.OnRejected = async (context, cancellationToken) =>
            {
                var response = context.HttpContext.Response;
                response.StatusCode = StatusCodes.Status429TooManyRequests;
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    response.Headers.RetryAfter = Math.Ceiling(retryAfter.TotalSeconds)
                        .ToString(CultureInfo.InvariantCulture);
                }
                await response.WriteAsJsonAsync(new
                {
                    error = "temporarily_unavailable",
                    error_description = "Too many requests. Please try again later."
                }, cancellationToken);
            };
        });

    public static IApplicationBuilder UsePublicApiProtection(this IApplicationBuilder app)
    {
        var enabled = app.ApplicationServices.GetService<PublicApiOptions>() != null;
        app.Use(async (context, next) =>
        {
            var protocol = context.Request.Path.StartsWithSegments("/connect", StringComparison.OrdinalIgnoreCase);
            var connections = context.Request.Path.StartsWithSegments(
                PublicApiDefaults.ConnectedAppsPath, StringComparison.OrdinalIgnoreCase);
            if ((protocol || connections) && !enabled)
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }
            if (IsSensitiveRequest(context.Request))
            {
                context.Response.OnStarting(() =>
                {
                    context.Response.Headers.CacheControl = "no-store";
                    context.Response.Headers.Pragma = "no-cache";
                    context.Response.Headers["Referrer-Policy"] = "no-referrer";
                    context.Response.Headers["X-Frame-Options"] = "DENY";
                    context.Response.Headers["Content-Security-Policy"] = "frame-ancestors 'none'; base-uri 'self'";
                    return Task.CompletedTask;
                });
            }
            await next(context);
        });
        if (enabled) app.UseRateLimiter();
        return app;
    }

    public static bool IsSensitiveRequest(HttpRequest request) =>
        request.Path.StartsWithSegments("/connect", StringComparison.OrdinalIgnoreCase) ||
        request.Path.StartsWithSegments(PublicApiDefaults.ConnectedAppsPath, StringComparison.OrdinalIgnoreCase) ||
        (request.Path.StartsWithSegments("/Identity", StringComparison.OrdinalIgnoreCase) &&
         request.Query["returnUrl"].Any(value =>
             value?.StartsWith("/connect/", StringComparison.OrdinalIgnoreCase) == true));
}
