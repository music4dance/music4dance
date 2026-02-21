using Microsoft.Extensions.Caching.Memory;
using System.Net;

namespace m4d.Middleware;

/// <summary>
/// Rate limiting middleware to protect Identity endpoints from bot attacks
/// Uses in-memory cache for tracking request counts per IP address
/// </summary>
public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IMemoryCache _cache;
    private readonly ILogger<RateLimitingMiddleware> _logger;
    private readonly RateLimitingOptions _options;

    public RateLimitingMiddleware(
        RequestDelegate next,
        IMemoryCache cache,
        ILogger<RateLimitingMiddleware> logger,
        IConfiguration configuration)
    {
        _next = next;
        _cache = cache;
        _logger = logger;
        _options = new RateLimitingOptions(configuration);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Only rate limit specific paths
        var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";
        
        if (!ShouldRateLimit(path))
        {
            await _next(context);
            return;
        }

        // Get client identifier (IP address)
        var clientId = GetClientIdentifier(context);
        var cacheKey = $"RateLimit:{path}:{clientId}";

        // Get current request count
        var requestInfo = _cache.GetOrCreate(cacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_options.WindowMinutes);
            return new RequestInfo
            {
                Count = 0,
                WindowStart = DateTime.UtcNow
            };
        });

        requestInfo!.Count++;

        // Check if rate limit exceeded
        if (requestInfo.Count > _options.MaxRequestsPerWindow)
        {
            _logger.LogWarning(
                "Rate limit exceeded for {ClientId} on {Path}: {Count} requests in {Window} minutes",
                clientId, path, requestInfo.Count, _options.WindowMinutes);

            context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
            context.Response.Headers["Retry-After"] = "60";
            await context.Response.WriteAsJsonAsync(new
            {
                error = "Too many requests",
                message = "Please try again later",
                retryAfter = 60
            });
            return;
        }

        // Update cache
        _cache.Set(cacheKey, requestInfo, TimeSpan.FromMinutes(_options.WindowMinutes));

        await _next(context);
    }

    private bool ShouldRateLimit(string path)
    {
        // Rate limit Identity pages (login, register, etc.)
        if (path.StartsWith("/identity/"))
        {
            return true;
        }

        // Rate limit specific high-value endpoints if needed
        // Add more paths here as needed
        
        return false;
    }

    private string GetClientIdentifier(HttpContext context)
    {
        // Try to get real IP from X-Forwarded-For (behind Azure Front Door)
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            // Take the first IP (original client)
            var clientIp = forwardedFor.Split(',')[0].Trim();
            return clientIp;
        }

        // Fallback to direct connection IP
        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    private class RequestInfo
    {
        public int Count { get; set; }
        public DateTime WindowStart { get; set; }
    }
}

/// <summary>
/// Configuration options for rate limiting
/// </summary>
public class RateLimitingOptions
{
    public int MaxRequestsPerWindow { get; set; } = 20;
    public int WindowMinutes { get; set; } = 1;

    public RateLimitingOptions(IConfiguration configuration)
    {
        var section = configuration.GetSection("RateLimiting");
        MaxRequestsPerWindow = section.GetValue<int>("MaxRequestsPerWindow", 20);
        WindowMinutes = section.GetValue<int>("WindowMinutes", 1);
    }
}
