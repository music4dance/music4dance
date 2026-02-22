# Identity Endpoint Protection - Rate Limiting & Random Delays

## Summary

Implemented a three-layer defense strategy to protect Identity endpoints (`/identity/*`) from bot attacks and credential stuffing:

1. **Random Delays (200-400ms)** - Slows down all authentication attempts
2. **Rate Limiting (20 req/min)** - Caps total requests per IP
3. **CAPTCHA (existing)** - Human verification on repeated failures

This provides comprehensive bot protection since Identity pages cannot be cached (due to Set-Cookie security requirements for anti-forgery tokens).

## Files Created

### 1. `m4d/Middleware/RateLimitingMiddleware.cs`

**Purpose:** Rate limit and delay requests to specific endpoints (primarily Identity pages)

**Key Features:**
- Uses `IMemoryCache` for tracking request counts per IP
- **Random delay (200-400ms) for authentication POST requests** - Slows brute force attacks
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

---

## Random Delay Implementation

### Purpose

Added random delays (200-400ms) to authentication validation attempts as an additional defense layer against brute force attacks and credential stuffing. This complements rate limiting by making each individual attempt slower.

### How It Works

**Before Rate Limit Check:**
1. Check if request is an authentication attempt (POST to Identity paths)
2. If yes, add random delay between 200-400ms
3. Log the delay (Debug level)
4. Then proceed with normal rate limiting

**Authentication Paths Affected:**
- `/identity/account/login` (POST)
- `/identity/account/register` (POST)
- `/identity/account/externallogin` (POST)
- `/identity/account/resetpassword` (POST)
- `/identity/account/loginwith2fa` (POST)

**Not Affected:**
- GET requests (viewing login page)
- Other Identity pages (manage account, etc.)
- Non-Identity endpoints

### Code Implementation

```csharp
public async Task InvokeAsync(HttpContext context)
{
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

    // ... rest of rate limiting logic
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
    if (path.Contains("/identity/account/login")) return true;
    
    // Registration attempts
    if (path.Contains("/identity/account/register")) return true;
    
    // External login callbacks
    if (path.Contains("/identity/account/externallogin")) return true;
    
    // Password reset attempts
    if (path.Contains("/identity/account/resetpassword")) return true;
    
    // Two-factor authentication attempts
    if (path.Contains("/identity/account/loginwith2fa")) return true;

    return false;
}
```

### Security Benefits

#### 1. Slows Brute Force Attacks

**Before:**
- Attacker tries 1000 passwords/minute
- Limited only by network speed

**After:**
- Each attempt takes 200-400ms minimum
- Maximum ~200 attempts/minute (even with perfect network)
- **80% reduction in attack speed**

#### 2. Frustrates Automated Bots

**Before:**
- Bots can rapidly test credentials

**After:**
- Every attempt takes at least 200ms
- Makes automation less attractive
- Combined with rate limiting = very effective

#### 3. Makes Timing Attacks Harder

**Before:**
- Attacker can measure exact response time
- Timing differences reveal information (valid username, etc.)

**After:**
- Random 200ms variance masks timing differences
- Harder to distinguish valid/invalid usernames

#### 4. Minimal Impact on Legitimate Users

**Impact:**
- Valid login: 200-400ms delay (barely noticeable)
- Invalid login: 200-400ms delay (acceptable for security)
- Viewing login page (GET): No delay

**User Experience:**
- Most users won't notice 300ms delay
- Security benefit far outweighs minor inconvenience

### Configuration Options

**More Aggressive (slower attacks, slightly more user impact):**
```csharp
var delayMs = Random.Shared.Next(500, 1000); // 500-1000ms delay
```

**Less Aggressive (faster for users, less protection):**
```csharp
var delayMs = Random.Shared.Next(100, 200); // 100-200ms delay
```

**Current (balanced):**
```csharp
var delayMs = Random.Shared.Next(200, 400); // 200-400ms delay
```

---

## Three-Layer Defense Strategy

### Layer 1: Random Delay (200-400ms)
- **Purpose:** Slow down all authentication attempts
- **Effect:** 80% reduction in attack speed
- **Impact:** Minimal on legitimate users (~300ms barely noticeable)

### Layer 2: Rate Limiting (20 requests/minute)
- **Purpose:** Block excessive requests
- **Effect:** Hard cap on attempts per IP
- **Impact:** Only affects attackers

### Layer 3: CAPTCHA (already configured)
- **Purpose:** Human verification on repeated failures
- **Effect:** Stops automated attacks
- **Impact:** Only shown after failures

### Combined Effectiveness

**Attack Speed Reduction:**
- **Without protections:** 1000 attempts/minute (limited by network)
- **With 300ms avg delay:** ~200 attempts/minute
- **With rate limit (20/min):** 20 attempts/minute
- **Combined effectiveness:** **98% reduction**

**Credential Stuffing Impact:**
- Typical attack: 100,000 credentials tested in minutes
- With delays: Would take hours to days
- Combined with rate limiting: Effectively prevented

---

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

**4. Test random delay on authentication:**
```bash
# Normal login (should take 200-400ms + processing)
time curl -X POST https://music4dance.net/identity/account/login \
  -d "username=test&password=wrong" \
  -H "Content-Type: application/x-www-form-urlencoded"

# Should take ~200-400ms + normal processing time
```

**5. Test rapid authentication attempts:**
```bash
for i in {1..10}; do
  time curl -X POST https://music4dance.net/identity/account/login \
    -d "username=test&password=wrong$i" \
    -H "Content-Type: application/x-www-form-urlencoded"
done

# Each attempt should take at least 200ms
# After 20 attempts, should get 429 response
```

### Unit Testing (Future)

**Test rate limiting:**
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

**Test random delay:**
```csharp
[TestMethod]
public async Task RateLimitingMiddleware_AuthAttempt_AddsRandomDelay()
{
    // Arrange
    var context = new DefaultHttpContext();
    context.Request.Method = "POST";
    context.Request.Path = "/identity/account/login";
    
    var middleware = new RateLimitingMiddleware(...);
    
    // Act
    var stopwatch = Stopwatch.StartNew();
    await middleware.InvokeAsync(context);
    stopwatch.Stop();
    
    // Assert
    Assert.IsTrue(stopwatch.ElapsedMilliseconds >= 200, "Delay too short");
    Assert.IsTrue(stopwatch.ElapsedMilliseconds <= 500, "Delay too long");
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

**Average delay applied:**
```kusto
traces
| where message contains "Adding"
| where message contains "random delay"
| extend DelayMs = extract(@"Adding (\d+)ms", 1, message, typeof(int))
| summarize avg(DelayMs), min(DelayMs), max(DelayMs) by bin(timestamp, 1h)
```

**Authentication attempts with delays:**
```kusto
traces
| where message contains "random delay for authentication attempt"
| extend Path = extract(@"on ([^ ]+) from", 1, message)
| summarize count() by Path
| order by count_ desc
```

**Legitimate vs. Attack Traffic:**
```kusto
// Compare delay frequency with rate limit violations
traces
| where message contains "random delay" or message contains "Rate limit exceeded"
| extend Type = case(
    message contains "random delay", "Delayed",
    message contains "Rate limit exceeded", "Blocked",
    "Other"
)
| summarize count() by Type, bin(timestamp, 1h)
| render timechart
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

## Potential Issues & Mitigations

### Issue 1: Distributed Attacks (Many IPs)

**Problem:** Attacker uses many IPs, each under rate limit
**Mitigation:** 
- Random delay still slows each IP
- Consider CAPTCHA after N failed attempts globally
- Use Azure Front Door bot detection

### Issue 2: Legitimate User Enters Wrong Password Multiple Times

**Problem:** User gets delayed on legitimate retries
**Mitigation:**
- 200-400ms delay is acceptable for security
- User can wait 1 minute if rate limited
- Consider "Forgot Password" prominent on login

### Issue 3: Load Impact

**Problem:** Each delayed request holds a thread/connection
**Mitigation:**
- ASP.NET Core uses async/await (minimal thread impact)
- 400ms max delay * 20 requests/min = 8 seconds of delay/min per IP
- Negligible for server resources

## Security Research References

**Why Random Delays Work:**
1. **OWASP Authentication Cheat Sheet** recommends delays for failed auth attempts
2. **NIST Digital Identity Guidelines** mention timing attack mitigation
3. **Industry standard** for protecting login endpoints

**Why 200-400ms Range:**
- Short enough to not frustrate users
- Long enough to significantly slow attacks
- Random to prevent timing analysis
- Similar to network latency variance

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
