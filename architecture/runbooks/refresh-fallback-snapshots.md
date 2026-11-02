# Refresh the Fallback Snapshots

**Type:** Runbook
**Status:** Current
**Last verified:** 2026-11-02 (against `AdminController.ExportFallbacks`)

## When to use

Two checked-in "last known good" snapshots live in `m4d/ClientApp/src/assets/content/`:

- `dance-environment-fallback.json`: dances, groups, and per-dance song counts and top songs.
  `DanceStatsFileManager` serves it when the runtime cache (`AppData/dance-environment.json`)
  doesn't exist yet.
- `artist-index-fallback.json`: every individual artist with a song count. `ArtistIndexFileManager`
  serves it when the runtime snapshot (`AppData/artist-index.json`) doesn't exist yet. See
  [individual-artists § 9.6](../songs/individual-artists.md#96-the-snapshot-on-disk).

Both cover a fresh deployment whose database or search index is unreachable. See
[service-resilience § Degraded behavior](../infrastructure/service-resilience.md#degraded-behavior).

Refresh them:

- periodically (roughly quarterly), so cold-start pages don't show stale counts
- after adding or renaming dances (see [add-a-dance](add-a-dance.md))
- after a large catalog change, or an artist backfill that materially changes the artist list

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
4. Click **Export Fallbacks** and confirm. It overwrites both files:
   - the dance file with `DanceStatsManager.Instance.SaveToJson()`
   - the artist file with an artist index built fresh by one streaming pass over the search index.
     It doesn't reuse the one `ArtistIndexCache` holds, which may have been read back from the old
     fallback. The pass takes a while on the full catalog, so expect the request to be slow.

   If the artist index comes back empty (for example, the index is mid-rebuild), neither file is
   written.
5. Review the diffs. Expect count changes and new top songs and artists, but not dances
   disappearing. Then commit them in a PR.

The artist file is written indented, which matches prettier's formatting, so `yarn format` has
nothing to change. Line endings are normalized to LF by `.gitattributes` on commit.

The manual `ArtistIndexSnapshot` test harness can also regenerate the artist file from an index
backup instead of a live index (see
[individual-artists § 12](../songs/individual-artists.md#12-analysis-harnesses)).

## Verification

- The Results page reports `Wrote dance stats snapshot to … and N artists to …`.
- To exercise the files, stop the app, delete `wwwroot/AppData/dance-environment.json` and
  `wwwroot/AppData/artist-index.json`, then start with the database unreachable. Load a dance page
  and `/song/artists`. The console should log
  `Runtime cache not found, loading from static fallback` for the dances, and the app log should
  show `Loaded artist index from disk` with the new snapshot's count and built time.

## Rollback

Revert the commit. The files are only read when the runtime cache is missing, so a bad snapshot
can't affect a running instance that already has its runtime cache.
