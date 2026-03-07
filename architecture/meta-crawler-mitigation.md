# Meta Crawler Loop Mitigation

## Problem

Diagnostic logging (deployed March 2026) revealed that the dominant source of identity endpoint traffic is **Meta/Facebook crawler IPs** (57.141.0.x subnet), generating thousands of requests per day to `/Identity/Account/Login`.

This is **not** normal Facebook Login OAuth traffic. It is Meta's link scraper (`facebookexternalhit`, `Facebot`, etc.) caught in a redirect loop.

## Root Cause

When a user shares a link to a page that requires authentication on Facebook, Instagram, or WhatsApp:

1. Meta's crawler fetches the shared URL
2. ASP.NET Identity returns `302 → /Identity/Account/Login?ReturnUrl=...`
3. The crawler follows the redirect, gets the login page HTML (no useful OpenGraph metadata)
4. Meta considers the preview "unstable" and retries the original URL
5. Repeat — across Meta's entire scraper fleet

This is a well-documented pattern. The loop is amplified because:

- The login page has no OpenGraph tags meaningful to a crawler
- Each retry generates a new anti-forgery token cookie (`Set-Cookie`), making the response look dynamic
- Meta's scraper fleet distributes retries across multiple IPs in the same subnet

## Why Rate Limiting Alone Doesn't Fix It

- Per-IP rate limits (10/min) are ineffective when Meta distributes across many IPs
- Global rate limits catch it, but also penalize legitimate users
- The crawlers are not malicious — they're doing what they're designed to do
- Returning 429 to a crawler just causes it to retry later

## Solution: Three-Phase Approach

### Phase 1: Meta Crawler Short-Circuit (Middleware)

**File:** `m4d/Middleware/RateLimitingMiddleware.cs`

When a known Meta crawler UA hits an identity path, return an immediate `200 OK` with:

- Minimal HTML with `<meta name="robots" content="noindex, nofollow">`
- OpenGraph tags telling Meta "this page requires login"
- No anti-forgery token, no session cookie, no redirect chain
- Always logged at Information level for visibility

**Known Meta crawler User-Agents:**

- `facebookexternalhit` — Main link preview scraper
- `Facebot` — Facebook crawler
- `Meta-ExternalFetcher` — Generic Meta fetcher
- `WhatsApp` — WhatsApp link preview
- `Instagram` — Instagram link preview

**Why this is safe for Facebook Login:**

- OAuth callbacks use POST requests through the backend auth pipeline
- OAuth redirects go to `/signin-facebook` (handled by ASP.NET auth middleware), not `/Identity/Account/Login`
- The crawler short-circuit only fires on GET requests with known crawler UAs

### Phase 2: Cache-Control on Identity Redirects

**File:** `m4d/Program.cs` (cache control middleware)

Ensure 302 responses to identity paths include `Cache-Control: no-store` to prevent Front Door or any intermediate proxy from caching the redirect itself, which would amplify the loop.

### Phase 3: robots.txt Update

**File:** `m4d/ClientApp/src/assets/robots.txt`

Add `Disallow: /Identity/` rules for Meta crawlers and the wildcard agent. This is a best-effort signal — Meta doesn't fully honor robots.txt, but it helps with other crawlers and is industry best practice.

### Phase 4: Diagnostic — Log Redirect Source (Future)

When Meta crawlers hit identity pages, log the `Referer` header and `returnUrl` query parameter to identify which specific shared URLs are triggering the loop, so the root cause pages can be fixed.

### Phase 5: Broader Crawler Bypass (Future)

Generalize the Meta-specific fix to all known crawlers hitting auth-required pages. Return minimal metadata instead of redirect chains.

## What NOT to Do

- **Don't block Meta IPs entirely** — breaks Facebook Login OAuth callbacks
- **Don't lower rate limits further for Meta** — the loop continues, just slower
- **Don't add `[AllowAnonymous]` to pages that need auth** — fix the crawler, not the auth policy
- **Don't serve fake login pages** — just give crawlers a clean "login required" signal

## Verification

After deployment, check:

1. Rate limit diagnostic logs should show dramatic reduction in Meta crawler hits
2. Facebook link previews for shared URLs should show "Login Required" instead of broken previews
3. Facebook Login OAuth flow should continue working normally
4. `SpiderManager.CreateBotReport()` on the Admin Diagnostics page should reflect the change
