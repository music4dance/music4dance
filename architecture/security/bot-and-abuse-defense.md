# Bot and Abuse Defense

**Type:** Reference
**Status:** Current
**Last verified:** 2026-10-01
**Code:** `m4d/Middleware/RateLimitingMiddleware.cs`, `m4d/Middleware/Http4xxTrackingMiddleware.cs`,
`m4d/Security/` (`AuthenticationTracker`, `RateLimitingTracker`, `Http4xxTracker`),
`m4d/Utilities/SpiderManager.cs`, `m4d/Utilities/GlobalState.cs`, `m4d/Views/Admin/Diagnostics.cshtml`

How music4dance handles crawlers, scrapers, credential stuffing and scanner noise. The four layers
are independent. All of them are in-memory and per-instance, which is fine for the single-instance
deployment (see [hosting-and-identity](../infrastructure/hosting-and-identity.md)), and all of them
report to **`/Admin/Diagnostics`**.

| Layer | Threat | Scope | Response |
| --- | --- | --- | --- |
| 1. Catalog bot short-circuit | Crawler load on SQL/Search-backed catalog pages | `/song/*`, `/customsearch/*`, usage logging | Static stub, block page, or no usage row |
| 2. Identity endpoint protection | Brute force, credential stuffing, distributed login attacks | `/identity/*` | Rate limits (429), random delay, CAPTCHA escalation, lockout |
| 3. Crawler short-circuit on identity pages | Link-preview crawlers stuck in login-redirect loops | `GET /identity/*` | Clean `200` "login required" page |
| 4. 4xx-by-URL tracking | Broken links, internal bugs, scanner noise | Every request | Admin table + CSV export, known-attack classification |

## Layer 1: Catalog bot short-circuit (`SpiderManager`)

This layer addresses **search-engine and scraper crawlers
generating expensive, repetitive load against the SQL/Azure-Search-backed catalog pages**
(`/song/*`, `/customsearch/*`). It was introduced in 2016 (`7e8bdc20`, "Create bot-friendly stubs
for search results ... to remove load from the SQL database") and extended incrementally since,
most recently when `CustomSearchController` picked up the same check in September 2024
(`07ba2607`). It shares no code with the identity-endpoint layer below.

### `SpiderManager`

**`SpiderManager`** (`m4d/Utilities/SpiderManager.cs`) is a static classifier + in-memory counter:

- `BotFilterInfo` binds to the `Configuration:BotFilter` section (Azure App Configuration/Key
  Vault in production — not checked into `appsettings.json`) and exposes three `;`-delimited
  lists:
  - `ExcludeTokens` — exact user-agent match (e.g. `alwayson`, a synthetic-monitoring UA)
  - `ExcludeFragments` — substring match (e.g. `spider`, `bot`) — the broad "this is some kind of
    crawler" set
  - `BadFragments` — substring match against a narrower, known-hostile subset of the exclude set
    (e.g. `baiduspider`)
- `CheckAnySpiders(userAgent, config)` → `true` if the UA is empty/whitespace (counted separately
  via `EmptyAgents`) or matches any `ExcludeToken`/`ExcludeFragment`. Every positive match
  increments an in-memory `Dictionary<string, long>` keyed by lowercased UA.
- `CheckBadSpiders(userAgent, config)` → same matching pass, but only returns `true` for UAs that
  also match `BadFragments` — i.e. "is a spider" is necessary but not sufficient; used where the
  intent is to actually block/redirect rather than just to suppress logging.
- `CreateBotReport()` snapshots the hit dictionary (+ `EmptyAgents`, `UpTime`) into
  `IEnumerable<BotHitModel>` (agent, hit count, hits/sec), rendered at `/Admin/Diagnostics` via
  `Views/Shared/_botreport.cshtml` (`ViewData["BotReport"]`, set in `AdminController.Diagnostics`).
  This is the entirety of the "tracking" half — an in-memory, per-instance, reset-on-restart
  hit-count-by-user-agent table for manual review, not a security event log like
  `RateLimitingTracker`.

### Three short-circuit call sites

All three consult `SpiderManager`, but check different lists and produce different results:

1. **Search-result stub** — `SongController.DoAzureSearch()` and
   `CustomSearchController.Index()` call the broad `CheckAnySpiders` (search-engine bots *should*
   get a response here, just a cheap static one) and, if positive and the current filter isn't
   already the degenerate stub filter (`SongFilter.IsEmptyBot` — prevents a redirect loop once the
   bot lands on the stub itself), throw `RedirectException("BotFilter", Filter)`. Caught by
   `ContentController.HandleRedirect`, which renders `View("BotFilter", Filter)`: a static page
   describing the filter in prose and pointing crawlers at the plain `/song` catalog instead of
   letting them re-crawl every filter permutation of the paginated, Azure-Search-backed results.

2. **Full-page block** — `DMController.CheckSpiders()` (called from `SongController.Details`,
   `Album`, `Artist`) uses the narrower `CheckBadSpiders` and, only for that hostile subset,
   returns `View("BotWarning")` (`Views/Shared/BotWarning.cshtml`) instead of running the action.
   Ordinary search-engine crawlers are deliberately *not* blocked here — they're allowed to index
   detail pages normally; only known-abusive scrapers are turned away.

3. **Usage-log suppression** — the legacy server-side tracking path in
   `DanceMusicController.OnActionExecutionAsync` (see [usage-tracking](../observability/usage-tracking.md#two-recording-paths)) calls
   `CheckAnySpiders` to skip writing a `UsageLog` row for bot traffic, independent of whether the
   request is otherwise short-circuited.

## Layer 2: Identity endpoint protection

`RateLimitingMiddleware` runs after authentication, for paths starting with `/identity/` only.
In order, it:

1. **Short-circuits known crawlers** on `GET`s. This is layer 3.
2. **Detects suspicious `returnUrl`.** A nested `returnUrl` (more than one occurrence) or more
   than three `?`-separated segments is logged, recorded as suspicious activity, and forces CAPTCHA.
   Separately, `LoginModelBase` records a non-local `returnUrl` as suspicious, and Identity's normal
   local-URL check still applies.
3. **Applies the global limit.** It counts every `/identity/*` request across all clients in a fixed
   window (`GlobalMaxRequestsPerWindow`, default 100 per `GlobalWindowMinutes` = 1). At
   `CaptchaThresholdPercent` of that limit (20%, so 20 requests a minute) it turns CAPTCHA on.
   Above the limit it returns a friendly HTML `429` with `Retry-After`. This is the defense against
   distributed attacks, where each IP stays under its own limit.
4. **Adds a random delay** of 200–400 ms to authentication `POST`s: login, register, external login,
   reset password, and 2FA. If that IP has a failed login in the last 5 minutes, it turns CAPTCHA on.
5. **Applies the per-IP limit.** It counts requests per **path and client IP** (`MaxRequestsPerWindow`,
   default 10 per `WindowMinutes` = 1) in `IMemoryCache`. Above the limit it returns a `429`.

Every decision goes to `RateLimitingTracker`, a 10,000-event circular buffer with IPs anonymized
to `a.b.c.xxx` and user agents truncated to 256 characters.

The client IP is `HttpContext.Connection.RemoteIpAddress`, after the forwarded-headers middleware
has run. Raw `X-Forwarded-For` is never trusted directly.

**CAPTCHA.** `GlobalState.RequireCaptcha` is a timed flag: setting it lasts 5 minutes, and setting
it again extends the window. It's enforced only where the `Captcha` feature flag allows:

- Login shows a CAPTCHA when the flag is on **and** `RequireCaptcha` is set (or after a failure
  on that form).
- Register requires a CAPTCHA when either the flag is on **or** `RequireCaptcha` is set.

With reCAPTCHA keys missing, `NullReCaptchaSiteVerify` fails open (see
[service-resilience](../infrastructure/service-resilience.md)).

**Account lockout**, in Identity options: 3 failed attempts locks the account for 15 minutes,
and this applies to new users too.

**`AuthenticationTracker`** keeps the last 1,000 login and registration attempts, or 24 hours,
whichever is less. Each attempt records the username, IP, success, and failure reason
(`InvalidPassword`, `UserNotFound`, `LockedOut`, or an Identity error code). The tracker also
keeps suspicious-activity events. It powers the dashboard's hourly tables, top targeted usernames,
and top failing IPs. The failed-login check in step 4 reads it too.

**Configuration** (`appsettings.json`, section `RateLimiting`): `MaxRequestsPerWindow`,
`WindowMinutes`, `GlobalMaxRequestsPerWindow`, `GlobalWindowMinutes`, `CaptchaThresholdPercent`.
Defaults are shown above.

## Layer 3: Crawler short-circuit on identity pages

When someone shares a login-protected URL on Facebook (or another link-preview service), the
crawler fetches it, receives a `302` to `/Identity/Account/Login`, finds no useful OpenGraph data,
and retries. Across Meta's whole scraper fleet (the `57.141.0.x` subnet), that produced thousands
of login-page hits a day in March 2026. Per-IP limits can't stop it, because the fleet spreads
across many IPs. Global limits *can* stop it, but they penalize real users, and a `429` just
schedules another retry.

So on `GET /identity/*`, a user agent that matches `KnownCrawlerFragments` gets an immediate
`200`. That response is a minimal `noindex` page with "login required" OpenGraph tags, no
antiforgery cookie, `Cache-Control: no-store`, and an HTML-encoded path. The matching fragments are:

- `facebookexternalhit`, `facebot`, `meta-externalagent`, `meta-externalfetcher`
- `googlebot`, `gptbot`, `dotbot`, `sogou`, `petalbot`, `newsai`

Hits are counted in `GlobalState.MetaCrawlerStats` and shown on the dashboard.

Design rules:

- **`GET` only.** OAuth callbacks are `POST`s to `/signin-facebook` and similar, so Facebook
  Login is unaffected.
- **`whatsapp` and `instagram` are deliberately not listed.** Those tokens appear in real users'
  in-app browser user agents.
- **Don't block Meta IP ranges, and don't add `[AllowAnonymous]`** to pages that genuinely need
  auth. Fix the crawler response instead.

Two companions reinforce it:

- The cache-control middleware sends `no-store` on any 3xx to `/identity/*`, so a redirect is
  never cached, and it never marks `/identity/*` responses cacheable. See
  [hosting-and-identity](../infrastructure/hosting-and-identity.md#request-pipeline-notes).
- `robots.txt` disallows `/identity/` for `*`, Googlebot, `facebookexternalhit` and `Facebot`.

## Layer 4: 4xx-by-URL tracking (`Http4xxTracker`)

Added Aug 12, 2026, in response to a spike of 4xx (mostly 400/404) responses in production
logs. `app.UseHttpLogging()` doesn't surface the request URL by default, and turning up log
verbosity to get it would clutter the logs further, so this adds a bounded, in-process
aggregate view instead: an admin-visible table of the URLs generating the most 4xx responses,
independent of both `SpiderManager` (keyed by user-agent, not URL/status) and
`RateLimitingTracker` (keyed by IP, scoped to `/identity/*`, and already covers 429s). The
recurring review of this data is [runbooks/triage-4xx](../runbooks/triage-4xx.md).

### Components

**`Http4xxTracker`** (`m4d/Security/Http4xxTracker.cs`) — DI singleton, same shape as
`RateLimitingTracker`: a `CircularBuffer<Http4xxEvent>` (10,000 capacity, reusing the
`CircularBuffer<T>` already defined in `RateLimitingTracker.cs`) behind a `lock`.
`RecordEvent(url, statusCode)` appends an event; `GetStats(topN = 100, filter = All)` groups by
`(Url, StatusCode)`, orders by count descending, and returns the top N (or every distinct URL, if
`topN` is `null`) as `Http4xxUrlStats` (URL, status, count, last-seen timestamp, `IsKnownAttack`)
plus `TotalEventsTracked` / `LastHourCount` computed over the filtered set.

**Known-attack classification** — `Http4xxTracker.IsKnownAttackUrl(url)` (static, also used
directly by tests) flags a path as scanner/exploit noise rather than a real bug (query string
stripped before matching): a `KnownAttackPathPrefixes` list matched against the *start* of the
path (`/wp-`, `/wp/`, `/administrator`) plus a `KnownAttackPathSubstrings` list matched *anywhere*
in the path — needed because scanners commonly prefix a guessed app/framework directory before the
actual probe (e.g. `/admin/.env`, `/laravel/.env`, `/backend/.env`), so a `StartsWith` check alone
missed most of them. Expanded 2026-09-14 after a triage pass through `local/4xx-urls-2026-09-14.csv`
turned up dozens of previously-unclassified secret/credential-scanning and CMS-exploit probes, and
again 2026-09-19 (`local/4xx-urls-2026-09-19.csv`) after a sweep of SSH/TLS key material, CI and
container config, database dumps, and appliance-specific exploit paths. The lists now run ~10
prefixes and ~107 substrings, grouped by probe family in the source; read them there rather than
duplicating them here. Two rules govern what goes in: a pattern must name a file or route this app
has never served and never will, and short generic segments a future feature could plausibly use
(`/login`, `/dashboard`, `/account`, `/mcp`, `/sse`, `/manifest.json`) stay out, as do benign
platform probes (`/sitemap.xml`, `/.well-known/*`, `/apple-touch-icon*.png`) that are worth keeping
visible. `Http4xxTrackerTests.IsKnownAttackUrl_ClassifiesKnownPatterns` pins both directions -
every added pattern and every must-stay-visible URL.

`Http4xxUrlFilter` (`All` / `KnownAttacksOnly` / `ExcludeKnownAttacks`) is
applied to the event set *before* grouping/top-N, so filtering changes which URLs are eligible
for the top 100, not just which rows get hidden client-side.

**`Http4xxTrackingMiddleware`** (`m4d/Middleware/Http4xxTrackingMiddleware.cs`) — registered
in `M4dApplicationExtensions` immediately after `app.UseRouting()`, before the DB-recovery and
`RateLimitingMiddleware` blocks. Runs for every request, calls `await next(context)`, and if
the final `context.Response.StatusCode` is in `[400, 500)` and not `429` (429s are already
tracked in detail by `RateLimitingTracker` — excluded here to avoid duplicating that data),
records `Path + QueryString` (truncated to 512 chars to bound memory against adversarially
long URLs) and the status code.

Placement matters: it sits *inside* (later-registered than) `UseStatusCodePagesWithReExecute`
(registered earlier, in the non-Development branch). For a real 404, the first pass
through the middleware sees the original path with status 404 and records it;
`UseStatusCodePagesWithReExecute` then re-executes the pipeline against `/Error/404` to render
the error body, which re-enters the middleware a second time — but that second pass sees
status 200 (the `ErrorController` view rendering successfully), which falls outside the
`[400, 500)` check and is correctly skipped. No double-counting.

**Admin wiring**: `AdminController` takes `Http4xxTracker` as a constructor-injected singleton
(alongside `AuthenticationTracker`/`RateLimitingTracker`). `Diagnostics(Http4xxUrlFilter
http4xxFilter = All)` sets `ViewBag.Http4xxStats = _http4xxTracker.GetStats(100, http4xxFilter)`
and `ViewBag.Http4xxFilter`. Rendered in `Views/Admin/Diagnostics.cshtml` under "HTTP 4xx Errors"
(between "Authentication Security" and "Bot Stats"), as a card + table following the same
convention as the Rate Limiting/Auth sections — one row per (URL, status code), sorted by count,
with an "attack" badge on rows `IsKnownAttack` flags. Three query-string-driven links (All /
Known Attacks Only / Excluding Known Attacks) re-run `Diagnostics` with the filter applied server
side, so the displayed "top 100" reflects the filter rather than being truncated first and
filtered client-side.

`AdminController.Http4xxExportCsv()` (`GET /Admin/Http4xxExportCsv`) exports the **complete**
(uncapped — `GetStats(null, ExcludeKnownAttacks)`) unique-URL list as CSV via `CsvWriter`, always
excluding known attacks — this is the feed intended for tracking real broken links/bugs over time
without re-deriving the known-attack filter by hand each time.

**Test coverage**: `m4d.Tests/Security/Http4xxTrackerTests.cs` (aggregation, top-N ordering,
top-N limit and `null` = uncapped, empty/null handling, `IsKnownAttackUrl` classification cases,
filter behavior for `KnownAttacksOnly`/`ExcludeKnownAttacks`) and
`m4d.Tests/Middleware/Http4xxTrackingMiddlewareTests.cs` (records 4xx, ignores 2xx/5xx/429,
includes query string, truncates long URLs).

**Limitations (by design, like the other trackers)**: in-memory only,
resets on app restart; single-instance in-process (production runs as a single instance — no cross-instance aggregation, which is fine at
current scale); no automated alerting, admin must check `/Admin/Diagnostics` manually.

## Admin diagnostics

`/Admin/Diagnostics` (admin only) shows:

- **Rate limiting:** hourly activity (requests, limited, unique IPs, global vs per-IP hits; up to
  48 hours) and the top requesting IPs. Each IP links to `RateLimitIpDetail`.
- **Authentication security:** hourly attempts (failed, suspicious, unique IPs and usernames),
  a suspicious-activity breakdown, the most targeted usernames, and the most active failing IPs.
- **Meta and crawler short-circuit counts.**
- **HTTP 4xx errors:** the layer 4 table, with All / Known attacks / Excluding known attacks
  filters and a CSV export.
- **Bot stats:** the layer 1 user-agent hit counts.
- **Rate-limit logging toggle** (`ToggleRateLimitLogging`). Turning it on logs every identity
  request's rate-limit decision at Information level. Use it during an active attack, then turn
  it off.

Procedures that use this page: [runbooks/respond-to-attack](../runbooks/respond-to-attack.md) and
[runbooks/triage-4xx](../runbooks/triage-4xx.md).

## Tests

- `m4d.Tests/Security/`: `AuthenticationTracker`, `RateLimitingTracker` and `Http4xxTracker`
  (aggregation, top-N, filters, and `IsKnownAttackUrl` classification in both directions).
- `m4d.Tests/Middleware/Http4xxTrackingMiddlewareTests.cs`: which statuses are recorded,
  query-string handling, truncation.
- `RateLimitingMiddleware` itself (limits, delay, CAPTCHA escalation, crawler short-circuit) has
  **no direct tests**. It was verified by hand when it was built.

## Limitations

- **Everything is in-memory and per instance**, so stats reset on restart. There's no
  cross-instance aggregation.
- **No automated alerting.** Someone has to look at `/Admin/Diagnostics`. Application Insights is
  not currently enabled; see
  [log-persistence-options](../plans/log-persistence-options.md).
- **The global limit can catch real users** during an attack. They get a friendly `429` and,
  after it clears, a CAPTCHA.
- **Fixed windows.** The rate-limit window is 1 minute, the CAPTCHA lasts 5 minutes, and history
  covers about 48 hours at most.

## Future improvements

- **Phase 2 and beyond**, covered in
  [plans/attack-mitigation-phase2](../plans/attack-mitigation-phase2.md): persistent telemetry and
  email alerts, an edge WAF and IP reputation, honeypot fields, user-agent analysis, and later
  behavioral, geolocation and fingerprinting rules.
- **Log the `Referer` and `returnUrl`** when crawlers hit identity pages, to find which shared
  URLs trigger the loops.
- **Make the CAPTCHA duration and the crawler list configurable**, instead of compiled in.
- **Unit tests for `RateLimitingMiddleware`**, which today is covered only by the tracker tests and manual checks.

## History

- 2016: The `SpiderManager` search-result stub was introduced (`7e8bdc20`). The
  `CustomSearchController` call site was added in September 2024 (`07ba2607`).
- 2026-03-05: A distributed attack from 10+ IPs probed login and register (open-redirect probing,
  username enumeration, credential stuffing). Per-IP limits contained it. **Phase 1** shipped the
  same day: the global limit, CAPTCHA escalation, `AuthenticationTracker` and `RateLimitingTracker`,
  the dashboard, 3-strike/15-minute lockout, and suspicious-`returnUrl` detection. Earlier
  identity-endpoint work had added the random delay and the original per-IP limit.
- 2026-03: The Meta crawler redirect loop was diagnosed. The crawler short-circuit, `no-store` on
  identity redirects, and `robots.txt` rules were added. The crawler list was later widened beyond
  Meta.
- 2026-08-12: 4xx-by-URL tracking was added. It turned up a `BotFilter` view that was missing for
  `CustomSearchController`: the view existed only in `Views/Song/`, so bot traffic on
  `/customsearch/*` returned `500`. It was moved to `Views/Shared/`.
- 2026-08-16 onward: The recurring 4xx triage began, and the known-attack lists grew (2026-09-14,
  2026-09-19). See [runbooks/triage-4xx](../runbooks/triage-4xx.md).
- 2026-10-01: Consolidated from `distributed-attack-mitigation.md`,
  `identity-endpoint-protection.md` and `meta-crawler-mitigation.md`. The triage log moved to a
  runbook and Phase 2/3 to a plan. Corrected along the way: CAPTCHA escalation is driven by global
  identity-path *volume* (20% of the global limit), not by the share of requests rate-limited; and
  the per-IP limit is keyed per path. Phase 1 code listings are dropped; read the source.

## Related

- [runbooks/triage-4xx](../runbooks/triage-4xx.md): the recurring 4xx review and its triage log
- [runbooks/respond-to-attack](../runbooks/respond-to-attack.md)
- [plans/attack-mitigation-phase2](../plans/attack-mitigation-phase2.md)
- [hosting-and-identity](../infrastructure/hosting-and-identity.md): pipeline order, cache headers, forwarded headers
- [account-management](../users-admin/account-management.md): Identity, lockout, user policies
- [payments-and-premium](../users-admin/payments-and-premium.md): CAPTCHA on anonymous donations
- [usage-tracking](../observability/usage-tracking.md): usage logging and the antiforgery `400` investigation
