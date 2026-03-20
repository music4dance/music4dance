# Simple Merge Implementation Summary

## Overview

Implemented server-side "simple merge" that concatenates song histories, annotates each action with the original song GUID, and sorts by timestamp. This approach preserves all original information, enabling potential unmerging in the future.

## Implementation

### New Method: `SongIndex.SimpleMergeSongs()`

**Location**: `m4dModels\SongIndex.cs`

**Algorithm**:
1. **Load full songs** - Ensures all SongProperties are available
2. **Group by edit blocks** - Properties between `.Create`/`.Edit` and `.User=` are kept together
3. **Annotate commands** - Replaces empty `.Create=` and `.Edit=` with `.Create={songGUID}` and `.Edit={songGUID}`
4. **Sort by timestamp** - Orders all edit blocks chronologically
5. **Add merge metadata** - Appends `.Merge=` command with source song GUIDs
6. **Create new song** - Generates new GUID, creates song from merged properties
7. **Delete source songs** - Removes original songs from database

### Updated AutoMerge

**Location**: `m4d\Controllers\SongController.cs`

**Before** (smart merge):
```csharp
var song = await SongIndex.MergeSongs(
    user, songs,
    ResolveStringField(Song.TitleField, songs),
    ResolveStringField(Song.ArtistField, songs),
    ResolveDecimalField(Song.TempoField, songs),
    ResolveIntField(Song.LengthField, songs),
    Song.BuildAlbumInfo(songs)
);
```

**After** (simple merge):
```csharp
var song = await SongIndex.SimpleMergeSongs(user, songs);
```

## Benefits

### 1. Preserves All Information
- Every edit from every source song is retained
- User attributions preserved with original song context
- Timeline of changes across all songs maintained

### 2. Enables Unmerging
- Original song GUIDs annotated on every command
- Can reconstruct source songs by filtering properties by GUID
- Complete audit trail of merge operations

### 3. Simpler Logic
- No complex conflict resolution
- No "smart" field selection
- Straightforward concatenation + sort

### 4. More Verbose but Accurate
- Larger merged songs (more properties)
- But contains complete, accurate history
- No information loss

## Test Coverage

**New Test File**: `m4dModels.Tests\MergeTests.cs`

**5 Tests** (all passing ✅):

1. **SimpleMerge_AnnotatesCreatAndEditCommands**
   - Verifies `.Create=` and `.Edit=` commands are annotated with song GUIDs
   - Checks `.Merge=` command references both source songs

2. **SimpleMerge_SortsByTimestamp**
   - Creates songs in random order (Jan 5, Jan 1, Jan 3)
   - Verifies merged song has edit blocks in chronological order

3. **SimpleMerge_PreservesAllProperties**
   - Merges songs with different tags, dance ratings, albums, samples
   - Confirms all properties from both songs are present in merged result

4. **SimpleMerge_DeletesSourceSongs**
   - Verifies source songs are deleted after merge
   - Confirms merged song has new GUID

5. **SimpleMerge_MultipleEditsFromSameSong**
   - Tests song with multiple `.Edit=` commands
   - Ensures all edits are annotated with same source GUID

## Example Merge

**Song 1**:
```
.Create=    User=dwgray  Time=01/01/2020 10:00:00 AM    Title=Song    Artist=Artist    Tempo=120.0    Tag+=Salsa:Dance
```

**Song 2**:
```
.Create=    User=user2   Time=01/02/2020 11:00:00 AM    Title=Song    Artist=Artist    Tempo=125.0    Tag+=Bachata:Dance
.Edit=      User=user2   Time=01/03/2020 12:00:00 PM    Tempo=130.0
```

**Merged Song**:
```
.Create={song1-guid}    User=dwgray  Time=01/01/2020 10:00:00 AM    Title=Song    Artist=Artist    Tempo=120.0    Tag+=Salsa:Dance
.Create={song2-guid}    User=user2   Time=01/02/2020 11:00:00 AM    Title=Song    Artist=Artist    Tempo=125.0    Tag+=Bachata:Dance
.Edit={song2-guid}      User=user2   Time=01/03/2020 12:00:00 PM    Tempo=130.0
.Merge={song1-guid};{song2-guid}    User=dwgray  Time=01/04/2020 01:00:00 PM
```

## Potential Future Work

### Unmerge Functionality
Could implement:
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

## Comparison: Smart vs Simple Merge

| Aspect | Smart Merge | Simple Merge |
|--------|-------------|--------------|
| **Properties** | Merged/deduplicated | All preserved |
| **Conflicts** | Manual resolution | All versions kept |
| **Size** | Smaller | Larger (all properties) |
| **Accuracy** | May lose data | 100% accurate |
| **Unmerge** | Difficult/impossible | Straightforward |
| **Complexity** | High (conflict logic) | Low (concat + sort) |
| **Use Case** | Manual admin merge | Auto-merge batches |

## Documentation Updates

**Updated**:
- `architecture/song-merge-algorithm.md` - Added Simple Merge section
- **Note**: Smart merge (`MergeSongs`) still exists for manual admin merges through UI

## Migration Notes

**AutoMerge** now uses simple merge (default behavior changed).
**Manual merge** (admin UI) still uses smart merge (MergeSongs) - unchanged.

If you want to revert AutoMerge to smart merge, change `SongController.AutoMerge` to call `MergeSongs` instead of `SimpleMergeSongs`.
