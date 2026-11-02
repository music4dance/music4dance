# Run From Package: Atomic Deploys Without a Staging Slot

**Type:** Plan
**Status:** Proposed
**Last verified:** 2026-11-01
**Code:** `azure-pipelines.yml`, `m4d/Configuration/M4dApplicationExtensions.cs`,
`m4d/Controllers/DMController.cs`, `m4d/Controllers/AdminController.cs`,
`m4d/Controllers/SongController.cs`, `m4d/ViewModels/SiteMapInfo.cs`,
`m4dModels/DanceStatsFileManager.cs`, `m4dModels/ArtistIndexFileManager.cs`,
`m4d/Services/UpdateMessageFileManager.cs`

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
- `ExportDanceFallback` writes to `ClientApp/src/...` and `FileEmailSender` is sandbox-only. Both
  are development tools.

## Design

### 1. One persistent data root

Add a setting, `M4D_DATA_ROOT`, read through `IConfiguration` so it can be an App Service app
setting:

- **Unset** (local dev, sandbox, tests): use `WebRootPath`. Behavior is unchanged, and files stay
  in `wwwroot/AppData`.
- **Test and production:** `/home/data/m4d`.

Register a small immutable record, for example `AppDataPaths(Root)`, with `AppData` =
`Root/AppData` and `Content` = `Root/content`. Create it in `AddM4dApplication` and create both
directories eagerly at startup, so a misconfigured path fails at boot rather than on the first
backup.

### 2. Repoint the writers

- `DanceStatsFileManager`, `ArtistIndexFileManager` and `UpdateMessageFileManager` get
  `AppDataPaths.Root` instead of `WebRootPath`. All three already append `AppData`, so their file
  layout doesn't change. Keep the `ConfigureSearch` gating on the last two: the sandbox still
  resolves to `m4d/wwwroot` and shares it with the dev app.
- `EnsureAppData` returns `AppDataPaths.AppData`.
- The two `File("~/AppData/" + fname, …)` returns become
  `PhysicalFile(path, MediaTypeNames.Text.Plain, fname)`, because the file is no longer under the
  web root.
- `BatchProcess` writes its before/after logs to `AppDataPaths.AppData`.
- Keep `BlockAppDataMiddleware` as defense in depth: locally `AppData` is still under `wwwroot`.

### 3. `blogmap.txt` / `helpmap.txt` overrides

`SiteMapFile` reads `<Root>/content/{name}.txt` when it exists **and is newer than** the
packaged `wwwroot/content/{name}.txt`. Otherwise it reads the packaged copy. It logs which copy
it used.

- **Fast path:** upload the edited file to `/home/data/m4d/content/` with Kudu, then run
  **Admin → Initialization Tasks → Update Sitemap**. The edit is live without a deploy.
- **The next deploy wins automatically:** the packaged copy's timestamp is the build time, so once
  the committed change ships, the package copy is newer and takes over. A stale override can't
  shadow later commits, and a deploy can't silently revert a fresh upload.

Check during implementation that the mounted zip reports entry times as `LastWriteTimeUtc`. If it
doesn't, compare against the assembly's build time instead.

### 4. Pipeline

Add `-M4D_DATA_ROOT /home/data/m4d -WEBSITE_RUN_FROM_PACKAGE 1` to both `appSettings` lines in
`azure-pipelines.yml` (the self-contained and framework-dependent `AzureWebApp@1` tasks), so the
portal needs no manual settings.

**Self-contained mode risk:** its startup command executes `/home/site/wwwroot/m4d` directly, and
that depends on the execute bit surviving in the mounted zip. Verify this on `m4d-test`, or retire
self-contained mode. Framework-dependent mode (`dotnet m4d.dll`) has no such issue and is the
default.

## Rollout

Each step is verified on `m4d-test` before production.

1. **PR 1: code.** `AppDataPaths`, repointed writers, and the sitemap override. Nothing changes
   while `M4D_DATA_ROOT` is unset. Unit tests:
   - path resolution (unset → `WebRootPath`; set → that path)
   - `SiteMapFile` picks the newer of override and package
   - the existing `ArtistIndexFileTests` still pass
2. **Test: move the data.** Set `M4D_DATA_ROOT=/home/data/m4d` on `m4d-test`. Over SSH, run
   `mkdir -p /home/data/m4d && cp -a /home/site/wwwroot/AppData /home/data/m4d/`, then restart.
   Verify:
   - a database backup and an index backup download, and the files land in `/home/data/m4d/AppData`
   - `dance-environment.json` and the artist snapshot are rewritten there
   - an update message set in Admin lands in `update-message.json` there and survives a restart
   - a `blogmap.txt` upload to `/home/data/m4d/content/` shows up after Update Sitemap
3. **PR 2: pipeline.** Add both settings and deploy to test while a loop polls the site, e.g.
   `while true; do curl -s -o /dev/null -w "%{http_code}\n" https://m4d-test.azurewebsites.net/; sleep 1; done`.
   Count the non-200s and how long they last. Confirm there are no `BadImageFormatException`s in
   the logs.
4. **Production.** Copy `AppData` first: once the package is mounted, it hides the old extracted
   `wwwroot`, including its `AppData` (backups, caches and any active `update-message.json`).
   Then deploy and pull the logs.

## Rollback

Remove `WEBSITE_RUN_FROM_PACKAGE` (portal or `az webapp config appsettings delete`) and redeploy.
Everything under `/home/data/m4d` is unaffected, and the code keeps using it while
`M4D_DATA_ROOT` is set.

## Open questions

- **The restart gap.** With a single instance, a deploy still restarts the app, and
  `WEBSITES_CONTAINER_START_TIME_LIMIT` is 600s. Expect a startup gap rather than 500s from a
  process running half-replaced assemblies. Record the measured gap from rollout step 3 here.
- **Cleaning up old backups.** `/home` counts against the plan's storage quota, and backups now
  persist across deploys. Consider pruning backups older than N days, or document a manual
  clean-up.

## Docs to update when this ships

Fold the as-built design into the reference docs and delete this plan (see
[CONVENTIONS](../CONVENTIONS.md)):

- [hosting-and-identity](../infrastructure/hosting-and-identity.md): the deployment table, Run From
  Package, and `M4D_DATA_ROOT`.
- [transfer-files-to-app-service](../runbooks/transfer-files-to-app-service.md) and
  [link-new-blog-posts](../runbooks/link-new-blog-posts.md): upload to `/home/data/m4d/content/`,
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
