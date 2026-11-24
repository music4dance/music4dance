# Song Merge Algorithm

**Type:** Reference
**Status:** Current
**Last verified:** 2026-11-23 (§ Matching new songs by title and § Level 3 rewritten for the working-tree change, not yet merged; the snapshot audit is in the plan; § Matching new songs by title traced 2026-11-21; § Merging and § Unmerging written with the code 2026-11-14; § Settling the fields traced 2026-11-04; § Matching new songs by service id traced 2026-11-03; the rest: code references checked, behavior not re-traced)
**Code:** `m4dModels/MergeManager.cs`, `m4d/Controllers/SongController.cs` (merge actions, `Unmerge`), `m4dModels/SongIndex.cs` (`SimpleMergeSongs`, `RenumberAlbums`, `UnmergeSong`, `BuildUnmerge`, service id matching), `m4dModels/Song.cs` (`GetAltids`, `GetUnmergeableSources`), `m4d/ClientApp/src/pages/song/components/FieldEditor.vue` (merged-value links), `m4d/ClientApp/src/pages/song/components/UnmergeSection.vue`

## Overview

The music4dance merge system identifies and consolidates duplicate songs in the catalog. Since songs come from multiple sources (user imports, music services like Spotify/iTunes, web scraping), duplicates are inevitable. The merge algorithm uses a multi-level matching strategy combined with conflict resolution to create a single, comprehensive song record.

The title-based levels below clean up duplicates after the fact. The first line of defense is
refusing to create one: see [Matching new songs by service id](#matching-new-songs-by-service-id).

## Matching New Songs by Service Id

A Spotify track id, an iTunes track id and an ISRC each identify one recording, so a new song
holding any id that a catalog song already holds is that song. Every path that adds songs checks
for this before it saves, and appends to the existing song instead of creating another.

**The lookup.** `SongIndex.FindSongsByServiceIds` runs one exact-match filter,
`ServiceIds/any(id: search.in(id, 'S:…,I:…,R:…', ','))`, for all of a song's ids.
`FindSongByServiceIds(song)` returns the other song sharing the most of them. `GetSongFromService`
goes through the same filter. The filter is exact, which is why purchase ids are stored in each
service's canonical spelling ([song-internal-format](song-internal-format.md) § 3.6). Amazon ids are
not in `ServiceIds` (`AmazonService.IsIndexed` is false), so they can't be matched.

**The merge.** `SongIndex.MergeIntoSong(user, target, edit)` folds a never-saved song into the one
it duplicates. It calls `Song.AlignAlbumIndexes` first: `AdditiveMerge` pairs albums by index,
which is right for an edit of the same song but would put a separate record's album-0 ids on
whichever album the target has at index 0. Aligning moves each incoming album onto the target's
album of the same name, or onto a fresh index.

| Path | When the check runs |
| --- | --- |
| Spotify playlist imports and the admin catalog upload (`MatchSongs`) | `MergeFromPurchaseInfo` matches on all three ids, after the SongId override and before the title match. `Song.CreateFromTrack` keeps the track's ISRC so it can. |
| The same, after enrichment (`DMController.MergeCatalogPipeline`) | `SongIndex.MergeNewSongsByServiceIds` runs after `ConditionalUpdateSongAndServices`, which can add iTunes ids and ISRCs that `MatchSongs` never saw. It folds each new song into a song earlier in the batch or in the index that holds one of its ids, and re-points its `LocalMerger` (`MatchType.Exact`), so the upload results page counts it as a merge. |
| Add a song by link or search (`MusicServiceManager.CreateSong`) | After enrichment. A hit attaches the user's track to the existing song, so the add page shows "Edit Song". |
| Save a new song (`POST /api/song` → `SongIndex.CreateOrMergeSong`) | Just before the save, which catches a double submit or a song another user added in the meantime. Skipped for a `.Merge`, whose properties carry the ids of the songs it replaces. |

Enrichment of an *existing* song (`UpdateSongAndServices`, `GetISRCData`, `AttachTracksToSong`)
was the source of nearly all shared ids: in the 2026-11-03 snapshot, about 99% of the second
copies of a shared id were attached by a later `batch-i`/`batch-s` edit, not at creation. It now
leaves an id that another song holds where it is, and queues the pair for review. See
[shared-id-review](shared-id-review.md), which also covers clearing the pairs that already
share an id.

## Matching New Songs by Title, and How the Shared-Id Review Differs

When no service id matches, a new song is matched by title and artist
(`SongIndex.MergeFromTitle`). That matcher and the one the [shared-id review](shared-id-review.md)
uses (`SharedIdMatcher`) do different jobs. The add-time one decides alone, with no one looking,
whether a song someone is adding already exists. The review one sorts clusters that are already
linked by a shared Spotify, iTunes or ISRC id, so most of them are the same song, and a person
reviews whatever isn't an exact match.

**The add-time match.** `MergeFromTitle(song)`:

1. Fetches up to 50 catalog songs by a full-text search of the new song's title as given
   (`SongsFromTitle`), and, when the title has a remaster or single-version note, of the title
   without it too (the two results are merged).
2. Keeps the ones that match, each of the three checks below:
   - **Title**: the same `SharedIdMatcher.AddTitleKey`, the `CreateTitleHash` of the title
     without its remaster or single-version notes (`SharedIdMatcher.WithoutVersionNotes`). So
     "Song Name - Remastered 2011" matches "Song Name". A qualifier ("- Radio Edit", "- Live")
     doesn't match here: `WithoutQualifiers` strips any trailing dash part, so "Kinney - The
     Remainder" and "Kinney - Don't Talk Like" would both become "Kinney".
   - **Length**: not `SharedIdMatcher.LengthsApart`. A song with no length is never apart from
     another, so the check only bites when both lengths are known. It applies to every match type
     below, not just the length one.
   - **Artist**: a blank credit on either side matches. Otherwise each credit is compared under
     its preferred name (`ArtistAliases.PreferredCredit`, so "Pink" is "P!nk") and without its
     leader's backing group (`ArtistSplitter.RemoveBackingGroup`, so "Benny Goodman and His
     Orchestra" is "Benny Goodman"). The catalog's artist knowledge is fetched for the two credits
     (`GetArtistKnowledge`, which only queries the index for a credit with ambiguous separators).
3. Of those, picks one that has the new song's first album (`MatchType.Exact`), else one whose
   length is within `SharedIdMatcher.LengthTolerance` (15 seconds, `MatchType.Length`), else the
   only candidate if there is just one (`MatchType.Weak`).

How each path uses the result:

| Path | Accepts | On a match |
| --- | --- | --- |
| Add by link or search (`MusicServiceManager.CreateSong`, through `FindMatchingSong`) | `Exact` and `Length` only, after the track's ISRC | The track and its enrichment are added to the existing song, and the add page shows "Edit Song". |
| Playlist imports and the catalog upload (`MatchSongs` → `MergeCatalog`) | Any match, `Weak` included, after the SongId override and the service ids | `AdditiveMerge`: the new song's dances, tags, albums and ids are added to the existing song. A scalar field such as title or artist is only filled in if it's blank. |
| Save a new song (`CreateOrMergeSong`) | No title match, service ids only | — |

**What's different.** The add-time match compares less loosely in some ways and more loosely in
others:

| | Add time (`MergeFromTitle`) | Shared-id review (`SharedIdMatcher`) |
| --- | --- | --- |
| Why the songs are compared | Only their titles: any catalog song with a similar title | They share a Spotify, iTunes or ISRC id |
| Title | `CreateTitleHash` of the title without its version notes (`AddTitleKey`): case, accents, punctuation, small words and bracketed text are ignored (apart from a dance name or BPM in brackets). Qualifiers ("- Radio Edit", "- Live") still differ. | The same, or the same letters and digits (`SquashedTitle`). Version notes ("- Remastered 2011", "- Single Version") are removed, and so are qualifiers ("- Radio Edit", "- Live") that don't name a dance or a tempo (`TitleKey`). |
| Artist | The credit under its preferred name, without the leader's backing group, as the review does (`CreditKey`). "Pink" and "P!nk" match; "X feat. Y" and "X" still differ. A blank credit matches any. | The credit under its preferred name (`ArtistAliases`), without the leader's backing group (`RemoveBackingGroup`). Later tiers also compare without feat. clauses, by individual artists, and by the lead artist alone. A song with no artist is never merged automatically. |
| Length | Every match type: lengths more than 15 seconds apart (`LengthsApart`) rule a candidate out, and the length match takes one within 15 seconds (`LengthsAgree`). A `Weak` match needs its length to be within 15 seconds too, when both are known. | Over 15 seconds apart (`LengthTolerance`) keeps a cluster out of every Merge all task, unless every song has a tempo and they're within 2 BPM (`TemposAgree`) |
| Tempo | Not used. Tempo usually isn't known until after the match (`UpdateAudioData`). | Can outweigh length |
| Album | The new song's first album on a candidate settles it (`Exact`) | Not used to match |
| Evidence from the services | Only the ISRC lookup before the title match | Track lookups confirm artists (`FewerArtists`, Merge artist) and decide which song owns an id (Sort out ids) |
| Names after the merge | The existing song keeps its title and artist | `FeaturedCleanup.Propose`: the title without its feat. clause or version notes, a credit that names every artist, and individual artists under their preferred names |
| A doubtful match | Add by link or search: a new song. Imports: `Weak`, merged when only one candidate remains. | Left for a person to review |

So adding a song still creates a duplicate when the difference is a feat. clause in the credit,
a qualifier ("- Radio Edit", "- Acoustic"), or a version note the title match doesn't take off,
unless a service id or ISRC links the two. Aliases, backing groups and version notes now match.
Lengths more than 15 seconds apart never match by title (service ids and ISRCs still link songs
whatever their lengths), so an import can no longer merge two
recordings whose lengths are far apart, even when only one candidate has the same title and
artist. The 2026-11-21 snapshot showed what the new rules change; see
[plans/add-song-matching-next](../plans/add-song-matching-next.md) for the numbers and what's
still to bring over.

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
- **Length Filter**: pairwise. Any two songs in the cluster whose lengths are more than
  `SharedIdMatcher.LengthTolerance` (15 seconds) apart are not both kept
- **Implementation**: Level 1 logic + `SharedIdMatcher.WithinLengthTolerance()`, the same pairwise
  rule the add-time match uses (`LengthsApart`). `MergeManager` and `MergeCluster` both call it.

**WithinLengthTolerance Algorithm** (pairwise, drops the song that disagrees most):

1. While some two songs with lengths are apart, find the song apart from the most others. Ties go
   to the song furthest in total from the rest of the cluster.
2. Drop that song and repeat.
3. Songs with no length data are always kept.

The cluster is then checked pair by pair, so no two songs left are apart. This replaces the earlier
rule, which kept songs within ±20 seconds of the cluster median (and before that the mean). A
median or mean is anchored to the cluster as a whole, so two songs could both sit within 20 seconds
of it while being 30 seconds apart, and a cluster of 180s, 190s, 200s, 210s kept all four.

The admin merge candidate list is therefore narrower than it was. A pair 16 to 20 seconds apart
that the median rule kept as candidates is now separated, so the list can drop a Level 3 lump of
two songs that the old rule showed. This is intended: at 15 seconds, a pair is held to the same
tolerance the add-time match uses, and a song that is still a duplicate can be merged by hand.

**Example**:

```
Cluster: [180s, 195s, 300s]
Pairs apart (more than 15s): 180–300, 195–300. 300s is apart from two others → dropped.
Left: [180s, 195s]: 15 apart, not more than 15 → both kept.
```

**Why pairwise**: the question a merge has to answer is whether two songs are the same recording,
and that is a question about two lengths, not about the middle of a group.

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

### 2. Merging

Every merge goes through `SongIndex.SimpleMergeSongs`: the admin bulk edit ("Merge" in the song
list's Multi-Edit menu, `POST /Song/BulkEdit` with `action=Merge`), auto-merge
(`MergeManager.AutoMerge`), and the shared-id review queue. Up to 2026-11 there was also a
field-by-field merge (`SongIndex.MergeSongs`, `Views/Song/Merge.cshtml`) that rebuilt the song from
the chosen title, artist, tempo, length and albums, and a client-side preview (`song-merge` page).
Both are gone; the editor step below replaces the field choices.

**Algorithm**:

1. **Load full songs** - Ensures all SongProperties are available
2. **Renumber albums** - Each song numbers its albums from 0, so `RenumberAlbums` moves every
   song's album properties (`Album`, `Track`, `Publisher`, `Purchase`, `Purchase-`,
   `PromoteAlbum`) into one shared numbering. An album whose name matches one an earlier song holds
   (`Song.NormalizeAlbumString`, case-insensitive) takes that album's index, so their purchase ids
   combine; every other index, including one for an album the song later deleted, gets a new one.
   `OrderAlbums` is dropped: it lists only one song's albums, and replaying it would hide the rest.
3. **Group by edit blocks** - Properties between `.Create`/`.Edit` and `.User=` are kept together
4. **Annotate commands** - Replaces empty `.Create=` and `.Edit=` with `.Create={songGUID}` and
   `.Edit={songGUID}`. Other blocks (`.Merge`, `.NoMerge`, `.FailedLookup`) keep their value,
   which means something else, and get a `MergeSource={songGUID}` property after their
   `User`/`Time` instead. Either way, merging an already-merged song overwrites the earlier source.
   Songs merged before 2026-11-14 have no `MergeSource`.
5. **Sort by timestamp** - Orders all edit blocks chronologically. A block with no time of its own
   (`.FailedLookup` is written inside an edit, after that edit's `User` and `Time`) sorts with
   its song's previous block. Sorted as the earliest block, it would start the log, and
   `SongPropertyCompression` only reads a stored log as plain text when it starts with `.Create`,
   `.Edit`, `.Merge` or `SongId`.
6. **Add merge metadata** - Appends `.Merge=` command with source song GUIDs
7. **Create new song** - Generates new GUID, creates song from merged properties
8. **Delete source songs** - Removes original songs from database

Replaying the woven history gives each field the value of the latest block that set it, and sums
dance ratings, tags and comments across the songs.

**Benefits**:

- **100% information preservation** - Every edit from every source song retained
- **Unmerge capability** - The stamps say which source each block came from (see [4. Unmerging](#4-unmerging))
- **Simpler logic** - No conflict resolution at merge time
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

### 3. Settling the fields

The bulk-edit merge commits, then redirects to the merged song's page with `?edit`, which opens
it in the editor for an admin. While an admin edits any song, the title, artist, tempo and length
each list the other values the song's history holds (`SongHistory.fieldValues`, passed to
`FieldEditor` as `alternatives`) as links; clicking one sets the field. Saving records an ordinary
`.Edit` after the `.Merge`, so the merge itself stays reversible. Batch merges (auto-merge, the
shared-id queue) skip this step and keep the latest values.

After the merge commits, the controller rebuilds the dance stats (`DanceStatsManager.ClearCache`).
A failed rebuild is logged as a warning and the redirect still happens: the sources are already
deleted, so an error page would leave the admin nothing to retry.

### 4. Unmerging

An admin can split one or more sources back out of a merged song from the song page. The song's
history makes this possible: every block before the last `.Merge` says which source it came from,
in its stamp or its `MergeSource`.

**Which songs qualify.** `Song.GetUnmergeableSources()` (client: `SongHistory.mergeSources`)
takes the ids in the song's last `.Merge`. It drops any that a later `.Unmerge` removed, and any
that own no stamped `.Create`/`.Edit` block. A merge from before the stamps (the old field-by-field
merge) has none of those blocks. The result is empty unless at least two sources are left,
because one always stays. Only the last merge layer can be split. Merging an already-merged song
restamps its blocks, so a restored intermediate song comes back whole and can't be split further.

**The operation.** `SongIndex.UnmergeSong(user, songId, extract, noMerge)` calls
`BuildUnmerge`, which saves nothing, and then saves the result:

- Each extracted source is rebuilt **under its original id** from the blocks it owns.
  `MergeSource` is removed, and the `.Create`/`.Edit` stamps are cleared back to empty. Its album
  indices are compacted to 0..n in order of first appearance (`CompactAlbums`). When the admin
  asks, a `.NoMerge` block is appended so that auto-merge won't merge it straight back. Deleting a
  song removes it from the index, so the id is free, and old links to the source work again.
- The merged song (the remainder) keeps its id, its other blocks, and every edit made after the
  merge. It gets an `.Unmerge=id1;id2` block appended. The `.Merge` block stays unchanged.
- A post-merge album property (`Album`, `Track`, `Publisher`, `Purchase`, `Purchase-`,
  `PromoteAlbum`) can be on an album index that only one extracted source's blocks set. Such a
  property moves to that source, in a copy of its block with the same user and time. On the
  remainder, it would replay as an album with no name. Properties on an index that two sources
  share stay on the remainder, and an edit left with nothing in it is dropped.
- `Song.GetAltids()` applies `.Merge` and `.Unmerge` in log order. The remainder stops listing
  the split-out ids, and a song merged back in later is listed again. This matters because
  `UploadIndex(trackDeleted: true)` deletes every alternate id. Without it, an index reload would
  delete the restored songs.
- `SharedIdLog.RejectSharedIds` records each pair of the saved songs that still shares a service
  id as **Rejected** in the shared-id queue, so the queue won't merge them back.

**UI.** `UnmergeSection.vue` sits in the admin area of the song page, next to Undo User Edits. It
appears only for a song with unmergeable sources. It shows one checkbox per source (title,
artist, creator, date and number of edits) and a "Mark unmerged songs NoMerge" checkbox, which is
on by default. It is disabled while the song is being edited, because the unmerge commits
immediately. The button needs at least one source checked and one left unchecked. After a
confirm dialog it posts to `POST /Song/Unmerge` (`dbAdmin`, anti-forgery checked). That action
clears the merge-candidate cache and rebuilds the dance stats; a failed rebuild is only logged, as
for a merge. It then shows the `Info` page with links to the remainder and each restored song,
and any ids they still share.

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

## Database Integration

### SongIndex Operations

**`SimpleMergeSongs()`**: Core merge implementation

- Creates the merged song and deletes the sources
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

1. Run auto-merge first (the admin page's AutoMerge link uses Level 3: title, artist and length)
2. Review Level 0 candidates manually (identical metadata)
3. Review Level 2 candidates carefully (title only)

**Typical Volumes** (estimates, not re-measured):

- Level 1: 50-500 candidates
- Level 0: 10-50 candidates (very high confidence)
- Level 2: 100-1000 candidates (requires review)

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

**Length filter tests**: `m4dModels.Tests/WithinLengthToleranceTests.cs` (`SharedIdMatcher.WithinLengthTolerance`, replacing the median-based `FilterLengthTests`)

- `Outlier_IsDropped`: a song 105 seconds from the other two is dropped
- `ExactlyTheTolerance_IsKept_OneMoreSecondIsNot`: 15 seconds apart stays, 16 goes
- `EvenCountWithADivergentEnd_KeepsAPairWithinTolerance`: four songs, two kept, no pair apart
- `Chain_LeavesNoPairApart`: neighbours within tolerance but the ends not; the rule removes until no pair is apart
- `NoLengths_AreAllKept`, `SongsWithoutLength_AreKeptBesideTheOthers`: songs with no length always stay

**Add-time match tests**: `m4dModels.Tests/AddSongTitleMatchTests.cs` (through `MatchSongs`, with the sandbox index answering the title search)

**MergeManager Tests**: `m4dModels.Tests/MergeManagerTests.cs`

- `GetMergeCandidates_Level1_GroupsByTitleAndArtist`
- `GetMergeCandidates_Level2_GroupsByTitleOnly`
- `GetMergeCandidates_Level0_RequiresFullEquivalence`
- `GetMergeCandidates_Level3_FiltersLengthDivergence`
- `GetMergeCandidates_EmptyArtist_IncludesAllSongsInCluster` (Level 2)
- `AutoMerge_MergesCluster_ReturnsMergedSong`
- Plus 8 additional tests for edge cases and .NoMerge filtering

**Integration Tests**: Merge workflow tests

- `MergeTests`: block weaving and GUID annotation, album renumbering (separate albums kept,
  same-named albums combined, deleted albums kept apart, `OrderAlbums` dropped), and a block with
  no time keeping its place
- `UnmergeTests`: a source restored exactly, two of three split out with `.NoMerge`, post-merge
  album properties moved (and an emptied edit dropped), shared ids recorded as Rejected, nested
  merges (outer layer, and the inner `.Merge` given back through `MergeSource`), invalid choices,
  unstamped merges, and `GetAltids` ordering
- User history preservation

**Client Tests**: `SongHistory.fieldValues`, the `FieldEditor` alternatives (shown only while
editing, current value left out, numeric values compared as numbers, a click sets the field),
`SongHistory.mergeSources`, and `UnmergeSection` (hidden without sources, what it posts, the
button's enabled states, submit only after confirming)

**Controller**: `SongControllerUnmergeTests` (`dbAdmin`, anti-forgery-checked POST)

**End-to-end**: `e2e/tests/merge.spec.ts` merges two songs from a search, picks the earlier song's
artist and tempo in the editor, saves, and checks the stored song keeps both albums. A second
test merges two songs, unmerges one from the song page, and checks each song has its own album
again

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

### Known Limitations

1. **Live vs Studio**: May merge different recordings of same song
2. **Cover Versions**: Different artists performing same song might merge incorrectly
3. **Language Variations**: "Bésame Mucho" vs "Besame Mucho" normalized identically
4. **Special Characters**: Some unicode characters may normalize unexpectedly

## History

- 2026-11-14: Unmerge (song page, `POST /Song/Unmerge`, `.Unmerge`); `MergeSource` on blocks a
  merge can't stamp; a block with no time keeps its place in the merge sort.

## Related Documentation

- **Testing Patterns**: `architecture/dev-testing/testing-patterns.md`
- **Dance Library**: `DanceLib/Dances.cs`
- **Song Model**: `m4dModels/Song.cs` (especially `MungeString` method)
- **Merge Controller**: `m4d/Controllers/SongController.cs` (`MergeCandidates`, with `autoCommit` for auto-merge; `ClearMergeCache`; `BulkEdit` → `Merge`)
- **Song editor**: [song-details-viewing-editing](song-details-viewing-editing.md) (`?edit`, merged-value links, the Unmerge section)
- **Shared-id review**: [shared-id-review](shared-id-review.md) (pairs an unmerge marks not the same)
- **Block Parser**: `m4dModels/SongPropertyBlockParser.cs` — shared infrastructure used by both `SimpleMergeSongs` (chronological sorting) and `ChunkedSong` (batch cleanup); keeps the block-layout contract (`[Action, User, Time, content…]`) in one place
