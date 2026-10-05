---
name: reload-local-data
description: >-
    Refreshes the local music4dance database and the SongIndexTest Azure search index from the
    newest production backups in local/ (backup-*.txt and index-*.txt) by driving the
    /Admin/ReloadDatabase and /Admin/LoadIdx actions, and fixes whatever breaks along the way.
    Use when the user asks to reload, refresh, or restore the local database or the test index
    from production, or mentions loading a backup-*.txt / index-*.txt.
---

# reload-local-data

Loads the newest `local/backup-*.txt` into the local SQL database, then the newest
`local/index-*.txt` into the **SongIndexTest** search index. Downloading the files from
production stays manual (`/Admin/UploadBackup` → "Backup the Database" and "Backup Index" on the
live site); if either file is missing, or looks older than the user expects, stop and say so.

The driver is [reload-local-data.ps1](reload-local-data.ps1) (PowerShell 7, `pwsh`). Run it from
the repo root, one step at a time, so a failing step can be fixed and retried on its own:

```powershell
pwsh -NoProfile -File .claude/skills/reload-local-data/reload-local-data.ps1 -Step start
pwsh -NoProfile -File .claude/skills/reload-local-data/reload-local-data.ps1 -Step reload-db
pwsh -NoProfile -File .claude/skills/reload-local-data/reload-local-data.ps1 -Step load-index
pwsh -NoProfile -File .claude/skills/reload-local-data/reload-local-data.ps1 -Step stop
```

`-BackupFile` / `-IndexFile` override the newest-file pick. Server output goes to
`local/reload-local-data/server.log`; the script prints a filtered tail of it on any failure.

`load-index` takes several minutes (upload, then batched pushes to Azure). Run it with
`run_in_background` and redirect its output to `local/reload-local-data/load-index.out` instead
of blocking on the foreground timeout.

## Safety: what keeps this off production

The test index and the local DB are the only targets. Don't loosen any of these:

- `start` launches its **own** server (`dotnet run --no-launch-profile`) with a pinned
  environment: `SEARCHINDEX=SongIndexTest`, `PROD_DB=false`, `TEST_DB=false`, no
  `SEARCHINDEXVERSION`. It refuses to start if port 5000/5001 is already in use (the user's own dev
  server might be on the `m4d-prod-db` profile), or if `AZURE_SQL_CONNECTIONSTRING` is set, and
  checks the startup log for the local `DanceMusicContextConnection`.
- The other steps only talk to a server whose PID the script recorded.
- `idxName` is hard-coded to `SongIndexTest`. Before posting, `load-index` reads
  `/Admin/Diagnostics` and refuses unless `Enviroment` is `SongIndexTest` and `Active Index`
  matches `songs-test-N`.
- If you ever change the target, the index name, or these checks, ask the user first. Never point
  `LoadIdx` at `SongIndexProd`: with `reset=true` it empties the live index before loading.

If the user's dev server is holding the ports, ask them to stop it; don't kill it.

## Logging in

The script signs in with `M4D_ADMIN_USER` / `M4D_ADMIN_PASSWORD` from the m4d user secrets
(`dotnet user-secrets list --project m4d`). Never print those values. The admin account is
re-seeded by `RestoreDb` during the reload, so the login keeps working after the user table is
replaced with production's.

## Verify, don't trust "Database restored"

`ReloadDatabase` reports success even when its loaders silently skip lines. After `reload-db`,
compare row counts to the backup's sections. Section markers are the `UserId\t…` header and the
`+++++DANCES+++++`, `+++++TAGSS+++++`, `+++++PLAYLISTS+++++` and `+++++SEARCHES+++++` lines:

```powershell
sqlcmd -S "(localdb)\mssqllocaldb" -d m4d -W -Q "SET NOCOUNT ON; SELECT 'users',COUNT(*) FROM AspNetUsers UNION ALL SELECT 'dances',COUNT(*) FROM Dances UNION ALL SELECT 'tags',COUNT(*) FROM TagGroups UNION ALL SELECT 'playlists',COUNT(*) FROM PlayLists UNION ALL SELECT 'searches',COUNT(*) FROM Searches"
```

Expected differences (as of the 2026-09-30 backup):

- **users**: DB = backup lines + the seeded local accounts (admin, tester, editor if configured).
- **playlists**: the section ends with one blank line, which isn't a row.
- **searches**: backups written before the fix for [#329](https://github.com/music4dance/music4dance/issues/329)
  come up about 1,000 short (the 2026-09-30 backup is one). Those are anonymous searches whose query
  text contains literal tabs or newlines (`Blueberry Hill<TAB>Fats Domino`), which
  `ParseSearchEntry` can't split. The restore message reports the count as `Database restored (N
  malformed search line(s) skipped)`. `SerializeSearches` now escapes those characters, so a backup
  taken after the fix should have no skipped lines. If one does, that's a bug to chase.

After `load-index`, the result message is `Index SongIndexTest loaded with N songs`. N should
roughly match the number of song lines in the index file. A big shortfall is a bug to chase.

## When a step fails

That's the point of the skill: diagnose it, fix the code, restart and retry. Loop:

1. Read the failure and the server-log tail the script printed (`server.log` has the rest; the
   Vite "manifest not found" errors are noise, since the client isn't built).
2. Find the cause in `m4d/Controllers/AdminController.cs` (`ReloadDatabase`, `LoadIdx`) or the
   loaders in `m4dModels/DanceMusicService.cs` (`LoadUsers`, `LoadDances`, `LoadTags`,
   `LoadPlaylists`, `LoadSearches`) and `SongIndex.UploadIndex`.
3. Fix it on a branch off `main` (not on whatever unrelated branch is checked out), following
   CLAUDE.md. Add a test when the bug lives in parsing or loading logic.
4. `-Step start` again: it stops the old server and rebuilds. Then re-run the failed step.
   `reload-db` wipes and reloads everything, so it's safe to repeat. `load-index` resets the
   index first, so a partial load is also fixed by re-running it.
5. When both steps pass and the counts check out, `-Step stop`. Run the server tests
   (`dotnet test --filter "FullyQualifiedName!~SelfCrawler"`) and confirm the build is warning
   clean, then report the fixes to the user. Commit and open a PR only if they ask.

Known failure modes already fixed (check for regressions first):

| Symptom | Cause | Fix |
| --- | --- | --- |
| `LoadIdx` returns HTTP 400 with no Results page, and nothing reaches the controller | The index file is over ASP.NET Core's default 128 MB `MultipartBodyLengthLimit`. The antiforgery filter's form read fails first. `[DisableRequestSizeLimit]` only lifts Kestrel's cap. | `[RequestFormLimits(MultipartBodyLengthLimit = int.MaxValue)]` on `LoadIdx` and `ReloadDatabase` (2026-10) |

Add a row whenever you fix a new one.

## Report

Tell the user which files were loaded, the DB row counts against the expected numbers, the index
song count, and any code you changed, with links.
