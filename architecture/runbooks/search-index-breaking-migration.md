# Search Index Breaking Migration

**Type:** Runbook
**Status:** Current
**Last verified:** 2026-10-02

## When to use

You need a change to the Azure AI Search schema that the current index can't serve: new or
renamed fields, or changed field types or analyzers. Changes that don't alter the schema don't need
this. The concepts (`CodeVersion` / `ConfigVersion`, `SongIndexNext`, naming) are in
[search-index-versioning](../search/search-index-versioning.md). Below, N is the current
`CodeVersion` and N+1 is the new schema.

## Prerequisites / access

- `showDiagnostics` (to run `UpdateSearchIdx`), `dbAdmin` (to run `CloneIdx` and `SetSearchIdx`,
  including the rollback below), and Azure AI Search access (to provision and delete indexes).
- Enough Search capacity for two full indexes at once.

## Steps: developing the change

1. **Define the new schema in `SongIndexNext`.** It must keep `IsNext => true`, because every
   operation picks the versioned index name from `IsNext`. Override `BuildIndex()` and anything
   else that differs. Leave `SongIndex` emitting schema N.
2. **Add filter differences to `SongFilterNext`**, if OData expressions change. Always obtain
   filters through `SearchService.GetSongFilter()`, never `new SongFilter(...)`.
3. **Add compatibility shims, tagged `// TODOIDX:`,** wherever running code must handle both
   schemas, guarding on `SongIndex.IsNext`.
4. **Add the N+1 index entries** (`SongIndexProd-N+1`, `SongIndexTest-N+1`) to `appsettings.json`.
   **Don't bump `CodeVersion` yet.** Cutover works by setting `ConfigVersion = CodeVersion + 1`.
5. **Test with the `m4d-next` profile**, with `SEARCHINDEXVERSION` set to N+1. Create and populate
   `songs-test-N+1` via **Admin → UpdateSearchIdx** on test, or with `CloneIdx` then `SetSearchIdx`.
6. **Provision `songs-prod-N+1`** in Azure (it can be empty). Merge, and deploy to production with
   `SEARCHINDEXVERSION` = N, unchanged, so schema N stays live. Verify the deployment is healthy.

## Steps: production cutover

1. On `/Admin/Diagnostics`, confirm the current index is active and the next-version index is
   configured but not live.
2. Run **`GET /Admin/UpdateSearchIdx`**. This calls `DanceMusicCoreService.UpdateIndex`, which:
   1. posts a site-wide "upgrading infrastructure" banner
   2. resets `songs-prod-N+1` (`SongIndexNext.ResetIndex()`)
   3. streams every song from the current index with `BackupIndexStreamingAsync()` and uploads
      them (see [index-backup-streaming](../search/index-backup-streaming.md))
   4. calls `RedirectToUpdate()` (`ConfigVersion = CodeVersion + 1`, so `NextVersion = true`)
   5. reloads dance stats and clears the banner

   This switch is in memory, and the app is single-instance (see
   [hosting-and-identity](../infrastructure/hosting-and-identity.md)). A restart reverts to
   `SEARCHINDEXVERSION`, so finish step 4 before the next deploy or restart.
3. **Verify:** spot-check search, dance pages and tag filters. `/Admin/Diagnostics` should show
   `songs-prod-N+1` active. Check that any new fields appear in diagnostics queries.
4. **Make it permanent:** fold the `SongIndexNext` / `SongFilterNext` differences into the base
   classes, bump `CodeVersion` to N+1, and set `SEARCHINDEXVERSION` = N+1 in `azure-pipelines.yml`.
   Then deploy.
5. **Clean up**, in a follow-up PR once the cutover is stable: remove the `TODOIDX` shims, delete
   the `-N` entries from `appsettings.json`, and delete the old indexes in Azure.

## Verification

Search and dance pages work, `/Admin/Diagnostics` shows the N+1 index, and after step 4 a restart
keeps it, with no `NextVersion` toggle needed.

## Rollback

Before step 4, clicking `SongIndexProd` under **Search Index** on `/Admin/Diagnostics` (a POST to
`SetSearchIdx`) switches straight back to the previous index with no redeploy, and dance stats
reload automatically. A restart does the same. Fix the problem, then run `UpdateSearchIdx` again.
