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

        // Add random delay for authentication attempts to slow down brute force attacks
        if (IsAuthenticationAttempt(context))
        {
            var delayMs = Random.Shared.Next(200, 400);
            _logger.LogDebug(
                "Adding {DelayMs}ms random delay for authentication attempt on {Path} from {ClientId}",
                delayMs, path, GetClientIdentifier(context));
            await Task.Delay(delayMs);
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

    private bool IsAuthenticationAttempt(HttpContext context)
    {
        // Only delay POST requests (actual authentication attempts)
        if (context.Request.Method != "POST")
        {
            return false;
        }

        var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";

        // Login attempts
        if (path.Contains("/identity/account/login"))
        {
            return true;
        }

        // Registration attempts
        if (path.Contains("/identity/account/register"))
        {
            return true;
        }

        // External login callbacks
        if (path.Contains("/identity/account/externallogin"))
        {
            return true;
        }

        // Password reset attempts
        if (path.Contains("/identity/account/resetpassword"))
        {
            return true;
        }

        // Two-factor authentication attempts
        if (path.Contains("/identity/account/loginwith2fa"))
        {
            return true;
        }

        return false;
    }

    private string GetClientIdentifier(HttpContext context)
    {
        // Use the connection's remote IP address, which will be populated correctly
        // when ASP.NET Core's forwarded headers middleware is configured with
        // trusted proxies/networks. Do not trust the raw X-Forwarded-For header here.
        var remoteIp = context.Connection.RemoteIpAddress;
        if (remoteIp != null)
        {
            return remoteIp.ToString();
        }

        // Fallback identifier when no remote IP is available (e.g., in some test hosts)
        return "unknown";
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
