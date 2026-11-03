# Run From Package: Atomic Deploys Without a Staging Slot

**Type:** Plan
**Status:** Partially implemented
**Last verified:** 2026-11-02
**Code:** `azure-pipelines.yml`, `m4d/Configuration/M4dApplicationExtensions.cs`,
`m4d/Services/DanceStatsHostedService.cs`, `m4dModels/DanceStatsManager.cs`,
`m4d/Controllers/DMController.cs`, `m4d/Controllers/AdminController.cs`,
`m4d/Controllers/SongController.cs`, `m4d/ViewModels/SiteMapInfo.cs`,
`m4dModels/DanceStatsFileManager.cs`, `m4dModels/ArtistIndexFileManager.cs`,
`m4d/Services/UpdateMessageFileManager.cs`, `m4d/Configuration/AppDataPaths.cs`

## Problem

Every production deploy produces about 4½ minutes of 500s. In the 2026-10-13 logs, that was 213
`BadImageFormatException: Index not found` errors plus 39 "The response has already started". The
cause is `AzureWebApp@1` zip-deploying over `/home/site/wwwroot` while the old process is still
serving, so assemblies change underneath it. (See the known-signatures table in
[read-production-logs](../runbooks/read-production-logs.md).)

## Why not a staging slot

A slot plus swap needs the Standard tier or higher and a second warm instance. That's more than
the site's budget supports (see [hosting-and-identity](../infrastructure/hosting-and-identity.md):
both apps run on a single instance as a cost decision).

## Approach: `WEBSITE_RUN_FROM_PACKAGE=1`

With this app setting, Kudu stores the deployed zip in `/home/data/SitePackages` and **mounts it
read-only** as `/home/site/wwwroot`, then restarts the app. Files never change under a running
process, and only a fully deployed package ever runs. (MS Learn: *Run your app from a ZIP
package*.) Only `/home` is persistent on Linux App Service.

What blocked this before is that the app writes under `wwwroot`, and the read-only mount makes
those writes fail. The fix is to move every runtime write to a persistent folder under `/home`.

## Inventory: what touches `wwwroot` at runtime

| What | Where today | Code |
| --- | --- | --- |
| Admin backups (`backup-*.txt`, `index-*.txt`) and usage exports | `wwwroot/AppData/`, returned with `File("~/AppData/" + fname, …)`, a web-root virtual path | `DMController.EnsureAppData` (`DMController.cs:456`); `AdminController.IndexBackup` (~1978, ~2015) and the database backup (~2043, ~2122) |
| Usage import file placed by hand | `wwwroot/AppData/` | `AdminController.LoadUsageFromAppData` (~1438) |
| Dance stats runtime cache `dance-environment.json` | `wwwroot/AppData/` | `DanceStatsFileManager` (appends `AppData` to the root it's given), wired with `WebRootPath` in `M4dApplicationExtensions.cs` ~477–489 |
| Artist index snapshot | `wwwroot/AppData/` | `ArtistIndexFileManager`, wired with `WebRootPath` in `M4dApplicationExtensions.cs` ~516–525 |
| Admin update message `update-message.json` | `wwwroot/AppData/` | `UpdateMessageFileManager` (appends `AppData`), wired with `WebRootPath` in `M4dApplicationExtensions.cs` ~527–535 |
| `BatchProcess` before/after logs | The process's current directory (relative path), which is `wwwroot` on App Service | `SongController.cs` ~1829–1830 |
| `blogmap.txt` / `helpmap.txt` fast-path edits | Overwritten in `wwwroot/content/` through Kudu (see [link-new-blog-posts](../runbooks/link-new-blog-posts.md)) | `SiteMapFile` reads `/wwwroot/content/{name}.txt` through the composite `IFileProvider` (`SiteMapInfo.cs:63`; provider built in `M4dApplicationExtensions.cs` ~444–445) |

These are already safe:

- `Directory.CreateDirectory(wwwroot/vclient)` (`M4dApplicationExtensions.cs` ~563): `vclient` is
  in the package, and creating an existing directory doesn't write.
- Data protection keys (self-contained mode) go to `$HOME/site/keys`, outside `wwwroot`.
- `ExportFallbacks` writes to `ClientApp/src/...` and `FileEmailSender` is sandbox-only. Both
  are development tools.

## Design

### 1. One persistent data root

Add a setting, `M4D_DATA_ROOT`, read through `IConfiguration` so it can be an App Service app
setting:

- **Unset** (local dev, sandbox, tests): use `WebRootPath`. Behavior is unchanged, and files stay
  in `wwwroot/AppData`.
- **Test and production:** `/home/data/m4d`.

As built: `AppDataPaths(Root)` (`m4d/Configuration/AppDataPaths.cs`), with `AppData` =
`Root/AppData`. Everything the app writes, and the sitemap overrides it reads, live there; there
is no separate `content` folder under the data root, since nothing there is served and the
public/private split that keeps `wwwroot/content` apart from `wwwroot/AppData` doesn't apply.
`AddM4dApplication` resolves it, registers it as a singleton, creates the directory at startup (so
a misconfigured path fails at boot rather than on the first backup) and logs
`App data root: <path>`.

### 2. Repoint the writers

- `UpdateMessageFileManager` gets `AppDataPaths.Root` instead of `WebRootPath`.
- `DanceStatsFileManager` and `ArtistIndexFileManager` take a new optional `dataRoot` for their
  `AppData` cache. They can't simply be repointed: they also read packaged files from
  `<root>/content` (`dances.json`, `dancegroups.json` and the `*-fallback.json` snapshots), which
  stay in the package. So they keep `WebRootPath` for content and get `dataRoot:
  AppDataPaths.Root`.
- The `ConfigureSearch` gating stays on the artist index and update message managers: the
  sandbox still resolves to `m4d/wwwroot` and shares it with the dev app.
- `EnsureAppData` returns `AppDataPaths.AppData`.
- The two `File("~/AppData/" + fname, …)` returns become
  `PhysicalFile(path, MediaTypeNames.Text.Plain, fname)`, because the file is no longer under the
  web root.
- `BatchProcess` writes its before/after logs to `AppDataPaths.AppData`. Only
  `BatchCleanupProperties?log=true` asks for them.
- Keep `BlockAppDataMiddleware` as defense in depth: locally `AppData` is still under `wwwroot`.

### 3. `blogmap.txt` / `helpmap.txt` overrides

`SiteMapFile` reads `<Root>/AppData/{name}.txt` when it exists **and is newer than** the
packaged `wwwroot/content/{name}.txt`. Otherwise it reads the packaged copy. It logs which copy
it used. Locally (no `M4D_DATA_ROOT`) that's `wwwroot/AppData`, which a build doesn't overwrite.

- **Fast path:** upload the edited file to `/home/data/m4d/AppData/` with Kudu, then run
  **Admin → Initialization Tasks → Update Sitemap**. The edit is live without a deploy.
- **The next deploy wins automatically:** the packaged copy's timestamp is the build time, so once
  the committed change ships, the package copy is newer and takes over. A stale override can't
  shadow later commits, and a deploy can't silently revert a fresh upload.

When an override exists, `SiteMapFile.ChoosePath` logs both timestamps (`Site map: using
override|packaged copy; override ... written ..., packaged ... written ...`). Use that line on
`m4d-test` to check that the mounted zip reports entry times as `LastWriteTimeUtc`. If it
doesn't, compare against the assembly's build time instead. Locally (sandbox with
`M4D_DATA_ROOT` pointed at a scratch folder) a newer override was picked up on the first load of
`/home/sitemap`.

### 4. Pipeline

**`appSettings` alone won't do it.** On Linux, `AzureWebApp@1` zip-deploys first and applies
`appSettings` afterwards (`PostDeploymentStep` in `BuiltInLinuxWebAppDeploymentProvider`). Kudu
decides when the zip arrives whether to extract it or store it in `/home/data/SitePackages` and
mount it. If `WEBSITE_RUN_FROM_PACKAGE=1` came in through `appSettings`, the deploy would extract
as before, then restart with the flag on and no stored package.

As built, a `runFromPackage` pipeline parameter (default `true`) drives an `AzureCLI@2` step that
runs **before** the deploy:

- It always sets `M4D_DATA_ROOT=/home/data/m4d`, so flipping `runFromPackage` never moves the data.
- With `runFromPackage`, it sets `WEBSITE_RUN_FROM_PACKAGE=1`; without it, it deletes the setting
  and the deploy extracts into `wwwroot` as before.
- It writes only what has changed, since each write restarts the app. An ordinary deploy with
  nothing to change makes no writes.
- `appsettings list` takes no `--ids`, so the step looks up the app's resource group and uses
  `-g`/`-n`.

The step is fatal on failure. If it fails before writing anything, nothing changes.

**Restart after a switch.** The first test deploy (2026-11-02) showed what happens when the
setting flips:

| Time (UTC) | What happened |
| --- | --- |
| 20:46:48 | Settings step restarts the app; with no package yet, the new container starts the old build from `wwwroot` while the previous container keeps serving |
| 20:47:34 | Package uploaded |
| 20:48:03 | App Service tries to mount the package into the still-starting container: "Container failed to remount volume. Terminate." It records `ContainerStartupFailure` and stops the site |
| 20:48:31 | Previous container stopped; the site is down from here |
| 20:53:24 | App Service retries on its own; the new build answers at about 20:53:48 |

That's about 5m15s down, nearly all of it App Service's wait before retrying. So the settings
step sets a `runFromPackageSwitched` pipeline variable whenever it adds or deletes
`WEBSITE_RUN_FROM_PACKAGE`, and a step after the deploy stops and starts the app when it's set.
It then polls the site until it answers with a 200 (up to 10 minutes), and logs how long that
took. If the site isn't up after three minutes it starts it once more. Ordinary deploys skip the
step.

It stops and starts rather than restarting because of a manual test on `m4d-test` the same
evening (all UTC):

- 21:01:03: a restart. A new container starts while the old one keeps serving.
- 21:01:45: a second restart cancels that start ("Site startup probe failed after 24.5s",
  "Failed to start site. Revert by stopping site"). The old container is still serving, but no new
  start follows.
- 21:02:01 and 21:02:10: a stop, then a start. The site is down from 21:02:18. The container
  launches at 21:03:36, about 80s after the start, and passes its startup probe 16s later, at
  21:03:53.

So a restart that lands mid-start can leave the site stopped, which is the state the restart step
exists to get out of. A stop followed by a start doesn't race with anything.

**Measured on `m4d-test`** (a 1-second poll of `/`, so outages are ±10s, the curl timeout):

- **A single restart from the portal (21:15):** no failed requests. The new container started at
  21:15:07 and passed its probe at 21:15:50. The old one served until then and stopped at
  21:16:11.
- **`runFromPackage` off (22:37):** deleting the setting restarted the app on the old extracted
  build, and the deploy extracted over it mid-start. It crashed (`Exception.ToString() failed`,
  `Aborted (core dumped)`): the failure this plan exists to remove. The stop-and-start had the new
  build up after ~45s down.
- **`runFromPackage` on (22:45):** the same remount failure as the first deploy. The stop-and-start
  had it up after ~45s down, instead of ~5 minutes.
- **An ordinary deploy, on (22:54):** no failed requests. The new package's container started at
  22:54:59 while the old one served, took over at 22:55:30, and the old one stopped at 22:55:51.

Both switching runs also had a second outage of 25–35s. The health check step rewrote
`/health/ready` on every deploy, and a config write restarts the app even when nothing changes.
Landing a few seconds after the stop-and-start, that restart didn't overlap the old container. The
step now writes only when the path differs, which should leave a switching deploy with one ~45s
outage.

**Self-contained mode risk:** its startup command executes `/home/site/wwwroot/m4d` directly, and
that depends on the execute bit surviving in the mounted zip. Verify this on `m4d-test`, or retire
self-contained mode. Framework-dependent mode (`dotnet m4d.dll`) has no such issue and is the
default.

### 5. No data migration

Once the package is mounted, the old extracted `wwwroot`, including its `AppData`, is hidden from
the app. That data isn't copied, because nothing in it needs to be:

- **Dance stats** start from the checked-in fallback snapshot. When they do,
  `DanceStatsHostedService` starts a background rebuild from the search index (the same
  `ClearCache(fromStore: true)` the 6-hourly songstats job runs), which writes the new cache.
  Before this change, a fallback load waited for that job. `IDanceStatsFileManager` now separates
  `GetStats` (runtime cache only) from `GetFallbackStats`, so the manager can tell the two apart;
  Admin → Diagnostics shows `Source = Fallback` until the rebuild lands.
- **The artist index** reads its fallback snapshot. That snapshot is older than its lifetime, so
  the first request starts a background rebuild, which writes the new snapshot.
- **The update message** only exists while an admin has one set. Set it again after the switch
  if one is active.
- **Backups and the usage import file** were downloaded or used when they were made. The old
  copies stay on disk in the hidden `wwwroot/AppData`, and reappear if run-from-package is turned
  off.

## Rollout

The code and the pipeline ship in one PR. Azure DevOps runs the YAML from the branch being
deployed, so the branch is deployed to test with the same pipeline that later deploys `main` to
production. No portal or SSH steps either time.

1. **Test.** Deploy the branch to `m4d-test` (framework-dependent, `runFromPackage` on) while a
   loop polls the site, e.g.
   `while true; do curl -s -o /dev/null -w "%{http_code}\n" https://m4d-test.azurewebsites.net/; sleep 1; done`.
   The first deploy has an extra restart, when the settings step switches the app over before the
   package arrives; see "Restart after a switch" above for what that did on the first run. To
   check the stop-and-start step, run the pipeline with `runFromPackage` off and then on again: each run
   should log `m4d-test is up after Ns`, and the outage should be close to a normal startup.
   Verify:
   - the log shows `App data root: /home/data/m4d`, and then the dance stats rebuild lines
     (`Dance stats loaded from the fallback snapshot ...`, `Dance stats rebuilt ...`)
   - `/home/data/m4d/AppData` gets `dance-environment.json`, and the artist snapshot after the
     first artist page
   - a database backup and an index backup download
   - an update message set in Admin survives a restart
   - a `blogmap.txt` uploaded to `/home/data/m4d/AppData/` shows up after Update Sitemap, and the
     `Site map: using ...` log line shows a sane packaged timestamp
   - a second deploy: count the non-200s and how long they last, and confirm there are no
     `BadImageFormatException`s in the logs
2. **Production.** Merge, then deploy `main` with the same parameters and pull the logs.

## Rollback

Run the pipeline with `runFromPackage` off. It deletes `WEBSITE_RUN_FROM_PACKAGE` and the deploy
extracts into `wwwroot` again. `M4D_DATA_ROOT` stays set, so the data under `/home/data/m4d` keeps
being used. Deploying a commit from before this change would ignore `M4D_DATA_ROOT` and go back to
the old `wwwroot/AppData`.

## Open questions

- **The restart gap.** Answered on `m4d-test`: none for an ordinary deploy, since the old
  container serves until the new one passes its probe (see "Measured on `m4d-test`" above). Only
  the deploy that switches the setting has an outage. Confirm the production numbers match.
- **Cleaning up old backups.** `/home` counts against the plan's storage quota, and backups now
  persist across deploys. Consider pruning backups older than N days, or document a manual
  clean-up.

## Docs to update when this ships

Fold the as-built design into the reference docs and delete this plan (see
[CONVENTIONS](../CONVENTIONS.md)):

- [hosting-and-identity](../infrastructure/hosting-and-identity.md): the deployment table, Run From
  Package, and `M4D_DATA_ROOT`.
- [transfer-files-to-app-service](../runbooks/transfer-files-to-app-service.md) and
  [link-new-blog-posts](../runbooks/link-new-blog-posts.md): upload to `/home/data/m4d/AppData/`,
  not `wwwroot`.
- AppData paths in [overview](../overview.md),
  [dance-domain-model](../dances/dance-domain-model.md),
  [service-resilience](../infrastructure/service-resilience.md),
  [individual-artists](../songs/individual-artists.md) and
  [data-layer](../infrastructure/data-layer.md).
- [read-production-logs](../runbooks/read-production-logs.md): mark the deploy-burst signature
  fixed.

## Related

- [hosting-and-identity](../infrastructure/hosting-and-identity.md)
- [ci-cd-and-release](../infrastructure/ci-cd-and-release.md)
- [transfer-files-to-app-service](../runbooks/transfer-files-to-app-service.md)
- [blog-help-sitemap](../pages/blog-help-sitemap.md)
