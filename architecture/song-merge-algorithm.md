# Song Merge Algorithm

## Overview

The music4dance merge system identifies and consolidates duplicate songs in the catalog. Since songs come from multiple sources (user imports, music services like Spotify/iTunes, web scraping), duplicates are inevitable. The merge algorithm uses a multi-level matching strategy combined with conflict resolution to create a single, comprehensive song record.

## Merge Levels

The system uses **four** merge levels defined in `MergeCluster.cs`. **The levels are NOT numbered sequentially by strictness** - this is legacy behavior.

**Actual Strictness Order**: Level 2 (loosest) → Level 1 (medium) → Level 3 (medium-strict) → Level 0 (strictest)

### Level 2: Title Only (LOOSEST)
- **Title Hash Match**: Normalized titles must match exactly
- **Artist**: Completely ignored
- **All other fields**: Ignored
- **Implementation**: Groups by title hash only, returns all songs in clusters with 2+ matches

**Use Case**: Find ALL songs with similar titles regardless of artist (covers, different performers)

### Level 1: Title + Artist (MEDIUM - DEFAULT)
- **Title+Artist Hash Match**: Combined hash of title+artist must match
- **Artist Hash Match**: Then groups by artist hash within cluster
- **Tempo**: May differ
- **Length**: May differ
- **Implementation**: Two-level grouping: title+artist hash, then artist hash

**Use Case**: Most common merge - same song by same artist with different tempo/length metadata

### Level 3: Title + Artist + Length Filter (MEDIUM-STRICT)
- **Same as Level 1** but adds length validation
- **Length Filter**: Songs must be within ±20 seconds of the cluster average length
- **Implementation**: Level 1 logic + `FilterLength()` method

**Use Case**: Level 1 with length check to avoid merging live vs studio, extended vs radio edit

### Level 0: Full Equivalence (STRICTEST)
- **Title+Artist Hash Match**: Combined hash must match
- **Song.Equivalent() Required**: ALL fields must match or be empty:
  - Artist must match (or be empty)
  - Tempo must match (or be empty)
  - Length must match (or be empty)
- **Implementation**: Groups by title+artist hash, then applies `Song.Equivalent()` filter

**Use Case**: Highest confidence - perfect duplicates with identical metadata

**Implementation Note**: The non-sequential numbering (2, 1, 3, 0) is legacy behavior. Consider renumbering to 0→3 for clarity.

## Title Normalization & Hashing

Song matching relies heavily on normalized title comparison via hashing. The normalization process:

1. **Unicode Normalization**: Decompose accented characters (FormD)
2. **Case Normalization**: Convert to uppercase
3. **Word Filtering**: Remove common words ("A", "THE", "AND", "OF", etc.)
4. **Character Filtering**: Keep only letters and digits
5. **Dance Remix Detection**: Preserve parenthetical content for dance-specific remixes

### Dance-Specific Version Detection

To prevent incorrect merges of dance-specific versions with originals, the system detects two patterns in parenthetical content:

#### 1. Numeric BPM Markers

Pattern: `\d+\s*BPM`

**Examples that are preserved:**

- "Summer Nights (128 BPM)" ≠ "Summer Nights"
- "Despacito (130BPM)" ≠ "Despacito"
- "La Bamba (120 BPM)" ≠ "La Bamba"

**Example that is ignored:**

- "Summer Nights (just BPM)" = "Summer Nights" (no number, treated as normal parenthetical)

#### 2. Dance Names (Alone)

**Dance Names Source**: Dynamically loaded from `Dances.Instance.GetAllDanceWordsUpper()`

- Includes full dance names: "SALSA", "WALTZ", "CHA CHA"
- Includes synonyms: dance-specific alternate names
- Includes word fragments: "WEST" from "West Coast Swing"
- Cached for performance during merge operations

**Examples that are preserved:**

- "Let's Dance (Salsa)" ≠ "Let's Dance"
- "Me Vuelvo un Cobarde (Bachata)" ≠ "Me Vuelvo un Cobarde"
- "Time to Say Goodbye (Waltz)" ≠ "Time to Say Goodbye"
- "Despacito (Bachata Version)" ≠ "Despacito" (dance name present, regardless of "Version")

**Examples that are ignored (no dance names):**

- "Satisfaction (I Can't Get No)" = "Satisfaction"
- "Hotel California (2013 Remaster)" = "Hotel California"

**Future Enhancement**: We may explore adding remix keywords ("REMIX", "VERSION", "MIX", "EDIT") as an additional filter in the future, requiring both a dance name AND a remix keyword for more precise detection. Currently, dance name alone is sufficient to preserve the distinction.

### Artist Normalization

Artist matching uses a breakdown approach:

1. Split artist name into words
2. Remove common words ("BAND", "FEAT", "FEATURING", "ORCHESTRA", etc.)
3. Match if:
   - Single-word artist appears in multi-word artist, OR
   - Two or more words overlap between artists

**Examples:**

- "Elvis Presley" matches "Elvis"
- "Glenn Miller Orchestra" matches "Glenn Miller"
- "David Guetta feat. Sia" matches "David Guetta"

## Merge Workflow

### 1. Finding Merge Candidates

```csharp
Database.FindMergeCandidates(count, level)
```

**Algorithm:**

1. Load songs from database
2. Group by `TitleHash` (normalized title)
3. Within each group, apply level-specific equivalence checks
4. Return clusters of 2+ matching songs

**Optimization**: Candidate clusters are cached to avoid repeated expensive queries

### 2. Conflict Resolution

When merging multiple songs, conflicts are resolved using `ResolveStringField`, `ResolveIntField`, `ResolveDecimalField`:

**Resolution Strategy:**

1. If form data provided (manual merge): Use user's selection
2. Otherwise (auto-merge): Use first non-null value

**Fields Resolved:**

- `Title` (string)
- `Artist` (string)
- `Tempo` (decimal?)
- `Length` (int?)

### 3. Album Merging

Albums are merged using `AlbumDetails.BuildAlbumInfo()`:

1. Collect all albums from all songs being merged
2. Deduplicate by album name (normalized comparison)
3. Merge purchase IDs (Spotify, iTunes, Amazon) across matching albums
4. Preserve track numbers and publisher information

### 4. Property Concatenation

**User History**: All edit histories from all source songs are preserved

- Each user's contributions remain attributed
- Timestamps preserved chronologically
- Vote history (like/dislike) maintained per user

**Tags**: Combined from all sources

- Song-level tags merged
- Dance-specific tags preserved per dance rating
- Duplicate tags removed

**Dance Ratings**: Summed across songs

- If Song A has "CHA+1" and Song B has "CHA+2", merged song has "CHA+3"
- Supports both positive (like) and negative (dislike) ratings

**Comments**: All comments preserved with attributions

## Merge Execution Modes

### Manual Merge (Admin UI)

**Access**: `GET /Song/BulkEdit` → Select "Merge" action

**Process:**

1. Admin selects songs to merge from search results
2. System presents merge form showing conflicts (title, artist, tempo, length)
3. Admin resolves conflicts by selecting preferred values
4. System executes merge with selected resolutions
5. `SongIndex.MergeSongs()` creates final merged song

**Result**: Precise control over merged song properties

### Auto Merge (Batch Processing)

**Access**: `GET /Song/MergeCandidates?autoCommit=true&level=1`

**Process:**

1. System finds all merge candidates at specified level
2. Groups candidates by title hash
3. For each cluster:
   - Reloads full songs (light loading → full loading)
   - Auto-resolves conflicts (first non-null value)
   - Calls `SongIndex.MergeSongs()` with resolved values
   - Removes merged songs from candidates cache
4. Returns list of merged songs

**Result**: Batch processing of high-confidence duplicates

### Simple Merge (Development/API)

**Access**: `SongMerge` method (Vue3 UI)

**Process:**

1. Concatenates all properties from all songs
2. Minimal conflict resolution
3. Primarily used for presenting merge preview in UI

**Note**: This is NOT used by AutoMerge - it's a display/preview mechanism

## Auto Merge Answer (Question 3)

**AutoMerge uses the "smart" merge technique via `SongIndex.MergeSongs()`:**

```csharp
private async Task<Song> AutoMerge(List<Song> songs, ApplicationUser user)
{
    // Reload full songs (candidates are light-loaded)
    songs = [.. (await SongIndex.FindSongs(songs.Select(s => s.SongId)))];

    // Call MergeSongs with auto-resolved fields
    var song = await SongIndex.MergeSongs(
        user, songs,
        ResolveStringField(Song.TitleField, songs),      // Smart resolution
        ResolveStringField(Song.ArtistField, songs),     // Smart resolution
        ResolveDecimalField(Song.TempoField, songs),     // Smart resolution
        ResolveIntField(Song.LengthField, songs),        // Smart resolution
        Song.BuildAlbumInfo(songs)                        // Album merging
    );

    Database.RemoveMergeCandidates(songs);
    return song;
}
```

**What `MergeSongs` does:**

1. Creates new merged song with resolved fields
2. Merges all user histories (preserves every edit)
3. Combines all tags (song-level and dance-level)
4. Sums dance ratings across all songs
5. Merges album information
6. Records merge in song history
7. Deletes source songs from index
8. Returns new consolidated song

**Simple Merge (`SongMerge`) is only used for:**

- Displaying merge preview in Vue3 UI
- Admin review interface
- NOT used in auto-merge batch processing

## Database Integration

### SongIndex Operations

**`MergeSongs()`**: Core merge implementation

- Transaction-safe merge operation
- Updates search index
- Records merge provenance in `.Merge` command

**`SaveSong()`**: Persists merged song

- Updates database
- Updates Azure Search index
- Invalidates dance stats cache

### Search Index Implications

After merge:

- Source songs removed from search
- Merged song added with combined content
- Search relevance increased (more user history)
- Tags from all sources searchable

## Performance Considerations

### Caching

**Merge Candidates Cache**: `Database.MergeCandidates`

- Stores pre-computed candidate clusters
- Cleared manually via `/Song/ClearMergeCache`
- Cleared automatically after merge operations

**Dance Names Cache**: `Dances.GetAllDanceWordsUpper()`

- Computed once per Dances instance load
- Contains all names, synonyms, and word fragments
- Critical for dance remix detection performance

### Batch Processing

**Recommended Practice:**

1. Run Level 1 auto-merge first (highest volume, high confidence)
2. Review Level 0 candidates manually (identical metadata)
3. Review Level 3 candidates carefully (title/artist only)

**Typical Volumes:**

- Level 1: 50-500 candidates (auto-merge safe)
- Level 0: 10-50 candidates (very high confidence)
- Level 3: 100-1000 candidates (requires review)

## Edge Cases & Special Handling

### Failed Merges

If merge fails:

1. Transaction rolled back
2. Original songs preserved
3. Error logged
4. Candidate remains in cache

### Tempo Conflicts

Songs with significantly different tempos (>10 BPM) may indicate:

- Different recordings (live vs studio)
- Different versions (extended mix)
- **Dance remixes** (now detected via BPM markers)

**Resolution**: Level 1 allows tempo differences; review Level 0 failures manually

### Artist Variations

Common variations handled automatically:

- "Elvis Presley" = "Elvis"
- "Glenn Miller Orchestra" = "Glenn Miller & His Orchestra"
- "David Guetta feat. Sia" = "David Guetta"

**Not Handled**: Completely different artists (e.g., "Various Artists" vs specific artist)

### Dance-Specific Version Prevention

**Before Dance-Specific Detection:**

- "Despacito" merged with "Despacito (Salsa)" ❌
- "Me Vuelvo un Cobarde" merged with "Me Vuelvo un Cobarde (Bachata)" ❌
- Lost dance-specific information
- Confused users searching for specific dance styles

**After Dance-Specific Detection:**

- "Despacito" kept separate from "Despacito (Salsa)" ✅
- "Despacito (Salsa)" kept separate from "Despacito (Bachata)" ✅
- Each BPM version (128 BPM, 130 BPM, etc.) remains distinct
- Dance styles properly attributed

## Testing & Validation

### Test Coverage

**Unit Tests**: `m4dModels.Tests/SongTests.cs`

- `DanceRemixDetection`: Verifies dance names (with or without keywords) prevent merging
- `BPMDetectionRequiresNumericPrefix`: Ensures "128 BPM" detected, "just BPM" ignored
- `AccentedDanceNamesDetected`: Confirms "Bachata", "Sálsa", etc. properly detected
- `NonDanceParenthesesStillIgnored`: Confirms "(Remaster)" still ignored

**Integration Tests**: Merge workflow tests

- Multi-song merge with conflict resolution
- Album merging with purchase ID consolidation
- User history preservation

### Validation Commands

**Check Merge Candidates:**

```
GET /Song/MergeCandidates?level=1&page=1
```

**Test Auto-Merge (dry run):**

```
GET /Song/MergeCandidates?level=1&page=1
(Review candidates without autoCommit)
```

**Execute Auto-Merge:**

```
GET /Song/MergeCandidates?level=1&autoCommit=true
```

## Future Improvements

### Potential Enhancements

1. **Machine Learning**: Train model on historical merge decisions
2. **Confidence Scoring**: Assign merge confidence percentages
3. **User Feedback**: Allow users to flag bad merges
4. **Acoustic Fingerprinting**: Use audio analysis for matching
5. **ISRC Matching**: Use International Standard Recording Code when available

### Known Limitations

1. **Live vs Studio**: May merge different recordings of same song
2. **Cover Versions**: Different artists performing same song might merge incorrectly
3. **Language Variations**: "Bésame Mucho" vs "Besame Mucho" normalized identically
4. **Special Characters**: Some unicode characters may normalize unexpectedly

## Related Documentation

- **Testing Patterns**: `architecture/testing-patterns.md`
- **Dance Library**: `DanceLib/Dances.cs`
- **Song Model**: `m4dModels/Song.cs` (especially `MungeString` method)
- **Merge Controller**: `m4d/Controllers/SongController.cs` (`MergeCandidates`, `AutoMerge`, `MergeResults`)
