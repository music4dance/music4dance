# Usage Tracking and Analysis

**Type:** Reference
**Status:** Current
**Last verified:** 2026-10-01
**Code:** `m4d/ClientApp/src/composables/useUsageTracking.ts`, `m4d/ClientApp/src/config/usageTracking.ts`,
`m4d/APIControllers/UsageLogController.cs`, `m4d/Controllers/DMController.cs` (`OnActionExecutionAsync`),
`m4d/Controllers/UsageLogController.cs` (admin analysis), `m4dModels/UsageLog.cs`, `m4d/Views/Shared/_head.cshtml`

music4dance records one `UsageLog` row per page view. The data feeds the admin usage-analysis
pages and the per-user `LastActive` / `HitCount` values, which the engagement system uses (see
[visitor-engagement-monetization](../pages/visitor-engagement-monetization.md)). There are two
recording paths, and a feature flag picks one.

## Two recording paths

| | Server-side (legacy) | Client-side |
| --- | --- | --- |
| Where | `DMController.OnActionExecutionAsync` on every MVC action | `useUsageTracking()` composable, initialized by `MainMenu.vue` |
| Enabled when | `UsageLogging` flag on **and** `ClientSideUsageLogging` off | `ClientSideUsageLogging` flag on |
| Identity | `usageId` cookie | `usageId` in `localStorage` |
| Works with edge-cached pages | No | Yes |
| Bot filtering | `SpiderManager.CheckAnySpiders` (see [bot-and-abuse-defense](../security/bot-and-abuse-defense.md)) | User-agent patterns (`bot`, `crawl`, `spider`, `slurp`, `headless`, `phantom`, `puppeteer`, `selenium`, `webdriver`), `navigator.webdriver`, and automation globals |
| Skips writes when the DB is down | Yes | Yes (writes go through the background queue) |

The client-side path exists so that tracking keeps working if anonymous pages are ever cached at
the edge. Cached responses never reach `OnActionExecutionAsync`. Azure Front Door is not deployed
today (see [plans/front-door-caching](../plans/front-door-caching.md)), and turning
`ClientSideUsageLogging` on is a prerequisite for that plan. When the flag is on, the server path
returns early, before setting any tracking cookie. **Rollback** is to turn the flag off in App
Configuration; server-side tracking resumes on the next request.

`_head.cshtml` disables client-side tracking (`menuContext.useClientSideTracking = false`) for
paths under `/admin/`, `/identity/` and `/api/`. The same exclusion suppresses the engagement
prompts.

## Client-side flow

1. On page load, `MainMenu.vue` calls `useUsageTracking()` using the settings from
   `menuContext.usageTracking`. Those come from the `UsageTracking` section of `appsettings.json`:
   `Enabled`, `AnonymousThreshold` (3), `AnonymousBatchSize` (5), `AuthenticatedBatchSize` (1)
   and `MaxQueueSize` (100).
2. If the visitor looks like a bot, nothing happens.
3. It loads or creates `usageId` (a random UUID), the event queue and the visit count, all in
   `localStorage`. If `localStorage` is unavailable, it uses the fixed fallback ID
   `00000000-0000-0000-0000-000000000001` and a visit count of 0, so anonymous batches never
   send. A random per-page UUID would be meaningless in this multi-page app.
4. It appends the page-view event to the queue. The queue is trimmed to `MaxQueueSize`.
5. **Sending:**
   - Authenticated visitors send every event immediately (threshold 1, batch 1).
   - Anonymous visitors queue until their third page, then send in batches of 5. This
     deliberately ignores single-page bounces.
6. **On unload** (`visibilitychange` to hidden, or `pagehide`): remaining queued events go out by
   `navigator.sendBeacon`. Where the Navigation API exists, a same-origin navigation sets a flag
   that skips this send, so the queue keeps building across internal page loads. Browsers
   without the Navigation API send on every unload.

Every send is `sendBeacon` to `POST /api/usagelog/batch`, as `FormData` containing `events`
(JSON) and `__RequestVerificationToken` (taken from `menuContext.xsrfToken`).

## Batch endpoint

`api/UsageLogController.LogBatch`:

1. **Validates antiforgery explicitly**, with `IAntiforgery.ValidateRequestAsync`, and logs a
   Warning on failure: whether the antiforgery cookie and the form token were present, the content
   type, and whether the user was authenticated. It doesn't use `[ValidateAntiForgeryToken]`,
   because that attribute fails silently. See [History](#history).
2. Rejects empty input, bad JSON, or more than 100 events, each with `400`.
3. Enqueues a background task (`IBackgroundTaskQueue`) that inserts the rows and, for an
   authenticated user, sets `LastActive` and adds the event count to `HitCount`.
4. Returns `202 Accepted` immediately. This is fire-and-forget: the client never learns the outcome.

## Data model

`UsageLog`:

- `Id`
- `UsageId`: the tracking GUID, indexed
- `UserName`: indexed
- `Date`
- `Page`: the path
- `Query`: the query string
- `Filter`: the serialized `SongFilter`, if any
- `Referrer`
- `UserAgent`

`UsageSummary` is the per-`UsageId` aggregate used by the analysis pages.

## Analysis pages

`/UsageLog/*` (`showDiagnostics` role), in `m4d/Controllers/UsageLogController.cs`:

| Action | What it shows |
| --- | --- |
| `Index(botFilter)` | High-use tracking IDs (more than 5 hits). The default `ExcludeBots` view is cached in a static model until `ClearCache` |
| `Pages(useBaseUrl, userHitFilter, pageType, botFilter)` | Page popularity by unique users. Defaults: other (non-song) pages, base URL, multi-hit users, bots excluded |
| `PageLog(page, exactMatch)` | Rows for one page. `/song/details/{id}` pages are enriched with the song's title and artist |
| `DayLog(days)`, `UserLog(user)`, `IdLog(usageId)` | Raw rows by day, by user, or by tracking ID |

Bot filtering in these queries is a SQL `LIKE` against the patterns `bot`, `spider`, `crawler`,
`slurp` and `Mediapartners`. It's independent of both the client-side filter and `SpiderManager`.
How to read the results, and the indexes worth adding locally, are in
[runbooks/analyze-usage-logs](../runbooks/analyze-usage-logs.md).

## Privacy

`usageId` is a random GUID in `localStorage` (client path) or a cookie (server path). It isn't
derived from personal data. Rows for signed-in users do carry `UserName`.

## Tests

- `m4d/ClientApp/src/composables/__tests__/useUsageTracking.test.ts`: ID persistence, bot
  detection, the queue and its trimming, threshold and batch logic, the `FormData` token, and
  `sendBeacon`.
- `m4d.Tests/APIControllers/UsageLogApiControllerTests.cs` (and
  `UsageLogApiControllerTests_Clean.cs`, which looks like a near-duplicate worth consolidating):
  validation, enqueueing, and authentication detection. These use the `DanceMusicTester`
  integration pattern from [testing-patterns](../dev-testing/testing-patterns.md).

## Known issues

- **Anonymous batches can set `UserName`.** The background task stores
  `userName ?? eventDto.UserName`. For an anonymous request, that means a username supplied by the
  client is written as-is, so a crafted batch could attribute page views to any user. The fix is to
  ignore `UserName` from the client when the request isn't authenticated.
- **No rate limiting on `/api/usagelog/batch`.** Today the background queue is the only throttle.
- **No retention or cleanup.** Rows accumulate indefinitely. An earlier version of this doc claimed
  90-day retention and a `usageTrackingOptOut` switch; neither exists.
- **`m4d/APIControllers/UsageLogApiController.cs` is an empty file.**

## Future improvements

- **Fix the known issues above:** ignore client `UserName`, add per-IP or per-`usageId` limits,
  add a retention job, and delete the empty file.
- **A user opt-out**, if it's ever wanted. Add it to the composable, and make it honor Do Not Track
  or Global Privacy Control.
- **Playwright coverage** of the Navigation API and unload behavior.
- **Offline queueing and session-level aggregation**, to send fewer, larger events.
- **A health signal** for the background task queue.

## History

- 2025-01: Client-side tracking built: the composable, the batch endpoint, and the
  `ClientSideUsageLogging` flag. It shipped turned off.
- 2026 (date unrecorded): Usage analysis pages added (`Pages`, `PageLog`, bot and hit filters,
  song enrichment). No schema change was needed.
- 2026-09-03: `LogBatch` switched from `[ValidateAntiForgeryToken]` to explicit validation with
  logging, after a recurring, unexplained `400` rate in the 4xx tracker. The built-in filter never
  logs, so the cause was invisible. `SuggestionController` got the same treatment on 2026-09-14.
- 2026-09-14: **Root cause of those 400s.** Persisted Warning logs showed 20 of 24 failures were
  "required antiforgery cookie ... is not present" with the form token present. The token itself
  was fine; the cookie was missing. The mechanism:
  - The antiforgery cookie was a *session* cookie, cleared when the browser closes.
  - The anonymous HTML that embeds the matching token is served with
    `Cache-Control: public, max-age=300` and survives a browser restart in the disk cache.
  - So a reopened, cached page offers a token with no cookie to match.

  It wasn't `sendBeacon`-specific: suggestion lookups failed the same way. It wasn't a CDN either,
  since Front Door was never deployed. **Fix:** `AddAntiforgery(o => o.Cookie.Expiration =
  TimeSpan.FromDays(1))`, matching the identity cookie. The explicit logging stays, to confirm
  the fix.
- 2026-10-01: Consolidated from `client-side-usage-logging.md`, `usage-log-analysis-plan.md` and
  `server-side-testing-analysis.md`.

## Related

- [runbooks/analyze-usage-logs](../runbooks/analyze-usage-logs.md)
- [visitor-engagement-monetization](../pages/visitor-engagement-monetization.md): consumer of `HitCount` and engagement levels
- [bot-and-abuse-defense](../security/bot-and-abuse-defense.md): `SpiderManager`, and the 4xx tracker that surfaced the antiforgery failures
- [hosting-and-identity](../infrastructure/hosting-and-identity.md): cache-control headers and antiforgery cookie lifetime
- [plans/front-door-caching](../plans/front-door-caching.md)
