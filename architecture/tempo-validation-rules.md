# Tempo and Meter Validation Rules for Imported Songs

## Executive Summary

**Problem**: Spotify/EchoNest tempo detection sometimes reports half-time or double-time errors (e.g., Salsa at 80 BPM instead of 160 BPM).

**Solution**: Validate and auto-correct tempo when adding it from external sources, but ONLY if it's the first dance being added to the song.

**Integration Point**: `MusicServiceManager.GetEchoData()` method (~line 737 in `m4d/Utilities/MusicServiceManager.cs`)

**User Attribution**: Use existing tempo-bot pseudo-user for corrections

**Initial Scope**: Salsa only (double if < 120 BPM, halve if > 250 BPM, flag 3/4 or 6/8 meter)

---

## Overview

When adding tempo data to songs from Spotify/EchoNest, implement sanity checks against the reported tempo and meter. The system should automatically correct common tempo detection errors and flag suspicious meter data for manual review.

**Key Insight**: Validation runs when adding tempo from external sources (Spotify/EchoNest), but **ONLY if this is the first dance being added to the song**. This ensures we don't modify tempos on songs that already have dance data.

## Goals

1. Detect and correct common tempo detection errors (e.g., half-time/double-time mistakes)
2. Flag songs with suspicious meter signatures for manual review
3. Start with Salsa (most problematic), extend to other dances later
4. Maintain audit trail of automatic corrections via existing "tempo-bot" pseudo-user

## Proposed Schema Changes

### Dance JSON Schema (`dances.json`)

Add a new `validation` property to dance instances using the simpler schema approach:

```json
{
  "id": "SLS",
  "name": "Salsa",
  "meter": { "numerator": 4, "denominator": 4 },
  "instances": [
    {
      "style": "Social",
      "tempoRange": { "min": 160.0, "max": 220.0 },
      "validation": {
        "doubleTempoIfBelow": 120.0,
        "halveTempoIfAbove": 250.0,
        "flagInvalidMeters": ["3/4", "6/8"]
      }
    }
  ]
}
```

**Note**: Start with Salsa only. Other dances can be added later after validating the approach.

### C# Model Changes (`Dance.cs` or new `DanceValidation.cs`)

```csharp
public class DanceValidation
{
    public decimal? DoubleTempoIfBelow { get; set; }
    public decimal? HalveTempoIfAbove { get; set; }
    public List<MeterSignature>? FlagInvalidMeters { get; set; }
}

public class DanceInstance
{
    public string Style { get; set; }
    public TempoRange TempoRange { get; set; }
    public DanceValidation? Validation { get; set; }
    // ... existing properties
}

public class MeterSignature
{
    public int Numerator { get; set; }
    public int Denominator { get; set; }

    public override string ToString() => $"{Numerator}/{Denominator}";
}
```

## Implementation Plan

### Phase 1: Data Model Updates

1. **Update `dances.json`** - Add validation rules for Salsa only (starting point)
2. **Update C# Models** - Add validation properties to DanceInstance/DanceObject in DanceLibrary
3. **Ensure DanceLibrary Support** - Validation rules must be accessible when loading dance data

### Phase 2: Validation Engine

Create a new helper method or service that validates tempo when adding dance data to a song:

**Key Integration Point**: When adding tempo from Spotify/EchoNest, check if this is the **first dance** being added to the song. If yes, run validation.

```csharp
public class TempoValidator
{
    public static ValidationResult ValidateTempo(
        decimal tempo,
        string meterString,  // e.g., "4/4"
        DanceObject dance)
    {
        var result = new ValidationResult();
        var danceInstance = dance.GetPrimaryInstance(); // or specific instance

        if (danceInstance?.Validation == null)
        {
            return result; // No validation rules for this dance
        }

        // Check tempo corrections
        if (danceInstance.Validation.DoubleTempoIfBelow.HasValue &&
            tempo < danceInstance.Validation.DoubleTempoIfBelow.Value)
        {
            result.CorrectedTempo = tempo * 2;
            result.CorrectionReason = $"Tempo {tempo} BPM below threshold {danceInstance.Validation.DoubleTempoIfBelow} - doubled to {result.CorrectedTempo}";
            result.RequiresCorrection = true;
        }
        else if (danceInstance.Validation.HalveTempoIfAbove.HasValue &&
            tempo > danceInstance.Validation.HalveTempoIfAbove.Value)
        {
            result.CorrectedTempo = tempo / 2;
            result.CorrectionReason = $"Tempo {tempo} BPM above threshold {danceInstance.Validation.HalveTempoIfAbove} - halved to {result.CorrectedTempo}";
            result.RequiresCorrection = true;
        }

        // Check meter validation
        if (danceInstance.Validation.FlagInvalidMeters?.Contains(meterString) == true)
        {
            result.RequiresMeterFlag = true;
            result.MeterFlagReason = $"Invalid meter {meterString} for {dance.Name} (expected {dance.Meter})";
        }

        return result;
    }
}

public class ValidationResult
{
    public bool RequiresCorrection { get; set; }
    public decimal? CorrectedTempo { get; set; }
    public string? CorrectionReason { get; set; }

    public bool RequiresMeterFlag { get; set; }
    public string? MeterFlagReason { get; set; }
}
```

### Phase 3: Integration Point - Inside GetEchoData()

**Location**: `MusicServiceManager.GetEchoData()` method (line ~713 in `m4d/Utilities/MusicServiceManager.cs`)

**Key Understanding**:

- By the time `GetEchoData()` is called, the dance has **already been added** to the song
- We validate tempo **only if `song.Dances.Count == 1`** (first dance scenario)
- Validation happens **inside** `GetEchoData()` before setting `edit.Tempo`
- Use tempo-bot user (already exists in system) to append correction properties
- No database changes needed - SongProperties already stores tempo history

**Implementation Approach**:

1. Add extension method `ValidateTempo()` on `DanceObject` in DanceLibrary
2. Modify `GetEchoData()` to call validation before setting tempo
3. Use existing `EditSong()` infrastructure to persist changes

**Revised Pseudo-code**:

```csharp
public async Task<bool> GetEchoData(DanceMusicCoreService dms, Song song)
{
    var service = MusicService.GetService(ServiceType.Spotify);
    var ids = song.GetPurchaseIds(service);
    var user = service.ApplicationUser;
    var edit = await Song.Create(song, dms);

    EchoTrack track = null;
    foreach (var id in ids)
    {
        track = await LookupEchoTrack(id, service);
        if (track != null)
        {
            break;
        }
    }

    if (track == null)
    {
        edit.Danceability = float.NaN;
        return await dms.SongIndex.EditSong(user, song, edit);
    }

    // NEW: Validate tempo if this is the first (and only) dance
    if (track.BeatsPerMinute != null)
    {
        var tempo = track.BeatsPerMinute.Value;

        // Short-circuit: Only validate if exactly one dance
        if (song.Dances.Count == 1)
        {
            var dance = song.Dances[0]; // Get the first (only) dance
            var validation = dance.ValidateTempo(tempo, track.Meter);

            if (validation.RequiresCorrection)
            {
                // Use tempo-bot for corrections
                var tempoBot = new ApplicationUser("tempo-bot", isSystem: true);

                // Set corrected tempo
                edit.Tempo = validation.CorrectedTempo;

                // TODO: Research how to append properties to edit
                // These need to be persisted as SongProperties
                // Possible approach:
                // - edit.AppendProperty(...)?
                // - Or append after EditSong() call?

                Logger.LogInformation(
                    $"Tempo-bot corrected {song.Title}: {tempo} -> {validation.CorrectedTempo} BPM. Reason: {validation.CorrectionReason}");
            }
            else
            {
                // Use original tempo
                edit.Tempo = tempo;
            }

            if (validation.RequiresMeterFlag)
            {
                // Add check-accuracy:Tempo tag for manual review
                // Tag will be added via UserTag below
                Logger.LogWarning(
                    $"Flagged {song.Title} for meter review: {validation.MeterFlagReason}");
            }
        }
        else
        {
            // Multiple dances or no dances - use tempo as-is without validation
            edit.Tempo = tempo;
        }
    }

    // Rest of method unchanged...
    if (track.Danceability != null)
    {
        edit.Danceability = track.Danceability;
    }

    if (track.Energy != null)
    {
        edit.Energy = track.Energy;
    }

    if (track.Valence != null)
    {
        edit.Valence = track.Valence;
    }

    var tags = edit.GetUserTags(user.UserName);
    var meter = track.Meter;
    if (meter != null)
    {
        tags = tags.Add($"{meter}:Tempo");
    }

    if (!await dms.SongIndex.EditSong(
        user, song, edit,
        [
            new UserTag { Id = string.Empty, Tags = tags }
        ]))
    {
        return false;
    }

    return true;
}
```

**Extension Method on DanceObject** (add to DanceLibrary):

```csharp
// New file: DanceLib/DanceValidationExtensions.cs
namespace DanceLibrary;

public static class DanceValidationExtensions
{
    public static TempoValidationResult ValidateTempo(
        this DanceObject dance,
        decimal tempo,
        string? meter)
    {
        var result = new TempoValidationResult();

        // Get validation rules from dance instance
        var instance = dance.GetPrimaryInstance(); // or specific style instance
        if (instance?.Validation == null)
        {
            return result; // No validation rules for this dance
        }

        var validation = instance.Validation;

        // Check tempo corrections
        if (validation.DoubleTempoIfBelow.HasValue &&
            tempo < validation.DoubleTempoIfBelow.Value)
        {
            result.RequiresCorrection = true;
            result.CorrectedTempo = tempo * 2;
            result.CorrectionReason =
                $"Tempo {tempo} BPM below {validation.DoubleTempoIfBelow} threshold for {dance.Name} - doubled to {result.CorrectedTempo}";
        }
        else if (validation.HalveTempoIfAbove.HasValue &&
            tempo > validation.HalveTempoIfAbove.Value)
        {
            result.RequiresCorrection = true;
            result.CorrectedTempo = tempo / 2;
            result.CorrectionReason =
                $"Tempo {tempo} BPM above {validation.HalveTempoIfAbove} threshold for {dance.Name} - halved to {result.CorrectedTempo}";
        }

        // Check meter validation
        if (!string.IsNullOrEmpty(meter) &&
            validation.FlagInvalidMeters?.Contains(meter) == true)
        {
            result.RequiresMeterFlag = true;
            result.MeterFlagReason =
                $"Invalid meter {meter} for {dance.Name} (expected {dance.Meter})";
        }

        return result;
    }
}

public class TempoValidationResult
{
    public bool RequiresCorrection { get; set; }
    public decimal? CorrectedTempo { get; set; }
    public string? CorrectionReason { get; set; }

    public bool RequiresMeterFlag { get; set; }
    public string? MeterFlagReason { get; set; }
}
```

### Phase 4: Tempo-Bot User

**Existing Implementation**: The tempo-bot user already exists and is used in `BatchCorrectTempo` method:

```csharp
// From SongController.cs
var applicationUser = user == null
    ? new ApplicationUser("tempo-bot", true)
    : await Database.FindOrAddUser(user);
```

**Usage**: When appending corrected tempo or meter flags, attribute them to tempo-bot user.

### Phase 5: Backward Compatibility

Use existing `BatchCorrectTempo` method to retroactively correct songs:

- Manually run batch correction on existing Salsa songs with tempo < 120 BPM
- Review flagged songs and manually fix meter issues
- No automatic retroactive processing initially

## Validation Rules by Dance Type

### Salsa (INITIAL IMPLEMENTATION)

- **Double if below**: 120 BPM (typical range 160-220)
- **Halve if above**: 250 BPM
- **Flag invalid meters**: 3/4, 6/8 (should be 4/4)

### Future Dances (After Salsa Validation)

**Note**: These are examples for future implementation. Start with Salsa only.

#### Waltz (Slow Waltz, Viennese Waltz)

- **Flag invalid meters**: 4/4, 2/4 (should be 3/4)
- **Double if below**: 60 BPM for Slow Waltz
- **Halve if above**: 100 BPM for Slow Waltz (typical 84-90)

#### East Coast Swing

- **Double if below**: 100 BPM (typical 126-148)
- **Halve if above**: 200 BPM
- **Flag invalid meters**: 3/4 (should be 4/4)

#### Cha Cha

- **Double if below**: 80 BPM (typical 100-128)
- **Halve if above**: 180 BPM
- **Flag invalid meters**: 3/4, 2/4 (should be 4/4)

## Testing Strategy

1. **Unit Tests**: Test validation logic with known edge cases
2. **Integration Tests**: Import playlists with known problem songs
3. **Manual Verification**: Review tempo-bot corrections on staging environment

### Test Cases

```csharp
[Theory]
[InlineData("SLS", 80.0, 160.0)]  // Salsa: double from 80 to 160
[InlineData("SLS", 100.0, 200.0)] // Salsa: double from 100 to 200
[InlineData("SLS", 280.0, 140.0)] // Salsa: halve from 280 to 140
public void ValidateSong_AppliesTempoCorrection(
    string danceId,
    decimal originalTempo,
    decimal expectedTempo)
{
    var song = new Song { Tempo = originalTempo };
    var dance = DanceLibrary.DanceFromId(danceId);

    var result = _validationService.ValidateSong(song, dance.PrimaryInstance);

    Assert.Single(result.Corrections);
    Assert.Equal(expectedTempo, result.Corrections[0].CorrectedTempo);
}
```

## Clarifying Questions - ANSWERED

### 1. Scope and Prioritization ?

**A**: Start with Salsa only. It's the most problematic. Extend to other dances after validating the approach.

### 2. Correction Behavior ?

**A**: When adding tempo, append properties to SongProperties including user (tempo-bot), timestamp, and new tempo (or tag). Only applies when creating new songs. Existing logic will handle adding to existing songs.

### 3. Threshold Configuration ?

**A**: Use JSON for configuration. No organization-level distinction initially, but may add styles later (not this pass).

### 4. Manual Review Workflow ?

**A**: Just add the tag for meter issues. Tempo-bot corrections are auto-approved. Keep correction range conservative to maintain confidence. No additional confidence score system needed.

### 5. Meter Validation ?

**A**: No automatic meter correction. Flag for manual review to understand legitimate edge cases before attempting automation.

### 6. Spotify Data Quality

**Q**: Documentation on Spotify's confidence scores?
**A**: Need to research Spotify Audio Analysis API documentation for confidence metrics.

**Spotify Audio Analysis API**: https://developer.spotify.com/documentation/web-api/reference/get-audio-analysis

The API returns:

- `tempo` - Overall estimated tempo in BPM
- `tempo_confidence` - Confidence of tempo estimate (0.0 to 1.0)
- `time_signature` - Estimated time signature (3, 4, 5, 6, 7)
- `time_signature_confidence` - Confidence of time signature estimate (0.0 to 1.0)

**Current Status**: Need to investigate whether we're already storing confidence scores and if we should use them for validation thresholds.

### 7. User Attribution ?

**A**: tempo-bot already exists as a pseudo-user. See `SongController.cs` `BatchCorrectTempo` method:

```csharp
var applicationUser = user == null
    ? new ApplicationUser("tempo-bot", true)
    : await Database.FindOrAddUser(user);
```

### 8. Backward Compatibility ?

**A**: Will manage manually using existing `BatchCorrectTempo` method for retroactive corrections on existing songs.

### 9. Edge Cases ?

**A**: **CRITICAL DECISION**: Run validation when adding tempo from Spotify/EchoNest, but **ONLY if this is the first dance being added to the song**. This avoids issues with:

- Songs that work for multiple dances at different tempos
- Songs that already have established tempo/dance data
- Simpler integration point than batch upload/playlist creation

Long term, multiple-tempo-per-dance may be solved by keeping tempos per dance, but not today.

### 10. Notifications and Monitoring ?

**A**: No additional complexity needed. Users already see that tempo was algorithmically generated. That's sufficient for now.

## Next Steps

1. ? **DONE**: Document approach and answer clarifying questions
2. ? **DONE**: Add validation rules to `dances.json` for Salsa only
3. ? **DONE**: Update DanceLibrary models to support `validation` property (DanceInstance, DanceValidation)
4. ? **DONE**: Create `DanceValidationExtensions.cs` with `ValidateTempo()` extension method
5. ? **DONE**: Create `ValidateAndCorrectTempo()` in `MusicServiceManager.cs` and call from `UpdateAudioData()`
6. **Test with real Salsa imports** from Spotify playlists
7. **Manual review** - Search for `check-accuracy:Tempo` tag and review flagged songs
8. **Monitor and refine** - Adjust thresholds based on false positives/negatives
9. **Extend to other dances** after Salsa proves successful

## Implementation Summary

### Files Modified:
- ? `m4d/ClientApp/src/assets/content/dances.json` - Added validation rules for Salsa Social style
- ? `DanceLib/DanceValidation.cs` - New class for validation rules
- ? `DanceLib/DanceInstance.cs` - Added `Validation` property
- ? `DanceLib/DanceValidationExtensions.cs` - New extension method `ValidateTempo()` on `DanceObject`
- ? `m4d/Utilities/MusicServiceManager.cs` - Added `ValidateAndCorrectTempo()` method, called from `UpdateAudioData()`

### How It Works:
1. When `GetEchoData()` retrieves tempo from Spotify/EchoNest, it commits the change as the Spotify pseudo-user
2. `UpdateAudioData()` then calls `ValidateAndCorrectTempo()` if tempo was added
3. `ValidateAndCorrectTempo()` checks if song has exactly 1 dance (first dance scenario)
4. If validation rules exist for that dance, it validates the tempo and meter
5. If correction needed, creates a new edit as tempo-bot user and commits the corrected tempo
6. If meter is suspicious, adds `check-accuracy:Tempo` tag for manual review
7. All changes are logged with detailed reasons

## Outstanding Research Questions

1. ? **RESOLVED**: How do we access song's dances? - Use `song.GetUserTags("").Filter("Dance")` and `TagsToDances()`
2. ? **RESOLVED**: How to append properties? - Use `SongIndex.EditSong()` with tempo-bot user
3. ? **RESOLVED**: How to get DanceObject? - Use `Dances.Instance.FromNames()` from TagList

## Important Notes

- ? **No database changes required** - Tempo-bot user already exists, SongProperties already stores history
- ? **No admin UI needed initially** - Manual search for `check-accuracy:Tempo` tag is sufficient
- ? **Existing tagging system** handles new tags without migration
- ? **Extension method approach** keeps validation logic clean and reusable

## Integration Point Research Needed

**FOUND**: The primary integration point is in `MusicServiceManager.GetEchoData()` method (around line 713 in `m4d/Utilities/MusicServiceManager.cs`).

This method:

1. Looks up tempo data from Spotify/EchoNest using `LookupEchoTrack()`
2. Sets `edit.Tempo = track.BeatsPerMinute` (line ~737)
3. Adds meter as a tag: `tags = tags.Add($"{meter}:Tempo")` (line ~757)
4. Calls `EditSong()` to persist changes

**Implementation Strategy**:

Add validation logic in `GetEchoData()` right after retrieving the tempo but before setting it:

```csharp
if (track.BeatsPerMinute != null)
{
    // Check if this is the first dance being added to the song
    var currentDances = song.GetDances(); // Need to find this method

    if (currentDances.Count == 0 || !song.HasTempo()) // First dance scenario
    {
        // Run validation
        var validation = ValidateTempo(track.BeatsPerMinute.Value, meter, danceId);

        if (validation.RequiresCorrection)
        {
            edit.Tempo = validation.CorrectedTempo;
            // Add correction metadata as properties
            // TODO: Append tempo correction properties here
        }
        else
        {
            edit.Tempo = track.BeatsPerMinute;
        }

        if (validation.RequiresMeterFlag)
        {
            tags = tags.Add("check-accuracy:Tempo");
            // TODO: Append meter flag properties here
        }
    }
    else
    {
        // Multiple dances - use tempo as-is
        edit.Tempo = track.BeatsPerMinute;
    }
}
```

**Questions to answer**:

1. ? **FOUND**: Tempo is added in `MusicServiceManager.GetEchoData()`
2. ?? How do we check if a song already has dances? (need to find `song.GetDances()` or equivalent)
3. ?? How do we get the danceId in this context? (may need to be passed in)
4. ? User attribution: `service.ApplicationUser` is already available, but we need tempo-bot
5. ?? How to append properties with tempo-bot user? (need to research Song/SongProperty APIs)

## Files to Modify

### Phase 1: Data Model

- ? `architecture/tempo-validation-rules.md` - This document
- ?? `m4d/ClientApp/src/assets/content/dances.json` - Add validation rules for Salsa
- ?? `DanceLib/` - Update DanceObject/DanceInstance models to include validation
- ?? `m4dModels/Dance.cs` - May need updates depending on DanceLibrary structure

### Phase 2: Validation Logic

- ?? New: `DanceLib/TempoValidator.cs` or add to existing service
- ?? Integration point (TBD - need to find where tempo is added from Spotify/EchoNest)

### Phase 3: Testing

- ?? Unit tests for validation logic
- ?? Integration tests with real Salsa tracks

### Legend

- ? Complete
- ?? To Do
- ?? Research Needed

## Summary

This architecture document defines an approach to validate tempo and meter data from Spotify/EchoNest when adding it to songs. The key innovation is running validation **only when adding the first dance** to a song, which avoids complications with songs that work for multiple dances at different tempos.

### Key Decisions

1. **Validation Trigger**: When tempo is added from external sources (Spotify/EchoNest) AND it's the first dance being added
2. **Correction Method**: Append properties to SongProperties (not creating new song records)
3. **User Attribution**: Use existing tempo-bot pseudo-user
4. **Initial Scope**: Salsa only, extend later
5. **Meter Handling**: Flag only, no auto-correction
6. **Configuration**: JSON-based validation rules in `dances.json`
7. **Backward Compatibility**: Manual correction using existing `BatchCorrectTempo`

### Implementation Priority

**Phase 1** (Current): Document and plan
**Phase 2** (Next): Update data models and add Salsa validation rules
**Phase 3**: Find integration point and implement validation logic
**Phase 4**: Test with real imports and monitor results
**Phase 5**: Extend to other problematic dances

---

**Document Status**: Draft - Answers Provided, Ready for Implementation
**Last Updated**: 2024
**Next Action**: Research integration points in codebase
