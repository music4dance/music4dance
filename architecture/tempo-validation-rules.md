# Tempo and Meter Validation Rules for Imported Songs

## Summary

Spotify/EchoNest tempo detection sometimes reports half-time or double-time errors (e.g., Salsa detected at 80 BPM instead of 160 BPM). When a song's tempo is populated from Spotify and the song has exactly one dance rating, the tempo is checked against dance-specific thresholds and auto-corrected if it looks like a detection error. Suspicious meters are flagged with a tag for manual review rather than auto-corrected.

Corrections are committed as a second edit under the `tempo-bot` pseudo-user, so the audit trail shows the algorithmic Spotify import separately from the bot's correction.

**Status**: Implemented and covered by unit/integration tests, scoped to Salsa only. Not yet exercised against real Spotify imports or run retroactively against existing songs.

## Data Model

`DanceInstance` carries an optional `Validation` property (`DanceLib/DanceInstance.cs:63`):

```csharp
public DanceValidation Validation { get; set; }
```

`DanceValidation` (`DanceLib/DanceValidation.cs`) holds three optional rules, deserialized from the `validation` block on a dance instance in `dances.json`:

```csharp
public class DanceValidation
{
    public decimal? DoubleTempoIfBelow { get; set; }
    public decimal? HalveTempoIfAbove { get; set; }
    public List<string> FlagInvalidMeters { get; set; }
}
```

Currently populated only for Salsa's Social style (`m4d/ClientApp/src/assets/content/dances.json`):

```json
{
  "style": "Social",
  "tempoRange": { "min": 160.0, "max": 220.0 },
  "validation": {
    "doubleTempoIfBelow": 120.0,
    "halveTempoIfAbove": 250.0,
    "flagInvalidMeters": ["3/4", "6/8"]
  }
}
```

A TypeScript mirror exists at `m4d/ClientApp/src/models/DanceDatabase/DanceValidation.ts` for client-side deserialization of `dances.json`. **Note**: it currently only mirrors `doubleTempoIfBelow`/`halveTempoIfAbove` — `flagInvalidMeters` isn't on the TS type. This is harmless today since nothing on the client reads it, but keep it in mind if a client feature ever needs meter-flag data.

## Validation Logic

`DanceValidationExtensions.ValidateTempo(this DanceObject dance, decimal tempo, string meter)` (`DanceLib/DanceValidationExtensions.cs`):

- Resolves validation rules from the dance: if called on a `DanceInstance`, uses its own `Validation`; if called on a `DanceType`, prefers the `Social` instance's rules, falling back to the first instance that has any.
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

1. Returns `false` immediately unless `song.DanceRatings.Count == 1` — this is the "first dance" gate. Songs with zero or multiple dance ratings are left untouched.
2. Looks up the `DanceObject` for `song.DanceRatings[0].DanceId` via `Dances.Instance.DanceFromId`; returns `false` if the dance isn't found or the song has no tempo.
3. Reads the meter back from the song's tag set: `song.TagSummary.GetTagSet("Tempo")`, matching the first tag against `^\d+/\d+$` (`MeterRegex`, `MusicServiceManager.cs:26`). This is the same tag `GetEchoData` just wrote.
4. Calls `dance.ValidateTempo(tempo, meter)`. If neither a correction nor a meter flag is needed, returns `false` with no edit made.
5. Otherwise builds a new `Song.Create(song, dms)` edit attributed to `tempo-bot` (`new ApplicationUser("tempo-bot", pseudo: true)`), sets `edit.Tempo` to the corrected value if applicable, adds a `check-accuracy:Tempo` tag if the meter flag fired, logs the reason (`Information` for corrections, `Warning` for meter flags), and commits via `SongIndex.EditSong`.

Both a tempo correction and a meter flag can fire on the same edit — they aren't exclusive of each other, only the double/halve tempo checks are.

## Attribution

`tempo-bot` is a pseudo `ApplicationUser` (`IsPseudo == true`), the same pattern already used by `SongController.BatchCorrectTempo` for manual retroactive corrections. No new user, role, or database change was needed — `SongIndex.EditSong` and `SongProperties` already support attributing an edit to any `ApplicationUser`.

## Current Scope: Salsa Only

| Rule | Value |
| --- | --- |
| Double if tempo below | 120 BPM |
| Halve if tempo above | 250 BPM |
| Flag meters | `3/4`, `6/8` |
| Typical valid range | 160–220 BPM (Social style) |

Quickstep's International Standard style also now has a `validation` block. The thresholds happen to match Salsa's numerically, but that's coincidental, not copy-paste — they've been validated as reasonable for Quickstep's own range (200–208 BPM, 200 flat under NDCA) independently. No other dance currently has a `validation` block, so `ValidateTempo` is a no-op for every other dance today.

## Running Against the Existing Catalog

`ValidateAndCorrectTempo` only requires `song.Tempo.HasValue` — it doesn't care whether that tempo came from a fresh Spotify import or has been sitting on the song for years. That makes it usable directly against already-populated catalog songs, independent of the `UpdateAudioData`/`GetEchoData` Spotify-refresh path described above.

`SongController.BatchValidateTempo` (`m4d/Controllers/SongController.cs`) exposes this via the same `BatchProcess` pattern as `BatchEchoNest`/`BatchISRC`: it streams every song matching the current admin song-list filter and calls `ValidateAndCorrectTempo` on each. It's wired into the song list's **Update** dropdown menu (`m4d/ClientApp/src/components/AdminFooter.vue`) as "Validate Tempo", alongside iTunes/EchoNest/ISRC/Samples. To roll out a newly-added dance's validation rules, filter the song list down to that dance (e.g. `dance:Quickstep`) before running it — the per-song `DanceRatings.Count == 1` gate then does the rest of the narrowing automatically.

## Manual Review Workflow

Songs with a suspicious meter are tagged `check-accuracy:Tempo` and otherwise left alone (no auto meter-correction exists). There is no admin UI for reviewing the tag itself yet — review means searching for `check-accuracy:Tempo` directly.

## Test Coverage

- `DanceTests/DanceValidationTests.cs` — unit tests for `ValidateTempo` itself: no-rules no-op, tempo doubling/halving at and around thresholds, meter flagging (including null/valid meter), both-at-once, and `DanceType` instance-resolution (prefers Social, falls back to first instance with rules).
- `m4d.Tests/Utilities/MusicServiceManagerIntegrationTests.cs` — integration tests for `ValidateAndCorrectTempo` against a real `TestSongIndex`: zero/multiple dance ratings, missing tempo, unknown dance ID, dance with no validation rules (Waltz), boundary tempos (120, 250 — inclusive, no correction), valid tempo/meter (no-op), invalid meter alone (tag added, tempo untouched), and combined tempo + meter correction in one edit. Assertions check both the `tempo-bot` attribution and the exact `EditSong` payload.
- No test yet exercises the real `GetEchoData` → `UpdateAudioData` → `ValidateAndCorrectTempo` chain end-to-end against live or recorded Spotify data — coverage stops at calling `ValidateAndCorrectTempo` directly with a pre-built `Song`.

## Open Follow-Ups

1. Test against real Salsa imports from Spotify playlists (no live/recorded-fixture test exists yet).
2. Run "Validate Tempo" (filtered to Quickstep) against the existing catalog as a first trial of the batch path on a dance other than Salsa; review the results before adding more dances.
3. Periodically search for `check-accuracy:Tempo` and review flagged songs.
4. Monitor false positive/negative rate before extending thresholds to other dances (Waltz, East Coast Swing, Cha Cha are plausible next candidates based on the same half/double-time failure mode).
5. Decide whether `flagInvalidMeters` needs to reach the TypeScript `DanceValidation` model, if a client feature ever wants it.
6. The single-dance-rating gate (`DanceRatings.Count == 1`) exists because tempo used to be one value per song. Now that dances carry their own tempos, songs with multiple dance ratings could in principle be validated per-dance instead of being skipped outright — noted as a follow-on improvement, not yet designed.
