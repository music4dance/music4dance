# Service Resilience

**Type:** Reference
**Status:** Current
**Last verified:** 2026-10-01
**Code:** `m4d/Services/ServiceHealth/`, `m4d/Services/DatabaseRecoveryService.cs`,
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
| `AppConfiguration` | Registration succeeds | Endpoint missing; registration or background connect (`StartupInitializationService`) fails | Falls back to local `appsettings.json` and feature-flag defaults |
| `GoogleOAuth`, `FacebookOAuth`, `SpotifyOAuth` | Credentials present at startup | Credentials missing at startup | Provider isn't registered, so it never appears on the login page |
| `EmailService` | ACS connection string present | Missing → `NullEmailSender` registered | Confirmation/reset email not sent |
| `ReCaptcha` | Keys present | Missing → `NullReCaptchaSiteVerify` (fails open) | No captcha challenge |

The OAuth, email and reCAPTCHA entries are **configuration checks at startup only**. Nothing
probes those services live.

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
- **Email:** `NullEmailSender`.
- **reCAPTCHA:** `NullReCaptchaSiteVerify`.

Search is deliberately *not* marked healthy at registration. The Azure SDK connects lazily, so
the first real query decides.

The database `DbContext` re-reads its connection string from `IConfiguration` on every context
resolution, with `EnableRetryOnFailure(5)` and a 60s command timeout for Azure SQL cold starts.
That way a connection string changed mid-run is picked up without a restart. Migrations run
synchronously before `app.Run()`, and `Database` is marked healthy or unavailable based on the
result.

After startup the app prints a `GenerateStartupReport()` summary to the console. If anything is
unavailable or degraded, it attaches a `ServiceHealthNotifier` and sends one consolidated
startup-failure email (see [Admin notifications](#admin-notifications)).

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
`SignInManager.GetExternalAuthenticationSchemesAsync()`. A provider whose credentials were
missing at startup was never registered, so it simply doesn't appear. (`Login.cshtml.cs` also sets
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
- **Everything else:** recovers only through the cooldown, or on restart.

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
same Azure Communication Services sender used for account email. It sends one email per failure
incident: `NotificationSent` suppresses repeats until the service is marked healthy again.
There are no recovery emails. Configuration and testing steps are in
[runbooks/configure-failure-email](../runbooks/configure-failure-email.md).

## Known issues

- **Runtime failure emails only work after a degraded startup.** The notifier is created and
  attached (`SetNotifier`) only inside the "startup had failures" branch of
  `M4dApplicationExtensions`. After a clean startup, `_notifier` stays null, so a later outage
  sends no email. The console logger factory created for that notifier is also disposed as soon
  as startup ends.
- **The cooldown can report the database ready while it's still down.** `IsServiceHealthy`
  reports any `Unavailable` service as healthy one minute after its last failure. A failed
  `DatabaseRecoveryService` probe doesn't re-mark the database, so between probes `/health/ready`,
  `menuContext.databaseHealthy` and the Identity-area guard can treat it as available. Request-path
  `SqlException`s do re-mark it under real traffic. Startup-only entries (OAuth, email,
  reCAPTCHA) also flip back to "healthy" in `/api/health/status`, but that's cosmetic: their
  fallbacks were fixed at registration.
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

- **Fix the known issues above.** Attach the notifier unconditionally. Re-mark the
  database on failed recovery probes, or exempt it from the cooldown. Add the song-list unavailable
  message. Delete the unused partials and flags.
- **Make the cooldown configurable** (`ServiceHealth:UnavailableCooldown`).
- **Automate the fallback snapshot refresh**, for example with a scheduled job that exports and
  opens a PR, or an export step in the deploy pipeline.
- **Live health probes** for the OAuth, email and reCAPTCHA providers, possibly informed by
  vendor status pages.
- **Optional recovery emails, and throttling** for notification storms.
- **Persist the update message**, so it survives restarts and could be scheduled.
- **Admin dashboard** with health history, response times and uptime, and export to Azure
  Monitor.
- **Circuit breakers** per service (closed / open / half-open), if the cooldown proves too
  coarse.
- **Cache common anonymous search results**, to serve during search outages.

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
- 2026-10-01: The plan and the eight phase reports were consolidated into this document. The
  originals are in git history under `architecture/infrastructure/service-resilience-*.md`.

## Related

- [overview](../overview.md): system map, including every external dependency
- [hosting-and-identity](hosting-and-identity.md): App Service, health check probe, startup sequence
- [background-work-and-startup](background-work-and-startup.md): hosted services, start order and failure behavior, `DatabaseRecoveryService` in context
- [runbooks/configure-failure-email](../runbooks/configure-failure-email.md)
- [runbooks/refresh-dance-fallback-snapshot](../runbooks/refresh-dance-fallback-snapshot.md)
- [user-name-visibility](../users-admin/user-name-visibility.md): `UNAVAILABLE` rendering when user data can't be loaded
- [admin-pages](../users-admin/admin-pages.md): admin diagnostics surfaces
