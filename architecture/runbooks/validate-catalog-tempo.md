# Validate Tempo Across the Existing Catalog

**Type:** Runbook
**Status:** Current. Not yet run for real (see [tempo-validation-rules § Open Follow-Ups](../songs/tempo-validation-rules.md#open-follow-ups)).
**Last verified:** 2026-10-01

## When to use

- After adding or changing per-dance tempo validation rules (see
  [tempo-validation-rules § Current Scope](../songs/tempo-validation-rules.md#current-scope)).
- To apply half/double-time correction to songs imported before the rules existed.

## Background

`ValidateAndCorrectTempo` only requires `song.Tempo.HasValue` — it doesn't care whether that tempo came from a fresh Spotify import or has been sitting on the song for years. That makes it usable directly against already-populated catalog songs, independent of the `UpdateAudioData`/`GetEchoData` Spotify-refresh path described above.

`SongController.BatchValidateTempo` (`m4d/Controllers/SongController.cs`) exposes this via the same `BatchProcess` pattern as `BatchEchoNest`/`BatchISRC`: it streams every song matching the current admin song-list filter and calls `ValidateAndCorrectTempo` on each. It's wired into the song list's **Update** dropdown menu (`m4d/ClientApp/src/components/AdminFooter.vue`) as "Validate Tempo", alongside iTunes/EchoNest/ISRC/Samples. To roll out a newly-added dance's validation rules, filter the song list down to that dance (e.g. `dance:Quickstep`) before running it — since validation now runs per dance rather than gating on a single-dance song, filtering is purely about scoping *which songs get processed* (and keeping the batch small), not a correctness requirement.

## Prerequisites / access

An admin account that sees the song list's **Update** menu (the admin footer).

## Steps

1. Open the song list and filter it to **one dance** whose rules are new or changed, e.g.
   `dance:Quickstep`. This keeps the batch small and reviewable.
2. Choose **Update → Validate Tempo**. It runs `SongController.BatchValidateTempo` as a
   background batch. Corrections are committed as `tempo-bot` edits.
3. Spot-check corrected songs. Their history shows the `tempo-bot` edit with the per-dance
   override, and a song-level promotion when every dance agreed.
4. Review meter flags: search for `check-accuracy:Tempo`. Those songs are tagged, not corrected.
   There's no review UI.
5. Repeat for the next dance.

## Verification

The `tempo-bot` edits on the sampled songs look right (for example, Salsa now around 160–220 BPM
rather than half that), and nothing a real user set was overwritten. User-set tempos are no-ops by
design.

## Rollback

Each correction is a separate `tempo-bot` edit block, so undo bad ones by removing those edit
blocks. For a whole run, use the admin bulk tools to target `tempo-bot` edits in the date range
(see [admin-search-bulk-modify](../users-admin/admin-search-bulk-modify.md)).
