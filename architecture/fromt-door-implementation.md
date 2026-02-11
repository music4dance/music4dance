# Azure Front Door Implementation Plan

## 1. Overview

This document outlines the implementation of Azure Front Door with intelligent caching to reduce origin server load while maintaining security for authenticated users.

## 2. Cache Control Middleware Implementation

### 2.1 The Problem

ASP.NET Core (specifically Identity/Razor Pages) was setting `Cache-Control: no-cache` headers globally for all responses, preventing Azure Front Door from caching any content - even for anonymous users. This resulted in poor performance and unnecessary origin server load.

### 2.2 The Solution

Middleware that:

1. **Removes** default no-cache headers for anonymous users
2. **Adds** cache-friendly headers for anonymous users (allowing Azure Front Door to cache)
3. **Enforces** strict no-cache headers for authenticated users (preventing personalized content from being cached)

**Key Technical Detail**: Uses `Response.OnStarting()` callback to modify headers at the perfect moment - after the pipeline completes (so authentication and status codes are available) but before headers are sent to the client (avoiding "headers are read-only" errors).

### 2.3 Implementation Details

#### Location

The middleware was added in `m4d/Program.cs` after `UseRouting()` and before `UseAuthorization()`.

#### Middleware Code

```csharp
app.Use(async (context, next) =>
{
    // Register callback to modify headers just before they're sent (after pipeline completes)
    context.Response.OnStarting(() =>
    {
        // Only modify cache headers on successful responses (200-299)
        if (context.Response.StatusCode >= 200 && context.Response.StatusCode < 300)
        {
            if (context.User.Identity?.IsAuthenticated == true)
            {
                // Authenticated users: Prevent all caching
                context.Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate";
                context.Response.Headers["Pragma"] = "no-cache";
            }
            else
            {
                // Anonymous users: Remove no-cache headers and allow caching by Azure Front Door
                context.Response.Headers.Remove("Cache-Control");
                context.Response.Headers.Remove("Pragma");

                // Set cache-friendly headers for Azure Front Door
                // Cache for 5 minutes, allow both client and proxy (CDN) caching
                context.Response.Headers["Cache-Control"] = "public, max-age=300";
            }
        }
        return Task.CompletedTask;
    });

    await next();
});
```

#### Key Features

1. **Uses OnStarting Callback**: The middleware registers a callback with `Response.OnStarting()` that executes **after the pipeline completes but before headers are sent**. This prevents the "headers are read-only" error that occurs when trying to modify headers after the response has started streaming.

2. **Authenticated Users - Strict No-Cache**:
   - `Cache-Control: no-store, no-cache, must-revalidate` - Prevents all caching
   - `Pragma: no-cache` - Legacy HTTP/1.0 compatibility
   - Ensures user-specific content is never cached

3. **Anonymous Users - Cache-Friendly**:
   - Removes any existing no-cache headers set by ASP.NET Core
   - Sets `Cache-Control: public, max-age=300`
   - Allows Azure Front Door to cache pages for 5 minutes
   - Reduces origin server load significantly

4. **Successful Responses Only**: Headers are only modified for successful responses (HTTP status codes 200-299), avoiding modification of error responses or redirects.

5. **Correct Pipeline Position**: Placed after `UseRouting()` (so routing happens first) and before `UseAuthorization()` (ensuring authentication is available but authorization hasn't redirected yet).

### 2.4 Behavior

#### For Authenticated Users

- All successful responses (200-299) will include:
  ```text
  Cache-Control: no-store, no-cache, must-revalidate
  Pragma: no-cache
  ```
- Applies to HTML pages, API responses, and all other content types
- Browser and CDN will not cache any user-specific content
- Ensures privacy and security of personalized data

#### For Anonymous Users

- Default no-cache headers are **removed**
- Cache-friendly headers are added:
  ```
  Cache-Control: public, max-age=300
  ```
- Azure Front Door can cache pages for 5 minutes
- Reduces origin server load
- Improves performance for public-facing content

#### For Static Assets

- Static files (CSS, JS, images) are handled by `UseStaticFiles()` / `MapStaticAssets()` earlier in the pipeline
- This middleware doesn't affect static assets
- Static assets maintain their own cache headers

### 2.5 Why This Approach?

1. **CDN Efficiency**: Allows Azure Front Door to cache public pages, reducing origin server load by 90%+ for anonymous traffic
2. **Security**: Prevents browsers and CDNs from caching personalized content that could be exposed to other users
3. **User Privacy**: Ensures user-specific data (playlists, ratings, preferences) isn't persisted in caches
4. **Performance**: Anonymous users benefit from CDN caching, authenticated users get fresh data
5. **Minimal Impact**: Surgical approach that only modifies what's necessary
6. **Simple Implementation**: Inline middleware is easy to understand and maintain

### 2.6 Cache Duration Considerations

The current implementation uses `max-age=300` (5 minutes) for anonymous pages. This can be adjusted based on your needs:

- **More aggressive caching** (e.g., `max-age=3600` = 1 hour): Better CDN hit ratio, but changes take longer to propagate
- **Less caching** (e.g., `max-age=60` = 1 minute): Changes propagate faster, but more origin requests
- **No caching** (remove the middleware): Every request hits origin server (not recommended for production)

Consider making this configurable via `appsettings.json`:

```json
{
  "CacheControl": {
    "AnonymousMaxAge": 300
  }
}
```

### 2.7 Testing the Middleware

1. **Verify Anonymous User Headers**:
   - Browse as anonymous user
   - Use browser DevTools (Network tab)
   - Check response headers for any HTML page
   - Confirm `Cache-Control: public, max-age=300` is present
   - Confirm `Pragma` header is NOT present

2. **Verify Authenticated User Headers**:
   - Log in to the application
   - Check response headers for any page/API call
   - Confirm `Cache-Control: no-store, no-cache, must-revalidate` is present
   - Confirm `Pragma: no-cache` is present

3. **Verify Status Code Filtering**:
   - Trigger a 404 or 500 error (both anonymous and authenticated)
   - Confirm cache headers are NOT modified for error responses

4. **Test Cache Busting on Login/Logout**:
   - Browse as anonymous (should get cached response)
   - Log in (should get fresh, no-cache response)
   - Log out (should return to cacheable responses)

### 2.8 Build Status

✅ Middleware implemented and tested successfully

This middleware must be deployed **before** enabling Front Door caching.

---

## 3. Azure Front Door Configuration Strategy

### 3.1 Goals

- Cache anonymous GET requests to reduce App Service load
- Prevent caching of authenticated content
- Allow legitimate Chinese traffic
- Block or challenge suspicious bot traffic

---

## 3.2 Front Door Routing Rules

### Rule: Cache Anonymous GET Requests

**Applies to:**
`/*`

**Conditions:**

- Method = GET
- No `Authorization` header (ASP.NET Core Identity uses cookies, so this is safe)

**Action:**

- Enable caching
- Set TTL (example):
  - Default: 1 hour
  - Override with origin Cache-Control headers when present
- Respect origin headers

### Rule: Do Not Cache Login POSTs

POST requests are never cached by AFD, so no special rule is required.

### Rule: Bypass Cache for Authenticated Users

Handled by the middleware.
AFD respects `Cache-Control: no-store`.

---

## 3.3 Front Door WAF / Bot Mitigation

### Recommended Rules

- Enable rate limiting on `/login` and `/account/*`
- Add a JavaScript challenge for suspicious clients
- Allow China as a region
- Block known bad ASNs (optional)
- Enable managed bot rules (Premium) or custom rules (Standard)

### Suggested Custom Rule (Standard Tier)

**Match:**

- Path begins with `/login` or `/account`
- AND
  - User-Agent is empty OR
  - User-Agent matches known automation patterns OR
  - Request rate exceeds threshold

**Action:**

- Challenge (JS challenge) or block

---

## 4. Caching Behavior Summary

| Scenario                              | Cached? | Reason                                               |
| ------------------------------------- | ------- | ---------------------------------------------------- |
| Anonymous user visiting login page    | Yes     | Safe, static page; reduces bot load                  |
| Anonymous user visiting static assets | Yes     | Standard CDN behavior                                |
| Anonymous user visiting public pages  | Yes     | Improves performance and reduces cost                |
| Authenticated user visiting any page  | No      | Middleware sets `no-store`                           |
| Login POST                            | No      | POSTs are never cached                               |
| Account pages (GET) for bots          | Yes     | Bots are anonymous; caching reduces App Service load |

---

## 4.1 Azure Front Door Configuration

To maximize caching efficiency, configure Azure Front Door with:

1. **Cache Query String Parameters**: Configure which query parameters should be included in cache keys
2. **Compression**: Enable response compression at the CDN level
3. **Custom Cache Rules**: Override `max-age` if needed for specific routes
4. **Purge API**: Use purge API when deploying content updates
5. **Respect Origin Headers**: Configure AFD to respect the `Cache-Control` headers set by the middleware

### Recommended Cache Settings

- **Default TTL**: 1 hour (3600 seconds)
- **Origin Override**: Enabled (respects origin `Cache-Control` headers)
- **Query String Caching**: Use query string (cache varies by query parameters)
- **Compression**: Enabled (gzip, brotli)

---

## 4.2 Monitoring

Monitor these metrics to verify caching is working:

### Azure Front Door Metrics

- **Cache hit ratio**: Should be >80% for anonymous traffic
- **Origin requests**: Should decrease significantly after AFD deployment
- **Response time**: Should improve for cached content
- **Bandwidth savings**: Track data transferred from origin vs. from cache

### Application Insights

- **Request count to origin**: Should decrease significantly
- **Server response time distribution**: Faster responses for cached content
- **Cache-Control header distribution**: Verify headers are being set correctly
- **Authentication patterns**: Monitor authenticated vs. anonymous request ratios

### Alert Thresholds

- Cache hit ratio < 70% (investigate caching issues)
- Origin request rate increases unexpectedly (possible cache misconfiguration)
- Authenticated users receiving cached content (critical security issue)

---

## 5. Rollout Plan

### Phase 1 — Application Prep (✅ COMPLETED)

- ✅ Deploy cache control middleware for authenticated users (Section 2)
- ✅ Verify headers in browser dev tools (Section 2.7)
- ✅ Test authenticated vs. anonymous behavior
- ✅ Deploy to production App Service

### Phase 2 — Front Door Deployment (PENDING)

- Create Front Door Standard profile
- Add origin pointing to App Service
- Configure caching rule for anonymous GETs (Section 4.1)
- Configure WAF rules (Section 3.3)
- Set up custom domain and SSL certificate

### Phase 3 — Testing (PENDING)

- Test anonymous caching (curl, browser incognito)
- Verify AFD respects `Cache-Control: public, max-age=300` for anonymous
- Verify AFD respects `Cache-Control: no-store` for authenticated
- Test login flow through AFD
- Test Chinese access via VPN
- Load test to verify origin request reduction
- Monitor cache hit ratios (Section 4.2)

### Phase 4 — Cutover (PENDING)

- Update DNS to point to Front Door endpoint
- Monitor logs and App Service metrics
- Watch for cache hit ratio improvements
- Verify App Service CPU/memory usage decreases
- Monitor for any authentication issues

### Phase 5 — Post-Deployment (PENDING)

- Review monitoring dashboards (Section 4.2)
- Fine-tune cache durations if needed
- Set up alerts for cache misconfigurations
- Document purge procedures for content updates

---

## 6. Future Enhancements

### Short-term (Next 3 months)

- Make cache duration configurable via `appsettings.json`
- Add per-route cache duration overrides (e.g., longer cache for static content pages)
- Implement ETag support for more efficient cache validation
- Add cache purge automation on deployment

### Medium-term (Next 6 months)

- Add custom bot signatures to WAF rules
- Implement per-country rate limiting
- Consider separate cache policies for API endpoints vs. HTML pages
- Add cache warming scripts for frequently accessed pages

### Long-term (Next 12 months)

- Move static assets to Azure Storage + CDN
- Consider upgrading to Premium if bot traffic escalates
- Implement cache versioning for zero-downtime deployments
- Add intelligent cache prefetching based on user navigation patterns

---

## 7. Troubleshooting

### Issue: Authenticated users receiving cached content

**Symptoms**: User logs in but sees stale data or content from another user

**Diagnosis**:

1. Check response headers - should have `Cache-Control: no-store`
2. Verify middleware is deployed and running
3. Check AFD configuration - should respect origin headers

**Resolution**:

1. Verify middleware is in correct position in pipeline (after `UseRouting()`, before `UseAuthorization()`)
2. Check AFD origin settings - ensure "Respect origin headers" is enabled
3. Purge AFD cache if necessary

### Issue: Low cache hit ratio

**Symptoms**: Cache hit ratio < 70%, high origin request rate

**Diagnosis**:

1. Check AFD metrics for cache misses
2. Review query string caching settings
3. Verify `Cache-Control` headers are being set correctly

**Resolution**:

1. Adjust query string caching behavior in AFD
2. Consider increasing `max-age` value in middleware
3. Review routes that might be generating unique cache keys

### Issue: Anonymous pages not being cached

**Symptoms**: Origin request rate not decreasing, AFD cache hits low

**Diagnosis**:

1. Check response headers for anonymous requests - should have `Cache-Control: public, max-age=300`
2. Verify middleware is setting headers correctly
3. Check AFD caching rules

**Resolution**:

1. Test middleware in isolation (browser DevTools)
2. Verify AFD is configured to cache GET requests
3. Check for other middleware that might be overriding headers

---

## 8. References

- [Azure Front Door Documentation](https://learn.microsoft.com/en-us/azure/frontdoor/)
- [HTTP Caching Headers](https://developer.mozilla.org/en-US/docs/Web/HTTP/Headers/Cache-Control)
- [ASP.NET Core Response Caching](https://learn.microsoft.com/en-us/aspnet/core/performance/caching/response)
- [Response.OnStarting() Method](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.http.httpresponse.onstarting)
