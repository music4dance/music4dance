# Rate Limiting Implementation for Identity Endpoints

## Summary

Added rate limiting middleware to protect Identity endpoints (`/identity/*`) from bot attacks, providing an alternative bot protection mechanism since these pages cannot be cached (due to Set-Cookie security requirements).

## Files Created

### 1. `m4d/Middleware/RateLimitingMiddleware.cs`

**Purpose:** Rate limit requests to specific endpoints (primarily Identity pages)

**Key Features:**
- Uses `IMemoryCache` for tracking request counts per IP
- Configurable window size and max requests
- Extracts real client IP from `X-Forwarded-For` header (Azure Front Door)
- Returns 429 Too Many Requests with Retry-After header
- Only applies to specific paths (currently `/identity/*`)

**Configuration:**
```csharp
public class RateLimitingOptions
{
    public int MaxRequestsPerWindow { get; set; } = 20;  // 20 requests
    public int WindowMinutes { get; set; } = 1;          // per 1 minute
}
```

**Default Limits:**
- 20 requests per minute per IP address
- Applies to all `/identity/*` paths
- Tracks via sliding window

## Files Modified

### 1. `m4d/appsettings.json`

**Added Configuration:**
```json
{
  "RateLimiting": {
    "MaxRequestsPerWindow": 20,
    "WindowMinutes": 1
  }
}
```

### 2. `architecture/client-side-usage-logging.md`

**Added Section:** "1.1 Cache Control Middleware & Security"

**Content:**
- Detailed security analysis of Set-Cookie caching vulnerability
- Why anti-forgery tokens cannot be cached
- Explanation of CSRF protection requirements
- Alternative bot protection strategies (including rate limiting)
- Complete middleware guard documentation
- RawSearchForm exclusion explanation

## Files Deleted

### 1. `CACHE-CONTROL-REFINEMENT.md`

**Reason:** Consolidated into `architecture/client-side-usage-logging.md` to keep architecture docs organized

## Implementation Required in Program.cs

**Add after `app.UseRouting()`:**

```csharp
// Rate limiting middleware: Protect Identity endpoints from bot attacks
app.UseMiddleware<m4d.Middleware.RateLimitingMiddleware>();
```

**Location:** After line 645 (`app.UseRouting();`)

**Full Context:**
```csharp
app.UseHttpLogging();
app.UseRouting();

// Rate limiting middleware: Protect Identity endpoints from bot attacks  
app.UseMiddleware<m4d.Middleware.RateLimitingMiddleware>();

// Authentication is handled by Identity UI (implicitly via UseAuthorization)
// Cache control middleware: Allow Azure Front Door to cache anonymous pages with careful exclusions
app.Use(async (context, next) =>
{
    // ... existing cache control middleware
});
```

## How It Works

### Request Flow

1. Request arrives at `/identity/account/login`
2. Rate limiting middleware intercepts
3. Extracts client IP from `X-Forwarded-For` header
4. Creates cache key: `"RateLimit:/identity/account/login:203.0.113.42"`
5. Checks current request count from IMemoryCache
6. If count > 20 in last 1 minute:
   - Returns 429 Too Many Requests
   - Sets `Retry-After: 60` header
   - Logs warning
7. Otherwise:
   - Increments count
   - Updates cache with 1-minute expiration
   - Allows request to proceed

### IP Address Detection

**Priority:**
1. `X-Forwarded-For` header (Azure Front Door provides real client IP)
2. `HttpContext.Connection.RemoteIpAddress` (direct connection)
3. `"unknown"` (fallback)

**Note:** Takes the FIRST IP from `X-Forwarded-For` (original client), ignoring proxy chain.

## Configuration Options

### Adjust Limits

**More Restrictive (for heavy bot attacks):**
```json
{
  "RateLimiting": {
    "MaxRequestsPerWindow": 10,   // 10 requests
    "WindowMinutes": 1            // per minute
  }
}
```

**More Permissive (for legitimate heavy usage):**
```json
{
  "RateLimiting": {
    "MaxRequestsPerWindow": 50,   // 50 requests  
    "WindowMinutes": 5            // per 5 minutes
  }
}
```

### Add More Paths

**Edit `RateLimitingMiddleware.cs`:**
```csharp
private bool ShouldRateLimit(string path)
{
    // Rate limit Identity pages
    if (path.StartsWith("/identity/"))
    {
        return true;
    }

    // Add more paths as needed
    if (path.StartsWith("/admin/"))
    {
        return true;
    }
    
    return false;
}
```

## Testing

### Manual Testing

**1. Test rate limiting:**
```bash
# Hit endpoint 25 times quickly
for i in {1..25}; do
  curl -I https://music4dance.net/identity/account/login
done

# First 20 should return 200
# Next 5 should return 429 with Retry-After: 60
```

**2. Verify IP detection:**
```bash
# With proxy
curl -I -H "X-Forwarded-For: 203.0.113.42" https://music4dance.net/identity/account/login

# Check logs for "Rate limit exceeded for 203.0.113.42"
```

**3. Verify non-identity pages not affected:**
```bash
# Should never return 429 (not rate limited)
for i in {1..100}; do
  curl -I https://music4dance.net/song/index
done
```

### Unit Testing (Future)

**Test middleware:**
```csharp
[TestMethod]
public async Task RateLimitingMiddleware_ExceedsLimit_Returns429()
{
    // Arrange
    var middleware = new RateLimitingMiddleware(...);
    
    // Act
    for (int i = 0; i < 25; i++)
    {
        var result = await middleware.InvokeAsync(context);
    }
    
    // Assert
    Assert.AreEqual(429, context.Response.StatusCode);
    Assert.IsTrue(context.Response.Headers.ContainsKey("Retry-After"));
}
```

## Monitoring

### Application Insights Queries

**Rate limit violations:**
```kusto
traces
| where message contains "Rate limit exceeded"
| summarize count() by bin(timestamp, 1h), tostring(customDimensions.ClientId)
| order by timestamp desc
```

**Most blocked IPs:**
```kusto
traces  
| where message contains "Rate limit exceeded"
| extend ClientId = tostring(customDimensions.ClientId)
| summarize BlockCount = count() by ClientId
| order by BlockCount desc
| take 20
```

### Metrics to Track

- Rate limit hit rate (429 responses / total requests)
- Most blocked IPs
- Time-of-day patterns for bot attacks
- Legitimate user impact (false positives)

## Benefits

1. **Bot Protection:** Slows down automated attacks on Identity pages
2. **Server Load Reduction:** Rejects excess requests early in pipeline
3. **Security:** Prevents brute force attacks, credential stuffing
4. **Configurable:** Easy to adjust limits without code changes
5. **Transparent:** Logged warnings help identify attack patterns

## Security Considerations

### Bypass Techniques (and Mitigations)

**1. Distributed Attack (many IPs):**
- Mitigation: Use Azure Front Door bot detection rules
- Mitigation: Implement CAPTCHA on repeated failures

**2. IP Spoofing:**
- Mitigated: Azure Front Door validates X-Forwarded-For
- Mitigated: Direct connection IP used as fallback

**3. Shared IP (NAT, Corporate Proxy):**
- Risk: Legitimate users behind same IP may be blocked together
- Mitigation: Set generous limits (20/min is typically safe)
- Mitigation: Monitor for false positives

## Alternative Approaches (Not Implemented)

### 1. Per-UsageId Rate Limiting
```csharp
var usageId = context.Request.Cookies["usageId"];
var cacheKey = $"RateLimit:{path}:{usageId}";
```
**Pros:** More granular, tracks individual sessions
**Cons:** Bots can generate new UsageIds easily

### 2. Progressive Penalties
```csharp
if (requestInfo.Count > 50) 
    return 429; // Permanent block
else if (requestInfo.Count > 20) 
    await Task.Delay(5000); // Slow down
```
**Pros:** More sophisticated bot deterrence
**Cons:** Adds complexity, resource overhead

### 3. Adaptive Rate Limits
```csharp
var limit = IsKnownBot(userAgent) ? 5 : 20;
```
**Pros:** Different limits for different clients
**Cons:** More complex, harder to configure

## Next Steps

1. **Add middleware to Program.cs** (see above)
2. **Deploy and monitor** for 24-48 hours
3. **Adjust limits** based on observed patterns
4. **Add CAPTCHA** if rate limiting alone insufficient
5. **Consider Azure Front Door rules** for known bot patterns

## Status

- ? Middleware created
- ? Configuration added
- ? Architecture documented
- ? Program.cs integration (requires file unlock)
- ? Deployment and monitoring

**Ready for testing once Program.cs is updated.**
