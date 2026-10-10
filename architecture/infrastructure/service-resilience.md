# Service Resilience

**Type:** Reference
**Status:** Current
**Last verified:** 2026-10-10
**Code:** `m4d/Services/ServiceHealth/`, `m4d/Services/DatabaseRecoveryService.cs`,
`m4d/Services/AppConfigurationRecoveryService.cs`, `m4d/Configuration/AppConfigurationStartup.cs`,
`m4d/Configuration/SecretBackedServices.cs`,
`m4d/Controllers/HealthController.cs`, `m4d/Configuration/M4dApplicationExtensions.cs`,
`m4d/ClientApp/src/composables/useServiceHealth.ts`, `m4d/ClientApp/src/components/ServiceStatusBanner.vue`

How music4dance keeps serving pages when an external dependency (SQL, Azure AI Search, App
Configuration, OAuth providers, email, reCAPTCHA) is missing or failing. The goal: every page
renders at least the site chrome and a clear status message, the app starts with any or every
dependency down, and it recovers without a restart once the dependency comes back.

## Tracked services

`ServiceHealthManager` (a singleton, created before DI so startup code can use it) holds one
`ServiceHealthStatus` per service name: `Status` (`Unknown` / `Healthy` / `Degraded` /
`Unavailable`), `LastChecked`, `LastHealthy`, `ErrorMessage`, `ResponseTime`,
`ConsecutiveFailures`, `NotificationSent`.

| Service name | Marked healthy | Marked unavailable | What degrades |
| --- | --- | --- | --- |
| `Database` | Startup migration succeeds; `DatabaseRecoveryService` probe succeeds; `DanceStatsInstance` reads succeed | No connection string; migration fails; `SqlException` in `DMController.OnActionExecutionAsync`; `DanceStatsHostedService` / `DanceStatsInstance` failures | Identity pages blocked; user treated as anonymous; dance data served from file cache |
| `SearchService` | Every successful live query (`SongIndex.DoSearch` → `ISearchServiceManager.ReportSearchSuccess`) | Client registration fails; any search entry point catching an "Azure Search service is unavailable" error (credential failure, `503`/`429` throttling) | Song lists/details return empty results or an error view; banner shown |
| `AppConfiguration` | Startup load succeeds; `AppConfigurationRecoveryService` completes a failed load | Endpoint missing; the startup load fails or times out (100s); the first background refresh (`StartupInitializationService`) fails | Falls back to local `appsettings.json` and feature-flag defaults until the load completes, which means no secrets, so OAuth, email and reCAPTCHA are unavailable too |
| `GoogleOAuth`, `FacebookOAuth`, `SpotifyOAuth` | Credentials present at startup, or when App Configuration recovers | Credentials missing at startup | Provider registered with placeholder options and hidden by `M4dSignInManager` |
| `EmailService` | ACS connection string present (startup or recovery) | Missing at startup | `NullEmailSender` resolved until the string appears; confirmation/reset email not sent |
| `ReCaptcha` | Keys present (startup or recovery) | Missing at startup | `NullReCaptchaSiteVerify` resolved (fails open) until the keys appear; no captcha challenge |

The OAuth, email and reCAPTCHA entries are **configuration checks**, made at startup and again
when App Configuration recovers (`SecretBackedServices.UpdateHealth`). Nothing probes those
services live. All three read their credentials from `IConfiguration` when used, not at
registration, so credentials that arrive late take effect without a restart (see
[Recovery](#recovery)).

### Healthy vs available

- `IsServiceHealthy(name)` is what callers use to decide whether to try a dependency. An unknown
  service counts as healthy (optimistic). An `Unavailable` service counts as unhealthy only
  until `UnavailableCooldown` (1 minute, hardcoded) has passed since its last failure. After
  that it reports healthy again, so the next caller retries the real operation. Another failure
  re-marks it and restarts the cooldown, so a sustained outage gets retried about once a minute
  rather than on every request, and a transient spike clears on its own.
- `IsServiceAvailable(name)` is stricter: `Healthy` or `Degraded` only, and `false` for unknown.

## Startup

Startup never throws for a missing dependency. Each registration in
`M4dApplicationExtensions` is wrapped in try/catch. On failure it marks the service unavailable,
logs it, and registers a fallback so DI resolution still works:

- **Search:** `NullSearchClientFactory` / `NullSearchIndexClientFactory`.
- **Database:** a placeholder `DbContext`.
- **Email:** `NullEmailSender`, chosen per resolution while the connection string is missing.
- **reCAPTCHA:** `NullReCaptchaSiteVerify`, chosen per resolution while the keys are missing.
- **OAuth:** the provider is still registered, with placeholder client id and secret, because the
  authentication middleware validates every remote handler's options on every request and an
  empty client id would fail the whole site. Its redirect to the provider is refused.

Search is deliberately *not* marked healthy at registration. The Azure SDK connects lazily, so
the first real query decides.

The database `DbContext` re-reads its connection string from `IConfiguration` on every context
resolution, with `EnableRetryOnFailure(5)` and a 60s command timeout for Azure SQL cold starts.
That way a connection string changed mid-run is picked up without a restart. Migrations run
synchronously before `app.Run()`, and `Database` is marked healthy or unavailable based on the
result.

After startup the app prints a `GenerateStartupReport()` summary to the console and attaches the
`ServiceHealthNotifier`. In Azure it also emails a status report once the instance is serving
(see [Admin notifications](#admin-notifications)).

## Degraded behavior

**Search.** `SongIndex.DoSearch` converts credential failures and `RequestFailedException`
`503`/`429` (for example Azure's `capacityOverloaded` throttling) into
`InvalidOperationException("Azure Search service is unavailable")`. `IsSearchServiceError` in
`DMController` / `DMApiController` recognizes it. The many search entry points (`SongSearch`,
`ServiceTrackController`, `MusicServiceController`, `SpotifyPlaylistController`,
`PlayListController`, …) mark `SearchService` unavailable and return empty results, an error view,
or a `503` JSON response instead of an unhandled exception.

**Database.**
- If `UserMetadata.Create` throws, `DMController.OnActionExecutionAsync` marks the database
  unavailable and continues with `UserMetadata.Anonymous`, so the page renders for an anonymous
  visitor instead of an error page.
- `_bs5-Layout.cshtml` replaces Identity-area pages (login, register, manage) with a "Database
  Temporarily Unavailable" alert while the database is unhealthy.
- Server-side usage logging (`DMController`) and search logging (`SongSearch`) skip their writes
  while the database is down.

**Dance data.** Dance pages, Tempi and Counter read the dance environment through
`DanceStatsFileManager.GetStats()`, which tries, in order:

1. The runtime cache, `wwwroot/AppData/dance-environment.json`. It's written whenever stats are
   rebuilt from the database.
2. The checked-in cold-start snapshot, `content/dance-environment-fallback.json`. Its source is
   `m4d/ClientApp/src/assets/content/dance-environment-fallback.json`.
3. Nothing, which leaves an empty dance environment.

The snapshot exists for fresh deployments, where the runtime cache hasn't been built yet. It
goes stale between refreshes; see
[runbooks/refresh-dance-fallback-snapshot](../runbooks/refresh-dance-fallback-snapshot.md).
`artist-index-fallback.json` plays the same role for the artist index (see
[individual-artists](../songs/individual-artists.md)).

**Login.** The external-login buttons come from
`SignInManager.GetExternalAuthenticationSchemesAsync()`, which `M4dSignInManager` overrides to
drop providers whose credentials aren't in configuration right now. The same filter covers the
register and manage-logins pages. (`Login.cshtml.cs` also sets
`GoogleAvailable` / `FacebookAvailable` / `SpotifyAvailable` from `IsServiceHealthy`, but no view
reads them.)

## Recovery

- **Search:** recovers immediately on the next successful live query, because
  `SearchServiceManager.OnSearchSuccess` is wired in DI to `MarkHealthy("SearchService")`.
  Otherwise it recovers implicitly through the cooldown.
- **Database:** `DatabaseRecoveryService.TriggerRecoveryIfNeeded()` runs early in the request
  pipeline. While `Database` is `Unavailable` and a connection string exists, it starts a
  fire-and-forget reconnection probe, never blocking the request. Probes run at most once per
  `ServiceHealth:DatabaseRetryInterval` (default `00:01:00`, set in `appsettings.json`), and only
  one runs at a time. On success it marks the database healthy and re-runs the deferred
  `FixupStats` initialization. This covers the on-demand Azure SQL cold start, where the app
  wakes 20–45 seconds before the database.
- **App Configuration and the secret-backed services:** see below.
- **Everything else:** recovers only through the cooldown, or on restart.

### App Configuration recovery

Every secret comes from App Configuration and its Key Vault references, so a failed startup load
used to leave OAuth, email and reCAPTCHA down until someone restarted the app. It now recovers in
place:

1. `AppConfigurationStartup` adds the provider as **optional** with a 100s startup timeout. A
   failed load leaves the provider in the configuration chain with no data, instead of throwing.
   Failures the provider doesn't treat as optional (a credential error, say) do throw; then a
   second provider is added whose first load is held back by a gated credential, so there's
   still one in the chain to complete later.
2. On failure, the Azure SDK events from the load window (identity, App Configuration, Key Vault)
   are printed to the console, so the log shows which call failed.
3. `AppConfigurationRecoveryService` calls the refresher every minute. The provider turns a
   refresh of a store that never loaded into a full load. It throttles those attempts itself:
   once per refresh interval (5 minutes), and after failures it backs the endpoint off for 30s,
   doubling to 10 minutes. The `UseAzureAppConfiguration()` middleware also triggers refreshes
   on requests. Each full load is a single attempt (no startup retry loop), so App Configuration
   requests get a 2-minute network timeout once startup is over, against 30s during the startup
   load: refreshes run in the background, and a store that is slow rather than down should
   succeed on the first try instead of failing every one and backing off.
4. When data arrives, `IConfiguration` reloads. Email, reCAPTCHA and the OAuth options read it
   on next use (the OAuth options are rebuilt through a `ConfigurationChangeTokenSource`), and
   marketing settings are re-read on the reload token. The service marks `AppConfiguration` and
   each configured secret-backed service healthy, logs it, and emails the admins. That is the
   first notice they get, since the startup failure email had no email settings to send with.

The app is never restarted, so an intermittently slow App Configuration costs a delay, not a
recycle.

### App Configuration quota

A store that is over its request quota answers every request with `429` and a body like
`{"title":"Resource utilization has surpassed the assigned quota","policy":"Read Requests"}`.
The startup load retries those requests until it times out, so the log shows a 100s failed load
with `429`s in the Azure SDK events, then `429`s about every 5 minutes from the recovery path.
Both the failed request and its one client retry count against the quota.

The store is on the Developer tier (6,000 requests an hour, reset at the end of the hour; see
[hosting-and-identity § Identity and secrets](hosting-and-identity.md#identity-and-secrets)).
An Azure Monitor metric alert, **App Configuration request quota above 80%**, watches
`Request Quota Usage` (Maximum, over 1 hour, checked every 15 minutes) and emails the admins
through an action group. To check usage by hand, open the store's **Monitoring → Metrics** and
chart `Request Quota Usage` (Max) at a 1-hour grain. The portal's Overview request-count chart
doesn't match it, so don't use that. To see who is calling, add a diagnostic setting that sends
the **HTTP Requests** log category to Log Analytics and query `AACHttpRequest`, summing
`HitCount` by `ClientIPAddress` and `UserAgent`.

## Health endpoints

| Endpoint | Purpose | Returns |
| --- | --- | --- |
| `/health/startup` | Liveness: the process is listening | Always `200` |
| `/health/ready` | Readiness: mapped directly on the pipeline, no MVC dependency | `200 ready` when `IsServiceHealthy("Database")`, else `503` |
| `GET /api/health` | Coarse status | `503` only when `Database` is `Unavailable`; else `healthy` / `degraded` |
| `GET /api/health/status` | Full JSON: per-service status, summary, and `updateMessage` (the admin "update warning") | `503` when any service is `Unavailable` |
| `GET /api/health/report` | Human-readable HTML table | `200` |

The deploy pipeline points the App Service health check at `/health/ready`. See
[hosting-and-identity § Health checks](hosting-and-identity.md#health-checks) for what Azure
does with it on a single-instance plan.

## Client status banner

`_head.cshtml` seeds `menuContext.searchHealthy`, `databaseHealthy` and `configurationHealthy`,
so the first render already knows the state. `useServiceHealth()` (used by `PageFrame.vue` and
the header app) then polls `/api/health/status`: every 30 seconds while anything is unhealthy or
an update message is showing, and hourly otherwise. `ServiceStatusBanner.vue` shows service
warnings plus the admin update message. The update message is set with `Admin/UpdateWarning`,
kept in memory in `GlobalState.UpdateMessage`, and lost on restart.

## Admin notifications

`ServiceHealthNotifier` emails the recipients in `ServiceHealth:AdminNotifications` through the
same Azure Communication Services sender used for account email. It's attached on every startup
and reads its settings and the email sender at send time, so it works after a clean start and
after App Configuration recovers. Subjects end with the App Service site name
(`WEBSITE_SITE_NAME`), and the body names the instance and lists every service's status.

| Email | When |
| --- | --- |
| **Status** (`Started healthy` / `Started degraded (...)`) | Once per start, when `ServiceHealth:AdminNotifications:StartupStatus` is `true` (set it per environment), on `ApplicationStarted`: after migrations and the hosted services' startup, so it shows the real database state. At that point a `[Notifications] Enabled=..., Recipients=..., StartupStatus=..., EmailService=...` line is printed to the console whether or not the email is on. |
| **Service Failure** | A service goes from healthy (or unknown) to unavailable. `NotificationSent` suppresses repeats until it's healthy again. |
| **Service Recovered** | A service whose failure was emailed is marked healthy again |
| **Service Recovered: AppConfiguration** | `AppConfigurationRecoveryService` completes a failed startup load. This doubles as the startup report for that case, since the status email couldn't be sent. |
| **Action Needed: Reconnect the Spotify service account** | `ServiceAccountMonitor` finds the Spotify service account within 14 days of expiry, expired, or rejected by Spotify. At most once a day, plus once at a new rejection. See [spotify-playlist-automation](../music-services/spotify-playlist-automation.md#the-service-account). |

A service's failure email is skipped if one went out for it in the last 30 minutes
(`NotificationCooldown`), so a flapping service (search throttling, say) sends one
failure/recovery pair per half hour, not one per flap. A skipped failure gets no recovery email.
Likewise, when notifications are disabled or email isn't configured, nothing is marked as
notified, so no lone "recovered" email follows. Configuration and testing steps are in
[runbooks/configure-failure-email](../runbooks/configure-failure-email.md).

## Known issues

- **No email while App Configuration is down.** The Azure Communication Services connection
  string and the `ServiceHealth:AdminNotifications` settings live in App Configuration, so the
  startup status email can't go out in exactly this case. The first email is the AppConfiguration
  recovery email. If App Configuration never recovers, no email is sent; check the log stream.
- **The cooldown can report the database ready while it's still down.** `IsServiceHealthy`
  reports any `Unavailable` service as healthy one minute after its last failure. A failed
  `DatabaseRecoveryService` probe doesn't re-mark the database, so between probes `/health/ready`,
  `menuContext.databaseHealthy` and the Identity-area guard can treat it as available. Request-path
  `SqlException`s do re-mark it under real traffic. Configuration-check entries (OAuth, email,
  reCAPTCHA) also flip back to "healthy" in `/api/health/status`, but that's cosmetic: those
  services check their credentials themselves on each use.
- **The song list has no "search unavailable" message.** `song-index/App.vue` has no `v-else`
  for `searchAvailable`, so while search is down the page renders only the chrome.
- **Unused code.** The partial views `Views/Shared/ServiceStatus/_SearchUnavailable`,
  `_DatabaseUnavailable`, `_AuthUnavailable` and `_ServiceUnavailableNotice` aren't referenced
  anywhere, and neither are the login page's `*Available` ViewData flags. The live messaging is
  the Vue banner and the Identity-area alert in `_bs5-Layout.cshtml`.
- **Root cause of the September 2026 search throttling is still open.** Was it a traffic spike,
  a concurrent reindex or backup, or too few replicas? Answering it needs the Search resource's
  metrics in the Azure Portal.

## Future improvements

- **Fix the known issues above.** Re-mark the database on failed recovery probes, or exempt it from the cooldown. Add the song-list unavailable
  message. Delete the unused partials and flags.
- **Make the cooldown configurable** (`ServiceHealth:UnavailableCooldown`).
- **Automate the fallback snapshot refresh**, for example with a scheduled job that exports and
  opens a PR, or an export step in the deploy pipeline.
- **Live health probes** for the OAuth, email and reCAPTCHA providers, possibly informed by
  vendor status pages.
- **Make the notification cooldown configurable**, and consider a digest instead of per-service
  emails if storms across several services become a problem.
- **Persist the update message**, so it survives restarts and could be scheduled.
- **Admin dashboard** with health history, response times and uptime, and export to Azure
  Monitor.
- **Circuit breakers** per service (closed / open / half-open), if the cooldown proves too
  coarse.
- **Cache common anonymous search results**, to serve during search outages.
- **Faster App Configuration recovery**, if a slow store keeps making recovery take tens of
  minutes even with the 2-minute timeout after startup:
  - *Check the startup log first.* A failed load prints the Azure SDK events from the load
    window. Requests timing out mean a slow store, which longer timeouts address. Requests
    failing outright (managed identity or Key Vault errors) won't be helped by any timeout.
  - *Retry the load more often.* The provider retries a never-loaded store at most once per
    `MinRefreshInterval`, the smallest of the sentinel and feature-flag refresh intervals (both
    5 minutes), so the 1-minute recovery loop mostly does nothing. Lowering both to 1 minute
    retries recovery five times as often, but it also multiplies routine polling about fivefold.
    Check the result against the Developer tier's 6,000 requests an hour and the 3,000 a day
    covered by its daily charge (see [App Configuration quota](#app-configuration-quota)).
    The provider's failure backoff (30s doubling to 10 minutes) isn't configurable.

## History

- 2025-12-14: Resilience plan drafted. Phase 1 added `ServiceHealthManager`, try/catch service
  registration, and a startup report.
- 2025-12-17: Phase 2. Backend degradation: health endpoints, `503` responses, OAuth provider
  checks on login, background logging guards.
- 2025-12-18: Phase 3. Frontend `ServiceStatusBanner` and polling, MenuContext health flags,
  Identity-area blocking, `UNAVAILABLE` user rendering.
- 2025-12: Phase 4. Admin failure emails (`ServiceHealthNotifier`); update warning carried by
  health polling.
- 2025-12-22: Phase 5. Static cold-start fallback. It was originally a client-side
  `public/cache/` with `LoadDanceDatabase.ts`; it is now the server-side
  `dance-environment-fallback.json` read by `DanceStatsFileManager`.
- 2026-01-14: Phase 6. Azure Search credential errors propagate to every search entry point,
  with no premature "healthy" at startup.
- 2026-06-02: Phase 7. `DatabaseRecoveryService`, live connection-string reload, and
  `UserMetadata.Anonymous` fallback.
- 2026-09-03: Phase 8. Search `503`/`429` classification, `UnavailableCooldown`, and
  `ReportSearchSuccess` recovery.
- 2026-10-06: App Configuration recovers in place after a failed startup load
  (`AppConfigurationStartup`, `AppConfigurationRecoveryService`). Email, reCAPTCHA, OAuth,
  marketing settings and admin notifications read their configuration at use time. Startup
  timeout cut from 100s (the provider default) to 30s.
- 2026-10-09: The first production recovery took 26 minutes, apparently from a slow store. The
  startup timeout is back to 100s, and the per-request network timeout is 2 minutes after
  startup (30s during the startup load).
- 2026-10-10: A test restart was throttled (`429`, request quota exceeded) for 40 minutes, then
  recovered in place. The Free tier's 1,000 requests a day ran out most days. The store moved to
  the Developer tier, and a `Request Quota Usage` alert was added.
- 2026-10-07: Notifier attached on every startup (runtime failure emails no longer need a
  degraded start). Added recovery emails, a 30-minute per-service failure-email cooldown, and a
  status email on each instance start in Azure, which replaces the startup-failure email.
- 2026-10-07: The status email is opted into with `ServiceHealth:AdminNotifications:StartupStatus`
  instead of running only in Azure, so it can be tested locally and turned off per environment.
  A `[Notifications]` console line on startup shows the settings the notifier sees.
- 2026-10-01: The plan and the eight phase reports were consolidated into this document. The
  originals are in git history under `architecture/infrastructure/service-resilience-*.md`.

## Related

- [overview](../overview.md): system map, including every external dependency
- [hosting-and-identity](hosting-and-identity.md): App Service, health check probe, startup sequence
- [background-work-and-startup](background-work-and-startup.md): hosted services, start order and failure behavior, `DatabaseRecoveryService` in context
- [configuration-and-feature-flags](configuration-and-feature-flags.md): the keys whose absence marks a service unavailable
- [data-layer](data-layer.md): `DanceMusicContext` registration and startup migrations
- [runbooks/configure-failure-email](../runbooks/configure-failure-email.md)
- [runbooks/refresh-dance-fallback-snapshot](../runbooks/refresh-dance-fallback-snapshot.md)
- [dance-domain-model](../dances/dance-domain-model.md): `DanceStats` build, file cache and startup load
- [user-name-visibility](../users-admin/user-name-visibility.md): `UNAVAILABLE` rendering when user data can't be loaded
- [admin-pages](../users-admin/admin-pages.md): admin diagnostics surfaces
- [frontend-architecture](../pages/frontend-architecture.md): `menuContext`, `PageFrame` and the header app that host the banner
