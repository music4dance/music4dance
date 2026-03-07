using m4d.Security;
using m4d.Utilities;
using Microsoft.Extensions.Caching.Memory;
using System.Net;
using System.Text.RegularExpressions;

namespace m4d.Middleware;

/// <summary>
/// Rate limiting middleware to protect Identity endpoints from bot attacks
/// Uses in-memory cache for tracking request counts per IP address
/// Phase 1: Global rate limiting, CAPTCHA escalation, suspicious URL detection
/// </summary>
public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IMemoryCache _cache;
    private readonly ILogger<RateLimitingMiddleware> _logger;
    private readonly RateLimitingOptions _options;
    private readonly RateLimitingTracker _rateLimitingTracker;
    private readonly AuthenticationTracker _authTracker;

    // Global request counter
    private static int _globalRequestCount = 0;
    private static DateTime _globalWindowStart = DateTime.UtcNow;
    private static readonly object _globalLock = new object();

    public RateLimitingMiddleware(
        RequestDelegate next,
        IMemoryCache cache,
        ILogger<RateLimitingMiddleware> logger,
        IConfiguration configuration,
        RateLimitingTracker rateLimitingTracker,
        AuthenticationTracker authTracker)
    {
        _next = next;
        _cache = cache;
        _logger = logger;
        _options = new RateLimitingOptions(configuration);
        _rateLimitingTracker = rateLimitingTracker;
        _authTracker = authTracker;
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

        // Short-circuit known Meta/Facebook crawlers on identity pages.
        // These crawlers get stuck in a redirect loop when a shared URL requires auth
        // (302 → /login → retry → 302 → ...). Return a clean 200 with OpenGraph metadata
        // instead of letting them enter the rate-limiting / anti-forgery pipeline.
        // This does NOT affect Facebook Login OAuth — those use POST callbacks to /signin-facebook.
        if (IsMetaCrawler(context))
        {
            var userAgent = context.Request.Headers.UserAgent.ToString();
            GlobalState.MetaCrawlerStats.Record(userAgent);

            _logger.LogInformation(
                "MetaCrawler: Returning static response for {UserAgent} on {Path} from {ClientId}",
                userAgent, path, AnonymizeIp(GetClientIdentifier(context)));

            await ReturnCrawlerResponse(context, path);
            return;
        }

        var verbose = GlobalState.RateLimitLogging;
        var clientId = GetClientIdentifier(context);

        // Check for suspicious returnUrl
        var returnUrl = context.Request.Query["returnUrl"].ToString();
        if (IsSuspiciousReturnUrl(returnUrl))
        {
            _logger.LogWarning(
                "Suspicious returnUrl detected from {ClientId}: {ReturnUrl}",
                AnonymizeIp(clientId), returnUrl);

            // Track this suspicious activity
            _authTracker.RecordSuspiciousActivity(AnonymizeIp(clientId), "Suspicious returnUrl");

            // Force CAPTCHA requirement
            GlobalState.RequireCaptcha = true;
        }

        // Check global rate limit FIRST (protects against distributed attacks)
        var globalCount = IncrementGlobalCount();
        var globalPercent = (double)globalCount / _options.GlobalMaxRequestsPerWindow * 100;

        // Enable CAPTCHA at threshold
        if (globalPercent >= _options.CaptchaThresholdPercent)
        {
            GlobalState.RequireCaptcha = true;
            if (verbose)
            {
                _logger.LogWarning(
                    "Global rate limit at {Percent:F1}% ({Count}/{Max}), CAPTCHA enabled",
                    globalPercent, globalCount, _options.GlobalMaxRequestsPerWindow);
            }
        }

        if (globalCount > _options.GlobalMaxRequestsPerWindow)
        {
            _rateLimitingTracker.RecordEvent(AnonymizeIp(clientId), path, true, globalCount, true);

            _logger.LogWarning(
                "GLOBAL rate limit exceeded: {Count} requests in {Window} minutes (limit: {Max})",
                globalCount, _options.GlobalWindowMinutes, _options.GlobalMaxRequestsPerWindow);

            await ReturnRateLimitResponse(context, isGlobal: true);
            return;
        }

        // Add random delay for authentication attempts to slow down brute force attacks
        if (IsAuthenticationAttempt(context))
        {
            var delayMs = Random.Shared.Next(200, 400);
            if (verbose)
                _logger.LogInformation(
                    "RateLimit: Adding {DelayMs}ms random delay for auth POST on {Path} from {ClientId}",
                    delayMs, path, AnonymizeIp(clientId));
            await Task.Delay(delayMs);

            // Check if this IP has failed logins recently - require CAPTCHA
            var recentFailures = _authTracker.GetFailedAttemptsForIP(clientId, TimeSpan.FromMinutes(5));
            if (recentFailures >= 1)
            {
                GlobalState.RequireCaptcha = true;
                if (verbose)
                {
                    _logger.LogWarning(
                        "IP {ClientId} has {Count} failed login(s) in last 5 minutes, CAPTCHA required",
                        AnonymizeIp(clientId), recentFailures);
                }
            }
        }

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

        // Thread-safe increment
        var currentCount = Interlocked.Increment(ref requestInfo!.Count);

        if (verbose)
            _logger.LogInformation(
                "RateLimit: {Method} {Path} from {ClientId} — request {Count}/{Max} in {Window}min window (global: {GlobalCount}/{GlobalMax})",
                context.Request.Method, path, AnonymizeIp(clientId), currentCount, _options.MaxRequestsPerWindow,
                _options.WindowMinutes, globalCount, _options.GlobalMaxRequestsPerWindow);

        // Check if per-IP rate limit exceeded
        if (currentCount > _options.MaxRequestsPerWindow)
        {
            _rateLimitingTracker.RecordEvent(AnonymizeIp(clientId), path, true, currentCount, false);

            _logger.LogWarning(
                "RateLimit: EXCEEDED for {ClientId} on {Path}: {Count} requests in {Window} minutes",
                AnonymizeIp(clientId), path, currentCount, _options.WindowMinutes);

            await ReturnRateLimitResponse(context, isGlobal: false);
            return;
        }

        // Record successful request (not limited)
        _rateLimitingTracker.RecordEvent(AnonymizeIp(clientId), path, false, currentCount, false);

        // Update cache
        _cache.Set(cacheKey, requestInfo, TimeSpan.FromMinutes(_options.WindowMinutes));

        await _next(context);
    }

    private int IncrementGlobalCount()
    {
        lock (_globalLock)
        {
            // Reset window if expired
            var elapsed = DateTime.UtcNow - _globalWindowStart;
            if (elapsed.TotalMinutes >= _options.GlobalWindowMinutes)
            {
                _globalRequestCount = 0;
                _globalWindowStart = DateTime.UtcNow;
            }

            return Interlocked.Increment(ref _globalRequestCount);
        }
    }

    private bool IsSuspiciousReturnUrl(string returnUrl)
    {
        if (string.IsNullOrEmpty(returnUrl)) return false;

        // Nested returnUrl parameters indicate open redirect probing
        if (returnUrl.Contains("returnUrl", StringComparison.OrdinalIgnoreCase))
        {
            var count = Regex.Matches(returnUrl, "returnUrl", RegexOptions.IgnoreCase).Count;
            if (count > 1) return true;
        }

        // Multiple query parameters that look suspicious
        if (returnUrl.Split('?').Length > 3) return true;

        return false;
    }

    private async Task ReturnRateLimitResponse(HttpContext context, bool isGlobal)
    {
        context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;

        // Calculate Retry-After from appropriate configured window
        var windowMinutes = isGlobal ? _options.GlobalWindowMinutes : _options.WindowMinutes;
        var retryAfterSeconds = (int)TimeSpan.FromMinutes(windowMinutes).TotalSeconds;
        context.Response.Headers.RetryAfter = retryAfterSeconds.ToString();

        context.Response.ContentType = "text/html";

        var html = @"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""utf-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Service Temporarily Unavailable</title>
    <style>
        body {
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif;
            line-height: 1.6;
            color: #333;
            max-width: 600px;
            margin: 100px auto;
            padding: 20px;
            text-align: center;
        }
        h1 {
            color: #d32f2f;
            font-size: 24px;
            margin-bottom: 20px;
        }
        p {
            font-size: 16px;
            margin-bottom: 15px;
        }
        .icon {
            font-size: 48px;
            margin-bottom: 20px;
        }
        .info {
            background-color: #f5f5f5;
            border-left: 4px solid #2196f3;
            padding: 15px;
            margin: 20px 0;
            text-align: left;
        }
        .retry-button {
            display: inline-block;
            margin-top: 20px;
            padding: 12px 24px;
            background-color: #2196f3;
            color: white;
            text-decoration: none;
            border-radius: 4px;
            font-weight: 500;
        }
        .retry-button:hover {
            background-color: #1976d2;
        }
    </style>
</head>
<body>
    <div class=""icon"">⚠️</div>
    <h1>Service Temporarily Unavailable</h1>
    <p>We're experiencing unusually high traffic right now.</p>
    " + (isGlobal
        ? @"<div class=""info"">
        <strong>What's happening?</strong><br>
        Our servers are receiving a very high volume of requests across all users.
        This temporary limit helps protect the service from being overwhelmed.
    </div>"
        : @"<div class=""info"">
        <strong>What's happening?</strong><br>
        Your connection has made too many requests in a short period.
        This limit helps protect against automated attacks.
    </div>") + @"
    <p>Please wait a moment and try again.</p>
    <a href=""javascript:history.back()"" class=""retry-button"">Go Back</a>
</body>
</html>";

        await context.Response.WriteAsync(html);
    }

    /// <summary>
    /// Known Meta/Facebook crawler user-agent fragments.
    /// These are dedicated crawler identifiers (used for link previews), not generic app names.
    /// Note: "whatsapp" and "instagram" are intentionally excluded — those tokens appear in
    /// in-app browser UAs for real users, and we don't want to short-circuit their Identity flow.
    /// </summary>
    private static readonly string[] MetaCrawlerFragments =
    [
        "facebookexternalhit",
        "facebot",
        "meta-externalfetcher"
    ];

    private static bool IsMetaCrawler(HttpContext context)
    {
        // Only intercept GET requests — OAuth callbacks are POST
        if (context.Request.Method != "GET")
            return false;

        var ua = context.Request.Headers.UserAgent.ToString();
        if (string.IsNullOrEmpty(ua))
            return false;

        var uaLower = ua.ToLowerInvariant();
        return MetaCrawlerFragments.Any(fragment => uaLower.Contains(fragment));
    }

    private static async Task ReturnCrawlerResponse(HttpContext context, string path)
    {
        context.Response.StatusCode = 200;
        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.Headers.CacheControl = "no-store, no-cache";

        // HTML-encode path to prevent markup injection from crafted URLs (UA check can be spoofed)
        var encodedPath = System.Net.WebUtility.HtmlEncode(path);

        var html = @"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""utf-8"">
    <meta name=""robots"" content=""noindex, nofollow"">
    <meta property=""og:title"" content=""Music4Dance - Login Required"">
    <meta property=""og:description"" content=""Sign in to access this page on Music4Dance."">
    <meta property=""og:type"" content=""website"">
    <meta property=""og:url"" content=""https://music4dance.net" + encodedPath + @""">
    <title>Music4Dance - Login Required</title>
</head>
<body>
    <h1>Login Required</h1>
    <p>This page requires authentication. Visit <a href=""https://music4dance.net"">music4dance.net</a> to sign in.</p>
</body>
</html>";

        await context.Response.WriteAsync(html);
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

    private string AnonymizeIp(string ipAddress)
    {
        // Anonymize IP address for logging to reduce PII exposure
        // For IPv4: show first 3 octets only (e.g., 192.168.1.xxx)
        // For IPv6: show first 4 groups only (e.g., 2001:db8:85a3:8d3:xxxx...)
        if (string.IsNullOrEmpty(ipAddress) || ipAddress == "unknown")
        {
            return ipAddress;
        }

        if (ipAddress.Contains('.'))
        {
            // IPv4
            var lastDot = ipAddress.LastIndexOf('.');
            if (lastDot > 0)
            {
                return string.Concat(ipAddress.AsSpan(0, lastDot), ".xxx");
            }
        }
        else if (ipAddress.Contains(':'))
        {
            // IPv6
            var parts = ipAddress.Split(':');
            if (parts.Length > 4)
            {
                return string.Join(":", parts.Take(4)) + ":xxxx...";
            }
        }

        return ipAddress;
    }

    private class RequestInfo
    {
        public int Count; // Field (not property) required for Interlocked.Increment
        public DateTime WindowStart { get; set; }
    }
}

/// <summary>
/// Configuration options for rate limiting
/// </summary>
public class RateLimitingOptions
{
    public int MaxRequestsPerWindow { get; set; } = 10;
    public int WindowMinutes { get; set; } = 1;
    public int GlobalMaxRequestsPerWindow { get; set; } = 100;
    public int GlobalWindowMinutes { get; set; } = 1;
    public int CaptchaThresholdPercent { get; set; } = 20;

    public RateLimitingOptions(IConfiguration configuration)
    {
        var section = configuration.GetSection("RateLimiting");
        MaxRequestsPerWindow = section.GetValue<int>("MaxRequestsPerWindow", 10);
        WindowMinutes = section.GetValue<int>("WindowMinutes", 1);
        GlobalMaxRequestsPerWindow = section.GetValue<int>("GlobalMaxRequestsPerWindow", 100);
        GlobalWindowMinutes = section.GetValue<int>("GlobalWindowMinutes", 1);
        CaptchaThresholdPercent = section.GetValue<int>("CaptchaThresholdPercent", 20);
    }
}
