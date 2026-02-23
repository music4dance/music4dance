# Client-Side Usage Logging Architecture

## Overview

Music4Dance implements a hybrid usage tracking system that captures page view analytics for both cached and uncached responses. This architecture enables Azure Front Door CDN caching while maintaining accurate user behavior data.

**System Status:** ? **Production Ready** (Feature flag controlled)

**Test Coverage:** 27/27 tests passing (100%)

- ? Client: 19 tests (useUsageTracking composable)
- ? Server: 8 tests (UsageLogApiController integration tests)
- ?? Testing Documentation: `architecture/testing-patterns.md`

**Key Features:**

- ? **Client-side tracking** - Works with CDN-cached pages
- ? **Server-side fallback** - Legacy tracking still available
- ? **Feature flag controlled** - Toggle between client/server tracking
- ? **Smart batching** - Different strategies for anonymous vs authenticated users
- ? **Bot detection** - Client and server-side filtering
- ? **Graceful degradation** - Handles localStorage unavailability
- ? **Privacy compliant** - GDPR-compatible with canonical fallback ID

**Related Documents:**

- [Azure Front Door Implementation](./front-door-implementation.md) - CDN caching architecture
- [Identity Endpoint Protection](./identity-endpoint-protection.md) - Rate limiting & random delays
- [Testing Patterns](./testing-patterns.md) - Test infrastructure and patterns

---

## 1. Why Client-Side Tracking?

**Problem:** When Azure Front Door caches a response, server-side middleware (`DMController.OnActionExecutionAsync`) never executes for cached requests.

**Impact:**

- ? No usage tracking for anonymous cached pages
- ? No user activity updates (`LastActive`, `HitCount`)
- ? Inaccurate analytics and user behavior data
- ? Cannot distinguish real users from bots on cached content

**Solution:** Client-side JavaScript tracking that works regardless of cache status, with deferred API reporting to reduce server load.

---

## 2. Architecture Overview

### 2.1 System Components

```
????????????????????????????????????????
?         Browser (Client)             ?
?                                      ?
?  ?????????????????????????????????? ?
?  ?  useUsageTracking Composable   ? ?
?  ?  - Bot detection               ? ?
?  ?  - localStorage management     ? ?
?  ?  - Smart batching              ? ?
?  ?  - SendBeacon API              ? ?
?  ?????????????????????????????????? ?
?             ?                        ?
????????????????????????????????????????
              ? POST /api/usagelog/batch
              ? (FormData + XSRF token)
              ?
????????????????????????????????????????
?      App Service (Origin)            ?
?                                      ?
?  ?????????????????????????????????? ?
?  ?  UsageLogController (API)      ? ?
?  ?  - [ValidateAntiForgeryToken]  ? ?
?  ?  - Payload validation          ? ?
?  ?  - Background task enqueue     ? ?
?  ?????????????????????????????????? ?
?             ?                        ?
?  ?????????????????????????????????? ?
?  ?  Background Task Queue         ? ?
?  ?  - Batch DB insert             ? ?
?  ?  - Update User stats           ? ?
?  ?????????????????????????????????? ?
????????????????????????????????????????
```

### 2.2 Data Flow

**Page Load:**

1. Browser loads page (may be cached by Azure Front Door)
2. `useUsageTracking` composable initializes
3. Checks if bot ? if yes, skip tracking
4. Generates/loads UsageId from localStorage
5. Creates usage event and adds to local queue
6. Increments visit counter

**Sending Strategy:**

**Authenticated Users:**

- Threshold: 1 page (immediate)
- Batch size: 1 event
- Sends after every page view

**Anonymous Users:**

- Threshold: 3 pages (wait for engagement)
- Batch size: 5 events
- Queues first 2 pages, sends batch after page 3+

**Page Unload:**

- Navigation API detects same-origin navigation ? skip send
- External navigation or tab close ? send remaining events via SendBeacon

---

## 3. Client-Side Implementation

### 3.1 Usage Tracking Composable

**File:** `m4d/ClientApp/src/composables/useUsageTracking.ts`

**Responsibilities:**

1. **Session Management**
   - Generates UUID v4 for new sessions
   - Stores in `localStorage.usageId`
   - Canonical fallback: `00000000-0000-0000-0000-000000000001` (localStorage unavailable)
   - Persists visit count in `localStorage.usageCount`

2. **Bot Detection**
   - User-agent pattern matching (`/bot/`, `/crawl/`, `/spider/`, etc.)
   - Headless browser detection (`__nightmare`, `__phantomas`)
   - WebDriver detection (`navigator.webdriver === true`)

3. **Event Queueing**
   - Stores events in `localStorage.usageQueue`
   - Tracks `lastSentIndex` to identify unsent events
   - Max queue size: 100 events (trims oldest when exceeded)
   - Never clears queue (useful for analytics, nag modals)

4. **Smart Batching**
   - **Authenticated:** Threshold=1, BatchSize=1 (immediate send)
   - **Anonymous:** Threshold=3, BatchSize=5 (wait for engagement)
   - Only sends when visit count ? threshold AND unsent events ? batch size

5. **SendBeacon Integration**
   - Uses `visibilitychange` event (most reliable)
   - Fallback: `pagehide` event (iOS Safari)
   - Respects 64KB size limit
   - Fire-and-forget (no await, no retries)

6. **Graceful Degradation**
   - localStorage unavailable ? uses canonical GUID, no tracking sent
   - API failure ? logs error, doesn't break page
   - Returns visit count = 0 when storage fails (anonymous users never reach threshold)

### 3.2 Configuration

**Server-Side:** `m4d/Views/Shared/_head.cshtml`

```html
@if (await _featureManager.IsEnabledAsync(FeatureFlags.ClientSideUsageLogging))
{
<script type="module">
  import { useUsageTracking } from "/vclient/composables/useUsageTracking.js";

  const tracker = useUsageTracking({
    enabled: menuContext.usageTracking?.enabled ?? true,
    anonymousThreshold: 3,
    anonymousBatchSize: 5,
    authenticatedBatchSize: 1,
    maxQueueSize: 100,
    xsrfToken: menuContext.xsrfToken,
    userName: menuContext.userName || null,
    isAuthenticated: menuContext.userName && menuContext.userName.length > 0,
  });

  tracker.trackPageView(window.location.pathname, window.location.search);
</script>
}
```

**Path Exclusions (Server-Side):**

- `/admin/` - Admin pages (excluded in `_head.cshtml`)
- `/identity/` - Login/register pages (excluded in `_head.cshtml`)
- `/api/` - API endpoints (excluded in `_head.cshtml`)

### 3.3 localStorage Fallback Strategy

**Problem:** In Multi-Page Application (MPA), localStorage unavailability means every page load resets the composable. In-memory fallback is useless.

**Solution:** Canonical GUID `00000000-0000-0000-0000-000000000001`

**Why:**

- Identifies "localStorage unavailable" users in analytics
- Returns `visitCount = 0` so anonymous users never reach threshold
- No page crashes or errors
- Easy to filter in database queries

**Behavior:**

```typescript
// localStorage unavailable
getUsageId() ? "00000000-0000-0000-0000-000000000001"
getVisitCount() ? 0
incrementVisitCount() ? 0

// Anonymous user with visitCount = 0 never sends (threshold = 3)
// Authenticated user detection would require cookie check (separate logic)
```

---

## 4. Server-Side Implementation

### 4.1 API Controller

**File:** `m4d/APIControllers/UsageLogController.cs`

**Features:**

- Inherits from `DanceMusicApiController`
- `[ValidateAntiForgeryToken]` for CSRF protection
- Accepts `FormData` with XSRF token
- Returns 202 Accepted immediately (fire-and-forget)
- Enqueues to `IBackgroundTaskQueue`
- Validates payload size (max 100 events)
- Server-side authentication takes precedence over client-reported username

**Endpoint:** `POST /api/usagelog/batch`

**Request Format:**

```typescript
// FormData (not JSON)
formData.append(
  "events",
  JSON.stringify([
    {
      usageId: "uuid",
      timestamp: 1234567890,
      page: "/dances",
      query: "?filter=CHA",
      referrer: "https://google.com",
      userAgent: "Mozilla/5.0...",
      filter: "CHA",
      userName: null, // Client-reported (server overrides if authenticated)
      isAuthenticated: false,
    },
  ]),
);
formData.append("__RequestVerificationToken", xsrfToken);
```

**Response:** 202 Accepted (immediate return, background processing)

### 4.2 Legacy Server-Side Tracking

**File:** `m4d/Controllers/DMController.cs`

**Status:** ? **Still Active** (disabled when client-side tracking enabled)

**Location:** `OnActionExecutionAsync` method

**How It Works:**

1. Request arrives at controller
2. Middleware executes before/after controller action
3. Checks if spider (via `SpiderManager.CheckAnySpiders`)
4. **Checks feature flag:** `FeatureFlags.ClientSideUsageLogging`
   - If enabled ? skip server-side logging
   - If disabled ? continue with server-side logging
5. Creates `UsageLog` record
6. Enqueues to background task queue
7. Updates `User.LastActive` and `User.HitCount`

**Cookie Management:**

- Cookie name: `"Usage"`
- Format: `{guid}_{visitCount}`
- Only used when server-side tracking is active

**Advantages:**

- No JavaScript required
- Works for browsers with scripts disabled

**Disadvantages:**

- Bypassed by Azure Front Door caching
- Higher server load (runs on every request)
- Cannot track cached pages

---

## 5. Feature Flag Strategy

### 5.1 Configuration

**Feature Flags:**

```csharp
public static class FeatureFlags
{
    public const string UsageLogging = "UsageLogging"; // Master switch
    public const string ClientSideUsageLogging = "ClientSideUsageLogging"; // Client vs Server
}
```

**appsettings.json:**

```json
{
  "FeatureManagement": {
    "UsageLogging": true,
    "ClientSideUsageLogging": false
  },
  "UsageTracking": {
    "Enabled": true,
    "AnonymousThreshold": 3,
    "AnonymousBatchSize": 5,
    "AuthenticatedBatchSize": 1,
    "MaxQueueSize": 100
  }
}
```

### 5.2 Toggling Between Client and Server

**Enable Client-Side Tracking:**

```json
{
  "FeatureManagement": {
    "ClientSideUsageLogging": true
  }
}
```

**Result:**

- ? Client-side tracking initializes in `MainMenu.vue`
- ? Server-side tracking skips logging in `DMController`
- ? API endpoint receives batched events
- ? Works with cached pages

**Disable Client-Side Tracking:**

```json
{
  "FeatureManagement": {
    "ClientSideUsageLogging": false
  }
}
```

**Result:**

- ? Client-side tracking doesn't initialize
- ? Server-side tracking resumes in `DMController`
- ? API endpoint still available but not used
- ? Cached pages not tracked

### 5.3 Rollback Plan

**If client-side tracking fails:**

1. Set `ClientSideUsageLogging = false` in App Configuration
2. Server-side tracking resumes immediately
3. No code changes required
4. Data continues flowing

---

## 6. Cache Control Middleware

### 6.1 Security Architecture

**File:** `m4d/Program.cs` (lines ~648-720)

**Purpose:** Set cache headers for Azure Front Door while protecting sensitive content

**Guards (Applied in Order):**

1. ? Only GET requests
2. ? Only 2xx responses
3. ? Exclude `/api/*` endpoints
4. ? Exclude `/song/rawsearchform` (has anti-forgery token)
5. ? **No Set-Cookie header** (critical for CSRF protection)
6. ? Only `text/html` responses
7. ? Check authentication state

**Caching Rules:**

**Authenticated Users:**

```http
Cache-Control: no-store, no-cache, must-revalidate
Pragma: no-cache
```

**Anonymous Users (cacheable):**

```http
Cache-Control: public, max-age=300
```

### 6.2 Why Identity Pages Aren't Cached

**Problem:** Identity pages (e.g., `/identity/account/login`) set anti-forgery cookies on GET requests.

**Security Risk if Cached:**

- Scenario 1 (CDN strips Set-Cookie): Users can't submit forms (validation fails)
- Scenario 2 (CDN doesn't strip): Multiple users share same token (CSRF broken)

**Decision:** Keep Set-Cookie check, don't cache Identity pages

**Alternative Bot Protection:**
Instead of caching, use three-layer defense:

1. **Random Delay (200-400ms)** - Slows authentication attempts by 80%
2. **Rate Limiting (20 req/min)** - Hard cap on requests
3. **CAPTCHA (existing)** - Human verification on failures

**See:** `architecture/identity-endpoint-protection.md`

---

## 7. Data Model

### 7.1 UsageLog Table

**Schema:** (No changes required from server-side implementation)

```csharp
public class UsageLog
{
    public int UsageLogId { get; set; }
    public string UsageId { get; set; }       // GUID or canonical fallback
    public string UserName { get; set; }      // Server-side auth (priority)
    public DateTime Date { get; set; }        // Server timestamp (converted from client)
    public string Page { get; set; }          // Request path
    public string Query { get; set; }         // Query string
    public string Filter { get; set; }        // Extracted from query
    public string Referrer { get; set; }      // HTTP Referer
    public string UserAgent { get; set; }     // Browser user-agent
}
```

### 7.2 Querying Data

**Identify localStorage Unavailable Users:**

```sql
SELECT *
FROM UsageLog
WHERE UsageId = '00000000-0000-0000-0000-000000000001'
```

**Count Unique Users (Excluding Fallback):**

```sql
SELECT COUNT(DISTINCT UsageId)
FROM UsageLog
WHERE UsageId != '00000000-0000-0000-0000-000000000001'
```

---

## 8. Testing

### 8.1 Automated Tests

**Client-Side (Vitest):**

- ? UsageId generation and persistence
- ? Bot detection patterns
- ? Queue management (add, overflow, trim)
- ? Visit count tracking
- ? Batching logic (anonymous vs authenticated)
- ? lastSentIndex tracking
- ? FormData with XSRF token
- ? SendBeacon calls

**File:** `m4d/ClientApp/src/composables/__tests__/useUsageTracking.test.ts`

**Server-Side (MSTest):**

- ? Request validation
- ? Background task enqueueing
- ? Authentication detection
- ? FormData parsing

**File:** `m4d.Tests/APIControllers/UsageLogApiControllerTests.cs`

### 8.2 Manual Testing

**localStorage Fallback:**

1. Open browser in private/incognito mode
2. Disable localStorage (DevTools ? Application ? Storage)
3. Load page, check console for fallback warnings
4. Verify no errors, page loads normally
5. Check UsageId in debug output: `00000000-0000-0000-0000-000000000001`

**Batching Behavior:**

1. Open browser with localStorage enabled
2. Enable debug mode (dev environment)
3. Visit 3 pages as anonymous user
4. Check console: should see batching messages
5. Visit 5 pages total: should send first batch
6. Close tab: should send remaining events via SendBeacon

**SendBeacon on Unload:**

1. Visit multiple pages (3+) as anonymous user
2. Open browser DevTools Network tab
3. Filter for `/api/usagelog/batch`
4. Close tab or navigate away
5. Verify beacon request sent with remaining events

---

## 9. Monitoring & Observability

### 9.1 Application Insights Queries

**Client-Side Errors:**

```kusto
traces
| where message contains "Failed to access localStorage"
| summarize count() by bin(timestamp, 1h)
| render timechart
```

**API Request Rate:**

```kusto
requests
| where url contains "/api/usagelog/batch"
| summarize count() by bin(timestamp, 1h), resultCode
| render timechart
```

**Fallback UsageId Usage:**

```kusto
customEvents
| where name == "UsageLog"
| extend UsageId = tostring(customDimensions.UsageId)
| where UsageId == "00000000-0000-0000-0000-000000000001"
| summarize count() by bin(timestamp, 1d)
```

### 9.2 Key Metrics

**Client-Side:**

- Batch send success rate
- Average queue size
- Bot detection rate
- localStorage failure rate

**Server-Side:**

- API request rate
- API error rate
- Background task queue depth
- Database insert rate

**Business:**

- Daily active users (by UsageId)
- Pages per session
- Anonymous vs authenticated ratio
- Top pages visited

---

## 10. Security Considerations

### 10.1 CSRF Protection

**Mechanism:** `[ValidateAntiForgeryToken]` attribute on API controller

**Implementation:**

1. Server generates token in `_head.cshtml`
2. Token passed to client via `menuContext.xsrfToken`
3. Client includes token in FormData: `__RequestVerificationToken`
4. Server validates token origin

**Why FormData (Not JSON):**

- Standard ASP.NET Core validation works without custom filters
- Token not logged in URLs
- More secure than query string

### 10.2 Rate Limiting

**See:** `architecture/identity-endpoint-protection.md`

**API Rate Limiting:** (Future enhancement)

- Per IP: 100 req/min
- Per UsageId: 10 batches/min

### 10.3 Data Privacy

**GDPR Compliance:**

- UsageId is not personally identifiable
- Canonical fallback ID doesn't track users
- localStorage (not cookies) - no banner update needed
- Opt-out mechanism available (set `usageTrackingOptOut` in localStorage)

**Retention:**

- Usage logs retained for 90 days (configurable)
- Automatic cleanup recommended

---

## 11. Future Enhancements

### 11.1 Storage Fallback Strategies

**Status:** ⏳ Not Yet Implemented  
**Priority:** TBD (depends on canonical GUID usage data)

**Problem:**  
Currently, when localStorage is unavailable (private browsing, strict privacy settings), we use a canonical fallback GUID (`00000000-0000-0000-0000-000000000001`) and don't track these users. However, other storage mechanisms might still be available.

**Proposed Fallback Chain:**

1. **localStorage** (primary) - Best: persists across sessions, large storage
2. **sessionStorage** (fallback 1) - Good: available in private browsing on some browsers
3. **Client-side cookies** (fallback 2) - Fair: smaller storage, sent with every request
4. **Server-side tracking** (fallback 3) - Last resort: fall back to legacy cookie-based tracking

**Implementation Considerations:**

**Option 1: sessionStorage Fallback**
```typescript
function getUsageId(): string {
  // Try localStorage first
  try {
    let usageId = localStorage.getItem(STORAGE_KEY_USAGE_ID);
    if (!usageId) {
      usageId = crypto.randomUUID();
      localStorage.setItem(STORAGE_KEY_USAGE_ID, usageId);
    }
    return usageId;
  } catch (error) {
    // Fallback to sessionStorage
    try {
      let usageId = sessionStorage.getItem(STORAGE_KEY_USAGE_ID);
      if (!usageId) {
        usageId = crypto.randomUUID();
        sessionStorage.setItem(STORAGE_KEY_USAGE_ID, usageId);
      }
      return usageId;
    } catch (sessionError) {
      // Final fallback: canonical GUID
      return FALLBACK_USAGE_ID;
    }
  }
}
```

**Trade-offs:**
- ✅ sessionStorage often works in private browsing (Safari, Firefox)
- ✅ Better than canonical GUID (tracks session, not just page)
- ❌ Doesn't persist across browser sessions
- ❌ Each new session = new UsageId (inflates DAU metrics)

**Option 2: Client-Side Cookie Fallback**
```typescript
function getUsageId(): string {
  // Try localStorage → sessionStorage → cookies
  // ...existing fallbacks...
  
  // Fallback to client-side cookie
  const cookieUsageId = getCookie('usageId');
  if (cookieUsageId) return cookieUsageId;
  
  const newId = crypto.randomUUID();
  setCookie('usageId', newId, 365); // 1 year expiration
  return newId;
}
```

**Trade-offs:**
- ✅ Works when localStorage/sessionStorage unavailable
- ✅ Persists across sessions (if user accepts cookies)
- ❌ Cookie sent with every request (bandwidth overhead)
- ❌ 4KB size limit (less than localStorage)
- ❌ May require cookie consent banner update

**Option 3: Server-Side Tracking Fallback**
```typescript
function initializeTracking(): boolean {
  // Test if localStorage is available
  if (!isStorageAvailable()) {
    if (finalConfig.debug) {
      console.log("Storage unavailable, using server-side tracking");
    }
    // Don't initialize client-side tracking
    // Server-side middleware will handle via cookie
    return false;
  }
  
  // Initialize client-side tracking
  return true;
}
```

**Server-Side Detection:**
```csharp
// In DMController.cs
if (await FeatureManager.IsEnabledAsync(FeatureFlags.ClientSideUsageLogging))
{
    // Check if client sent UsageId = canonical GUID (storage failed)
    var clientUsageId = ExtractUsageIdFromRequest();
    if (clientUsageId == "00000000-0000-0000-0000-000000000001")
    {
        // Fall back to server-side tracking for this user
        await TrackUsageServerSide(context);
        return;
    }
    
    // Skip server-side tracking (client-side handles it)
    return;
}
```

**Trade-offs:**
- ✅ Best coverage (tracks users even without client-side storage)
- ✅ No data loss for private browsing users
- ✅ Reuses existing server-side infrastructure
- ❌ More complex coordination (client signals failure, server responds)
- ❌ Requires detecting canonical GUID in multiple places
- ❌ Higher server load for private browsing users

**Recommendation:**

Wait for production data before implementing. After feature is enabled:

1. **Query canonical GUID usage:**
   ```sql
   SELECT 
       COUNT(*) as FallbackUsers,
       COUNT(*) * 100.0 / (SELECT COUNT(DISTINCT UsageId) FROM UsageLog) as Percentage
   FROM UsageLog
   WHERE UsageId = '00000000-0000-0000-0000-000000000001'
   ```

2. **Decision Matrix:**
   - If < 1% of users → **Don't implement** (not worth complexity)
   - If 1-5% of users → **Consider sessionStorage** (simple, low risk)
   - If 5-10% of users → **Add cookie fallback** (better coverage)
   - If > 10% of users → **Implement server-side fallback** (best coverage)

3. **Browser-Specific Analysis:**
   ```sql
   SELECT 
       UserAgent,
       COUNT(*) as FallbackCount
   FROM UsageLog
   WHERE UsageId = '00000000-0000-0000-0000-000000000001'
   GROUP BY UserAgent
   ORDER BY FallbackCount DESC
   ```
   
   If fallbacks concentrated in specific browsers (e.g., Safari private), targeted solution may be better.

**Why Not Implemented Now:**
- Don't know scale of problem yet
- Canonical GUID lets us measure need accurately
- Adding complexity before validating need is premature optimization
- Can implement later without breaking changes

**Browser Compatibility Research:**

| Browser              | Private Mode | localStorage | sessionStorage | Cookies |
|---------------------|--------------|--------------|----------------|---------|
| Chrome/Edge         | Incognito    | ❌ Blocked    | ✅ Available    | ✅ Available |
| Firefox             | Private      | ❌ Blocked    | ✅ Available    | ✅ Available |
| Safari              | Private      | ❌ Blocked    | ✅ Available    | ⚠️ Limited |
| Safari (ITP strict) | Normal       | ⚠️ Cleared    | ⚠️ Cleared     | ⚠️ Limited |

**Key Insight:** sessionStorage appears to be the best first fallback (widely available in private browsing).

---

### 11.2 Other Enhancements (Not Yet Implemented)

**Priority: Low**

- ? **Client-side path filtering** - Dynamic exclusions without server restart
- ? **Anti-replay protection** - Timestamp age validation (XSRF already prevents cross-origin)
- ? **Health check endpoint** - Monitor background task queue health
- ? **API rate limiting** - Per IP/UsageId limits (fire-and-forget currently)

**Priority: Medium**

- ? **Navigation API E2E tests** - Playwright for real browser testing
- ? **Offline support** - Queue events when offline, sync on reconnect
- ? **Session aggregation** - Send summaries instead of individual events

**Priority: Future**

- ? **Real-time analytics** - SignalR dashboard updates
- ? **A/B testing integration** - Track experiment variants
- ? **Bot behavior analysis** - ML model for sophisticated bot detection

### 11.3 Why Not Implemented?

**Rate Limiting:**

- Fire-and-forget approach means API failures don't block users
- Background task queue naturally throttles database writes
- Can add later if abuse detected

**Anti-Replay:**

- XSRF token prevents cross-origin replay
- Same-origin replay just duplicates own data (low value attack)
- Timestamp validation adds overhead without clear benefit

**Health Check:**

- Usage logging is non-critical (doesn't block user functionality)
- Errors logged to Application Insights
- Can add later for proactive monitoring

---

## 12. Decision Log

### 12.1 Key Architectural Decisions

| Decision                        | Rationale                                         | Status        |
| ------------------------------- | ------------------------------------------------- | ------------- |
| **SendBeacon-only** (no fetch)  | Simpler, more reliable, better unload handling    | ? Implemented |
| **FormData (not JSON)**         | Standard validation works, more secure            | ? Implemented |
| **Smart batching**              | Reduce server load, wait for anonymous engagement | ? Implemented |
| **Feature flag toggle**         | Safe rollout, easy rollback                       | ? Implemented |
| **Canonical fallback ID**       | Better than random UUID in MPA                    | ? Implemented |
| **No Vue Router**               | Project uses full page loads                      | ? Documented  |
| **Server-side path exclusions** | Cleaner than client-side, centralized             | ? Implemented |
| **Fire-and-forget API**         | Don't block page, graceful degradation            | ? Implemented |
| **Navigation API**              | 85-90% browser support, graceful fallback         | ? Implemented |
| **Keep server-side tracking**   | Fallback mechanism, Razor Pages support           | ? Implemented |

### 12.2 Trade-Offs

**Client-Side Tracking:**

- ? Works with CDN caching
- ? Reduces server load (batching)
- ? Requires JavaScript enabled
- ? Can be blocked by ad blockers
- ? Bot detection more complex

**Server-Side Tracking:**

- ? Works without JavaScript
- ? Simple bot detection
- ? Bypassed by CDN caching
- ? Higher server load
- ? Cannot track cached pages

---

## 13. Historical Notes

### 13.1 Implementation Timeline

**Phase 1 (2 days):** Client-side foundation

- Created `useUsageTracking` composable
- Implemented localStorage management
- Bot detection logic
- Unit tests (19 tests)

**Phase 2 (2 days):** API endpoint

- Created `UsageLogController`
- Background task integration
- FormData + XSRF validation
- Integration tests (8 tests)

**Phase 3 (2 days):** Integration

- Added feature flag
- Updated `_head.cshtml`
- Conditional server-side logging
- End-to-end manual testing

**Phase 4 (In progress):** Production deployment

- Feature flag disabled by default
- Staged rollout plan
- Monitoring setup

### 13.2 Lessons Learned

**What Worked Well:**

- ? FormData + SendBeacon approach (simpler than expected)
- ? Navigation API integration (excellent browser support)
- ? Smart batching (reduces server load effectively)
- ? Feature flag strategy (safe, reversible)
- ? Server-side path exclusions (clean, maintainable)

**What Could Be Improved:**

- ?? Could add more E2E tests (Playwright)
- ?? Health check endpoint would be nice (non-critical)
- ?? Canonical GUID discovered late (initial in-memory approach wouldn't work in MPA)

**Recommendations for Similar Features:**

1. Start with server-side configuration
2. Use feature flags for safe rollout
3. Navigation API is production-ready
4. FormData + SendBeacon is the right pattern
5. Consider MPA vs SPA implications early
6. Document testing limitations upfront

---

**Document Version:** 3.0 (Current State Architecture)
**Last Updated:** 2025-01-21
**Author:** Architecture Team
**Status:** ? **Production Ready** (Feature Flag Controlled)
