# Artist Index Operations

**Type:** Runbook
**Status:** Current. Both the test and production indexes are rolled out (confirmed 2026-10-01).
**Last verified:** 2026-10-01

## When to use

- Rolling the `Artists` field out to a **new** index or environment (for example after a search
  index migration).
- Re-running the splitter after a heuristic change.
- Repairing coverage, or rolling back.

What `BatchArtists`, the save hook and the coverage check do is described in
[individual-artists §6](../songs/individual-artists.md#6-backfill-and-admin-operations).

## Prerequisites / access

- `dbAdmin` / `showDiagnostics` on the target environment.
- Change rights on the `ArtistIndex` flag in Azure App Configuration (production) or
  `appsettings` (local and Sandbox).

## First rollout, per environment

Do the test index first. All steps are on **Admin → Initialization Tasks** unless noted.

1. Deploy with `ArtistIndex` **off**. Nothing changes: the field isn't in the live index, and the
   save hook is dormant until `artist-bot` exists.
2. **Take an index backup** (`/Admin/IndexBackup`) — the rollback artifact.
3. **Add Missing Fields.** Read what it reports: it adds *every* missing field, not just `Artists`.
4. **Wait 10 minutes** (the schema cache) or restart, so every instance includes the field in
   uploads. The page shows the field state and when this instance last looked — but only for the
   instance serving the page.
5. **BatchArtists → Report.** Creates `artist-bot`, which also wakes the save hook. Sanity-check
   `Changed` against the offline analysis.
6. **BatchArtists → Apply.**
7. **Verify coverage** on the same page: populated should equal total minus the credit-less songs.
8. Turn `ArtistIndex` **on**. In production that is an Azure App Configuration flag and propagates
   within 5 minutes without a restart, per instance.

## After a heuristic change

Bump `ArtistSplitter.Version`, re-run the analysis harness and diff the reports, then **Report** and
**ApplyChanged**. `Apply` is only needed to repair coverage — `ApplyChanged` skips songs whose log
doesn't change, which is exactly the set with a missing index value.

## Rollback

Turn the flag off; artist pages revert to credit matching. Bad bot edits are corrected by re-running
with a fixed splitter — human and service lists are never touched. The unused field is harmless.

## Verification

**Admin → Initialization Tasks** shows the `Artists` field present, and populated equals total
minus the credit-less songs. `/song/artists` and the artist pages work with the flag on.
