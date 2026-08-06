# Tempo and Meter Validation Rules for Imported Songs

## Summary

Spotify/EchoNest tempo detection sometimes reports half-time or double-time errors (e.g., Salsa detected at 80 BPM instead of 160 BPM). When a song's tempo is populated from Spotify, each of the song's dances is checked independently against its own dance-specific thresholds and auto-corrected if it looks like a detection error. A dance's "effective tempo" is its own per-dance override if it has one, otherwise the song-level tempo. Corrections are applied as per-dance overrides, one dance at a time — a multi-dance song where only one dance's tempo looks wrong gets only that dance corrected. If every dance ends up agreeing on the same effective tempo afterward and that differs from the song-level tempo, the song-level tempo is promoted to match. Suspicious meters are flagged with a tag for manual review rather than auto-corrected.

Corrections are committed as a second edit under the `tempo-bot` pseudo-user, so the audit trail shows the algorithmic Spotify import separately from the bot's correction.

**Status**: Implemented and covered by unit/integration tests, scoped to Salsa and Quickstep. Not yet exercised against real Spotify imports or run retroactively against the existing catalog (see "Running Against the Existing Catalog" below).

## Data Model

`DanceType` carries an optional `Validation` property (`DanceLib/DanceType.cs`):

```csharp
public DanceValidation Validation { get; set; }
```

`DanceValidation` (`DanceLib/DanceValidation.cs`) holds three optional rules, deserialized from the top-level `validation` block on a dance in `dances.json`:

```csharp
public class DanceValidation
{
    public decimal? DoubleTempoIfBelow { get; set; }
    public decimal? HalveTempoIfAbove { get; set; }
    public List<string> FlagInvalidMeters { get; set; }
}
```

For example, Salsa (`m4d/ClientApp/src/assets/content/dances.json`) — see "Current Scope" below for which dances currently have a `validation` block:

```json
{
  "id": "SLS",
  "name": "Salsa",
  "validation": {
    "doubleTempoIfBelow": 120.0,
    "halveTempoIfAbove": 250.0,
    "flagInvalidMeters": ["3/4", "6/8"]
  },
  "instances": [ /* American Rhythm, Social, ... */ ]
}
```

**Validation is scoped to the dance as a whole, not to a specific style/instance.** It was originally placed on `DanceInstance` (one style of a dance, e.g. Salsa's "Social" style vs. its "American Rhythm" competition style), with `ValidateTempo` falling back through a `DanceType`'s instances (preferring "Social", else the first instance with rules) whenever it was called with a `DanceType` rather than a specific instance. That fallback was doing real work silently: `DanceRating.DanceId` (what a song's dance rating actually stores) resolves through `Dances.Instance.DanceFromId` to a `DanceType`, never a specific instance, so every real caller was hitting the fallback heuristic regardless of which style a given song was actually tagged with. Moving `Validation` onto `DanceType` makes that the explicit, only behavior instead of an implicit one. Per-style validation was considered and rejected: there's no reliable way to resolve "which style is this song" today — a song's style tag (e.g. `Tag+:QST=International:Style`) is freeform crowd-tagged text with no controlled mapping to `DanceInstance.Style` strings — and the actual failure mode being caught (a raw Spotify tempo-detection error) doesn't depend on competitive style anyway.

A TypeScript mirror exists at `m4d/ClientApp/src/models/DanceDatabase/DanceValidation.ts`, referenced from `DanceType.ts` (not `DanceInstance.ts`) the same way, and mirrors all three fields including `flagInvalidMeters` (a `string[]`, kept in sync even though nothing on the client reads it today — parity avoids future drift). `doubleTempoIfBelow`/`halveTempoIfAbove` **are** read client-side: the admin Tempo List page's optional "Range" column (`m4d/ClientApp/src/pages/tempo-list`) shows `DanceType.validationRange`, the span between those two thresholds, as a sanity-check display alongside each dance's normal tempo range.

Each `DanceRating` also carries an optional `decimal? Tempo` (`m4dModels/DanceRating.cs:79`) — a per-dance tempo override, independent of the validation feature (it predates this correction logic; see "per-dance tempo" in the broader song model). A dance's *effective tempo* is `DanceRating.Tempo ?? Song.Tempo`. Corrections write to this field via a dance-qualified `Tempo:{danceId}=value` property rather than the song-level `Tempo` field — see "Per-Dance Tempo Edits" below. This is a separate axis from the type-vs-instance question above: `DanceRating.Tempo` is per-*dance* (keyed by `DanceId`, the same `DanceType`-level ID `Validation` now lives on), not per-style.

## Validation Logic

`DanceValidationExtensions.ValidateTempo(this DanceObject dance, decimal tempo, string meter)` (`DanceLib/DanceValidationExtensions.cs`):

- Resolves validation rules via `(dance as DanceType)?.Validation` — a no-op if `dance` isn't a `DanceType` (e.g. a bare `DanceInstance`) or the `DanceType` has no `Validation` set.
- If `tempo < DoubleTempoIfBelow`, doubles it. Else if `tempo > HalveTempoIfAbove`, halves it. (The two checks are mutually exclusive via `else if` — a dance can't trigger both in one call.)
- If `meter` is non-empty and appears in `FlagInvalidMeters`, sets `RequiresMeterFlag` independent of the tempo outcome.
- Returns a `TempoValidationResult` (`RequiresCorrection`, `CorrectedTempo`, `CorrectionReason`, `RequiresMeterFlag`, `MeterFlagReason`); all false/null when the dance has no validation rules.

Meter strings are plain `"n/d"` text (e.g. `"3/4"`), matched by exact string containment against `FlagInvalidMeters` — no numeric parsing or reduction (`4/4` and `8/8` are treated as different values).

## Integration Point

`MusicServiceManager.UpdateAudioData` (`m4d/Utilities/MusicServiceManager.cs:76`) calls `GetEchoData` to pull tempo/danceability/energy/valence from Spotify, then — only if that call reported a change and the song now has a tempo — calls `ValidateAndCorrectTempo`:

```csharp
changed |= await GetEchoData(dms, sd);
if (changed && sd.Tempo.HasValue)
{
    changed |= await ValidateAndCorrectTempo(dms, sd);
}
```

`GetEchoData` (`MusicServiceManager.cs:844`) is unchanged from its pre-validation form: it sets `edit.Tempo` from `track.BeatsPerMinute` and, if Spotify returned a meter, adds a `{meter}:Tempo` tag (e.g. `4/4:Tempo`) under the Spotify service user. It commits that edit on its own — validation is a separate follow-up edit, not inlined into this method.

`ValidateAndCorrectTempo` (`MusicServiceManager.cs:910`):

1. Returns `false` immediately if `song.DanceRatings.Count == 0`.
2. Reads the meter once from the song's tag set: `song.TagSummary.GetTagSet("Tempo")`, matching the first tag against `^\d+/\d+$` (`MeterRegex`, `MusicServiceManager.cs:26`). This is the same tag `GetEchoData` just wrote, and it's shared across all of a song's dances — there's no per-dance meter.
3. Loops over every `DanceRating` on the song independently:
   - Computes that dance's effective tempo: `dr.Tempo ?? song.Tempo`. Skips the dance if neither is set.
   - Looks up the `DanceObject` via `Dances.Instance.DanceFromId(dr.DanceId)`; skips the dance if not found.
   - Calls `dance.ValidateTempo(effectiveTempo, meter)`. A correction is recorded per dance ID in a `Dictionary<string, decimal>`; a meter flag sets a single shared `meterFlagged` bool (one `check-accuracy:Tempo` tag covers the whole song, not one per dance).
4. If no dance needed a correction and no meter flag fired, returns `false` with no edit made.
5. Otherwise builds a new `Song.Create(song, dms)` edit attributed to `tempo-bot` (`new ApplicationUser("tempo-bot", pseudo: true)`):
   - For each corrected dance, sets `edit.DanceRatings[i].Tempo` to the corrected value (see "Per-Dance Tempo Edits" below for how this differs from a plain scalar-field edit).
   - Recomputes every dance's effective tempo (using the new corrected values where applicable) and, if they all now agree on a single value that differs from `song.Tempo`, sets `edit.Tempo` to that value too — promoting the song-level tempo once the dances converge.
   - Adds a `check-accuracy:Tempo` tag if the meter flag fired.
   - Logs the reason per correction (`Information`) and per meter flag (`Warning`), then commits via `SongIndex.EditSong`.

Both tempo corrections and a meter flag can fire on the same edit — they aren't exclusive of each other, only the per-dance double/halve tempo checks are.

### Per-Dance Tempo Edits

`Song.Edit` normally diffs a fixed set of `ScalarFields` (`Song.cs:301`, includes song-level `Tempo`) by reflection: compare `edit.<Field>` to `this.<Field>`, and if different, mutate `this` and record a plain `Field=value` property. That mechanism has no concept of per-dance data, so it can't be used to move a single `DanceRating.Tempo` value.

`Song.EditCore` (`Song.cs:2016`) additionally calls `UpdateDanceTempos(edit)` (`Song.cs:2777`), which diffs `DanceRating.Tempo` per dance ID between `this.DanceRatings` and `edit.DanceRatings`. When they differ, it mutates the rating in place and records a dance-qualified `Tempo:{danceId}=value` property — the same property format already used for a user-authored `Tempo:CHA=128.0` edit (see `m4dModels.Tests/PerDanceTempoTests.cs`) and replayed by the `TempoField` case in `Song.LoadProperties` (`Song.cs:1688`). This keeps `ValidateAndCorrectTempo`'s per-dance corrections consistent with every other way a per-dance tempo override can be set, and lets them replay correctly the next time the song is reloaded from its full property history.

**Both song-level and per-dance `Tempo` are guarded against pseudo-user overwrites.** `Song.LoadProperties` tracks, per field, whether a *real* (non-pseudo) user has ever set it (`isUserModified`, `Song.cs:1581`) — one entry keyed `"Tempo"` for the song-level field, and one entry per dance keyed `"Tempo:{danceId}"` for that dance's override. Once a real user has set a given field, a later edit from a pseudo user (like `tempo-bot`, or a service account) targeting that *same* field is silently ignored on replay — the edit still "succeeds" (`SongIndex.EditSong` returns `true`, the property gets appended to history) but the value doesn't actually change once the song is reloaded from its full property list. This is intentional: it protects a human's explicit tempo entry — at either the song or the per-dance level — from being clobbered by an automated correction. The two guards are independent: a real user setting the song-level tempo doesn't block a bot from correcting a dance that has no explicit override of its own (its effective tempo just falls through to the now-locked song-level value, which the bot also can't touch), and a real user setting one dance's override doesn't affect any other dance. Net effect: `ValidateAndCorrectTempo`'s correction for a given dance is absorbed on replay only if a real user already set *that specific dance's* override; the song-level promotion step is absorbed only if a real user already set the *song-level* tempo. See `ValidateAndCorrectTempo_ConvergingCorrections_RealUserTempo_SongLevelNotOverwritten` and `ValidateAndCorrectTempo_RealUserDanceTempoOverride_NotOverwritten` in `MusicServiceManagerIntegrationTests.cs` for the regression tests documenting each case.

## Attribution

`tempo-bot` is a pseudo `ApplicationUser` (`IsPseudo == true`), the same pattern already used by `SongController.BatchCorrectTempo` for manual retroactive corrections. No new user, role, or database change was needed — `SongIndex.EditSong` and `SongProperties` already support attributing an edit to any `ApplicationUser`.

## Current Scope: Salsa and Quickstep

| Rule | Value |
| --- | --- |
| Double if tempo below | 120 BPM |
| Halve if tempo above | 250 BPM |
| Flag meters | `3/4`, `6/8` |
| Typical valid range | 160–220 BPM (Salsa's Social-style tempo range, for reference — the rule itself applies to Salsa as a whole, not just that style) |

Quickstep also has a `validation` block (same dance-level scoping as Salsa). The thresholds happen to match Salsa's numerically, but that's coincidental, not copy-paste — they've been validated as reasonable for Quickstep's own range (200–208 BPM, 200 flat under NDCA) independently. No other dance currently has a `validation` block, so `ValidateTempo` is a no-op for every other dance today.

## Running Against the Existing Catalog

`ValidateAndCorrectTempo` only requires `song.Tempo.HasValue` — it doesn't care whether that tempo came from a fresh Spotify import or has been sitting on the song for years. That makes it usable directly against already-populated catalog songs, independent of the `UpdateAudioData`/`GetEchoData` Spotify-refresh path described above.

`SongController.BatchValidateTempo` (`m4d/Controllers/SongController.cs`) exposes this via the same `BatchProcess` pattern as `BatchEchoNest`/`BatchISRC`: it streams every song matching the current admin song-list filter and calls `ValidateAndCorrectTempo` on each. It's wired into the song list's **Update** dropdown menu (`m4d/ClientApp/src/components/AdminFooter.vue`) as "Validate Tempo", alongside iTunes/EchoNest/ISRC/Samples. To roll out a newly-added dance's validation rules, filter the song list down to that dance (e.g. `dance:Quickstep`) before running it — since validation now runs per dance rather than gating on a single-dance song, filtering is purely about scoping *which songs get processed* (and keeping the batch small), not a correctness requirement.

## Manual Review Workflow

Songs with a suspicious meter are tagged `check-accuracy:Tempo` and otherwise left alone (no auto meter-correction exists). There is no admin UI for reviewing the tag itself yet — review means searching for `check-accuracy:Tempo` directly.

## Test Coverage

- `DanceTests/DanceValidationTests.cs` — unit tests for `ValidateTempo` itself: no-rules no-op, tempo doubling/halving at and around thresholds, meter flagging (including null/valid meter), both-at-once, that resolution is unaffected by which/how-many instances a `DanceType` has (no more style-based fallback to reason about), and that calling `ValidateTempo` directly on a bare `DanceInstance` is always a no-op now.
- `m4d.Tests/Utilities/MusicServiceManagerIntegrationTests.cs` — integration tests for `ValidateAndCorrectTempo` against a real `TestSongIndex`: zero dance ratings, missing tempo, unknown dance ID, dance with no validation rules (Waltz), boundary tempos (120, 250 — inclusive, no correction), valid tempo/meter (no-op), invalid meter alone (tag added, tempo untouched), and combined tempo + meter correction in one edit. Multi-dance coverage: a dance with rules gets corrected independently of a sibling dance with none; two dances that converge on the same corrected tempo promote the song-level tempo; the same convergence scenario with a real-user-set song tempo instead leaves the song-level tempo untouched while the per-dance overrides still apply; a real user's explicit per-dance override is likewise left untouched by a would-be correction to that same dance (see "Per-Dance Tempo Edits" above for both guards). Assertions check both the `tempo-bot` attribution and the exact `EditSong` payload.
- `m4dModels.Tests/PerDanceTempoTests.cs` exercises the underlying per-dance tempo data model and property replay independent of this feature (i.e., the `Tempo:{danceId}=value` format and effective-tempo semantics `ValidateAndCorrectTempo` builds on).
- `m4d/ClientApp/src/models/DanceDatabase/__tests__/DanceType.test.ts` and `.../DanceInstance.test.ts` cover the TypeScript `validationRange` getter's new home on `DanceType`; `m4d/ClientApp/src/pages/tempo-list/components/__tests__/TempoList.test.ts` covers the admin Tempo List page's "Range" column that reads it.
- No test yet exercises the real `GetEchoData` → `UpdateAudioData` → `ValidateAndCorrectTempo` chain end-to-end against live or recorded Spotify data — coverage stops at calling `ValidateAndCorrectTempo` directly with a pre-built `Song`.

## Open Follow-Ups

1. Test against real Salsa imports from Spotify playlists (no live/recorded-fixture test exists yet).
2. Run "Validate Tempo" (filtered to Quickstep) against the existing catalog as a first trial of the batch path on a dance other than Salsa; review the results before adding more dances.
3. Periodically search for `check-accuracy:Tempo` and review flagged songs.
4. Monitor false positive/negative rate before extending thresholds to other dances (Waltz, East Coast Swing, Cha Cha are plausible next candidates based on the same half/double-time failure mode).
5. On songs where a real user already set the song-level tempo (or a given dance's own override), the corresponding correction/promotion step is a guaranteed no-op (see "Per-Dance Tempo Edits" above). That's likely fine for most catalog songs, but hasn't been evaluated against how much of the catalog's tempo data traces back to real users versus service imports; worth checking before relying on the correction/promotion behavior at scale.
