# Song Merge Algorithm

## Overview

The music4dance merge system identifies and consolidates duplicate songs in the catalog. Since songs come from multiple sources (user imports, music services like Spotify/iTunes, web scraping), duplicates are inevitable. The merge algorithm uses a multi-level matching strategy combined with conflict resolution to create a single, comprehensive song record.

## Merge Levels

The system uses **four** merge levels implemented in `MergeManager.cs`. **The levels are NOT numbered sequentially by strictness** - this is legacy behavior.

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
- **Length Filter**: Songs must be within ±20 seconds of the cluster **median** length
- **Implementation**: Level 1 logic + `FilterLength()` method

**FilterLength Algorithm** (median-based, robust to outliers):

1. Collect lengths of all songs that have a length value
2. Sort lengths and compute the **median** (middle value for odd count; average of two middle values for even count)
3. Keep songs whose length is within 20 seconds of the median, plus all songs with no length data

**Why median, not mean**: A single outlier (e.g. a 5-minute DJ remix in a cluster of 3-minute songs) skews the mean so far that all songs may fall outside the ±20s window. The median is unaffected by outlier magnitude and anchors to the majority cluster.

**Example**:

```
Cluster: [180s, 195s, 300s]
Mean  = 225s  → |180-225|=45 > 20 → all filtered out ❌
Median= 195s  → |180-195|=15 ✓, |195-195|=0 ✓, |300-195|=105 → removes 300s ✅
```

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

#### 3. Exclusion Words

**Exclusion Words**: Parenthetical content indicating different versions that should remain separate

**Words List** (in `Song.cs`):

- "INSTRUMENTAL", "VOCAL", "VOCALS"
- "A CAPPELLA", "ACAPPELLA", "KARAOKE"
- "ACOUSTIC", "LIVE", "UNPLUGGED"
- "RADIO EDIT", "EXTENDED", "ORCHESTRAL", "REPRISE", "REMIX"

**Examples that are preserved:**

- "Wonderful Tonight (Instrumental)" ≠ "Wonderful Tonight"
- "Hotel California (Live)" ≠ "Hotel California"
- "Bohemian Rhapsody (Acoustic)" ≠ "Bohemian Rhapsody"
- "Paradise (Extended Version)" ≠ "Paradise"
- "Despacito (Remix)" ≠ "Despacito"

**Rationale**: These words indicate significantly different musical arrangements or performance contexts that should be preserved as distinct songs.

**Future Enhancement**: This list can be expanded based on observed false merges in production data.

### Manual Merge Prevention (.NoMerge Sentinel)

**Status**: ✅ Implemented

Songs can be explicitly excluded from merge candidates by adding a `.NoMerge` command to their history:

**Format**:

```
.NoMerge=    User={admin}    Time=MM/dd/yyyy hh:mm:ss tt
```

**Usage**:

1. Admin identifies songs that should never be merged (e.g., live vs studio versions with same title)
2. Adds `.NoMerge` command via `BatchAdminEdit` or direct song editing
3. Song is automatically excluded during AutoMerge execution

**Implementation**: `.NoMerge` check occurs in `MergeManager.AutoMergeSingleCluster()` after full song reload, where SongProperties are available. This design is optimal because:

- **Efficient Light-Song Clustering**: Initial candidate selection uses light-loaded songs (Title, Artist, Tempo, Length) for fast clustering via `MergeManager.GetMergeCandidates()`
- **Equivalence Checks**: Methods like `Equivalent()`, `WeakEquivalent()`, and `TitleArtistEquivalent()` only need light-song fields, so they remain fast
- **Strategic Full Reload**: Full songs (with SongProperties) are only loaded once clusters are identified, minimizing expensive database I/O
- **.NoMerge Filtering**: Check happens after full reload but before `SimpleMergeSongs()` execution, preventing merges of marked songs

**Code Flow**:

```
1. MergeManager.GetMergeCandidates()
   → LoadLightSongsStreamingAsync() (Title, Artist, Tempo, Length only)

2. MergeManager.AutoMerge(IReadOnlyCollection<Song>, int, ApplicationUser)
   → Equivalence checks using light songs (fast)
   → Groups into merge clusters

3. MergeManager.AutoMergeSingleCluster(List<Song>, ApplicationUser)
   → FindSongs() - Reload FULL songs with SongProperties
   → .NoMerge check HERE ← filters before merge
   → SimpleMergeSongs() - executes merge
```

**Example**:

```
.Create=    User=dwgray     Time=01/01/2020 10:00:00 AM    Title=Shape of You    Artist=Ed Sheeran
.Edit=      User=admin      Time=06/15/2023 02:00:00 PM    Tag+=Live:Other
.NoMerge=   User=admin      Time=06/15/2023 02:01:00 PM
```

This song will never be merged, even if another "Shape of You" by Ed Sheeran exists. The merge operation will be skipped with a log entry indicating .NoMerge filtering.

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

The system provides **two merge strategies**: Smart Merge and Simple Merge.

### Smart Merge (Manual Admin Operations)

**Implementation**: `SongIndex.MergeSongs()`
**Location**: `m4dModels\SongIndex.cs`

**Used For**:

- Manual admin merges via UI (`GET /Song/BulkEdit` → "Merge" action)
- Requires admin review and conflict resolution

**Process**:

1. Admin selects songs to merge from search results
2. System presents merge form showing conflicts (title, artist, tempo, length)
3. Admin resolves conflicts by selecting preferred values
4. Creates new merged song with resolved fields
5. Merges user histories (preserves every edit)
6. Combines tags (song-level and dance-level)
7. Sums dance ratings across all songs
8. Merges album information
9. Records `.Merge=` command in song history
10. Deletes source songs from index
11. Returns new consolidated song

**Conflict Resolution**:

- If form data provided: Uses admin's selection
- Otherwise: Uses first non-null value

**Result**: Precise control over merged song properties with smart field selection

### Simple Merge (Automatic Batch Operations)

**Implementation**: `SongIndex.SimpleMergeSongs()`
**Location**: `m4dModels\SongIndex.cs`

**Used For**:

- Auto-merge batch processing (`GET /Song/MergeCandidates?autoCommit=true`)
- Admin simple merge via UI (`POST /Song/BulkEdit` → "SimpleMerge" button)
- Preserves complete history for potential unmerging

**Admin UI Access**:

- Navigate to Admin → Initialization Tasks → "Merge Songs" section
- Enter two song GUIDs in the text boxes
- Click **"SimpleMerge"** button to execute automatic merge
- Click **"Preview"** button to see Vue3 merge preview (no actual merge)

**Algorithm**:

1. **Load full songs** - Ensures all SongProperties are available
2. **Group by edit blocks** - Properties between `.Create`/`.Edit` and `.User=` are kept together
3. **Annotate commands** - Replaces empty `.Create=` and `.Edit=` with `.Create={songGUID}` and `.Edit={songGUID}`
4. **Sort by timestamp** - Orders all edit blocks chronologically
5. **Add merge metadata** - Appends `.Merge=` command with source song GUIDs
6. **Create new song** - Generates new GUID, creates song from merged properties
7. **Delete source songs** - Removes original songs from database

**Benefits**:

- **100% information preservation** - Every edit from every source song retained
- **Unmerge capability** - Original song GUIDs allow reconstruction
- **Simpler logic** - No complex conflict resolution
- **Complete audit trail** - User attributions preserved with original song context

**Example Merge**:

**Song 1** (created 01/01/2020):

```
.Create=    User=dwgray  Time=01/01/2020 10:00:00 AM    Title=Song    Artist=Artist    Tempo=120.0    Tag+=Salsa:Dance
```

**Song 2** (created 01/02/2020, edited 01/03/2020):

```
.Create=    User=user2   Time=01/02/2020 11:00:00 AM    Title=Song    Artist=Artist    Tempo=125.0    Tag+=Bachata:Dance
.Edit=      User=user2   Time=01/03/2020 12:00:00 PM    Tempo=130.0
```

**Merged Song** (merged 01/04/2020):

```
.Create={song1-guid}    User=dwgray  Time=01/01/2020 10:00:00 AM    Title=Song    Artist=Artist    Tempo=120.0    Tag+=Salsa:Dance
.Create={song2-guid}    User=user2   Time=01/02/2020 11:00:00 AM    Title=Song    Artist=Artist    Tempo=125.0    Tag+=Bachata:Dance
.Edit={song2-guid}      User=user2   Time=01/03/2020 12:00:00 PM    Tempo=130.0
.Merge={song1-guid};{song2-guid}    User=dwgray  Time=01/04/2020 01:00:00 PM
```

**Result**: Verbose but accurate - contains complete history from all source songs

### Comparison: Smart vs Simple Merge

| Aspect              | Smart Merge            | Simple Merge                 |
| ------------------- | ---------------------- | ---------------------------- |
| **Properties**      | Merged/deduplicated    | All preserved (concatenated) |
| **Conflicts**       | Manual/auto resolution | All versions kept            |
| **Size**            | Smaller                | Larger (all properties)      |
| **Accuracy**        | May lose data          | 100% accurate                |
| **Unmerge**         | Difficult/impossible   | Straightforward              |
| **Complexity**      | High (conflict logic)  | Low (concat + sort)          |
| **GUID Annotation** | No                     | Yes (every command)          |
| **Use Case**        | Manual admin merge     | Auto-merge batches           |
| **Implementation**  | `MergeSongs()`         | `SimpleMergeSongs()`         |

### Auto Merge Behavior

**Current Implementation** (as of latest update):

```csharp
// Location: m4dModels\MergeManager.cs
private async Task<Song> AutoMergeSingleCluster(List<Song> songs, ApplicationUser user)
{
    // Reload full songs (candidates are light-loaded)
    songs = [.. (await _songIndex.FindSongs(songs.Select(s => s.SongId)))];

    // Filter out .NoMerge songs now that we have full properties
    songs = songs.Where(s =>
        !s.SongProperties.Any(p => p.Name == Song.NoMergeCommand)
    ).ToList();

    if (songs.Count < 2) return null;

    // Uses Simple Merge for complete history preservation
    return await _songIndex.SimpleMergeSongs(user, songs);
}
```

**Entry point**: `SongController` receives `GET /Song/MergeCandidates?autoCommit=true&level=1` and delegates to `MergeManager.AutoMerge()`.

**Process**:

1. System finds all merge candidates at specified level (2, 1, 3, or 0)
2. Groups candidates by title hash
3. For each cluster:
   - Reloads full songs (light loading → full loading)
   - Filters `.NoMerge` songs
   - Calls `SimpleMergeSongs()` to preserve complete history
   - Removes merged songs from candidates cache
4. Returns list of merged songs

**Result**: Batch processing of high-confidence duplicates with complete audit trail

### Future: Unmerge Functionality

With Simple Merge's GUID annotation, unmerging becomes feasible:

```csharp
public async Task<List<Song>> UnmergeSong(Guid mergedSongId)
{
    var merged = await FindSong(mergedSongId);
    var mergeCommand = merged.SongProperties.First(p => p.Name == Song.MergeCommand);
    var sourceIds = mergeCommand.Value.Split(';').Select(Guid.Parse).ToList();

    var reconstructedSongs = new List<Song>();
    foreach (var sourceId in sourceIds)
    {
        // Filter properties annotated with this GUID
        var props = merged.SongProperties
            .Where(p =>
                (p.Name == Song.CreateCommand || p.Name == Song.EditCommand) &&
                p.Value == sourceId.ToString())
            // Get properties until next command
            .SelectMany(GetPropertiesUntilNextCommand)
            .ToList();

        var song = await Song.Create(sourceId, props, DanceMusicService);
        reconstructedSongs.Add(song);
    }

    return reconstructedSongs;
}
```

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

**FilterLength Tests**: `m4dModels.Tests/FilterLengthTests.cs`

- `FilterLength_SingleOutlier_RemovesOutlier`: Median keeps main cluster, drops lone outlier
- `FilterLength_MultipleOutliers_KeepsMedianCluster`: Both high and low outliers removed
- `FilterLength_NoLengthSongs_AllKept`: Songs with null length always pass through
- `FilterLength_EvenCount_UsesAverageOfTwoMiddle`: Correct median for even-sized lists
- `FilterLength_BoundaryExactly20_Excluded`: Exactly 20s difference = excluded (strict `<`)
- Plus 6 additional edge-case tests

**MergeManager Tests**: `m4dModels.Tests/MergeManagerTests.cs`

- `GetMergeCandidates_Level1_GroupsByTitleAndArtist`
- `GetMergeCandidates_Level2_GroupsByTitleOnly`
- `GetMergeCandidates_Level0_RequiresFullEquivalence`
- `GetMergeCandidates_Level3_FiltersLengthDivergence`
- `GetMergeCandidates_EmptyArtist_IncludesAllSongsInCluster` (Level 2)
- `AutoMerge_MergesCluster_ReturnsMergedSong`
- Plus 8 additional tests for edge cases and .NoMerge filtering

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
- **Block Parser**: `m4dModels/SongPropertyBlockParser.cs` — shared infrastructure used by both `SimpleMergeSongs` (chronological sorting) and `ChunkedSong` (batch cleanup); keeps the block-layout contract (`[Action, User, Time, content…]`) in one place
