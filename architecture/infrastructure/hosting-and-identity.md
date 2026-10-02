# Hosting and Identity

**Type:** Reference
**Status:** Current
**Last verified:** 2026-10-01
**Code:** `azure-pipelines.yml`, `m4d/Program.cs`, `m4d/Configuration/M4dApplicationExtensions.cs`,
`m4d/Services/StartupInitializationService.cs`, `m4d/m4d.csproj`

Where music4dance runs, how it's deployed, how it authenticates to Azure, and what happens
between process start and serving traffic. For step-by-step procedures, see the runbooks
linked under [Related](#related).

## Environments

| | Production | Test |
| --- | --- | --- |
| App Service (Linux) | `msc4dnc` | `m4d-test` |
| `ASPNETCORE_ENVIRONMENT` | `Production` | `Staging` |
| `SEARCHINDEX` / `SEARCHINDEXVERSION` | `SongIndexProd` / `3` | `SongIndexTest` / `3` |
| Instances | 1 | 1 |

Both apps run as a **single instance**. That's a cost decision: no SLA is offered and the paid
subscriber base is small. It matters for health checks (below) and means the in-memory state
(trackers, `GlobalState.UpdateMessage`, caches) is never shared across instances.

Shared Azure resources:
- App Configuration: `https://music4dance.azconfig.io`
- Key Vault: `music4dance`
- Azure AI Search, two services:
  - `music4dance` (`https://music4dance.search.windows.net`) holds the song indexes, one per
    environment and schema version; see [search-index-versioning](../search/search-index-versioning.md).
  - `m4d` holds the site page index used by site search, which SelfCrawler writes to.
- Azure SQL server `n8a541qjnq`
- Azure Communication Services, for email

Resources live in resource group `m4d-Web`.

## Deployment

`azure-pipelines.yml` is the only pipeline. It's an Azure DevOps pipeline with two runtime
parameters: **environment** (`test` by default, or `production`) and **deploymentMode**
(`framework-dependent` by default, or `self-contained`). It runs these steps:

1. Builds the Vue client (Node 22, Yarn via Corepack).
2. Builds and publishes `m4d` for `linux-x64` with the .NET 10 SDK.
3. Deploys with `AzureWebApp@1` through the `m4d-release` service connection. The same deploy
   step sets the app settings `SELF_CONTAINED_DEPLOYMENT`, `ASPNETCORE_ENVIRONMENT`,
   `SEARCHINDEX`, `SEARCHINDEXVERSION` and `WEBSITES_CONTAINER_START_TIME_LIMIT=600`, plus the
   startup command, so nothing needs to be set by hand in the portal.
4. Sets the App Service health check path to `/health/ready`. This step is non-fatal.

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
- **Refresh:** a change to `Configuration:Sentinel` triggers a full refresh. Both the sentinel
  and the feature flags are checked every 5 minutes, through `UseAzureAppConfiguration()`.
- **Fallback:** if App Configuration is unreachable, the app keeps running on `appsettings.json`
  (see [service-resilience](service-resilience.md)).

## Startup sequence

The app is built to pass a liveness probe in seconds, even while dependencies are slow:

1. Create the shared credential.
2. Register App Configuration without connecting.
3. Register search clients lazily, never connecting at startup.
4. Register the remaining services, each wrapped for resilience.
5. Run `builder.Build()`.
6. Map `/health/startup` and `/health/ready`.
7. **Run database migrations synchronously** before `app.Run()`, so the schema exists before
   any hosted service runs. A failure marks `Database` unavailable instead of crashing.
8. Print the startup health report, and send one failure email if anything is unavailable.
9. Start accepting requests.
10. `StartupInitializationService` waits 2s, then performs the first App Configuration refresh
    in the background.

## Health checks

- `/health/startup`: always `200` once the process is listening. Don't point Azure's probe
  here. It reports healthy mid-migration, and doing so once caused a production incident:
  requests hit a database that wasn't ready yet.
- `/health/ready`: `503` while `Database` is unhealthy, `200 ready` otherwise. The pipeline
  sets this as the App Service health check path on every deploy. Changing the path restarts
  the app.

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
- **Cache-control middleware** runs after authentication and rate limiting:
  - Anonymous `GET 200` HTML responses that don't set cookies get
    `Cache-Control: public, max-age=300`.
  - Authenticated HTML gets `no-store, no-cache, must-revalidate`.
  - `/identity/*` (including redirects to it), `/api/*` and `/song/rawsearchform` are excluded.
  - **Azure Front Door is not deployed**, so today this only affects browser caching. One
    consequence is replayed anonymous pages carrying stale antiforgery tokens, which is why the
    antiforgery cookie now lasts a day. The CDN rollout is in
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
  `SELF_CONTAINED_DEPLOYMENT` branches.
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
- 2026-10-01: Consolidated from `SELF_CONTAINED_DEPLOYMENT.md`, `managed-identity-self-contained-plan.md`
  and the architecture sections of `azure-app-service-setup-managed-identity.md`. Stale details
  dropped: legacy pipeline files, `appsettings.SelfContained.json`, and in-app Kestrel port and
  certificate code, none of which exist.

## Related

- [runbooks/deploy](../runbooks/deploy.md): running a deployment
- [runbooks/provision-app-service](../runbooks/provision-app-service.md): creating a new instance end to end
- [service-resilience](service-resilience.md): degradation, recovery and health endpoints
- [plans/key-vault-rbac-migration](../plans/key-vault-rbac-migration.md)
- [plans/front-door-caching](../plans/front-door-caching.md)
- [search-index-versioning](../search/search-index-versioning.md)
