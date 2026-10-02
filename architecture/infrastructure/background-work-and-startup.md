# Background Work and Startup

**Type:** Reference
**Status:** Current
**Last verified:** 2026-10-01
**Code:** `m4d/Services/BackgroundTaskQueue.cs`, `m4d/Services/BackgroundQueueHostedService.cs`,
`m4d/Services/DanceStatsHostedService.cs`, `m4d/Services/StartupInitializationService.cs`,
`m4d/Services/DatabaseRecoveryService.cs`, `m4d/PublicApi/DanzQClientInitializer.cs`,
`m4d/APIControllers/RecomputeController.cs`, `m4d/Utilities/TokenRequirement.cs`,
`m4dModels/AdminMonitor.cs`, `m4d/Configuration/M4dApplicationExtensions.cs`

Everything in `m4d` that runs outside a normal request/response: the hosted services that run at
startup, the in-process work queue, the fire-and-forget admin jobs, and the externally scheduled
recompute endpoint. The hosting side of startup (credential, App Configuration, health probe) is
in [hosting-and-identity](hosting-and-identity.md#startup-sequence); what each dependency does
when it's down is in [service-resilience](service-resilience.md). This doc covers what runs, in
what order, and what happens when it fails.

## Inventory

| Piece | Kind | Registered | Runs |
| --- | --- | --- | --- |
| `DanzQClientInitializer` | `IHostedService` | `AddPublicApiFoundation`, only when the `PublicApi` feature flag is on | Once, at host start |
| `BackgroundQueueHostedService` | `BackgroundService` | `AddM4dApplication`, with the `IBackgroundTaskQueue` singleton | For the life of the process |
| `DanceStatsHostedService` | `IHostedService` | `AddM4dApplication` | Once, at host start (blocks until done) |
| `StartupInitializationService` | `BackgroundService` | `AddM4dApplication` | Once, 2s after start |
| `DatabaseRecoveryService` | Singleton, **not** a hosted service | `AddM4dApplication`; called from inline middleware | On requests, while `Database` is unavailable |
| `RecomputeController` | API endpoint | MVC | When an Azure Logic App calls it |
| Admin jobs (`Task.Run` + `AdminMonitor`) | Fire-and-forget | n/a | When an admin starts one |

There is no scheduler inside the app (no Quartz, no timers). Anything periodic is triggered from
outside, by a Logic App calling an HTTP endpoint.

## Startup order

`m4d/Program.cs` calls `builder.AddM4dApplication(...)`, `builder.Build()`,
`await app.UseM4dPipeline(...)`, then `app.Run()`. In order:

1. **Service registration** (`AddM4dApplication`). Each external dependency is registered inside
   a try/catch with a fallback (see [service-resilience § Startup](service-resilience.md#startup)).
   Hosted services are registered in this order: `DanzQClientInitializer` (if enabled),
   `BackgroundQueueHostedService`, `DanceStatsHostedService`, `StartupInitializationService`.
2. **Pipeline setup** (`UseM4dPipeline`):
   1. Map `/health/startup` and `/health/ready`.
   2. Print `GenerateStartupReport()` and, if anything is already unavailable or degraded, attach
      the `ServiceHealthNotifier` and send one startup-failure email in a `Task.Run`.
   3. Build the middleware pipeline, including the `DatabaseRecoveryService` hook.
   4. **Run EF migrations synchronously** (`MigrateAsync` on a context without retry-on-failure), when
      `ConfigureDatabase` is set and neither `PROD_DB` nor `TEST_DB` is. Success marks `Database`
      healthy; failure marks it unavailable and startup continues. In Development it then
      applies `UserManagerHelpers.SeedData`.
3. **`app.Run()`** starts the host. The generic host calls each hosted service's `StartAsync` in
   registration order, one after another, **before** Kestrel starts listening:
   - `DanzQClientInitializer.StartAsync` creates or updates the DanzQ OpenIddict client.
   - `BackgroundQueueHostedService` and `StartupInitializationService` are `BackgroundService`s,
     so their `StartAsync` returns at once and `ExecuteAsync` carries on in the background.
   - `DanceStatsHostedService.StartAsync` awaits the whole dance-stats load, so the process doesn't
     accept requests (and `/health/startup` doesn't answer) until it finishes.
4. Kestrel starts listening.
5. About 2 seconds later, `StartupInitializationService` does the first App Configuration refresh.

Because the startup report (step 2.2) is printed before migrations (step 2.4), a migration
failure doesn't appear in that report and doesn't trigger the startup-failure email. It is logged
to the console, and `/health/ready` returns `503` until the database recovers.

`m4d.Sandbox/Program.cs` follows the same sequence but calls `app.StartAsync()` instead of
`app.Run()`, so it can seed songs after `DanceStatsHostedService` has initialized the stats (see
[contributor-test-environments](../dev-testing/contributor-test-environments.md)).

## Hosted services

### `DanceStatsHostedService`

Creates a scope, builds a `DanceMusicService`, and calls `IDanceStatsManager.Initialize`.
`DanceStatsManager.Initialize` loads the dance environment from the file cache first
(`LoadFromAppData`: the runtime `dance-environment.json`, then the checked-in fallback snapshot)
and only rebuilds it from the search index (`LoadFromAzure`) if no file exists. Either path ends
in `DanceStatsInstance.FixupStats`, which wires up dance groups and then does the
database-dependent part: attaching each dance's `SpotifyFromSearch` playlist id and creating
placeholder `Dance` rows for new dances. `FixupStats` skips that part when `Database` is already
unhealthy (for example after a failed migration), and catches a `SqlException` itself.

**Failure:** any exception from `Initialize` is caught, logged as "starting in degraded mode",
and marks `Database` unavailable, even if the cause was something else, such as search. The app
still starts. `Initialize` throws if called twice, so nothing else can initialize the stats; a
later rebuild goes through `ClearCache` instead (see [Recompute jobs](#recompute-jobs)).

### `StartupInitializationService`

Waits 2 seconds, then, if an `IConfigurationRefresher` exists and `AppConfiguration` is
available, calls `RefreshAsync` and logs the remote `Configuration:Sentinel` value. On failure it
marks `AppConfiguration` unavailable and the app keeps running on local configuration. It does
nothing else; the "background migrations" mentioned in an older comment in `UseM4dPipeline` now
run synchronously (step 2.4 above).

### `DanzQClientInitializer`

Registered only when `FeatureManagement:PublicApi` is `true` (which `AddPublicApiFoundation`
refuses in Production or with `PROD_DB`). It upserts the DanzQ client descriptor through
`IOpenIddictApplicationManager`. It has **no try/catch**, so a database error here fails host
startup rather than degrading. See [plans/public-api-authorization](../plans/public-api-authorization.md).

### `BackgroundQueueHostedService` and `BackgroundTaskQueue`

`IBackgroundTaskQueue` is a singleton in-memory queue of
`Func<IServiceScopeFactory, CancellationToken, Task>`: a `ConcurrentQueue` plus a `SemaphoreSlim`
counting items. `BackgroundQueueHostedService.ExecuteAsync` loops: `DequeueAsync` (blocks until
an item is available), then runs it with the root `IServiceScopeFactory` and the host's stopping
token. Each work item creates its own scope, so it never touches the request's disposed
`DbContext`.

Properties that follow from that design:

- **Serial.** One consumer, so work items run one at a time, in order.
- **Unbounded and in-memory.** Nothing limits the queue length, and anything still queued at
  shutdown or on a crash is lost.
- **Failures are isolated.** An exception from a work item is logged ("An error occurred during
  execution of a background task") and the loop continues. The current callers also catch and
  log inside their own lambdas.

The current producers are all fire-and-forget logging writes:

| Producer | Work item |
| --- | --- |
| `DMController` (server-side page-view logging) | Add a `UsageLog` row, update the user's `LastActive` / `HitCount` |
| `UsageLogController` (API, `POST` batch of client-side events) | Add the `UsageLog` rows, update the user's `LastActive` / `HitCount` |
| `SongSearch` (search logging) | Insert or bump the `Searches` row for this user and query |

`DMController` and `SongSearch` skip enqueuing while `Database` is unhealthy. See
[usage-tracking](../observability/usage-tracking.md) and
[saved-searches](../search/saved-searches.md) for what those writes mean. Tests use
`m4d.Tests/TestHelpers/TestBackgroundTaskQueue.cs`, which captures work items so a test can run
them explicitly ([testing-patterns](../dev-testing/testing-patterns.md)).

## `DatabaseRecoveryService`

A singleton that inline middleware calls on every request, right after the 4xx-tracking
middleware and before forwarded headers and authentication. While `Database` is `Unavailable`
and a connection string exists, it starts a throttled, fire-and-forget probe (at most one every
`ServiceHealth:DatabaseRetryInterval`, one at a time). On success it marks `Database` healthy and
re-runs `FixupStats` in a fresh scope, so the parts skipped during a cold start get done. If
`DanceStatsManager.Instance` is still null (the hosted service never loaded the stats at all), it
logs a warning and skips `FixupStats`. The full behavior, and the cooldown interaction that can
make `/health/ready` report ready between failed probes, are in
[service-resilience § Recovery](service-resilience.md#recovery) and its Known issues.

## Recompute jobs

`GET /api/recompute/{id}` (`RecomputeController.Get`) is the entry point for scheduled
maintenance. It isn't behind cookie authentication. Instead:

1. `TokenRequirement.Authorize` checks for `Authorization: Token {base64(key)}`, where the key is
   `Authentication:RecomputeJob:Key`. Otherwise `401`.
2. `AdminMonitor.StartTask(id)` takes the process-wide admin task slot. If another admin task is
   running, `409`.
3. It dispatches on the part of `id` before the first `-`:

| `id` | Work |
| --- | --- |
| `songstats` | `IDanceStatsManager.ClearCache(dms, fromStore: true)`: rebuild `DanceStatsInstance` from the search index (`LoadFromAzure`), write the runtime `dance-environment.json`, and save the index's tag types (`UpdateAzureIndex(null, ...)`). This also refreshes each dance's `SpotifyPlaylist` id on the dance pages. |
| `subscription` | Find users whose `SubscriptionEnd` is earlier than `DateTime.Now` and remove `PremiumRole` from any who still have it. |
| anything else | Releases the slot (`CompleteTask(false, ...)`) and returns `400`. |

The work runs **inside the request**, not on the queue, and the response is `200`
`{ changed: true, message }`. A failure inside the work is recorded in `AdminMonitor` (so
`/Admin/AdminStatus` shows it) but the HTTP response is still `200`, with a message beginning
"Failed to". A Logic App can't tell success from failure by status code.

**Who calls it.** The Logic App definitions live in Azure, not in this repo. The inventory in
[spotify-playlist-automation](../music-services/spotify-playlist-automation.md) lists
**UpdateSongStats**, which calls `/api/recompute/songstats` every 6 hours. The caller of
`subscription` isn't recorded in the repo; the public API plan describes it as a nightly job.
The same token also guards `GET /PlayList/UpdateBatch` and `UpdateBatchStatus`, which compete for
the same `AdminMonitor` slot.

An admin can do the same stats rebuild interactively with `/Admin/ClearSongCache`, which also
clears `DanceMusicController`'s JSON cache and `UsersController`'s cache. The recompute endpoint
does not clear those two.

## Fire-and-forget admin jobs and `AdminMonitor`

Long-running admin operations don't use the queue. They take the `AdminMonitor` slot, grab a
transient `DanceMusicCoreService` (`Database.GetTransientService()`, which has its own
`DbContext`), and start a `Task.Run` that the request doesn't await:

- `AdminController`: `AdminModifyBySearch`, `AdminRefreshBySearch`, `ReloadAllSongs`,
  `BatchArtists`
- `SongController`: `BatchAdminModify` and the `BatchProcess` family (`BatchCleanupProperties`,
  `BatchReloadSongs`, `BatchValidateTempo`, …)
- `PlayListController`: `Update`, `UpdateAll` / `UpdateBatch`, `Restore`, `RestoreAll`

`AdminMonitor` (`m4dModels/AdminMonitor.cs`) is a static, process-wide, single-slot tracker:
`StartTask` fails if anything is running, `UpdateTask` sets the phase and iteration,
`CompleteTask` records success or failure plus the last exception. `/Admin/AdminStatus` shows the
current state and `/Admin/ResetAdmin` force-clears it. Because it's in memory, a restart mid-job
loses both the job and its status, and a job that never calls `CompleteTask` holds the slot until
someone resets it. The individual jobs are documented in
[bulk-operations](../users-admin/bulk-operations.md),
[playlist-management](../music-services/playlist-management.md) and
[individual-artists](../songs/individual-artists.md).

Two other singletons start their own background tasks: `ServiceHealthManager` sends failure
emails in a `Task.Run` ([service-resilience § Admin notifications](service-resilience.md#admin-notifications)),
and `ArtistIndexCache` loads and rebuilds the artist snapshot in the background
([individual-artists](../songs/individual-artists.md)).

## Known issues

- **Startup report precedes migrations.** The comment says the report is generated "AFTER
  database migrations", but migrations run later in `UseM4dPipeline`, so a migration failure
  never reaches the report or the startup email.
- **Recompute failures return `200`.** Only `AdminMonitor` records the failure.
- **`TokenRequirement` caches the key forever.** `SetSecurityToken` uses `??=`, so rotating
  `Authentication:RecomputeJob:Key` in App Configuration needs a restart. A header whose token
  isn't valid base64 makes `Convert.FromBase64String` throw, so the caller gets a `500` rather
  than a `401`.
- **Dead code.** The `TokenAuthorization` policy registered in `AddM4dApplication` isn't
  referenced by any `[Authorize]` attribute (callers use `TokenRequirement.Authorize` directly).
  `RecomputeController` still has a commented-out `RecomputeInfo` class and an unused
  `DoHandleRecompute` delegate.
- **`DanceStatsHostedService` blames the database for everything.** Any `Initialize` exception
  marks `Database` unavailable, which then turns on the Identity-area block and the readiness
  `503` even if the database is fine.
- **The queue drops work on shutdown.** Usage and search log writes queued at shutdown are lost.
  This is acceptable for logging but would not be for anything that must happen.

## Future improvements

- Move the startup report and failure email after migrations, or report the migration result
  separately.
- Return a non-2xx status (or `{ changed: false }`) when a recompute job fails, so the Logic App
  run shows as failed.
- Read the recompute key from configuration on each request, and treat a malformed header as
  `401`.
- Remove the unused `TokenAuthorization` policy and the dead code in `RecomputeController`, or
  switch the token-guarded endpoints to `[Authorize(Policy = "TokenAuthorization")]`.
- Narrow `DanceStatsHostedService`'s catch so only database failures mark `Database` unavailable.
- Wrap `DanzQClientInitializer` like the other hosted services, so it can't block startup, before
  the public API is enabled anywhere that matters.
- Check `Database` health in the API `UsageLogController` before enqueuing, as the other
  producers do.

## History

- 2021-07 (PR 186): `DanceStatsHostedService` added, loading dance stats at startup.
- 2022-05 (PR 295): `BackgroundTaskQueue` and `BackgroundQueueHostedService` added, with search
  logging as the first producer. PR 372 (2023-03) cleaned up the background tasks.
- 2022-06 (PR 301): `recompute/subscription` expires premium subscriptions.
- 2025-12 (#95): `DanceStatsHostedService` passes `ServiceHealthManager` into the stats load.
- 2026-01 (#97): `StartupInitializationService` added; App Configuration connects after the app
  starts listening.
- 2026-05 (#163): `DanceStatsHostedService` catches failures and starts in degraded mode.
- 2026-05 (#172): EF migrations run synchronously before `app.Run()`.
- 2026-06 (#180): `DatabaseRecoveryService` added.
- 2026-08 (#250): Startup moved into `AddM4dApplication` / `UseM4dPipeline` so `m4d.Sandbox` can
  share it.
- 2026-09 (#254): `DanzQClientInitializer` added behind the `PublicApi` flag.
- 2026-10-01: This document created.

## Related

- [hosting-and-identity](hosting-and-identity.md): environments, deployment, health check probe,
  startup sequence from the hosting side
- [service-resilience](service-resilience.md): per-service degradation and recovery,
  `ServiceHealthManager`, health endpoints
- [spotify-playlist-automation](../music-services/spotify-playlist-automation.md): the Logic App
  inventory and `UpdateBatch`
- [bulk-operations](../users-admin/bulk-operations.md): the admin jobs that run under
  `AdminMonitor`
- [usage-tracking](../observability/usage-tracking.md) and
  [saved-searches](../search/saved-searches.md): what the queued writes record
- [runbooks/refresh-dance-fallback-snapshot](../runbooks/refresh-dance-fallback-snapshot.md): the
  dance-stats file the startup load falls back to
