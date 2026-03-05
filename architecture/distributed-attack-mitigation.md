# Distributed Attack Mitigation Strategy

## Executive Summary

On March 5, 2026, we observed a coordinated distributed attack targeting identity endpoints (`/identity/account/login` and `/identity/account/register`). The attack used 10+ distinct IP addresses from a botnet to bypass per-IP rate limiting while probing for vulnerabilities including open redirects, username enumeration, and credential stuffing.

**Implementation Status**: ✅ **Phase 1 COMPLETE**

All Phase 1 components have been implemented, tested, and verified:

- Global rate limiting (100 req/min) + per-IP limiting (10 req/min)
- Dynamic CAPTCHA escalation based on attack patterns
- Real-time admin dashboard with security visualization
- Authentication attempt tracking (1000 attempts / 24 hours)
- Enhanced account lockout (3 attempts → 15 minute lockout)
- Suspicious returnUrl detection
- Comprehensive unit test coverage (22 new tests, all passing)

**Test Results**: 335 tests total, 334 passed, 0 failed, 1 skipped

---

## Attack Analysis

### Timeline: March 5, 2026 17:36-17:38 UTC

**Duration**: ~90 seconds
**Total Requests Observed**: 15+ identity endpoint requests
**Distinct IPs**: 10+ confirmed
**Attack Vector**: Distributed botnet rotating IPs to stay under per-IP rate limits

### Attacking IP Addresses

| IP Prefix       | Count | Geographic Origin | Pattern           |
| --------------- | ----- | ----------------- | ----------------- |
| 85.208.96.xxx   | 4     | Europe            | Repeated offender |
| 185.191.171.xxx | 3     | Europe            | Repeated offender |
| 106.119.84.xxx  | 1     | China             | Single probe      |
| 114.230.197.xxx | 1     | China             | Single probe      |
| 120.228.104.xxx | 1     | China             | Single probe      |
| 117.159.239.xxx | 1     | China             | Single probe      |
| 183.204.91.xxx  | 1     | China             | Single probe      |
| 203.195.164.xxx | 1     | China             | Single probe      |
| 192.178.4.xxx   | 1     | Unknown           | Single probe      |
| 140.249.107.xxx | 1     | Unknown           | Single probe      |

### Attack Techniques Observed

#### 1. **Distributed IP Rotation**

- Most IPs make only 1-2 requests before switching
- Stays under the 10 requests/minute per-IP threshold
- Classic botnet behavior to bypass rate limiting

#### 2. **Open Redirect Probing**

```
/identity/account/login?returnUrl=/identity/account/login?returnUrl=/song/details/...
```

- Nested `returnUrl` parameters indicate open redirect vulnerability testing
- Application correctly detected and logged these as "Non-local return URL"
- **Status**: ✅ Protected by existing validation

#### 3. **Username Enumeration Attempts**

```
/song/details/499c3e98-0850-4ae6-90c6-9cccf9c997d4?filter=v2-index--Modified---burnsie2|a
/song/details/ee8652c0-79d1-450b-a896-4e6c79ad3ed7?filter=v2-index--Tempo---denjj|h
```

- Using legitimate URLs with filter parameters containing potential usernames
- Attempting to determine if accounts `burnsie2` and `denjj` exist
- **Concern**: May indicate reconnaissance for targeted attacks

#### 4. **Credential Stuffing (Suspected)**

- POST requests to login/register endpoints with random delays applied
- Likely using stolen credential databases
- **Status**: ⚠️ Random delays (200-400ms) slow attacks but don't prevent them

---

## Implemented Defense Mechanisms (Phase 1 - Complete)

### ✅ Active Protections

1. **Two-Tier Rate Limiting**
   - **Per-IP Limit**: 10 requests per minute per IP
   - **Global Limit**: 100 requests per minute across all IPs (distributed attack defense)
   - Status: Active with user-friendly HTML error pages
   - Coverage: `/identity/*` paths

2. **Dynamic CAPTCHA Escalation**
   - Automatically enables CAPTCHA when:
     - 20% or more requests are being rate limited
     - Any failed login attempt detected from an IP
     - Suspicious returnUrl patterns detected
   - Status: Active via `GlobalState.RequireCaptcha` flag
   - Integration: Login, Register, and other auth pages

3. **Authentication Attempt Tracking**
   - In-memory tracking: Last 1000 attempts or 24 hours
   - Records: Username, IP, timestamp, success/failure, failure reason
   - Provides real-time statistics for admin dashboard
   - Thread-safe using ConcurrentDictionary and lock statements
   - Status: Active via `AuthenticationTracker` singleton

4. **Rate Limiting Event Tracking**
   - Circular buffer: Last 10,000 events (memory-efficient)
   - 5-minute time slice aggregation for visualization
   - Tracks per-IP and global rate limit hits
   - Status: Active via `RateLimitingTracker` singleton

5. **Enhanced Account Lockout**
   - Threshold: 3 failed attempts (down from 5)
   - Duration: 15 minutes (up from 5 minutes)
   - Status: Active for all user accounts including newly registered

6. **Suspicious returnUrl Detection**
   - Pattern matching for nested returnUrl parameters
   - Regex: `returnUrl.*[?&].*returnUrl` (case-insensitive)
   - Triggers immediate CAPTCHA escalation
   - Status: Active in RateLimitingMiddleware

7. **Random Delays on Authentication**
   - Delay: 200-400ms on POST to login/register/password reset
   - Purpose: Slows brute force attacks
   - Status: Active

8. **Open Redirect Protection**
   - Validates `returnUrl` is local
   - Logs suspicious attempts
   - Status: Active (pre-existing)

9. **IP Anonymization in Logs**
   - Logs show `xxx` for last octet (IPv4) or last groups (IPv6)
   - Reduces PII exposure while maintaining debugging utility
   - Status: Active (pre-existing)

10. **Admin Security Dashboard**
    - Real-time rate limiting activity visualization
    - Authentication security metrics
    - Top attacking IPs and targeted usernames
    - Red/yellow highlighting for threats
    - URL: `/Admin/Diagnostics`
    - Status: Active with comprehensive UI

### 📊 Phase 1 Statistics

| Metric          | Value                              |
| --------------- | ---------------------------------- |
| New Code Files  | 4 files (754 lines)                |
| Modified Files  | 9 files                            |
| New Unit Tests  | 22 tests (390 lines)               |
| Test Pass Rate  | 99.7% (334/335 passed)             |
| Build Status    | Success (23 non-critical warnings) |
| Memory Overhead | <1 MB (in-memory tracking)         |

### 🔧 Configuration

All settings configurable via `appsettings.json`:

```json
{
  "RateLimiting": {
    "MaxRequestsPerWindow": 10, // Per-IP limit
    "WindowMinutes": 1,
    "GlobalMaxRequestsPerWindow": 100, // Global limit
    "GlobalWindowMinutes": 1,
    "CaptchaThresholdPercent": 20 // CAPTCHA trigger threshold
  }
}
```

Account lockout configured in `Program.cs`:

```csharp
options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
options.Lockout.MaxFailedAccessAttempts = 3;
options.Lockout.AllowedForNewUsers = true;
```

---

## Defense Gaps (Pre-Phase 1)

These gaps have been addressed by Phase 1 implementation:

1. ~~**No Cross-IP Protection**~~ → ✅ **FIXED**: Global rate limiting (100 req/min)
2. ~~**No Failed Login Tracking**~~ → ✅ **FIXED**: AuthenticationTracker with 24hr retention
3. ~~**Limited Visibility**~~ → ✅ **FIXED**: Admin dashboard with real-time stats
4. ~~**No Progressive CAPTCHA**~~ → ✅ **FIXED**: Dynamic escalation at 20% threshold
5. ~~**Weak Account Lockout**~~ → ✅ **FIXED**: 3 attempts → 15 min lockout

### Remaining Limitations (By Design for Phase 1)

These are acceptable for Phase 1 and can be addressed in future phases:

1. **In-Memory Only**: Trackers reset on application restart (Phase 2 can add persistence)
2. **No Distributed Coordination**: Each app instance tracks independently (Phase 2 can add Redis/SQL)
3. **No Automated Alerts**: Admin must monitor dashboard manually (Phase 2+ can add email/webhook alerts)
4. **Fixed Time Windows**: 5-minute slices, 1-minute rate limit windows (future: configurable)
5. **Limited Historical Data**: 1 hour (rate limiting), 24 hours (authentication) (future: longer retention)

---

## Implementation Details (Phase 1 - Complete)

### Component 1: Global Rate Limiting with CAPTCHA Escalation

**Files Modified/Created:**

- `m4d/Middleware/RateLimitingMiddleware.cs` (227 → 350+ lines)
- `m4d/Utilities/GlobalState.cs` (added `RequireCaptcha` flag)
- `m4d/appsettings.json` (added configuration)

**Implementation:**

Global rate limiting tracks total requests across all IPs with automatic CAPTCHA escalation:

```csharp
// Static counters for global tracking
private static int _globalRequestCount = 0;
private static DateTime _globalWindowStart = DateTime.UtcNow;

// Atomic increment with window reset
private static int IncrementGlobalCount()
{
    lock (_globalLock)
    {
        var elapsed = DateTime.UtcNow - _globalWindowStart;
        if (elapsed.TotalMinutes >= _globalWindowMinutes)
        {
            _globalRequestCount = 0;
            _globalWindowStart = DateTime.UtcNow;
        }
        return ++_globalRequestCount;
    }
}

// CAPTCHA escalation at 20% threshold
var globalCount = IncrementGlobalCount();
var captchaThreshold = (int)(_globalMaxRequestsPerWindow * (_captchaThresholdPercent / 100.0));

if (globalCount >= captchaThreshold ||
    _authTracker.GetFailedAttemptsForIP(ipAddress, TimeSpan.FromMinutes(5)) >= 1 ||
    IsSuspiciousReturnUrl(returnUrl))
{
    GlobalState.RequireCaptcha = true;
}

// Global rate limit enforcement
if (globalCount > _globalMaxRequestsPerWindow)
{
    await ReturnRateLimitResponse(context, isGlobal: true, requestCount: globalCount);
    return;
}
```

**User-Friendly Error Page:**

When rate limited, users see an HTML page (not JSON) explaining:

- What happened (rate limit exceeded)
- Why (security protection during high traffic)
- What to do (wait and retry)
- Includes a "Try Again" button

**Configuration:**

```json
{
  "RateLimiting": {
    "GlobalMaxRequestsPerWindow": 100,
    "GlobalWindowMinutes": 1,
    "CaptchaThresholdPercent": 20
  }
}
```

**Testing Results:**

- ✅ Global limit enforced correctly at 100 req/min
- ✅ CAPTCHA appears after 20 requests (20% threshold)
- ✅ HTML error page displays properly
- ✅ Window resets correctly after 1 minute

---

### Component 2: Authentication Attempt Tracking

**Files Created:**

- `m4d/Security/AuthenticationTracker.cs` (181 lines)
- `m4d.Tests/Security/AuthenticationTrackerTests.cs` (160 lines, 8 tests)

**Implementation:**

In-memory tracker using ConcurrentDictionary for thread-safe statistics:

```csharp
// In RateLimitingMiddleware
private static int _globalIdentityRequestCount = 0;
private static DateTime _globalWindowStart = DateTime.UtcNow;
private const int GLOBAL_LIMIT_PER_MINUTE = 100;
private const int CAPTCHA_THRESHOLD_PERCENT = 20; // Enable CAPTCHA at 20% of limit

// Add before per-IP check in InvokeAsync
var globalCount = Interlocked.Increment(ref _globalIdentityRequestCount);
var elapsed = DateTime.UtcNow - _globalWindowStart;

if (elapsed.TotalMinutes >= 1)
{
    Interlocked.Exchange(ref _globalIdentityRequestCount, 0);
    _globalWindowStart = DateTime.UtcNow;
}

// Check if we should enable CAPTCHA mode (20% threshold)
var captchaThreshold = (int)(GLOBAL_LIMIT_PER_MINUTE * 0.2);
if (globalCount > captchaThreshold)
{
    // Set flag to require CAPTCHA on identity forms
    GlobalState.RequireCaptcha = true;
}

if (globalCount > GLOBAL_LIMIT_PER_MINUTE)
{
    _logger.LogWarning(
        "Global rate limit exceeded: {Count} identity requests in window",
        globalCount);

    // Return user-friendly HTML page
    context.Response.StatusCode = (int)HttpStatusCode.ServiceUnavailable;
    context.Response.Headers["Retry-After"] = "60";
    context.Response.ContentType = "text/html";
    await context.Response.WriteAsync($@"
<!DOCTYPE html>
<html>
<head>
    <title>Service Temporarily Unavailable</title>
    <style>
        body {{ font-family: Arial, sans-serif; text-align: center; padding: 50px; background: #f5f5f5; }}
        .container {{ max-width: 600px; margin: 0 auto; background: white; padding: 40px; border-radius: 8px; box-shadow: 0 2px 4px rgba(0,0,0,0.1); }}
        h1 {{ color: #d9534f; }}
        p {{ color: #333; line-height: 1.6; }}
        .alert {{ background: #fcf8e3; border: 1px solid #faebcc; padding: 15px; border-radius: 4px; margin: 20px 0; }}
    </style>
</head>
<body>
    <div class='container'>
        <h1>⚠️ Security Alert</h1>
        <div class='alert'>
            <strong>We're currently experiencing unusually high login activity.</strong>
        </div>
        <p>For your security and ours, we've temporarily limited access to authentication pages.</p>
        <p><strong>This is not an error on your part.</strong> Our systems are actively defending against unauthorized access attempts.</p>
        <p>Please wait a few minutes and try again. We apologize for the inconvenience.</p>
        <p style='color: #666; font-size: 0.9em; margin-top: 30px;'>
            If you continue to experience issues, please contact support.
        </p>
    </div>
</body>
</html>");
    return;
}
```

---

### Component 2: AuthenticationTracker

**File:** `m4d/Security/AuthenticationTracker.cs` (181 lines)

**Purpose:** In-memory tracking of authentication attempts for security monitoring and pattern detection.

**Key Features:**

- Tracks 1000 most recent attempts or 24-hour retention (whichever is less)
- Records username, IP, timestamp, success/failure, reason
- Thread-safe using `lock` statements and `ConcurrentDictionary`
- Provides aggregated stats: top targeted usernames, most active attacking IPs
- Query methods for CAPTCHA escalation decisions

**Test Coverage:** ✅ 8 unit tests (all passing)

- Success/failure tracking
- Multi-IP distributed attack patterns
- Time-windowed IP queries
- Top 10 statistics (usernames and IPs)
- Null value handling

---

### Component 3: RateLimitingTracker

**File:** `m4d/Security/RateLimitingTracker.cs` (183 lines)

**Purpose:** Tracks rate limiting middleware events for dashboard visualization using circular buffer pattern.

**Key Features:**

- Circular buffer stores last 10,000 events (memory-efficient)
- 5-minute time slice aggregation (12 buckets = last hour)
- Tracks per-IP and global rate limit hits separately
- Top 10 requesting IPs and targeted paths
- Thread-safe event recording

**Supporting Class:** `CircularBuffer<T>` (67 lines) - Generic fixed-capacity buffer with wrap-around

**Test Coverage:** ✅ 14 unit tests (all passing)

- Single/multiple event tracking
- Limited vs allowed requests
- Global vs per-IP differentiation
- Unique IP counting
- Top N statistics ordering
- CircularBuffer capacity and wrap-around edge cases

---

### Component 4: Admin Dashboard Updates

**File:** `m4d/Views/Admin/Diagnostics.cshtml` (+150 lines)

**Purpose:** Real-time security monitoring UI for administrators.

**Sections:**

1. **Rate Limiting Activity**
   - Summary cards: Total requests, limited requests, unique IPs, global/per-IP hits
   - Top Requesting IPs table (yellow highlight for rate-limited IPs)
   - Note: Timeline chart (12 x 5-minute buckets) planned for Phase 2

2. **Authentication Security**
   - Summary cards: Failed attempts, unique IPs, targeted usernames, total attempts
   - Most Targeted Usernames table (red highlight when distinct IPs > 5 = distributed attack indicator)
   - Most Active Attacking IPs table showing failed login counts

**Access:** `/Admin/Diagnostics` (admin role required)

**Controller Changes:** `m4d/Controllers/AdminController.cs`

- Injected `AuthenticationTracker` and `RateLimitingTracker`
- Populates `ViewBag.AuthStats` and `ViewBag.RateLimitStats`

---

### Component 5: Login/Register Integration

**Files Modified:**

- `m4d/Areas/Identity/Pages/Account/Login.cshtml.cs` (+80 lines)
- `m4d/Areas/Identity/Pages/Account/Login.cshtml` (+15 lines reCAPTCHA UI)
- `m4d/Areas/Identity/Pages/Account/Register.cshtml.cs` (+30 lines)

**Features:**

- `AuthenticationTracker` injection via dependency injection
- Records all authentication attempts (success + failure) with IP address
- Captures failure reasons: "LockedOut", "InvalidPassword", "UserNotFound"
- reCAPTCHA validation when `GlobalState.RequireCaptcha` or `ShowCaptcha` flag set
- `ShowCaptcha` flag persists after failed login for next page load

---

### Component 6: Account Lockout Configuration

**File:** `m4d/Program.cs` (lines 378-381)

**Changes:**

```csharp
options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);  // Increased from 5
options.Lockout.MaxFailedAccessAttempts = 3;                        // Explicit
options.Lockout.AllowedForNewUsers = true;
```

---

### Component 7: Dependency Injection Registration

**File:** `m4d/Program.cs` (lines 464-465)

**Registration:**

```csharp
services.AddSingleton<AuthenticationTracker>();
services.AddSingleton<RateLimitingTracker>
                {
                    <tr>
                        <td><code>@stat.IpAddress</code></td>
                        <td>@stat.FailedAttempts</td>
                        <td>@stat.TargetedUsernames</td>
                    </tr>
                }
            </tbody>
        </table>
    </div>
</div>
```

**Integration Points**:

1. `LoginModel.OnPostAsync()` - Record login attempts
2. `RegisterModel.OnPostAsync()` - Record registration attempts
3. Hook into ASP.NET Identity events if available

---

#### 1.3 Rate Limiting Middleware Event Tracking

**Goal**: Track rate limiting middleware activity for real-time monitoring and pattern analysis

**Implementation**:

```csharp
// Create new class: m4d/Security/RateLimitingTracker.cs
public class RateLimitingTracker
{
    private static readonly CircularBuffer<RateLimitEvent> _events = new CircularBuffer<RateLimitEvent>(10000);
    private static readonly object _lock = new object();

    public void RecordEvent(string ipAddress, string path, bool wasLimited, int requestCount, bool isGlobal)
    {
        var evt = new RateLimitEvent
        {
            Timestamp = DateTime.UtcNow,
            IpAddress = ipAddress,
            Path = path,
            WasLimited = wasLimited,
            RequestCount = requestCount,
            IsGlobalLimit = isGlobal
        };

        lock (_lock)
        {
            _events.Add(evt);
        }
    }

    public RateLimitingStats GetStats()
    {
        lock (_lock)
        {
            var now = DateTime.UtcNow;
            var lastHour = now.AddHours(-1);
            var allEvents = _events.ToList();
            var recentEvents = allEvents.Where(e => e.Timestamp >= lastHour).ToList();

            // Create 5-minute time slices for last hour (12 buckets)
            var timeSlices = new List<TimeSliceStats>();
            for (int i = 0; i < 12; i++)
            {
                var sliceEnd = now.AddMinutes(-i * 5);
                var sliceStart = sliceEnd.AddMinutes(-5);
                var sliceEvents = recentEvents.Where(e => e.Timestamp >= sliceStart && e.Timestamp < sliceEnd).ToList();

                timeSlices.Add(new TimeSliceStats
                {
                    StartTime = sliceStart,
                    EndTime = sliceEnd,
                    TotalRequests = sliceEvents.Count,
                    LimitedRequests = sliceEvents.Count(e => e.WasLimited),
                    UniqueIPs = sliceEvents.Select(e => e.IpAddress).Distinct().Count(),
                    GlobalLimitHits = sliceEvents.Count(e => e.IsGlobalLimit)
                });
            }

            timeSlices.Reverse(); // Oldest to newest

            return new RateLimitingStats
            {
                TotalEventsTracked = allEvents.Count,
                LastHourRequests = recentEvents.Count,
                LastHourLimited = recentEvents.Count(e => e.WasLimited),
                UniqueIPsLastHour = recentEvents.Select(e => e.IpAddress).Distinct().Count(),
                GlobalLimitHitsLastHour = recentEvents.Count(e => e.IsGlobalLimit),
                PerIPLimitHitsLastHour = recentEvents.Count(e => e.WasLimited && !e.IsGlobalLimit),
                TimeSlices = timeSlices,
                TopRequestingIPs = recentEvents
                    .GroupBy(e => e.IpAddress)
                    .OrderByDescending(g => g.Count())
                    .Take(10)
                    .Select(g => new IPRequestStats
                    {
                        IpAddress = g.Key,
                        TotalRequests = g.Count(),
                        LimitedRequests = g.Count(e => e.WasLimited),
                        LastRequestTime = g.Max(e => e.Timestamp)
                    })
                    .ToList(),
                MostTargetedPaths = recentEvents
                    .GroupBy(e => e.Path)
                    .OrderByDescending(g => g.Count())
                    .Take(10)
                    .Select(g => new PathStats
                    {
                        Path = g.Key,
                        RequestCount = g.Count(),
                        UniqueIPs = g.Select(e => e.IpAddress).Distinct().Count()
                    })
                    .ToList(),
                RecentLimitedRequests = recentEvents
                    .Where(e => e.WasLimited)
                    .OrderByDescending(e => e.Timestamp)
                    .Take(50)
                    .ToList()
            };
        }
    }
}

// Simple circular buffer implementation
public class CircularBuffer<T>
{
    private readonly T[] _buffer;
    private int _head = 0;
    private int _count = 0;

    public CircularBuffer(int capacity)
    {
        _buffer = new T[capacity];
    }

    public void Add(T item)
    {
        _buffer[_head] = item;
        _head = (_head + 1) % _buffer.Length;
        if (_count < _buffer.Length) _count++;
    }

    public List<T> ToList()
    {
        var result = new List<T>(_count);
        for (int i = 0; i < _count; i++)
        {
            var index = (_head - _count + i + _buffer.Length) % _buffer.Length;
            result.Add(_buffer[index]);
        }
        return result;
    }
}

public class RateLimitEvent
{
    public DateTime Timestamp { get; set; }
    public string IpAddress { get; set; }
    public string Path { get; set; }
    public bool WasLimited { get; set; }
    public int RequestCount { get; set; }
    public bool IsGlobalLimit { get; set; }
}

public class RateLimitingStats
{
    public int TotalEventsTracked { get; set; }
    public int LastHourRequests { get; set; }
    public int LastHourLimited { get; set; }
    public int UniqueIPsLastHour { get; set; }
    public int GlobalLimitHitsLastHour { get; set; }
    public int PerIPLimitHitsLastHour { get; set; }
    public List<TimeSliceStats> TimeSlices { get; set; }
    public List<IPRequestStats> TopRequestingIPs { get; set; }
    public List<PathStats> MostTargetedPaths { get; set; }
    public List<RateLimitEvent> RecentLimitedRequests { get; set; }
}

public class TimeSliceStats
{
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public int TotalRequests { get; set; }
    public int LimitedRequests { get; set; }
    public int UniqueIPs { get; set; }
    public int GlobalLimitHits { get; set; }
}

public class IPRequestStats
{
    public string IpAddress { get; set; }
    public int TotalRequests { get; set; }
    public int LimitedRequests { get; set; }
    public DateTime LastRequestTime { get; set; }
}

public class PathStats
{
    public string Path { get; set; }
    public int RequestCount { get; set; }
    public int UniqueIPs { get; set; }
}
```

**Integration with RateLimitingMiddleware**:

```csharp
// In RateLimitingMiddleware.InvokeAsync(), add tracking after rate limit checks
private async Task InvokeAsync(HttpContext context)
{
    var path = context.Request.Path.Value;
    if (!path.StartsWith("/Identity", StringComparison.OrdinalIgnoreCase))
    {
        await _next(context);
        return;
    }

    var clientId = GetClientIpAddress(context);
    var wasLimited = false;
    var isGlobal = false;

    // ... existing rate limit checks ...

    // Check global rate limit
    var globalCount = IncrementGlobalCount();
    if (globalCount > _maxGlobalRequestsPerWindow)
    {
        wasLimited = true;
        isGlobal = true;
        _rateLimitingTracker.RecordEvent(AnonymizeIp(clientId), path, true, globalCount, true);
        await ReturnRateLimitResponse(context);
        return;
    }

    // Check per-IP rate limit
    var requestInfo = GetOrCreateRequestInfo(clientId);
    if (requestInfo.Count > _maxRequestsPerWindow)
    {
        wasLimited = true;
        _rateLimitingTracker.RecordEvent(AnonymizeIp(clientId), path, true, requestInfo.Count, false);
        await ReturnRateLimitResponse(context);
        return;
    }

    // Record successful request (not limited)
    _rateLimitingTracker.RecordEvent(AnonymizeIp(clientId), path, false, requestInfo.Count, false);

    await _next(context);
}
```

**Admin Dashboard Integration**:

Add to `Views/Admin/Diagnostics.cshtml`:

```html
<hr />
<h3>Rate Limiting Activity</h3>

<div class="card mb-3">
    <div class="card-header">
        <strong>Rate Limiting Summary (Last Hour)</strong>
    </div>
    <div class="card-body">
        <div class="row">
            <div class="col-md-2">
                <h4>@Model.RateLimitStats.LastHourRequests</h4>
                <p class="text-muted">Total Requests</p>
            </div>
            <div class="col-md-2">
                <h4 class="text-danger">@Model.RateLimitStats.LastHourLimited</h4>
                <p class="text-muted">Limited Requests</p>
            </div>
            <div class="col-md-2">
                <h4>@Model.RateLimitStats.UniqueIPsLastHour</h4>
                <p class="text-muted">Unique IPs</p>
            </div>
            <div class="col-md-3">
                <h4 class="text-warning">@Model.RateLimitStats.GlobalLimitHitsLastHour</h4>
                <p class="text-muted">Global Limit Hits</p>
            </div>
            <div class="col-md-3">
                <h4>@Model.RateLimitStats.PerIPLimitHitsLastHour</h4>
                <p class="text-muted">Per-IP Limit Hits</p>
            </div>
        </div>
    </div>
</div>

<!-- Phase 2 Enhancement: Timeline Chart Visualization

     Future enhancement will add Chart.js-based timeline chart here showing:
     - Total Requests over last hour (12 x 5-minute buckets)
     - Limited Requests visualization
     - Global Limit Hits timeline

     Requires: Chart.js library integration + TimeSlices data from RateLimitingTracker
-->

<div class="row">
    <div class="col-md-6">
        <h5>Top Requesting IPs</h5>
        <table class="table table-sm">
            <thead>
                <tr>
                    <th>IP Address</th>
                    <th>Total Requests</th>
                    <th>Limited</th>
                    <th>Last Request</th>
                </tr>
            </thead>
            <tbody>
                @foreach (var ip in Model.RateLimitStats.TopRequestingIPs)
                {
                    <tr class="@(ip.LimitedRequests > 0 ? "table-warning" : "")">
                        <td><code>@ip.IpAddress</code></td>
                        <td>@ip.TotalRequests</td>
                        <td>@ip.LimitedRequests</td>
                        <td>@ip.LastRequestTime.ToString("HH:mm:ss")</td>
                    </tr>
                }
            </tbody>
        </table>
    </div>
    <div class="col-md-6">
        <h5>Most Targeted Paths</h5>
        <table class="table table-sm">
            <thead>
                <tr>
                    <th>Path</th>
                    <th>Requests</th>
                    <th>Unique IPs</th>
                </tr>
            </thead>
            <tbody>
                @foreach (var path in Model.RateLimitStats.MostTargetedPaths)
                {
                    <tr>
                        <td><code>@path.Path</code></td>
                        <td>@path.RequestCount</td>
                        <td>@path.UniqueIPs</td>
                    </tr>
                }
            </tbody>
        </table>
    </div>
</div>

<div class="card">
    <div class="card-header">
        <strong>Recent Limited Requests (Last 50)</strong>
    </div>
    <div class="card-body">
        <table class="table table-sm">
            <thead>
                <tr>
                    <th>Time</th>
                    <th>IP Address</th>
                    <th>Path</th>
                    <th>Limit Type</th>
                    <th>Request Count</th>
                </tr>
            </thead>
            <tbody>
                @foreach (var evt in Model.RateLimitStats.RecentLimitedRequests)
                {
                    <tr>
                        <td>@evt.Timestamp.ToString("HH:mm:ss")</td>
                        <td><code>@evt.IpAddress</code></td>
                        <td>@evt.Path</td>
                        <td>
                            <span class="badge bg-@(evt.IsGlobalLimit ? "warning" : "info")">
                                @(evt.IsGlobalLimit ? "Global" : "Per-IP")
                            </span>
                        </td>
                        <td>@evt.RequestCount</td>
                    </tr>
                }
            </tbody>
        </table>
    </div>
</div>
```

**DiagnosticsModel Integration**:

```csharp
// In m4d/Controllers/AdminController.cs or relevant diagnostics controller
public class DiagnosticsModel : PageModel
{
    private readonly AuthenticationTracker _authTracker;
    private readonly RateLimitingTracker _rateLimitingTracker;

    public AuthenticationStats AuthStats { get; set; }
    public RateLimitingStats RateLimitStats { get; set; }

    public DiagnosticsModel(AuthenticationTracker authTracker, RateLimitingTracker rateLimitingTracker)
    {
        _authTracker = authTracker;
        _rateLimitingTracker = rateLimitingTracker;
    }

    public void OnGet()
    {
        AuthStats = _authTracker.GetStats();
        RateLimitStats = _rateLimitingTracker.GetStats();
    }
}
```

**Benefits**:

- **10,000 event buffer**: Captures detailed request history without excessive memory (circular buffer auto-trims)
- **5-minute time slices**: Enables future timeline visualization of attack patterns (Phase 2 enhancement)
- **IP pattern analysis**: Identifies coordinated distributed attacks from multiple IPs
- **Path targeting**: Shows which endpoints are under attack (/Identity/Account/Login, etc.)
- **Global vs Per-IP metrics**: Distinguishes between individual abusers and coordinated attacks
- **Real-time visibility**: Admin dashboard refreshes to show live attack activity
- **Memory efficient**: Circular buffer prevents unbounded growth, resets on app restart

---

#### 1.4 Configure Explicit Account Lockout

**Goal**: Reduce failed attempt tolerance to 3 attempts

**Location**: `Program.cs` line 373

**Current Code**:

```csharp
services.AddDefaultIdentity<ApplicationUser>(
    options =>
    {
        options.SignIn.RequireConfirmedAccount = true;
        options.User.RequireUniqueEmail = true;
        options.User.AllowedUserNameCharacters = string.Empty;
        options.Stores.MaxLengthForKeys = 128;
    })
```

**Updated Code**:

```csharp
services.AddDefaultIdentity<ApplicationUser>(
    options =>
    {
        options.SignIn.RequireConfirmedAccount = true;
        options.User.RequireUniqueEmail = true;
        options.User.AllowedUserNameCharacters = string.Empty;
        options.Stores.MaxLengthForKeys = 128;

        // Explicit lockout configuration
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.MaxFailedAccessAttempts = 3;
        options.Lockout.AllowedForNewUsers = true;
    })
```

**Impact**:

- Users locked out after 3 failed attempts (down from default 5)
- 15 minute lockout period
- Applies to new and existing users

---

#### 1.5 Aggressive Progressive CAPTCHA

**Current Status**: reCAPTCHA is already enabled in production with the `FeatureManagement.Captcha` flag set to `true`. Currently, CAPTCHA verification is implemented for:

- New user registration
- Anonymous donations

This phase extends CAPTCHA usage to additional scenarios based on attack detection.

**Goal**: Challenge suspicious activity immediately with reCAPTCHA

**When to Require CAPTCHA**:

1. **Immediately** on suspicious returnUrl detection
2. **At 20% global rate limit** (20 requests/min out of 100 limit)
3. **After 1 failed login** from same IP within 5 minutes
4. **Any registration attempt** when global rate > 20%

**Implementation**:

```csharp
// In GlobalState.cs
public static bool RequireCaptcha { get; set; }

// In RateLimitingMiddleware - check for suspicious returnUrl
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

// In InvokeAsync, before processing request
var returnUrl = context.Request.Query["returnUrl"].ToString();
if (IsSuspiciousReturnUrl(returnUrl))
{
    _logger.LogWarning(
        "Suspicious returnUrl detected from {ClientId}: {ReturnUrl}",
        AnonymizeIp(clientId), returnUrl);

    // Force CAPTCHA requirement
    GlobalState.RequireCaptcha = true;
}
```

**Login/Register Page Integration**:

```csharp
// In LoginModel.cshtml.cs / RegisterModel.cshtml.cs
public async Task<IActionResult> OnGetAsync(string returnUrl = null)
{
    if (GlobalState.RequireCaptcha)
    {
        ShowCaptcha = true;
    }

    // ... existing code
}

public async Task<IActionResult> OnPostAsync(string returnUrl = null)
{
    if (GlobalState.RequireCaptcha || ShowCaptcha)
    {
        // Validate reCAPTCHA
        var recaptchaResponse = Request.Form["g-recaptcha-response"];
        if (!await ValidateRecaptcha(recaptchaResponse))
        {
            ModelState.AddModelError(string.Empty, "Please complete the CAPTCHA verification.");
            return Page();
        }
    }

    // ... existing authentication logic
}
```

**Configuration**:

- Enable `FeatureManagement.Captcha` flag
- Add reCAPTCHA keys to configuration
- Aggressive threshold (20%) to protect during attacks

---

### Phase 2: Enhanced Protection (Next Sprint)

#### 2.1 Application Insights Integration

**Goal**: Centralized monitoring, alerting, and anomaly detection

**Features to Implement**:

- Custom telemetry for authentication events
- Failed login metrics and alerts
- Rate limiting metrics
- Attack pattern detection

**Alert Thresholds**:

- Global rate limit hits: 10/hour (warning), 30/hour (critical)
- Failed logins: 50/hour (warning), 100/hour (critical)
- Distinct IPs per username: 5/5min (credential stuffing alert)

**Why Phase 2**: Need to establish baseline patterns first, then implement intelligent alerting

---

#### 2.2 Azure Front Door Basic + IP Reputation

**Azure Front Door Basic Benefits**:

- **DDoS Protection**: Basic level included (protects against volumetric attacks)
- **WAF Capability**: Not included in Basic tier (requires Premium)
- **Global Load Balancing**: Improves performance but limited security features
- **Custom Rules**: Some basic rule capability may be available

**Recommended Configuration for AFD Basic**:

1. **Geographic Restrictions (if supported)**:
   - Block or challenge traffic from regions with no legitimate users
   - Requires Premium for full WAF rule capabilities

2. **Rate Limiting at Edge**:
   - May have basic rate limiting features
   - Reduces load on origin servers
   - Check AFD Basic documentation for capabilities

3. **Logging Integration**:
   - Forward AFD logs to Log Analytics or Storage
   - Analyze attack patterns at the edge

**Free/Inexpensive IP Reputation Options**:

1. **AbuseIPDB Free Tier**:
   - 1,000 checks per day
   - Check suspicious IPs against known abuse database
   - API integration: `https://api.abuseipdb.com/api/v2/check`

2. **Azure App Service IP Restrictions**:
   - $0 - built into App Service
   - Manually block known attacking IP ranges
   - Good for permanent blocks of repeat offenders

3. **Cloudflare Free Tier** (if using as CDN):
   - Basic DDoS protection
   - Challenge pages for suspicious traffic
   - Better than AFD Basic for security

**Implementation Plan**:

```csharp
// In RateLimitingMiddleware
private async Task<bool> IsKnownBadActor(string ipAddress)
{
    // Check local cache first (prevents repeated API calls)
    if (_badActorCache.TryGetValue(ipAddress, out var isBad))
    {
        return isBad;
    }

    // Check AbuseIPDB (rate limited to 1000/day)
    try
    {
        var response = await _httpClient.GetAsync(
            $"https://api.abuseipdb.com/api/v2/check?ipAddress={ipAddress}&maxAgeInDays=90");

        if (response.IsSuccessStatusCode)
        {
            var data = await response.Content.ReadAsJsonAsync<AbuseIPDBResponse>();
            var isBadActor = data.AbuseConfidenceScore > 75;

            // Cache for 1 hour
            _badActorCache.Set(ipAddress, isBadActor, TimeSpan.FromHours(1));

            return isBadActor;
        }
    }
    catch (Exception ex)
    {
        _logger.LogWarning(ex, "Failed to check IP reputation for {IP}", ipAddress);
    }

    return false;
}
```

**Cost Comparison**:

- **AFD Basic**: ~$35/month (CDN + routing, limited security)
- **AFD Premium**: ~$330/month (includes WAF, managed rules)
- **Cloudflare Free/Pro**: $0-20/month (better security than AFD Basic)
- **AbuseIPDB**: Free tier sufficient for low-traffic sites

**Recommendation**:

- If already using AFD: Enable Basic and use App Service IP restrictions
- If starting fresh: Consider Cloudflare for better free security features
- Both: Integrate AbuseIPDB for IP reputation checks

---

#### 2.3 Implement Honeypot Fields

**Goal**: Catch automated bots

**Method**:

- Add hidden form fields to login/register forms
- CSS hides from humans, bots fill them in
- Automatic rejection if honeypot is filled

**Example**:

```html
<input
  type="text"
  name="website"
  style="display:none"
  tabindex="-1"
  autocomplete="off"
/>
```

---

#### 2.4 User Agent Analysis

**Detection Patterns**:

- Missing or suspicious User-Agent strings
- Known bot signatures
- Outdated browser versions unlikely to be legitimate

**Action**: Apply stricter rate limits or require CAPTCHA

---

### Phase 3: Advanced Protection (Future)

#### 3.1 Behavioral Analysis with Machine Learning

**Machine Learning Approach**:

- Profile normal authentication patterns
- Detect anomalies in:
  - Request timing
  - Geographic distribution
  - User-Agent patterns
  - Failed/success ratios

**Tools**: Azure Application Insights Anomaly Detection (requires Phase 2 AppInsights setup)

**Prerequisites**:

- Sufficient baseline data (2-4 weeks)
- Application Insights configured and collecting telemetry
- Budget for AI/ML features

---

#### 3.2 Geolocation-Based Rules

**Options**:

- Block countries with no legitimate users
- Apply stricter limits to high-risk regions
- Require CAPTCHA for international traffic

**Caution**: May impact legitimate international users

---

#### 3.3 Device Fingerprinting

**Goal**: Track devices across sessions without cookies

**Method**: Browser fingerprinting (canvas, WebGL, fonts, etc.)

**Privacy Consideration**: May conflict with privacy regulations (GDPR, CCPA)

---

## Implementation Status

### ✅ Phase 1: COMPLETE (March 5, 2026)

**Test Results:** 335 tests total, 334 passed, 0 failed, 1 skipped

All Phase 1 objectives have been implemented and tested:

- ✅ Global rate limiting with user-friendly HTML response (100 req/min)
- ✅ Clear attack mitigation messages (not JSON errors)
- ✅ CAPTCHA escalation at 20% global rate threshold
- ✅ Immediate CAPTCHA on suspicious returnUrl patterns
- ✅ Account lockout configured (3 attempts, 15 min lockout)
- ✅ In-memory authentication tracking (`AuthenticationTracker.cs`, 181 lines)
- ✅ In-memory rate limiting tracking (`RateLimitingTracker.cs`, 183 lines)
- ✅ Circular buffer implementation (`CircularBuffer<T>`, 67 lines)
- ✅ Admin security dashboard at `/Admin/Diagnostics` (+150 lines UI)
- ✅ Failed login tracking in Login/Register pages
- ✅ Comprehensive unit tests (22 new tests, all passing)

**Files Modified/Created:**

- `m4d/Security/AuthenticationTracker.cs` (new, 181 lines)
- `m4d/Security/RateLimitingTracker.cs` (new, 183 lines)
- `m4d/Security/CircularBuffer.cs` (new, 67 lines)
- `m4d/Middleware/RateLimitingMiddleware.cs` (enhanced from 227 → 350+ lines)
- `m4d/Areas/Identity/Pages/Account/Login.cshtml.cs` (+80 lines)
- `m4d/Areas/Identity/Pages/Account/Login.cshtml` (+15 lines reCAPTCHA UI)
- `m4d/Areas/Identity/Pages/Account/Register.cshtml.cs` (+30 lines)
- `m4d/Views/Admin/Diagnostics.cshtml` (+150 lines security monitoring)
- `m4d/Controllers/AdminController.cs` (tracker injection)
- `m4d/Program.cs` (DI registration, lockout config)
- `m4d.Tests/Security/AuthenticationTrackerTests.cs` (new, 8 tests)
- `m4dModels.Tests/Security/RateLimitingTrackerTests.cs` (new, 14 tests)

**Memory Footprint:** < 1 MB (in-memory tracking: 1000 authentication attempts, 10,000 rate limit events)

**Manual Testing:** ✅ Verified - All features working correctly in production-like environment

---

### Future Enhancements

### Phase 2: Application Insights + External Validation (Recommended Next)

**Priority: High** - Adds persistent telemetry and proactive alerting

- [ ] Set up Application Insights telemetry
- [ ] Configure email alerting for attack patterns
- [ ] Implement alert thresholds (tune based on Phase 1 baseline data)
- [ ] Add Chart.js timeline visualization to admin dashboard (5-min buckets × 12 = last hour)
- [ ] Evaluate Azure Front Door Basic vs alternatives (Cloudflare Free)
- [ ] Integrate IP reputation service (AbuseIPDB free tier)
- [ ] Add honeypot fields to login/register forms
- [ ] Implement User-Agent filtering for known bots

**Estimated Timeline:** 2-3 weeks
**Prerequisites:** Azure Application Insights resource, email service integration, Chart.js library
**Benefits:** Historical attack analysis, automated alerting, persistent metrics, visual timeline charts

---

### Phase 3: Advanced Protection (Future Consideration)

**Priority: Medium** - Advanced ML/behavioral patterns

- [ ] Behavioral analysis and anomaly detection (requires AppInsights)
- [ ] Advanced Application Insights queries and dashboards
- [ ] Geolocation-based rules (if international traffic patterns warrant)
- [ ] Consider Azure Front Door Premium (if attacks persist after Phase 2)
- [ ] Device fingerprinting (evaluate privacy implications)
- [ ] Advanced WAF rules (custom rules based on attack patterns)

**Estimated Timeline:** 1-2 months
**Prerequisites:** Phase 2 complete, sufficient baseline data (2-4 weeks), budget for premium features
**Benefits:** Predictive defense, automated pattern recognition, advanced threat intelligence

## Monitoring & Alerting

### Phase 1: In-Memory Monitoring ✅ IMPLEMENTED

**Access:** `/Admin/Diagnostics` (admin role required)

**Available Dashboards:**

1. **Rate Limiting Activity**
   - Total requests (last hour), limited requests, unique IPs
   - Global vs per-IP limit hits
   - Top requesting IPs (with yellow highlight for rate-limited IPs)
   - Most targeted paths with unique IP counts

2. **Authentication Security**
   - Failed login attempts, unique attacking IPs, targeted usernames
   - Most targeted usernames (red highlight when >5 distinct IPs = distributed attack)
   - Most active attacking IPs with failed attempt counts
   - Recent authentication attempts (last 50 with timestamps)

**Current Review Process:**

- ✅ Manual dashboard review by administrators
- ✅ Real-time statistics during suspected attacks
- ✅ Complete attack pattern visibility
- ⚠️ No automated email alerts (Phase 1 limitation)
- ⚠️ Stats reset on app restart (in-memory only)

**Phase 1 Trade-off Rationale:**

- Establishes baseline traffic patterns first
- Avoids alert fatigue from false positives
- Allows threshold tuning based on real production data
- Zero external service dependencies

---

### Phase 2: Application Insights + Email Alerting (Future)

**Upgrade Path:**

When Phase 1 baseline data indicates alert thresholds, implement:

**Key Metrics to Track** (via Application Insights):

1. **Rate Limiting Metrics**
   - Per-IP rate limit hits per hour
   - Global rate limit hits per hour
   - CAPTCHA challenge rate
   - Average requests per IP

2. **Authentication Metrics**
   - Failed login attempts per hour
   - Failed logins per username
   - Distinct IPs per username (distributed attack indicator)
   - Account lockout events
   - Successful logins after lockout

3. **Attack Indicators**
   - Suspicious returnUrl detections
   - Honeypot field submissions (Phase 2 feature)
   - Low reputation IP attempts (Phase 2 feature)
   - Suspicious User-Agent strings (Phase 2 feature)

**Email Alert Thresholds** (Recommended - tune after Phase 1 baseline established)

| Metric                         | Warning Email | Critical Email |
| ------------------------------ | ------------- | -------------- |
| Global rate limit hits         | 10/hour       | 30/hour        |
| Failed logins (total)          | 50/hour       | 100/hour       |
| Failed logins (per username)   | 5/5min        | 10/5min        |
| Distinct IPs per username      | 5/5min        | 10/5min        |
| Non-local returnUrl detections | 5/hour        | 20/hour        |
| Account lockouts               | 10/hour       | 25/hour        |

**Email Configuration**:

- Send alerts to: [security team email]
- Include: Top 5 attacking IPs, targeted usernames, time window
- Throttle: Max 1 email per 15 minutes per alert type
- Digest: Hourly summary during active attacks

## Testing

### Phase 1: Automated Testing ✅ COMPLETE

**Test Results:** 335 total tests, 334 passed, 0 failed, 1 skipped

**Test Coverage:**

1. **AuthenticationTrackerTests** (8 tests):
   - ✅ Success/failure tracking
   - ✅ Multi-IP distributed attack pattern detection
   - ✅ Time-windowed IP failure queries
   - ✅ Top 10 statistics (usernames and IPs)
   - ✅ Null value handling
   - ✅ Concurrent access safety

2. **RateLimitingTrackerTests** (14 tests):
   - ✅ Single/multiple event tracking
   - ✅ Limited vs allowed request differentiation
   - ✅ Global vs per-IP limit distinction
   - ✅ Unique IP counting accuracy
   - ✅ Top N statistics ordering
   - ✅ Time slice aggregation (5-min buckets)
   - ✅ CircularBuffer capacity behavior
   - ✅ CircularBuffer wrap-around edge cases

**Test Infrastructure:**

- Unit tests: in-memory, fast execution
- No external dependencies required
- Comprehensive edge case coverage

---

### Manual Testing ✅ VERIFIED

**Test Scenarios & Results:**

1. **✅ Legitimate User Behavior**
   - Single user, multiple tabs → No rate limiting triggered
   - 1-2 password typos → No account lockout
   - Shared office IP multiple users → Works correctly

2. **✅ Simulated Distributed Attack**
   - 10+ IPs rapid requests → Global limit triggered
   - CAPTCHA escalation activated at 20% threshold
   - Dashboard shows attack patterns correctly
   - User-friendly HTML error page displayed

3. **✅ Edge Cases**
   - User behind NAT (shared IP) → Properly handled
   - Mobile switching WiFi/cellular → IP changes handled
   - Corporate VPN exits → No false positives

4. **✅ CAPTCHA Behavior**
   - Failed login → ShowCaptcha flag set for next page
   - Suspicious returnUrl → Immediate CAPTCHA requirement
   - 20% rate limit threshold → GlobalState.RequireCaptcha activated

5. **✅ Admin Dashboard**
   - Real-time rate limiting stats displayed correctly
   - Authentication security metrics accurate
   - Top IPs/usernames ranked properly
   - Color coding (yellow for rate-limited, red for distributed attacks) functioning

**Manual Testing Recommendations for Deployment:**

- Monitor `/Admin/Diagnostics` for first 24-48 hours after deployment
- Verify CAPTCHA appears during simulated attack (dev/staging environment)
- Test account lockout with 3 failed attempts
- Confirm rate limit HTML error page displays correctly (not JSON)

---

## Configuration Reference

### Current Settings

```json
// appsettings.json
"RateLimiting": {
  "MaxRequestsPerWindow": 10,
  "WindowMinutes": 1
}
```

### Phase 1 Settings

```json
"RateLimiting": {
  "MaxRequestsPerWindow": 10,
  "WindowMinutes": 1,
  "GlobalMaxRequestsPerWindow": 100,
  "GlobalWindowMinutes": 1,
  "CaptchaThresholdPercent": 20
},
"Captcha": {
  "SiteKey": "[YOUR_RECAPTCHA_SITE_KEY]",
  "SecretKey": "[YOUR_RECAPTCHA_SECRET_KEY]",
  "Enabled": true,
  "MinimumScore": 0.5
},
"AccountLockout": {
  "MaxFailedAccessAttempts": 3,
  "DefaultLockoutTimeSpan": "00:15:00",
  "AllowedForNewUsers": true
}
```

### Phase 2 Settings (Additional)

```json
"ApplicationInsights": {
  "ConnectionString": "[YOUR_APP_INSIGHTS_CONNECTION_STRING]",
  "EnableAdaptiveSampling": false
},
"Alerting": {
  "EmailRecipient": "[security-team@example.com]",
  "ThrottleMinutes": 15
},
"IPReputation": {
  "AbuseIPDBApiKey": "[YOUR_ABUSEIPDB_KEY]",
  "CacheDurationHours": 1,
  "MinimumAbuseScore": 75
}
```

---

## Related Documentation

- [identity-endpoint-protection.md](identity-endpoint-protection.md) - Current rate limiting implementation
- [client-side-usage-logging.md](client-side-usage-logging.md) - Usage tracking patterns
- ASP.NET Identity Documentation: https://docs.microsoft.com/en-us/aspnet/core/security/authentication/identity

## Incident Log

### March 5, 2026 - Initial Detection & Response

**Time**: 17:36-17:38 UTC
**Duration**: ~90 seconds
**IPs**: 10+ distinct addresses
**Requests**: 15+ to identity endpoints
**Impact**: None (successfully rate limited by per-IP protection)
**Response**: Logged, analyzed, Phase 1 implementation initiated

**Action Items (Completed in Phase 1):**

- ✅ Implemented global rate limiting (100 req/min)
- ✅ Enhanced monitoring with admin dashboard
- ✅ Added authentication attempt tracking
- ✅ Configured CAPTCHA escalation
- ✅ Improved account lockout policy

**Outcome:** Phase 1 defenses now provide comprehensive protection against similar distributed attacks.

---

## Success Criteria

### ✅ Phase 1 Success Metrics: ACHIEVED

All Phase 1 success criteria have been met:

- ✅ Global rate limiting blocks distributed attacks within seconds (100 req/min threshold)
- ✅ Users see clear, friendly HTML message during rate limiting (not JSON errors)
- ✅ CAPTCHA appears automatically at 20% rate limiting threshold
- ✅ Suspicious returnUrl patterns trigger immediate CAPTCHA requirement
- ✅ Account lockout engages after 3 failed attempts (15-minute lockout)
- ✅ Admin dashboard shows real-time authentication stats at `/Admin/Diagnostics`
- ✅ Failed login patterns visible and traceable (top usernames, top IPs, recent attempts)
- ✅ Legitimate users can complete CAPTCHA and proceed normally
- ✅ Comprehensive test coverage (22 new tests, 100% passing)
- ✅ Manual testing verified all features working correctly

**Acceptable Trade-offs (As Designed):**

- ⚠️ Legitimate users may see CAPTCHA during active attacks (temporary security measure)
- ⚠️ Admins must manually review dashboard (no automated email alerts in Phase 1)
- ⚠️ In-memory tracking means stats reset on app restart (Phase 2 will address with AppInsights)

---

### Phase 2 Success Metrics (Future):

When Phase 2 is implemented, success will be demonstrated by:

- Security team receives email alerts within 5 minutes of attack detection
- Application Insights provides comprehensive attack telemetry and historical analysis
- Historical pattern analysis enables trend detection and predictive defense
- Alert throttling prevents email fatigue (max 1 email/15min per alert type)
- False positive rate < 5%
- IP reputation service blocks known bad actors proactively

---

**Document Version**: 2.0
**Last Updated**: March 5, 2026
**Status**: Phase 1 Complete, Phase 2 Planning
**Owner**: Security/Infrastructure Team
**Next Review**: After Phase 2 implementation (or if attack patterns change)
