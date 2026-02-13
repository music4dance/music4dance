# Client-Side Usage Logging Architecture

## Implementation Status

? **Phase 1-2 COMPLETE:** Client-side tracking and API endpoint implemented
? **Phase 3 COMPLETE:** Integration with Razor Pages
? **Phase 4 PENDING:** Deployment with feature flag

**Test Coverage:** 27/27 tests passing (100%)

- ? Client: 19 tests (useUsageTracking composable)
- ? Server: 8 tests (UsageLogApiController integration tests)
- ?? Documentation: `architecture/testing-patterns.md`

**Key Decisions:**

- ? **SendBeacon-only approach** - More reliable than fetch, simpler implementation
- ? **Bot detection in tests** - Mock user agent to avoid false bot detection
- ? **Synchronous testing** - No async timing issues, deterministic results
- ? **Integration tests** - DanceMusicTester pattern with reflection for internal dependencies
- ? **Feature flag gated** - ClientSideUsageLogging controls initialization

**Phase 3 Implementation:**

- ? Script module added to `_head.cshtml`
- ? Feature flag check (`FeatureFlags.ClientSideUsageLogging`)
- ? **Path exclusions (case-insensitive):** `/identity/`, `/api/`
- ? Imports composable from Vite build output (`/vclient/`)
- ? Initializes with server configuration from `menuContext`
- ? Tracks page view on load
- ? Passes authentication state (userName, isAuthenticated)
- ? Includes XSRF token for API calls

---

## 1. Executive Summary

This document outlines the migration of usage logging from server-side middleware (in `DanceMusicController.OnActionExecutionAsync`) to client-side JavaScript with deferred API reporting. This change is **required** for Azure Front Door caching to work correctly, as cached responses bypass the server-side middleware entirely.

**Related Documents:**

- [Azure Front Door Implementation](./fromt-door-implementation.md) - Parent architecture requiring this change

## 2. Current State Analysis

### 2.1 Server-Side Implementation

**Location:** `m4d/Controllers/DMController.cs` - `OnActionExecutionAsync` method

**Current Flow:**

1. Request arrives at controller
2. `OnActionExecutionAsync` executes BEFORE controller action
3. Generates/retrieves UsageId from cookie
4. Creates UserMetadata (checks authentication)
5. Controller action executes
6. `OnActionExecutionAsync` executes AFTER controller action
7. Checks if spider (via `SpiderManager.CheckAnySpiders`)
8. Checks if UsageLogging feature is enabled
9. Extracts filter from context
10. Creates `UsageLog` record
11. Enqueues background task to save to database
12. Updates `User.LastActive` and `User.HitCount`

**Data Captured:**

```csharp
var usage = new UsageLog
{
    UsageId = usageId,           // From cookie, format: {guid}_{count}
    UserName = user?.UserName,   // From authenticated user
    Date = time,                 // Server timestamp
    Page = page,                 // Request.Path
    Query = query,               // Request.QueryString
    Filter = filterString,       // Extracted from query/form
    Referrer = referrer,         // Request.GetTypedHeaders().Referer
    UserAgent = userAgent        // Request.Headers["User-Agent"]
};
```

**Cookie Management:**

- Cookie name: `"Usage"`
- Format: `{guid}_{visitCount}`
- Incremented on each page view
- No expiration set (session cookie)

**Bot Detection:**

```csharp
SpiderManager.CheckAnySpiders(userAgent, Configuration)
```

### 2.2 Limitations with CDN Caching

**Critical Issue:** When Azure Front Door caches a response, the server-side `OnActionExecutionAsync` **never executes** for cached requests. This means:

? No usage tracking for anonymous cached pages
? No user activity updates (`LastActive`, `HitCount`)
? Inaccurate analytics and user behavior data
? Cannot distinguish between real users and bots on cached content

**Current Scope:**

- Only tracks MVC controller pages (classes inheriting from `DanceMusicController`)
- Does NOT track Razor Pages (e.g., Identity pages like `/Identity/Account/Login`)
- Does NOT track static assets
- Does NOT track API-only endpoints

## 3. Problem Statement

### 3.1 Requirements

**Must Have:**

1. ? Track page views for both cached and uncached responses
2. ? Maintain user session tracking across visits
3. ? Filter bot traffic on client-side
4. ? Reduce server load by batching/throttling requests
5. ? Preserve data fidelity (same information as current implementation)
6. ? Work with Azure Front Door caching
7. ? Handle authenticated and anonymous users
8. ? Maintain privacy compliance (GDPR)
9. ? **Graceful degradation if API fails** - Don't break site functionality

**Should Have:**

1. ?? Configurable throttling (n pages before first send for anonymous users, default n=3)
2. ?? Configurable batch size for beacon sends (default m=5)
3. ?? Persistent queue with visit count tracking (useful for nag modals, usage analytics)
4. ?? Use SendBeacon API for reliable end-of-session tracking

**Could Have (Future Enhancements):**

1. ?? Shared client/server bot detection patterns (via configuration API)
2. ?? Client-side analytics aggregation
3. ?? Offline support with sync on reconnect
4. ?? Real-time dashboard updates (SignalR)
5. ?? Client-side A/B testing integration

### 3.2 Constraints

1. **Privacy:** Must comply with GDPR and cookie consent
2. **Performance:** Must not impact page load times
3. **Reliability:** Must handle network failures gracefully
4. **Security:** API endpoint must prevent abuse
5. **Compatibility:** Must work in all supported browsers
6. **Migration:** Must run alongside server-side logging during transition

## 4. Proposed Solution

### 4.1 Architecture Overview

```text
???????????????????????????????????????????????????????????????
?                    Client Browser                            ?
?                                                              ?
?  ??????????????????????????????????????????????????????    ?
?  ?  Vue Application (SPA)                              ?    ?
?  ?                                                      ?    ?
?  ?  ????????????????????????????????????????????????  ?    ?
?  ?  ?  Usage Tracker (Composable)                  ?  ?    ?
?  ?  ?  - Detects page navigation                   ?  ?    ?
?  ?  ?  - Filters bots (client-side)               ?  ?    ?
?  ?  ?  - Generates/loads UsageId from localStorage?  ?    ?
?  ?  ?  - Queues events locally                     ?  ?    ?
?  ?  ?  - Sends batch after n pages                 ?  ?    ?
?  ?  ????????????????????????????????????????????????  ?    ?
?  ?                        ?                             ?    ?
?  ?                        ? (when threshold reached)    ?    ?
?  ?                        ?                             ?    ?
?  ?  ????????????????????????????????????????????????  ?    ?
?  ?  ?  API Client (Axios/Fetch)                    ?  ?    ?
?  ?  ?  - Async POST to /api/usage                  ?  ?    ?
?  ?  ?  - Fire-and-forget (no await)                ?  ?    ?
?  ?  ?  - Error logging only                        ?  ?    ?
?  ?  ????????????????????????????????????????????????  ?    ?
?  ??????????????????????????????????????????????????????    ?
?                                                              ?
?  ??????????????????????????????????????????????????????    ?
?  ?  localStorage                                       ?    ?
?  ?  - usageId: "{guid}"                               ?    ?
?  ?  - usageQueue: [{page, time, ...}, ...]          ?    ?
?  ?  - usageCount: number                              ?    ?
?  ??????????????????????????????????????????????????????    ?
???????????????????????????????????????????????????????????????
                            ?
                            ? POST /api/usage/batch
                            ?
???????????????????????????????????????????????????????????????
?                    App Service (Origin)                      ?
?                                                              ?
?  ??????????????????????????????????????????????????????    ?
?  ?  UsageLogController (API)                          ?    ?
?  ?  - [AllowAnonymous]                                ?    ?
?  ?  - Rate limiting (per IP/UsageId)                  ?    ?
?  ?  - Validates payload                               ?    ?
?  ?  - Enqueues to background task                     ?    ?
?  ??????????????????????????????????????????????????????    ?
?                        ?                                     ?
?                        ?                                     ?
?  ??????????????????????????????????????????????????????    ?
?  ?  Background Task Queue                              ?    ?
?  ?  - Batch insert to UsageLog table                  ?    ?
?  ?  - Update User.LastActive, User.HitCount           ?    ?
?  ?  - Error handling and retry                        ?    ?
?  ??????????????????????????????????????????????????????    ?
???????????????????????????????????????????????????????????????
```

### 4.2 Data Flow

```text
User loads page (cached by Front Door)
    ?
Vue app initializes
    ?
Usage tracker composable activates
    ?
Load or generate UsageId from localStorage
    ?
Detect if bot (user-agent patterns, behavior)
    ?
If not bot:
    ?
    Capture page view event
        - Page URL
        - Timestamp
        - Referrer
        - User-Agent
        - Filter (from URL query)
        - Authentication state
    ?
    Add to local queue (localStorage)
    ?
    Increment visit counter
    ?
    Determine send strategy:

    IF authenticated user:
        ? Threshold: 1 (hardcoded - always send)
        ? Batch size: 1 (configurable, start at 1, increase with confidence)
        ? Send immediately when queue reaches batch size
        ? Mark last sent index in queue

    ELSE IF anonymous user:
        ? Threshold: 3 (configurable - wait for engagement)
        ? Batch size: 5 (configurable)
        ? IF visit counter < threshold: Queue only (don't send)
        ? IF visit counter >= threshold AND queue >= batch size:
            ? Send batch of 5 events
            ? Mark last sent index in queue

    ?
    On page unload (visibilitychange/pagehide):
        ? Use SendBeacon API to send unsent events (since last sent index)
        ? For anonymous: Only if visit counter >= threshold
        ? For authenticated: Always send remaining events
        ? SendBeacon has 64KB size limit - send what fits
```

**Key Strategy:**

- **Authenticated users:** Threshold=1 (immediate), BatchSize=1 (start conservative)
- **Anonymous users:** Threshold=3 (wait for engagement), BatchSize=5
- **Queue management:** Track last sent index, never clear history (for analytics)
- **SendBeacon:** Reliable delivery of remaining events on page unload

````

## 5. Technical Design

### 5.1 Client-Side Components

#### 5.1.1 Usage Tracker Composable (? IMPLEMENTED)

**File:** `m4d/ClientApp/src/composables/useUsageTracking.ts`

**Status:** ? Complete - 19/19 tests passing

**Implementation Notes:**
- Uses **sendBeacon exclusively** (no fetch API)
- Synchronous operation (simpler, more reliable)
- Bot detection via user agent patterns and webdriver detection
- Queue management with lastSentIndex tracking
- Smart batching (authenticated: immediate, anonymous: threshold-based)
- SendBeacon for page unload (visibilitychange + pagehide events)

**File:** `m4d/ClientApp/src/composables/useUsageTracking.ts`

```typescript
interface UsageEvent {
  usageId: string;
  timestamp: number; // Unix timestamp
  page: string;
  query: string;
  referrer: string | null;
  userAgent: string;
  filter: string | null;
  userName: string | null; // From authentication
  isAuthenticated: boolean;
}

interface UsageQueue {
  events: UsageEvent[];
  lastSentIndex: number; // Track last sent event for batching
}

interface UsageTrackerConfig {
  enabled: boolean;
  // Anonymous user settings
  anonymousThreshold: number; // Pages before sending (default: 3)
  anonymousBatchSize: number; // Events per batch (default: 5)
  // Authenticated user settings
  authenticatedBatchSize: number; // Events per batch (default: 1, increase with confidence)
  // Queue management
  maxQueueSize: number; // Prevent localStorage overflow (default: 100)
  apiEndpoint: string; // "/api/usagelog/batch"
  botPatterns: RegExp[]; // Client-side bot detection
}

export function useUsageTracking(config?: Partial<UsageTrackerConfig>) {
  // Load or generate UsageId
  // Track page views (manual call, no Vue Router)
  // Queue events with lastSentIndex tracking
  // Send batches based on user type
  // Use SendBeacon on page unload for remaining events
  // Handle errors gracefully
}
````

**Key Responsibilities:**

1. **Session Management:**
   - Generate UUID v4 for new sessions
   - Store in `localStorage.usageId`
   - Persist across page reloads
   - Track visit count in `localStorage.usageCount`

2. **Bot Detection:**
   - Check user-agent against patterns
   - Monitor rapid navigation (< 100ms between pages)
   - Check for headless browser indicators
   - Flag suspicious localStorage access patterns

3. **Event Queueing with Index Tracking:**
   - Store events in `localStorage.usageQueue`
   - Track `lastSentIndex` to identify unsent events
   - Always keep queue for analytics (nag modals, etc.)
   - Limit queue size to prevent overflow (trim oldest)
   - Never clear queue entirely (keep for history)

4. **Smart Batching Strategy:**
   - **Authenticated users:**
     - Threshold: 1 (hardcoded - always send)
     - Batch size: 1 (configurable, start conservative)
     - Send when queue has >= batchSize unsent events
   - **Anonymous users:**
     - Threshold: 3 (configurable - wait for engagement)
     - Batch size: 5 (configurable)
     - Don't send until visit counter >= threshold
     - Send when queue has >= batchSize unsent events

5. **SendBeacon Integration:**
   - Use `visibilitychange` event (more reliable than `beforeunload`)
   - Fallback to `pagehide` event for iOS Safari
   - Send only unsent events (from lastSentIndex to end)
   - Respect 64KB size limit (truncate if needed)
   - For anonymous: Only send if visit counter >= threshold
   - For authenticated: Always send remaining events
   - Cannot await result (fire-and-forget)

6. **Error Handling:**
   - Log errors to console (dev mode)
   - **Graceful degradation:** If API fails, don't break site
   - Don't retry failed sends (fire-and-forget)
   - Update lastSentIndex only on successful send
   - Clear problematic events from queue if persistent errors

#### 5.1.2 Page Load Integration (? IMPLEMENTED)

**File:** `m4d/Views/Shared/_head.cshtml`

**Status:** ? Complete - Integrated with feature flag

**Implementation:**

```html
@* Initialize client-side usage tracking *@ @if (await
_featureManager.IsEnabledAsync(FeatureFlags.ClientSideUsageLogging)) {
<script type="module">
  import { useUsageTracking } from "/vclient/composables/useUsageTracking.js";

  // Initialize usage tracker with configuration from server
  const tracker = useUsageTracking({
    enabled: menuContext.usageTracking?.enabled ?? true,
    anonymousThreshold: menuContext.usageTracking?.anonymousThreshold ?? 3,
    anonymousBatchSize: menuContext.usageTracking?.anonymousBatchSize ?? 5,
    authenticatedBatchSize:
      menuContext.usageTracking?.authenticatedBatchSize ?? 1,
    maxQueueSize: menuContext.usageTracking?.maxQueueSize ?? 100,
    xsrfToken: menuContext.xsrfToken,
    userName: menuContext.userName || null,
    isAuthenticated: menuContext.userName && menuContext.userName.length > 0,
  });

  // Track page view on load
  tracker.trackPageView(window.location.pathname, window.location.search);
</script>
}
```

**Key Features:**

- Feature flag gated (`FeatureFlags.ClientSideUsageLogging`)
- ES6 module import from Vite build output (`/vclient/`)
- Configuration from server-side `menuContext`
- Automatic page view tracking on load
- Authentication state passed to composable
- XSRF token included for API security

**Note:** This project doesn't use Vue Router (full page loads), so tracking happens on each page load automatically.

#### 5.1.3 Bot Detection Logic

**File:** `m4d/ClientApp/src/utils/botDetection.ts`

```typescript
export function isBot(): boolean {
  const ua = navigator.userAgent.toLowerCase();

  // Check common bot patterns
  const botPatterns = [
    /bot/i,
    /crawl/i,
    /spider/i,
    /slurp/i,
    /headless/i,
    /phantom/i,
    /puppeteer/i,
    /selenium/i,
    /webdriver/i,
  ];

  if (botPatterns.some((pattern) => pattern.test(ua))) {
    return true;
  }

  // Check for headless Chrome/Firefox
  if ("__nightmare" in window || "__phantomas" in window) {
    return true;
  }

  // Check for webdriver
  if (navigator.webdriver === true) {
    return true;
  }

  return false;
}
```

**Future Enhancement (Could Have):** Share bot detection patterns between client and server via configuration API endpoint. This would allow updating patterns without redeploying client code and ensure consistency with server-side `SpiderManager`.

#### 5.1.4 SendBeacon Best Practices (from Mozilla)

**File:** `m4d/ClientApp/src/utils/sendBeacon.ts`

```typescript
/**
 * Sends remaining usage events using navigator.sendBeacon()
 * Based on Mozilla best practices: https://developer.mozilla.org/en-US/docs/Web/API/Navigator/sendBeacon
 */
export function sendRemainingEvents(
  events: UsageEvent[],
  endpoint: string,
  antiForgeryToken: string,
): boolean {
  if (events.length === 0) {
    return true;
  }

  // Construct payload
  const payload = JSON.stringify({ events });

  // Check size (64KB limit)
  const sizeInBytes = new Blob([payload]).size;
  if (sizeInBytes > 65536) {
    console.warn(
      `SendBeacon payload too large: ${sizeInBytes} bytes. Truncating...`,
    );
    // Truncate to fit (approximate - keep first N events that fit)
    const maxEvents = Math.floor((events.length * 65536) / sizeInBytes);
    return sendRemainingEvents(
      events.slice(0, maxEvents),
      endpoint,
      antiForgeryToken,
    );
  }

  // Create headers (sendBeacon doesn't support custom headers, use Blob with type)
  const headers = {
    "Content-Type": "application/json",
    RequestVerificationToken: antiForgeryToken,
  };

  // Note: sendBeacon doesn't support custom headers directly
  // Workaround: append token to URL or use FormData
  const urlWithToken = `${endpoint}?__RequestVerificationToken=${encodeURIComponent(antiForgeryToken)}`;

  try {
    const success = navigator.sendBeacon(urlWithToken, payload);
    return success;
  } catch (error) {
    console.error("SendBeacon failed:", error);
    return false;
  }
}

/**
 * Registers event listeners for page unload using recommended approach
 */
export function registerUnloadHandler(callback: () => void) {
  // Primary: visibilitychange (most reliable)
  document.addEventListener("visibilitychange", () => {
    if (document.visibilityState === "hidden") {
      callback();
    }
  });

  // Fallback: pagehide (for iOS Safari)
  window.addEventListener("pagehide", callback);

  // Note: Do NOT use 'beforeunload' or 'unload' - unreliable and deprecated
}
```

**Key Points from Mozilla Docs:**

1. **Use `visibilitychange` event** - Most reliable, fires when tab/window loses focus
2. **Fallback to `pagehide`** - For iOS Safari compatibility
3. **Avoid `beforeunload` and `unload`** - Unreliable, deprecated, may not fire
4. **64KB size limit** - Check payload size and truncate if needed
5. **Cannot await result** - Fire-and-forget, no error handling
6. **Header workaround** - sendBeacon doesn't support custom headers, append token to URL
7. **Returns boolean** - Indicates if browser accepted the request (not if it succeeded)

### 5.2 Server-Side Components

#### 5.2.1 Usage Log API Controller (? IMPLEMENTED)

**File:** `m4d/APIControllers/UsageLogApiController.cs`

**Status:** ? Complete - 8/8 integration tests passing

**Implementation Notes:**

- Inherits from `DanceMusicApiController` for consistency
- Uses `[ValidateAntiForgeryToken]` for CSRF protection
- Enqueues to background task queue (fire-and-forget)
- Returns 202 Accepted immediately
- Validates payload size (max 100 events)
- Detects authenticated users via HttpContext.User

**File:** `m4d/APIControllers/UsageLogApiController.cs`

```csharp
[ApiController]
[Route("api/[controller]")]
[ValidateAntiForgeryToken] // Prevent CSRF attacks from malicious sites
public class UsageLogApiController : DanceMusicApiController
{
    public UsageLogApiController(
        DanceMusicContext context,
        UserManager<ApplicationUser> userManager,
        ISearchServiceManager searchService,
        IDanceStatsManager danceStatsManager,
        IConfiguration configuration,
        ILogger<UsageLogApiController> logger)
        : base(context, userManager, searchService, danceStatsManager, configuration, logger)
    {
    }

    [HttpPost("batch")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> LogBatch([FromBody] UsageLogBatchRequest request)
    {
        // 1. Validate payload
        if (request?.Events == null || request.Events.Count == 0)
        {
            return BadRequest("No events provided");
        }

        if (request.Events.Count > 100)
        {
            return BadRequest("Batch size exceeds limit (100 events)");
        }

        // 2. Detect authenticated user (inheriting from DanceMusicApiController gives us access to these)
        var isAuthenticated = User?.Identity?.IsAuthenticated == true;
        var userName = isAuthenticated ? User.Identity.Name : null;

        // Optional: Get full user object if needed
        ApplicationUser authenticatedUser = null;
        if (isAuthenticated)
        {
            authenticatedUser = await UserManager.GetUserAsync(User);
        }

        // 3. Rate limit check
        var rateLimitKey = isAuthenticated ? $"user:{userName}" : $"ip:{HttpContext.Connection.RemoteIpAddress}";
        if (IsRateLimited(rateLimitKey))
        {
            return StatusCode(StatusCodes.Status429TooManyRequests, "Rate limit exceeded");
        }

        // 4. Enqueue to background task (using inherited TaskQueue from base controller)
        TaskQueue.EnqueueTask(async (serviceScopeFactory, cancellationToken) =>
        {
            try
            {
                using var scope = serviceScopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<DanceMusicContext>();

                foreach (var eventDto in request.Events)
                {
                    var usageLog = new UsageLog
                    {
                        UsageId = eventDto.UsageId,
                        UserName = userName ?? eventDto.UserName, // Server-side auth takes precedence
                        Date = DateTimeOffset.FromUnixTimeMilliseconds(eventDto.Timestamp).DateTime,
                        Page = eventDto.Page,
                        Query = eventDto.Query,
                        Filter = eventDto.Filter,
                        Referrer = eventDto.Referrer,
                        UserAgent = eventDto.UserAgent
                    };

                    await dbContext.UsageLog.AddAsync(usageLog, cancellationToken);
                }

                // Update user LastActive and HitCount if authenticated
                if (authenticatedUser != null)
                {
                    authenticatedUser.LastActive = DateTime.Now;
                    authenticatedUser.HitCount += request.Events.Count;
                }

                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to save usage log batch");
            }
        });

        // 5. Return 202 Accepted immediately (background processing)
        return Accepted();
    }

    private bool IsRateLimited(string key)
    {
        // TODO: Implement rate limiting using IMemoryCache
        // Return true if rate limit exceeded
        return false;
    }
}

public class UsageLogBatchRequest
{
    public List<UsageEventDto> Events { get; set; }
}

public class UsageEventDto
{
    [Required]
    public string UsageId { get; set; }

    [Required]
    public long Timestamp { get; set; } // Unix timestamp (client time)

    [Required]
    public string Page { get; set; }

    public string Query { get; set; }

    public string Referrer { get; set; }

    [Required]
    public string UserAgent { get; set; }

    public string Filter { get; set; }

    // Client-reported username (for validation, but server auth takes precedence)
    public string UserName { get; set; }
}
```

**Key Design Decisions:**

1.  **Inherit from DanceMusicApiController:**
    - Pattern: Same as `ServiceUserController`, `DanceEnvironmentController`, `ServiceTrackController`
    - Benefits: Access to `Database`, `UserManager`, `TaskQueue`, `Logger` properties
    - Consistency: All API controllers follow this pattern

2.  **Authentication Detection Methods:**
    - `User?.Identity?.IsAuthenticated` - Check if user is authenticated (from `ControllerBase`)
    - `User.Identity.Name` - Get username (returns null if anonymous)
    - `await UserManager.GetUserAsync(User)` - Get full `ApplicationUser` object
    - **Server-side authentication always takes precedence** over client-reported username

3.  **CSRF Protection:**
    - `[ValidateAntiForgeryToken]` attribute prevents arbitrary calls from malicious sites
    - Client must include anti-forgery token in request headers
    - **Important:** Anti-forgery token must be generated in Razor Page/View and passed to JavaScript
    - Anonymous users can still call API if token is present (token validates origin, not authentication)
    - Token retrieval pattern (in Razor Page/View):
      ```html
      @inject Microsoft.AspNetCore.Antiforgery.IAntiforgery Antiforgery
      <script>
        window.antiForgeryToken =
          "@Antiforgery.GetAndStoreTokens(HttpContext).RequestToken";
      </script>
      ```
    - Token usage pattern (in JavaScript):
      ```typescript
      fetch("/api/usagelog/batch", {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          RequestVerificationToken: window.antiForgeryToken,
        },
        body: JSON.stringify(request),
      });
      ```

4.  **Key Responsibilities:**
    - Anonymous users can still call API if token is present (token validates origin, not authentication)

5.  **Key Responsibilities:**

        public string Filter { get; set; }

    }

````

**Key Responsibilities:**

1. **Validation:**
   - Check payload size (max 100 events per batch)
   - Validate UsageId format (GUID)
   - Validate timestamps (not too old, not in future)
   - Validate URLs (must be from same domain)

2. **Rate Limiting:**
   - Per IP: 100 requests/minute
   - Per UsageId: 10 batches/minute
   - Return 429 Too Many Requests if exceeded

3. **Authentication Matching:**
   - Check if `HttpContext.User.Identity.IsAuthenticated`
   - Match UsageId to authenticated user if possible
   - Update `User.LastActive` and `User.HitCount`

4. **Background Processing:**
   - Enqueue to existing `IBackgroundTaskQueue`
   - Batch insert to `UsageLog` table
   - Handle database errors gracefully

#### 5.2.2 Rate Limiting Middleware

**File:** `m4d/Middleware/UsageLogRateLimitMiddleware.cs`

```csharp
public class UsageLogRateLimitMiddleware
{
    // Use in-memory cache (IMemoryCache) for rate limit tracking
    // Key: IP address or UsageId
    // Value: Request count in sliding window
    // Cleanup: Expire entries after 1 minute
}
````

**Configuration:**

```json
{
  "UsageLogging": {
    "RateLimit": {
      "PerIp": 100,
      "PerUsageId": 10,
      "WindowMinutes": 1
    }
  }
}
```

### 5.3 Data Model Changes

**No database schema changes required.** The existing `UsageLog` table schema already supports all required fields.

**Existing Schema:**

```csharp
public class UsageLog
{
    public int UsageLogId { get; set; }
    public string UsageId { get; set; }       // ? Already GUID-compatible
    public string UserName { get; set; }      // ? Will be matched server-side
    public DateTime Date { get; set; }        // ? Convert from client timestamp
    public string Page { get; set; }          // ? From client
    public string Query { get; set; }         // ? From client
    public string Filter { get; set; }        // ? From client
    public string Referrer { get; set; }      // ? From client
    public string UserAgent { get; set; }     // ? From client
}
```

### 5.4 Configuration

**File:** `m4d/ClientApp/src/config/usageTracking.ts`

```typescript
export const usageTrackingConfig = {
  enabled: import.meta.env.VITE_USAGE_TRACKING_ENABLED !== "false",
  // Anonymous user settings
  anonymousThreshold: parseInt(
    import.meta.env.VITE_USAGE_ANONYMOUS_THRESHOLD || "3",
  ),
  anonymousBatchSize: parseInt(
    import.meta.env.VITE_USAGE_ANONYMOUS_BATCH_SIZE || "5",
  ),
  // Authenticated user settings (threshold is hardcoded to 1)
  authenticatedBatchSize: parseInt(
    import.meta.env.VITE_USAGE_AUTHENTICATED_BATCH_SIZE || "1",
  ),
  // Queue management
  maxQueueSize: parseInt(import.meta.env.VITE_USAGE_MAX_QUEUE_SIZE || "100"),
  apiEndpoint: "/api/usagelog/batch",
  antiForgeryToken: (window as any).antiForgeryToken, // From Razor Page
  debug: import.meta.env.DEV,
};
```

**File:** `m4d/ClientApp/.env.development`

```text
VITE_USAGE_TRACKING_ENABLED=true
VITE_USAGE_ANONYMOUS_THRESHOLD=2
VITE_USAGE_ANONYMOUS_BATCH_SIZE=5
VITE_USAGE_AUTHENTICATED_BATCH_SIZE=1
VITE_USAGE_MAX_QUEUE_SIZE=100
```

**File:** `m4d/ClientApp/.env.production`

```text
VITE_USAGE_TRACKING_ENABLED=true
VITE_USAGE_ANONYMOUS_THRESHOLD=3
VITE_USAGE_ANONYMOUS_BATCH_SIZE=5
VITE_USAGE_AUTHENTICATED_BATCH_SIZE=1
VITE_USAGE_MAX_QUEUE_SIZE=100
```

**Key Configuration Points:**

1. **Authenticated users:**
   - Threshold: Hardcoded to 1 (always send after first page)
   - Batch size: Configurable, starts at 1 (conservative)
   - Increase batch size once confident (e.g., to 3 or 5)

2. **Anonymous users:**
   - Threshold: 3 pages (wait for engagement)
   - Batch size: 5 events per batch
   - Reduces server load for casual browsers

3. **SendBeacon considerations:**
   - Mozilla recommends using `visibilitychange` event
   - Fallback to `pagehide` for iOS Safari
   - 64KB size limit - approximately 50-100 events depending on data
   - Cannot await result (fire-and-forget)

````

## 6. Implementation Plan

### Phase 1 - Client-Side Foundation

**Goal:** Set up basic client-side tracking infrastructure

**Tasks:**
1. ? Create `useUsageTracking` composable
2. ? Implement localStorage management (UsageId, queue, count)
3. ? Implement bot detection logic
4. ? Add page load integration (no Vue Router needed)
5. ? Add SendBeacon integration
6. ? Add configuration management
7. ? Unit tests for composable

**Deliverables:**
- Client-side tracking code (no API calls yet)
- Unit tests
- Development mode console logging

**Testing (Automated Unit Tests):**
- ? UsageId generation and persistence
- ? Bot detection patterns
- ? Queue management (add, overflow, trim oldest)
- ? Visit count tracking
- ? Send strategy (authenticated vs. anonymous)

### Phase 2 - API Endpoint

**Goal:** Create server-side API to receive usage data

**Tasks:**
1. ? Create `UsageLogApiController`
2. ? Implement request validation
3. ? Implement rate limiting middleware
4. ? Integrate with existing `IBackgroundTaskQueue`
5. ? Add authentication matching logic
6. ? Unit tests for API controller

**Deliverables:**
- API endpoint `/api/usagelog/batch`
- Rate limiting middleware
- Unit tests

**Testing (Automated Unit Tests):**
- ? Valid/invalid payloads
- ? Rate limiting logic
- ? Authentication matching
- ? Background task enqueueing

### Phase 3 - Integration & Feature Flag

**Goal:** Connect client-side tracking to API and add feature flag

**Tasks:**
1. ? Implement API client in composable (fetch + SendBeacon)
2. ? Add smart sending logic (authenticated vs. anonymous)
3. ? Add error handling with graceful degradation
4. ? Add feature flag: `FeatureFlags.ClientSideUsageLogging`
5. ? Conditional server-side logging (disabled if feature flag on)
6. ? Ad-hoc integration testing (manual by developer)

**Deliverables:**
- Working end-to-end usage tracking
- Feature flag for toggling client/server tracking
- Error handling and logging

**Testing:**
- Ad-hoc integration tests by developer
- Test full flow (page view ? queue ? API ? database)
- Test with network failures (graceful degradation)
- Test with authenticated/anonymous users
- Compare data accuracy between client/server modes

### Phase 4 - Deployment & Monitoring

**Goal:** Deploy to production with feature flag disabled, then gradually enable

**Tasks:**
1. ? Deploy code with feature flag disabled (server-side logging active)
2. ? Enable feature flag for testing
3. ? Monitor database insert rates
4. ? Verify data accuracy
5. ? Enable feature flag for production
6. ? Remove server-side logging code from `DMController`

**Deliverables:**
- Production deployment
- Monitoring dashboard
- Documentation updates

**Testing:**
- Monitor API error rates
- Verify no data loss
- Spot-check database records for accuracy

### Estimated Timeline

- **Phase 1:** 1-2 days (client-side foundation + unit tests)
- **Phase 2:** 1-2 days (API endpoint + unit tests)
- **Phase 3:** 1-2 days (integration + feature flag + manual testing)
- **Phase 4:** 1 day (deployment + monitoring)
- **Total:** ~1 week (for hobby site with 2 developers)

## 7. Testing Strategy

### 7.1 Automated Unit Tests

**Client-Side (Vitest):**

- ? `useUsageTracking.test.ts`
  - UsageId generation and persistence
  - Queue management (add, overflow, trim oldest)
  - Visit count tracking
  - Bot detection
  - Send strategy (authenticated vs. anonymous)
  - SendBeacon integration

- ? `botDetection.test.ts`
  - Bot user-agent patterns
  - Headless browser detection
  - Webdriver detection

**Server-Side (MSTest):**

- ? `UsageLogApiControllerTests.cs`
  - Request validation
  - Rate limiting
  - Authentication matching
  - Background task enqueuing

### 7.2 Ad-Hoc Integration Testing (Manual)

**Performed by developers during Phase 3:**

- ? End-to-end flow (page view ? queue ? API ? database)
- ? Network failure handling (graceful degradation)
- ? Authentication state matching
- ? Rate limiting enforcement (via curl/Postman)
- ? Compare client-side vs. server-side data accuracy
- ? Test with various browsers and devices

**No automated load testing required** (hobby site with modest traffic)

## 8. Feature Flag Strategy

### 8.1 Feature Flag Configuration

**File:** `m4dModels/FeatureFlags.cs`

```csharp
public static class FeatureFlags
{
    public const string UsageLogging = "UsageLogging"; // Existing
    public const string ClientSideUsageLogging = "ClientSideUsageLogging"; // New
}
````

**Configuration:** `appsettings.json`

```json
{
  "FeatureManagement": {
    "UsageLogging": true,
    "ClientSideUsageLogging": false
  }
}
```

### 8.2 Server-Side Conditional Logic

**File:** `m4d/Controllers/DMController.cs`

```csharp
public override async Task OnActionExecutionAsync(
    ActionExecutingContext context, ActionExecutionDelegate next)
{
    var usageId = GetUsageId();
    var time = DateTime.Now;
    var userAgent = Request.Headers[HeaderNames.UserAgent];
    var userMetadata = await UserMetadata.Create(UserName, UserManager);
    ViewData["UserMetadata"] = userMetadata;

    _ = await next();

    // Check if client-side tracking is enabled
    if (await FeatureManager.IsEnabledAsync(FeatureFlags.ClientSideUsageLogging))
    {
        return; // Skip server-side logging
    }

    // Existing server-side logging logic...
    if (SpiderManager.CheckAnySpiders(userAgent, Configuration) ||
        !await FeatureManager.IsEnabledAsync(FeatureFlags.UsageLogging))
    {
        return;
    }

    // ... rest of usage logging code
}
```

### 8.3 Rollout Plan

**Step 1: Deploy with flag disabled**

- Client-side code deployed
- Feature flag `ClientSideUsageLogging = false`
- Server-side logging active (business as usual)

**Step 2: Enable for testing**

- Set `ClientSideUsageLogging = true` in development
- Test manually (ad-hoc integration tests)
- Verify data accuracy
- Fix any issues

**Step 3: Enable in production**

- Set `ClientSideUsageLogging = true` in production
- Monitor database insert rates
- Compare data accuracy over 1-2 days
- Rollback if issues detected (set flag to `false`)

**Step 4: Cleanup (optional)**

- Remove server-side logging code from `DMController`
- Remove `UsageId` cookie generation/reading
- Remove `GetUsageId()` method
- Update documentation

### 8.4 Rollback Plan

**Trigger:** Data accuracy < 90% OR critical errors

**Actions:**

1. Set `ClientSideUsageLogging = false` (immediate)
2. Server-side logging resumes automatically
3. Investigate root cause
4. Fix and re-test
5. Resume rollout

## 9. Migration Strategy Simplified

**No complex phased rollout** - Use feature flag toggle:

- ? Deploy code with flag disabled
- ? Test with flag enabled (development)
- ? Enable flag in production
- ? Monitor for 1-2 days
- ? Remove old code (optional)

## 10. Privacy and Compliance

### 9.1 GDPR Considerations

**Data Collected:**

- UsageId (UUID, not personally identifiable)
- Page URL (may contain user-specific routes)
- Timestamp (client local time)
- Referrer (may contain PII if external site)
- User-Agent (browser fingerprinting concern)
- Username (only if authenticated)

**Privacy Measures:**

1. **Consent:** Require cookie consent banner before tracking
2. **Anonymization:** UsageId is not linked to user until authentication
3. **Retention:** Delete usage logs after 90 days (configurable)
4. **Access:** Users can request their usage data
5. **Deletion:** Users can request deletion of usage data

### 9.2 Cookie Policy Updates

**Current:** Session cookie `"Usage"` stores UsageId

**New:** localStorage `usageId` stores UUID

**Impact:**

- localStorage is not a cookie ? No cookie banner update needed
- However, localStorage is persistent ? May require privacy policy update
- Consider: Add "Clear Tracking Data" button in user settings

### 9.3 Opt-Out Mechanism

**Implementation:**

```typescript
// User can opt-out of tracking
localStorage.setItem("usageTrackingOptOut", "true");

// Check opt-out in composable
if (localStorage.getItem("usageTrackingOptOut") === "true") {
  return; // Don't track
}
```

**UI:**

- Add toggle in user account settings
- Add "Do Not Track" browser setting respect

## 10. Monitoring and Alerting

### 10.1 Metrics to Track

**Client-Side:**

- Usage events queued per session
- Batch send success rate
- Batch send error rate (by error type)
- Average queue size
- Bot detection rate

**Server-Side:**

- API request rate (requests/sec)
- API error rate (by status code)
- Rate limit hit rate
- Database insert rate
- Background task queue depth

**Business Metrics:**

- Daily active users (by UsageId)
- Pages per session
- Session duration
- Top pages visited
- Bot traffic percentage

### 10.2 Alerts

**Critical:**

- API error rate > 5%
- Database insert failures > 1%
- Rate limit hit rate > 10%

**Warning:**

- Client-side error rate > 2%
- Queue overflow events > 1%
- Bot detection rate changes > 20%

### 10.3 Dashboards

**Development Dashboard:**

- Real-time usage events (console)
- Queue status
- API call history
- Error log

**Production Dashboard (Application Insights):**

- Usage trends (daily, weekly)
- Top pages
- User retention
- Bot traffic analysis
- Performance metrics

## 11. Security Considerations

### 11.1 API Security

**Threats:**

1. **DDoS:** Flood API with fake usage events
2. **Data Poisoning:** Submit false data to skew analytics
3. **PII Leakage:** Expose sensitive user data in logs
4. **CSRF:** Submit forged requests from other domains

**Mitigations:**

1. **Rate Limiting:** 100 req/min per IP, 10 batches/min per UsageId
2. **Validation:** Strict payload validation (size, format, content)
3. **Origin Checking:** Verify requests come from same domain
4. **Sanitization:** Strip PII from URLs and referrers
5. **Anti-CSRF:** Validate request origin headers

### 11.2 Client-Side Security

**Threats:**

1. **Script Injection:** XSS attack modifies tracking code
2. **localStorage Tampering:** User modifies UsageId or queue
3. **Bot Spoofing:** Bots bypass detection logic

**Mitigations:**

1. **CSP:** Content Security Policy prevents script injection
2. **Integrity Checking:** Validate localStorage data format
3. **Bot Detection:** Multiple signals (user-agent, behavior, timing)

### 11.3 Data Privacy

**Best Practices:**

1. **Minimal Data:** Only collect necessary fields
2. **Anonymization:** Don't log sensitive query parameters
3. **Encryption:** HTTPS for API calls (already enforced)
4. **Retention:** Auto-delete old logs (90 days)

## 12. Future Enhancements

### 12.1 Real-Time Analytics (SignalR)

**Goal:** Push usage data to dashboard in real-time

**Implementation:**

```csharp
// Server: Broadcast usage events to connected clients
await Clients.Group("Admins").SendAsync("UsageEvent", usageEvent);
```

```typescript
// Client: Subscribe to usage events
signalRConnection.on("UsageEvent", (event) => {
  updateDashboard(event);
});
```

### 12.2 Offline Support

**Goal:** Queue events when offline, sync when reconnected

**Implementation:**

```typescript
window.addEventListener("online", () => {
  usageTracker.sendQueuedEvents();
});
```

### 12.3 Client-Side Aggregation

**Goal:** Reduce API calls by aggregating events on client

**Implementation:**

```typescript
// Instead of sending individual page views, send session summary
{
  usageId: "...",
  sessionStart: 1234567890,
  sessionEnd: 1234567899,
  pagesVisited: ["/", "/dances", "/songs"],
  totalTime: 9000 // milliseconds
}
```

### 12.4 A/B Testing Integration

**Goal:** Track which variant users see

**Implementation:**

```typescript
usageTracker.trackEvent({
  type: "ab_test",
  testName: "homepage_layout",
  variant: "B",
});
```

### 12.5 Bot Behavior Analysis

**Goal:** ML model to detect sophisticated bots

**Implementation:**

- Track user behavior patterns (mouse movement, scroll, timing)
- Train model on known bot vs. human patterns
- Flag suspicious sessions for review

## 13. Documentation Updates

**At conclusion of feature work, this document should reflect:**

- ? What was implemented (mark completed sections)
- ? Any deviations from original plan
- ? Future enhancements moved to Could Have section
- ? Lessons learned

**Other documentation:**

- [ ] Add JSDoc comments to `useUsageTracking.ts`
- [ ] Add XML comments to `UsageLogApiController.cs`
- [ ] Update this architecture doc with "as-built" details

**No need to update CONTRIBUTING.md** - This is internal implementation detail

## 14. Open Questions

### 14.1 Technical Decisions

### 14.1 Resolved Technical Decisions

**Q1:** Should we use SignalR for real-time updates, or stick with REST API?

- **Decision:** ? REST API + SendBeacon (simpler), SignalR is future enhancement

**Q2:** Should we send individual events or session summaries?

- **Decision:** ? Individual events (matches current model), aggregation is future enhancement

**Q3:** Should we retry failed API calls?

- **Decision:** ? No retries (fire-and-forget with graceful degradation)

**Q4:** Should we support IE11?

- **Decision:** ? No, Vue 3 doesn't support IE11 anyway

**Q5:** Should we track API-only endpoints (e.g., `/api/search`)?

- **Decision:** ? Not initially, consider as future enhancement

**Q6:** Vue Router integration needed?

- **Decision:** ? No, project doesn't use Vue Router (full page loads)

**Q7:** Send strategy for authenticated vs. anonymous?

- **Decision:** ? Authenticated: immediate send; Anonymous: threshold-based (n=3)

**Q8:** Use SendBeacon for page unload?

- **Decision:** ? Yes, for reliable end-of-session tracking

### 14.2 Resolved Product Decisions

**Q1:** What batch threshold is optimal (n pages before send)?

- **Decision:** ? n=3 for anonymous users (configurable)

**Q2:** Should we show users their own usage data?

- **Decision:** ? Not initially, consider as future enhancement

**Q3:** Should we allow users to opt-out of tracking?

- **Decision:** ? Yes, required for privacy compliance

**Q4:** Should we track Razor Pages (Identity pages)?

- **Decision:** ? Not initially (out of scope for DMController)

**Q5:** Rollout strategy for hobby site?

- **Decision:** ? Simple feature flag toggle (not complex phased rollout)

**Q6:** Load testing needed?

- **Decision:** ? No (hobby site with modest traffic)

**Q7:** Keep queue for other uses (nag modals)?

- **Decision:** ? Yes, keep visit count and history persistent

## 15. Success Criteria

### 15.1 Functional Requirements

? Client-side tracking captures ?90% of page views (vs. server-side, accounting for bots)
? Bot detection filters known bots effectively
? API endpoint handles expected traffic without errors
? Data integrity matches server-side implementation
? No impact on page load times (< 50ms overhead)
? Graceful degradation if API fails

### 15.2 Non-Functional Requirements

? Privacy compliant (GDPR)
? Secure (no data leakage, rate-limited)
? Reliable (< 1% error rate for non-network issues)
? Maintainable (well-documented, tested)
? Scalable (handles 10x traffic growth)

### 15.3 Business Goals

? Accurate usage analytics for Azure Front Door caching
? Reduced server load (background processing)
? Improved user experience (no server-side overhead)
? Better bot detection and filtering
? Foundation for advanced analytics

## 16. Risks and Mitigation

### 16.1 Technical Risks

| Risk                                   | Probability | Impact | Mitigation                                                         |
| -------------------------------------- | ----------- | ------ | ------------------------------------------------------------------ |
| Client-side tracking misses page views | Medium      | High   | Parallel operation phase, comparison testing                       |
| API rate limiting too strict           | Low         | Medium | Monitor hit rate, adjust thresholds                                |
| localStorage quota exceeded            | Low         | Low    | Implement queue size limit (100 events)                            |
| Bot detection ineffective              | Medium      | Medium | Multiple detection signals, server-side validation                 |
| Network failures lose data             | Medium      | Low    | Acceptable (fire-and-forget with graceful degradation), log errors |

### 16.2 Business Risks

| Risk                      | Probability | Impact   | Mitigation                               |
| ------------------------- | ----------- | -------- | ---------------------------------------- |
| Data accuracy concerns    | Low         | High     | Feature flag toggle, ad-hoc testing      |
| Privacy compliance issues | Low         | Critical | Opt-out mechanism, privacy policy        |
| User confusion (opt-out)  | Low         | Low      | Clear UI, help documentation             |
| Performance degradation   | Low         | Medium   | Monitoring, rollback plan (feature flag) |

## 17. Dependencies

### 17.1 External Dependencies

- Vue 3 (already in use)
- Fetch API (browser built-in)
- SendBeacon API (browser built-in)
- localStorage API (browser built-in)

### 17.2 Internal Dependencies

- Azure Front Door implementation (parent requirement)
- Feature flag system (already in use)
- Background task queue (already in use)
- Database schema (no changes required)

### 17.3 Team Dependencies

- 2 developers (can work on frontend and backend concurrently)
- No QA team (ad-hoc integration testing by developers)
- No separate DevOps (developers deploy)

## 18. Timeline Summary

| Phase                  | Duration    | Start | End | Status            |
| ---------------------- | ----------- | ----- | --- | ----------------- |
| Phase 1 - Foundation   | 1-2 days    | TBD   | TBD | ?? Pending Review |
| Phase 2 - API Endpoint | 1-2 days    | TBD   | TBD | ?? Pending Review |
| Phase 3 - Integration  | 1-2 days    | TBD   | TBD | ?? Pending Review |
| Phase 4 - Deployment   | 1 day       | TBD   | TBD | ?? Pending Review |
| **Total**              | **~1 week** |       |     |                   |

## 19. Next Steps

**After Architecture Review:**

1. ? Create feature branch `feature/client-side-usage-tracking`
2. ? Begin Phase 1 implementation (client-side foundation)
3. ? Implement automated unit tests
4. ? Continue through Phases 2-4
5. ? Deploy with feature flag and test
6. ? Update this document with "as-built" details

**No legal review needed** - Same data as existing server-side tracking

---

**Document Version:** 2.0 (Revised based on feedback)
**Last Updated:** 2024
**Author:** Architecture Team
**Reviewers:** David Gray (Approved)
**Status:** ? **APPROVED - Ready for Implementation**
