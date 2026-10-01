# Azure Front Door Caching

**Type:** Plan
**Status:** Not started (owner confirmed 2026-10-01). Phase 1 (application prep) shipped; Front Door
itself has never been deployed.
**Last verified:** 2026-10-01

Put Azure Front Door in front of the App Service to cache anonymous HTML at the edge, cut origin
load, and add WAF and bot rules, while never caching authenticated or Identity responses.

## 1. Where things stand

- **Shipped:** the cache-control middleware that marks anonymous `GET 200` HTML as
  `public, max-age=300` and authenticated HTML as `no-store`, with exclusions for `/identity/*`,
  `/api/*`, `/song/rawsearchform` and cookie-setting responses. It's described in
  [hosting-and-identity § Request pipeline notes](../infrastructure/hosting-and-identity.md#request-pipeline-notes).
  With no CDN in front, it currently only affects browser caching.
- **Shipped:** the original blocker, client-side usage logging. It's behind the
  `ClientSideUsageLogging` feature flag; see
  [client-side-usage-logging](../observability/client-side-usage-logging.md). Edge-cached pages
  skip `DMController.OnActionExecutionAsync`, so that flag **must be on** before caching is
  enabled, or page-view analytics silently drop.
- **Not started:** the Front Door profile, routing and caching rules, WAF, and cutover. These are
  sections 3–5 below.

Before starting, re-check two things:
- The forwarded-header handling (`KnownProxies` / `KnownNetworks` aren't restricted today), so
  that the client IP used by rate limiting comes from Front Door's `X-Forwarded-For` / `X-Azure-ClientIp`.
- The antiforgery-token interaction with cached anonymous pages, which is the reason the
  antiforgery cookie lasts a day.

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

### Phase 1 — Application Prep (done)

- Deploy cache control middleware (see [hosting-and-identity](../infrastructure/hosting-and-identity.md#request-pipeline-notes))
- Verify headers in browser dev tools
- Test authenticated vs. anonymous behavior
- Deploy to production App Service
- Implement client-side usage logging (shipped behind `ClientSideUsageLogging`; turn the flag on before Phase 2)

**Note:** Phase 2 cannot proceed until client-side usage logging is fully implemented and tested. Server-side usage logging will not capture analytics for cached pages.

### Phase 2 — Front Door Deployment (not started)

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
