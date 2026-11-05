# Hosting and Identity

**Type:** Reference
**Status:** Current
**Last verified:** 2026-11-04
**Code:** `azure-pipelines.yml`, `m4d/Program.cs`, `m4d/Configuration/M4dApplicationExtensions.cs`,
`m4d/Configuration/AppDataPaths.cs`, `m4d/Middleware/BlockAppDataMiddleware.cs`,
`m4d/Services/StartupInitializationService.cs`, `m4d/m4d.csproj`

Where music4dance runs, how it's deployed, how it authenticates to Azure, and what happens
between process start and serving traffic. For step-by-step procedures, see the runbooks
linked under [Related](#related).

## Environments

| | Production | Test |
| --- | --- | --- |
| App Service (Linux) | `msc4dnc` | `m4d-test` |
| App Service plan | `m4dweb-linux`, **B2** (2 cores, 3.5 GB) | `m4d-test`, F1 |
| `ASPNETCORE_ENVIRONMENT` | `Production` | `Staging` |
| `SEARCHINDEX` / `SEARCHINDEXVERSION` | `SongIndexProd` / `3` | `SongIndexTest` / `3` |
| Instances | 1 | 1 |

Both apps run as a **single instance**. That's a cost decision: no SLA is offered and the paid
subscriber base is small. It matters for health checks (below) and means the in-memory state
(trackers, caches) is never shared across instances. The admin update message is the exception:
it's kept in a file on the shared `/home` volume (see
[service-resilience § Update message](service-resilience.md#update-message)).

Shared Azure resources:
- App Configuration: `https://music4dance.azconfig.io`, on the **Developer** tier (6,000
  requests an hour; see [App Configuration](#identity-and-secrets) below)
- Key Vault: `music4dance`
- Azure AI Search, two services:
  - `music4dance` (`https://music4dance.search.windows.net`) holds the song indexes, one per
    environment and schema version; see [search-index-versioning](../search/search-index-versioning.md).
  - `m4d` holds the site page index used by site search, which SelfCrawler writes to.
- Azure SQL server `n8a541qjnq`
- Azure Communication Services, for email

Resources live in resource group `m4d-Web`.

## Deployment

`azure-pipelines.yml` is the only pipeline. It's an Azure DevOps pipeline with three runtime
parameters: **environment** (`test` by default, or `production`), **deploymentMode**
(`framework-dependent` by default, or `self-contained`) and **runFromPackage** (on by default).
It runs the YAML from the branch being deployed. It runs these steps:

1. Builds the Vue client (Node 22, Yarn via Corepack).
2. Builds and publishes `m4d` for `linux-x64` with the .NET 10 SDK.
3. Sets `M4D_DATA_ROOT=/home/data/m4d`. It also sets `WEBSITE_RUN_FROM_PACKAGE=1` when
   **runFromPackage** is on and deletes it when off. This has to happen before the deploy,
   because Kudu decides when the zip arrives whether to extract or mount it, and the deploy task
   applies its own app settings only afterwards. The step writes only settings that changed,
   because each write restarts the app.
4. Deploys with `AzureWebApp@1` through the `m4d-release` service connection. The same deploy
   step sets the app settings `SELF_CONTAINED_DEPLOYMENT`, `ASPNETCORE_ENVIRONMENT`,
   `SEARCHINDEX`, `SEARCHINDEXVERSION` and `WEBSITES_CONTAINER_START_TIME_LIMIT=600`, plus the
   startup command, so nothing needs to be set by hand in the portal.
5. Stops and starts the app, but only when step 3 switched `WEBSITE_RUN_FROM_PACKAGE` on or off,
   then waits for it to answer with a 200. Without this, App Service fails to remount `wwwroot`
   when the package arrives while the app is mid-startup, stops the site, and waits about five
   minutes before retrying. It's a stop and a start rather than a restart, because a restart that
   lands while a start is in progress cancels that start and leaves the site stopped.
6. Sets the App Service health check path to `/health/ready`, only if it isn't already, since
   every config write restarts the app. This step is non-fatal.

### Run From Package

With **runFromPackage** on (`WEBSITE_RUN_FROM_PACKAGE=1`), Kudu keeps the zip in
`/home/data/SitePackages` (`packagename.txt` names the current one) and mounts it read-only as
`/home/site/wwwroot`, then restarts. Files never change under a running process, so a deploy
can't serve half-replaced assemblies. Before this, `AzureWebApp@1` extracted the zip over the
running app and every production deploy produced about 4½ minutes of
`BadImageFormatException: Index not found` 500s (213 of them, plus 39 "The response has already
started", on 2026-10-13). A staging slot would also fix that, but it needs the Standard tier and
a second warm instance, which is over budget.

**An ordinary deploy has no downtime.** App Service starts a container on the new package while
the old one keeps serving, and switches over once the new one passes its startup probe. On
`m4d-test`, a deploy polled once a second (2026-11-02) and a portal restart both saw no failed
requests. The first ordinary production deploy (2026-11-04, polled about once a second for 21
minutes) got a 200 on every request. The zip deploy was the only Azure operation, with no
settings write, stop-and-start or health check write. The new container started on the same
worker, with the runtime image already cached, 9s after the deploy call, and passed its startup
probe 41s later; the old one stopped 21s after that. Four requests in the first five minutes
took 1.2–1.7s against the usual 0.2s, while the new process warmed up.

**A deploy that switches the setting on or off has an outage**, once:

- The settings write in step 3 restarts the app before the package exists. When the package
  arrives mid-start, App Service logs "Container failed to remount volume. Terminate.", records
  `ContainerStartupFailure`, stops the site, and waited about five minutes before retrying on the
  first test deploy. Turning the setting off has its own version: the restart runs the old
  extracted build, the deploy extracts over it, and it crashes (`Aborted (core dumped)`, exit
  code 134).
- Step 5's stop-and-start gets out of that state without waiting. It isn't a restart: a restart
  that lands while a start is in progress cancels that start, and App Service then stops the site
  and leaves it stopped.
- Measured: about 45s down on `m4d-test`. On production (2026-11-03) it was about 4 minutes,
  including some 502s, because the start landed on a different worker: about 2½ minutes before
  that worker logged anything, then 29s pulling the runtime image, then 41s to pass the probe.
  Which worker a start lands on isn't something the pipeline controls.

**Rollback:** run the pipeline with **runFromPackage** off. That deletes the setting and the
deploy extracts into `wwwroot` again, with the switching outage above. `M4D_DATA_ROOT` stays
set, so the app keeps its data. Deploying a commit from before 2026-11-02 would ignore
`M4D_DATA_ROOT` and go back to `wwwroot/AppData`.

| | Framework-dependent (default) | Self-contained |
| --- | --- | --- |
| Runtime | App Service `DOTNETCORE\|10.0` stack | Bundled; `PublishSingleFile` + `PublishReadyToRun` (`m4d.csproj`, conditional on `SelfContained`) |
| Startup command | `dotnet m4d.dll` | `/home/site/wwwroot/m4d` |
| Static files | `MapStaticAssets()` | `UseStaticFiles()`, because the static-assets manifest doesn't survive single-file publish |
| Data protection keys | Platform default | Persisted to `$HOME/site/keys` (application name `music4dance`) |
| Package size | ~20–30 MB | ~100–150 MB |

Self-contained mode existed to run .NET 10 before App Service offered the runtime. It's still
supported, but framework-dependent is the default.

The app reads `SELF_CONTAINED_DEPLOYMENT` to pick between these code paths. Port binding is
left to App Service (`PORT` / `WEBSITES_PORT`). The app has no code of its own for ports or
certificates.

`SMOKE_TEST_MODE=true` boots a minimal app that skips all Azure configuration. It's for
container diagnostics.

### Runtime data root

`M4D_DATA_ROOT` sets where the app writes at runtime (`AppDataPaths`). The pipeline sets it to
`/home/data/m4d`, on the persistent `/home` volume and outside the read-only package, whether or
not **runFromPackage** is on. Everything lives in `<root>/AppData`:

- Admin backups and usage exports. Downloads go through `PhysicalFile`, not static files.
- `BatchProcess` before/after logs (`BatchCleanupProperties?log=true`).
- The dance stats cache (`dance-environment.json`), the artist index snapshot
  (`artist-index.json`) and the update message (`update-message.json`). `DanceStatsFileManager`
  and `ArtistIndexFileManager` take this as a separate `dataRoot`, because their packaged
  fallbacks still come from `wwwroot/content`.
- Uploaded `blogmap.txt` / `helpmap.txt` overrides, read when newer than the packaged copy (see
  [blog-help-sitemap](../pages/blog-help-sitemap.md)).

Unset (local runs, the sandbox and tests), the root is the web root, so those files land in
`wwwroot/AppData`. That's why `BlockAppDataMiddleware` still returns 404 for `/AppData`. The app
logs the root at startup (`App data root: ...`) and creates the folder then.

There's no separate `content` folder under the data root. `wwwroot/content` and `wwwroot/AppData`
are kept apart because one is served publicly and the other isn't, and nothing under
`/home/data/m4d` is served.

Nothing was copied over when production switched. The dance stats loaded the checked-in
fallback and rebuilt from the search index in the background (19s on production), the artist
index rebuilds on first use, and older backups stay on disk in the old extracted
`wwwroot/AppData`, hidden while the package is mounted. On the next deploy (2026-11-04) the
dance stats loaded straight from the runtime cache under `/home/data/m4d/AppData`, with no
fallback or rebuild, so the cache now survives deploys.

## Identity and secrets

**Credential.** One `TokenCredential` is created at startup and shared by every Azure client:

- **Azure (non-Development):** `DefaultAzureCredential` with the Visual Studio, VS Code, Azure CLI,
  PowerShell and interactive credentials excluded. That leaves managed identity and environment
  credentials, and cuts token acquisition from ~17s to ~2–3s.
- **Development with a service principal:** `ClientSecretCredential` built from
  `AZURE_TENANT_ID` / `AZURE_CLIENT_ID` / `AZURE_CLIENT_SECRET`, read from configuration so they
  can live in user secrets.
- **Development otherwise:** the full `DefaultAzureCredential` chain.

**Per service.** Each App Service has a **system-assigned managed identity**:

| Service | How the app authenticates | Grant on the resource |
| --- | --- | --- |
| App Configuration | Managed identity | RBAC: App Configuration Data Reader |
| Azure AI Search (both services) | Managed identity | RBAC: Search Index Data Reader **and** Search Index Data Contributor, because the app writes songs. Search Service Contributor is needed only to create or delete indexes. |
| Key Vault (secrets referenced from App Configuration) | Managed identity, through App Configuration's `ConfigureKeyVault` | **Access policy**: Secrets Get/List. The vault uses the access-policy model, so RBAC role assignments on it have no effect. See [plans/key-vault-rbac-migration](../plans/key-vault-rbac-migration.md). |
| Azure SQL | Connection string; for managed identity, Service Connector injects `AZURE_SQL_CONNECTIONSTRING` | Service Connector creates the database user |
| OAuth, reCAPTCHA, Azure Communication Services | Secrets in App Configuration / Key Vault | — |

Earlier, self-contained deployments used access keys for App Configuration and Search. Both
modes now use managed identity.

**Connection string precedence.** The `DbContext` re-reads this on every resolution:
`AZURE_SQL_CONNECTIONSTRING`, then `ProdConnectionString` if `PROD_DB=true`, then
`TestConnectionString` if `TEST_DB=true`, then `ConnectionStrings:DanceMusicContextConnection`.
The `PROD_DB` / `TEST_DB` switches are for local development against the shared databases; see
[runbooks/provision-app-service § Local Development](../runbooks/provision-app-service.md#local-development-access-production-database-via-azure-ad).

**App Configuration.**
- **Labels:** the app loads keys and feature flags with no label, overlaid by keys labeled with
  the environment name (`Production` / `Staging`).
- **Refresh:** a change to the unlabeled `Configuration:Sentinel` triggers a full refresh in
  every environment. The refresh watch has no label fallback (it polls one exact key and label),
  so it watches the unlabeled key rather than the environment's; an environment-labeled sentinel
  is ignored. Both the sentinel and the feature flags are checked every 5 minutes, through `UseAzureAppConfiguration()`.
  That's about 3 requests per instance per cycle (the sentinel, plus one list request for each
  of the two feature-flag selectors), and a startup or full refresh adds a load of every
  selector.
- **Tier and quota:** the store is on the Developer tier: 6,000 requests an hour, with requests
  over the quota getting `429` until the end of the hour. It costs a $0.12 daily charge that
  covers 3,000 requests a day, which prod and test stay under. It was on the Free tier until
  2026-10-10, whose 1,000 requests a day (counting every poll) ran out most days, so a
  restart that day started degraded. Standard was ruled out on cost. Neither Free nor
  Developer has an SLA or guaranteed throughput. An Azure Monitor alert on the store's
  `Request Quota Usage` metric (above 80%) emails the admins before throttling starts; see
  [service-resilience § App Configuration quota](service-resilience.md#app-configuration-quota).
- **Fallback:** if App Configuration is unreachable, the app keeps running on `appsettings.json`
  (see [service-resilience](service-resilience.md)). Every secret comes from App Configuration
  and its Key Vault references, so this is a degraded mode: no OAuth logins, email or reCAPTCHA.
  It recovers in place once App Configuration loads (see
  [service-resilience § App Configuration recovery](service-resilience.md#app-configuration-recovery)).

## Startup sequence

The app is built to pass a liveness probe in seconds, even while dependencies are slow:

1. Create the shared credential.
2. Load App Configuration (and its Key Vault references) **synchronously**
   (`AppConfigurationStartup`). Adding the provider to `builder.Configuration` loads it on the
   spot, and startup waits until the load succeeds or the 100s startup timeout expires. The
   per-request client retry options don't cap that total: each request gets 30s during the
   startup load and 2 minutes afterwards. The provider is optional, so a
   failure leaves it in place, empty, and `AppConfigurationRecoveryService` completes the load
   in the background (see [background-work-and-startup](background-work-and-startup.md#appconfigurationrecoveryservice)).
   Don't lengthen this wait: App Service kills a container that doesn't answer its startup
   probe in time.
3. Register search clients lazily, never connecting at startup.
4. Register the remaining services, each wrapped for resilience.
5. Run `builder.Build()`.
6. Map `/health/startup` and `/health/ready`.
7. Print the startup health report and attach the admin email notifier. This happens before
   migrations, so a migration failure isn't in the console report. In Azure, a status email is
   sent once the app has started (after step 9), and that does include it.
8. **Run database migrations synchronously** before `app.Run()`, so the schema exists before
   any hosted service runs. A failure marks `Database` unavailable instead of crashing.
9. Run the hosted services' `StartAsync`, including the dance-stats load, then start accepting
   requests (see [background-work-and-startup](background-work-and-startup.md#startup-order)).
10. `StartupInitializationService` waits 2s, then performs the first App Configuration refresh
    in the background (a refresh check, not the initial load, which step 2 already did).

## Health checks

- `/health/startup`: always `200` once the process is listening. Don't point Azure's probe
  here. It reports healthy mid-migration, and doing so once caused a production incident:
  requests hit a database that wasn't ready yet.
- `/health/ready`: `503` while `Database` is unhealthy, `200 ready` otherwise. The pipeline
  makes this the App Service health check path, writing it only when it isn't already set,
  because a write restarts the app even when the value doesn't change.

Azure pings the path every minute and marks the instance unhealthy after
`WEBSITE_HEALTHCHECK_MAXPINGFAILURES` consecutive failures (default 10). On a **single-instance**
plan, Azure does *not* pull the instance from rotation, because that would take the site down.
The only automatic remedy is a forced worker replacement after an hour of continuous failure.
`DatabaseRecoveryService` is what actually recovers the common case. See
[service-resilience](service-resilience.md) for the full endpoint list.

## Request pipeline notes

- **Forwarded headers:** `X-Forwarded-For` and `X-Forwarded-Proto` are honored so
  `RemoteIpAddress` and the scheme are correct behind App Service's front end. `KnownProxies` /
  `KnownNetworks` are not restricted.
- **Response compression (anonymous only).** Linux App Service doesn't compress dynamic
  responses, so the app does: `AddResponseCompression` (brotli, then gzip, both
  `CompressionLevel.Fastest`) with `EnableForHttps = false`, and `UseResponseCompression()` sits
  ahead of the static-file middleware. `AnonymousCompressionMiddleware`, after
  `UseAuthentication()`, sets `IHttpsCompressionFeature.Mode` to `Compress` for anonymous
  requests and `DoNotCompress` for authenticated ones. Authenticated pages stay uncompressed
  because of BREACH (a secret plus reflected input, compressed over TLS). Precompressed static
  assets already carry `Content-Encoding` and pass through untouched. Anonymous HTML shrinks by
  about 80–90%; `/dances/waltz` goes from 56 KB to 11 KB.
- **Cache-control middleware** runs after authentication and rate limiting:
  - Anonymous `GET 200` HTML responses that don't set cookies get `Cache-Control: no-cache`:
    browsers may keep them but revalidate every time, and `UseResponseCaching` won't store
    them, because it isn't `public`. Until 2026-10-29 this branch never ran. Every page minted
    an antiforgery token, so every response set a cookie and carried antiforgery's own
    `no-cache, no-store`. Anonymous pages no longer carry a token (see
    [frontend-architecture](../pages/frontend-architecture.md#menucontext-per-request-user-and-site-state)).
    The branch used to say `public, max-age=300`, but that isn't safe yet.
    `UseResponseCaching` doesn't check cookies, so it would serve a stored anonymous page to
    signed-in users, and a browser would show the signed-out page for five minutes after login.
    Real anonymous caching needs a cache keyed on sign-in state. That's Phase 2 of
    [plans/bot-load-and-public-page-rendering](../plans/bot-load-and-public-page-rendering.md).
  - Responses that set any cookie are left alone. That includes the legacy server-side `Usage`
    cookie, which `DanceMusicController` sets only while `ClientSideUsageLogging` is off.
  - Authenticated HTML gets `no-store, no-cache, must-revalidate`.
  - `/identity/*` (including redirects to it), `/api/*` and `/song/rawsearchform` are excluded.
    `/api/antiforgery/token` sets `no-store` itself.
  - **Azure Front Door is not deployed.** The CDN rollout is in
    [plans/front-door-caching](../plans/front-door-caching.md).
- **Database recovery hook:** `DatabaseRecoveryService.TriggerRecoveryIfNeeded()` runs early
  on every request (see [service-resilience](service-resilience.md)).

## Logs

App Service filesystem application logging captures Warning and above. The console output,
including the startup report, is at `/home/LogFiles/Application/` in Kudu. See
[logging-and-diagnostics](../observability/logging-and-diagnostics.md).

## Future improvements

- **Key Vault RBAC migration**: [plans/key-vault-rbac-migration](../plans/key-vault-rbac-migration.md)
  (not started).
- **Front Door with anonymous-page caching and WAF**:
  [plans/front-door-caching](../plans/front-door-caching.md) (not started).
- **Restrict `KnownProxies` / `KnownNetworks`** once a fixed front end exists.
- **Retire self-contained mode**, if framework-dependent stays reliable. That would remove the
  `SELF_CONTAINED_DEPLOYMENT` branches. It's also untested with Run From Package: its startup
  command runs `/home/site/wwwroot/m4d` directly, which needs the execute bit to survive in the
  mounted zip.
- **Prune old backups** under `/home/data/m4d/AppData`. `/home` counts against the plan's storage
  quota, and backups now persist across deploys.
- **Scale out to two instances**, so the health check can pull a bad instance, if an SLA or
  subscriber growth justifies the cost.

## History

- 2025-12: Unified `azure-pipelines.yml`, with runtime parameters and automatic app settings and
  startup command. It replaced four per-scenario pipelines.
- 2025-12: Managed identity unified for both deployment modes, removing the App Configuration and
  Search access keys used by self-contained mode.
- 2026-01: New-instance provisioning guide with Service Connector for SQL. Fast-startup work:
  trimmed credential chain, deferred App Configuration connect.
- 2026: `/health/ready` added and wired into the pipeline after an incident caused by
  `/health/startup`.
- 2026-10-06: A production restart after a segfault hit a 100s App Configuration startup timeout
  and stayed degraded (no secrets) until a manual restart. The "deferred connect" above turned
  out to be a synchronous load. The load is now optional with a 30s timeout and recovers in
  place, and a failure prints the Azure SDK events from the load.
- 2026-10-09: The first in-place recovery in production took 26 minutes, apparently from a slow
  (not down) store. The startup timeout went back to 100s, and requests after startup get a
  2-minute network timeout instead of 30s.
- 2026-10-10: A test restart hit `429`s ("Resource utilization has surpassed the assigned quota")
  for 40 minutes before recovering in place. `Request Quota Usage` showed the Free tier's daily
  quota running out most days, which may also explain the 10-06 and 10-09 slow loads. The store
  moved to the Developer tier, and a quota alert was added.
- 2026-10-25: Production ran out of memory and restarted several times a day from 2026-10-22,
  with up to about 15 minutes down per restart and one 6-hour outage. The peak working set was
  pinned at about 1.25 GB, the ceiling for the container on B1; it was 700 MB–1 GB before. There
  was no deploy, and traffic was lower; the platform had just moved to base image
  `dotnetcore:10.0_20261022.3`. The plan went from B1 to B2, and `DOTNET_GCConserveMemory=5` was
  set on `msc4dnc`. To scale back down, run
  `az appservice plan update -g m4d-Web -n m4dweb-linux --sku B1`, but only if peak memory on B2
  stays well under about 1.2 GB. The signatures are in
  [runbooks/read-production-logs](../runbooks/read-production-logs.md).
- 2026-10-29: No OOMs since the move to B2. On base image `.3`, B2 still saw peaks of 1.1–1.6 GB
  on 10-26. After the platform moved to `dotnetcore:10.0_20261022.4` (10-27) and then
  `10.0_20261027.8` (10-29), peaks stayed at or below about 920 MB, and the 10-29 process held
  flat near 500 MB. That points at the `.3` image, though it's a correlation, not proof. A
  `gcdump` at 14 hours showed about 73 MB of live managed objects and no leak (see
  [runbooks/capture-memory-diagnostics](../runbooks/capture-memory-diagnostics.md)). HTTP
  request logs now go to storage, so per-client traffic can be checked next time
  ([logging-and-diagnostics § HTTP request logs](../observability/logging-and-diagnostics.md#http-request-logs)).
- 2026-10-29: Response compression for anonymous responses (brotli/gzip), A1 of
  [plans/bot-load-and-public-page-rendering](../plans/bot-load-and-public-page-rendering.md).
  Dynamic HTML had gone out uncompressed to every crawler and visitor.
- 2026-10-29: Anonymous pages stopped minting an antiforgery token (A3 of the same plan), so
  they set no cookie. The cache-control middleware's anonymous branch, which had never run,
  went from `public, max-age=300` to `no-cache`.
- 2026-11-02: Run From Package and `M4D_DATA_ROOT` (#373), to end the 500s each deploy caused
  by extracting over a running process. Tested on `m4d-test`, then switched on in production on
  2026-11-03 (about 4 minutes down, once). The stop-and-start after a switch and the
  write-only-if-different health check step both came from the test runs. A background dance
  stats rebuild after a fallback load came in the same PR.
- 2026-11-04: The first ordinary production deploy with Run From Package on had no failed
  requests, and the dance stats cache survived it (see [Run From Package](#run-from-package)).
- 2026-10-01: Consolidated from `SELF_CONTAINED_DEPLOYMENT.md`, `managed-identity-self-contained-plan.md`
  and the architecture sections of `azure-app-service-setup-managed-identity.md`. Stale details
  dropped: legacy pipeline files, `appsettings.SelfContained.json`, and in-app Kestrel port and
  certificate code, none of which exist.

## Related

- [overview](../overview.md): system map (projects, request flow, data stores, external services)
- [runbooks/deploy](../runbooks/deploy.md): running a deployment
- [ci-cd-and-release](ci-cd-and-release.md): GitHub checks, branch rules, and how a change reaches production
- [runbooks/provision-app-service](../runbooks/provision-app-service.md): creating a new instance end to end
- [service-resilience](service-resilience.md): degradation, recovery and health endpoints
- [background-work-and-startup](background-work-and-startup.md): hosted services, their start order, the work queue and recompute jobs
- [configuration-and-feature-flags](configuration-and-feature-flags.md): every configuration key and feature flag, its source and default
- [data-layer](data-layer.md): what's in SQL vs the search index, EF migrations applied at startup
- [plans/key-vault-rbac-migration](../plans/key-vault-rbac-migration.md)
- [plans/front-door-caching](../plans/front-door-caching.md)
- [search-index-versioning](../search/search-index-versioning.md)
