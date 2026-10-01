# Refresh the Dance Fallback Snapshot

**Type:** Runbook
**Status:** Current
**Last verified:** 2026-10-01 (against `AdminController.ExportDanceFallback`)

## When to use

`m4d/ClientApp/src/assets/content/dance-environment-fallback.json` is the checked-in
"last known good" dance environment: dances, groups, and per-dance song counts and top songs.
`DanceStatsFileManager` serves it when the runtime cache (`AppData/dance-environment.json`)
doesn't exist yet, which happens on a fresh deployment whose database or search index is
unreachable. See [service-resilience § Degraded behavior](../infrastructure/service-resilience.md#degraded-behavior).

Refresh it:
- periodically (roughly quarterly), so cold-start pages don't show stale counts
- after adding or renaming dances (see [add-a-dance](add-a-dance.md))
- after a large catalog change

## Prerequisites / access

- A **local** development checkout. The export writes into the source tree
  (`ContentRootPath/ClientApp/src/assets/content/`), so it only works when running from source,
  not on a deployed App Service.
- A local run connected to a **production-like** database and search index, so the counts are
  real and not a sparse dev dataset. For example, use the `PROD_DB` profile described in
  [provision-app-service § Local Development](provision-app-service.md#local-development-access-production-database-via-azure-ad).
- An account with the `showDiagnostics` role.

## Steps

1. Start `m4d` locally against the production-like data.
2. Open **Admin → Initialization Tasks** (`/Admin/InitializationTasks`).
3. Under **Reload Song Stats**, click **From Store** (`ClearSongCache?reloadFromStore=true`).
   This rebuilds the in-memory dance stats from the database and index.
4. Click **Export Dance Fallback** and confirm. It overwrites the checked-in JSON with
   `DanceStatsManager.Instance.SaveToJson()`.
5. Review the diff in `dance-environment-fallback.json`. Expect count changes and new top songs,
   but not dances disappearing. Then commit it in a PR.

## Verification

- The Results page reports `Wrote dance stats snapshot to …`.
- To exercise the file, stop the app, delete `wwwroot/AppData/dance-environment.json`, start with
  the database unreachable, and load a dance page. The console should log
  `Runtime cache not found, loading from static fallback`, and the page should render from the
  new snapshot.

## Rollback

Revert the commit. The file is only read when the runtime cache is missing, so a bad snapshot
can't affect a running instance that already has its runtime cache.
