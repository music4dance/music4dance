# Song Upload File Format for UploadCatalog

This document describes the full set of options and fields supported by the song upload file for the `UploadCatalog` feature. The information is derived from the implementation of `Song.BuildHeaderMap` and `Song.CreateFromRow` in the music4dance codebase.

---

## File Structure

- The file is a delimited text file (default: tab-separated, but you can specify another separator).
- The **first line** is a header row, listing the field names.
- Each subsequent line is a song record, with fields in the same order as the header.

---

## Supported Header Fields

The following header fields are recognized (case-insensitive). You may use any of these as column headers:

| Header Name          | Internal Field  | Description                                        |
| -------------------- | --------------- | -------------------------------------------------- |
| DANCE                | DanceRating     | Dance style(s) for the song (see below for format) |
| TITLE                | Title           | Song title                                         |
| ARTIST               | Artist          | Song artist                                        |
| CONTRIBUTING ARTISTS | Artist          | Alternate header for artist                        |
| LABEL                | Publisher       | Publisher/label                                    |
| USER                 | User            | User name (for per-user properties)                |
| TEMPO                | Tempo           | Tempo in BPM                                       |
| BPM                  | Tempo           | Alternate header for tempo                         |
| BEATS-PER-MINUTE     | Tempo           | Alternate header for tempo                         |
| LENGTH               | Length          | Song length (seconds or mm:ss)                     |
| TRACK                | Track           | Track number                                       |
| ALBUM                | Album           | Album name                                         |
| #                    | Track           | Alternate header for track number                  |
| PUBLISHER            | Publisher       | Alternate header for publisher                     |
| AMAZONTRACK          | Purchase (AS)   | Amazon track ID                                    |
| AMAZON               | Purchase (AS)   | Amazon album ID                                    |
| ITUNES               | Purchase (IS)   | iTunes ID                                          |
| PATH                 | OwnerHash       | File path hash                                     |
| TIME                 | Length          | Alternate header for length                        |
| COMMENT              | Tag+            | Adds tags (see below)                              |
| COMMENTS             | Tag+            | Alternate header for tags                          |
| RATING               | R               | Used for rating (see below)                        |
| DANCERS              | DancersCell     | Dancer names                                       |
| TITLE+ARTIST         | TitleArtistCell | Combined title and artist (see below)              |
| DANCETAGS            | DanceTags       | Dance-specific tags                                |
| SONGTAGS             | SongTags        | General song tags                                  |
| GENRE                | GenreTags       | Genre/style tags                                   |
| YEAR                 | SongYear        | Year (adds a tag)                                  |
| MPM                  | MPM             | Measures-per-minute (tempo)                        |
| MULTIDANCE           | MultiDance      | Multiple dances with tags (see below)              |
| DANCECOMMENT         | DanceComment    | Comment for a dance                                |

> **Note:** The internal field is for reference; use the header name in your file.

---

## Special Field Formats

### DANCE / DanceRating

- Specifies the dance(s) for the song.
- Can be a single dance or multiple, separated by `;` or `,`.
- Example: `Cha Cha;West Coast Swing`

### MULTIDANCE

- Format: `DanceId|tag1|tag2||DanceId2|tag3`
- Example: `WCS|Contemporary||CHA|Classic`
- Each group is separated by `||`, with the first part as the dance ID and the rest as tags.
- Tags within each dance group are separated by `|`.

### TITLE+ARTIST

- Combines title and artist in one field, separated by an em dash (—) or hyphen (-).
- Example: `Uptown Funk — Mark Ronson`
- The system will attempt to split on " — ", " – ", or " - " to extract title and artist.

### TEMPO / BPM / MPM

- **TEMPO/BPM**: Beats per minute (e.g., `120`)
- **MPM**: Measures per minute (used for dances with different meters)
- Tempo values should be positive decimals < 250

### RATING

- Used for dance rating, typically a number (e.g., 1-5).
- Can be numeric or descriptive (implementation-dependent).

### YEAR

- Adds a tag in the format `YYYY:Other`.
- Example: `2014` becomes tag `2014:Other`

### LENGTH / TIME

- Song duration in seconds or `mm:ss` format.
- Examples: `270` or `4:30` (both represent 4 minutes 30 seconds)

### PURCHASE FIELDS (AMAZON, AMAZONTRACK, ITUNES)

- **AMAZON**: Amazon album ID (ASIN)
- **AMAZONTRACK**: Amazon track ID
- **ITUNES**: iTunes track/album ID
- Used to generate purchase links for the song.

---

## Tags System (Detailed)

Tags are a flexible categorization system used throughout music4dance. Multiple fields can add tags to a song, each with specific behaviors.

### Tag-Related Fields

| Field Name                 | Purpose             | Auto-Categorization                                         |
| -------------------------- | ------------------- | ----------------------------------------------------------- |
| **COMMENT** / **COMMENTS** | General song tags   | Tags added as-is unless they match genre patterns           |
| **SONGTAGS**               | Explicit song tags  | Tags added as-is                                            |
| **DANCETAGS**              | Dance-specific tags | Applied to dance ratings, not the song itself               |
| **GENRE**                  | Musical genre/style | Automatically suffixed with `:Music` if no category present |
| **YEAR**                   | Release year        | Automatically formatted as `YYYY:Other`                     |

### Tag Format

Tags use a hierarchical format with optional categories:

- **Simple tag**: `Contemporary` or `Classic`
- **Categorized tag**: `Pop:Music` or `2010s:Other`
- **Multiple tags**: Separated by `|` (pipe character)

### Tag Separator

All tag fields use the pipe character (`|`) as a separator:

```text
Pop|Contemporary|Latin
```

### Tag Normalization

Tags are automatically normalized:

1. **Whitespace**: Leading/trailing spaces removed
2. **Case**: Typically preserved, but lookups are case-insensitive
3. **Category Assignment**: Some tags are auto-categorized (see below)

### Auto-Categorization Rules

#### GENRE Field

Genre tags automatically get the `:Music` suffix if they don't already have a category:

- Input: `Pop` → Stored as: `Pop:Music`
- Input: `Latin Jazz` → Stored as: `Latin Jazz:Music`
- Input: `2010s:Pop` → Stored as: `2010s:Pop` (category already present)

#### YEAR Field

Year values are automatically formatted as `YYYY:Other`:

- Input: `2014` → Stored as: `2014:Other`
- Input: `2020` → Stored as: `2020:Other`

#### Tag Categories

The system uses a fixed set of tag categories. You cannot create custom categories.

**Song Tag Categories** (for COMMENT, SONGTAGS, GENRE fields):

- `:Music` - Musical genres (Pop, Rock, Jazz, Latin, etc.)
- `:Other` - General descriptors (decades, moods, characteristics)
- `:Tempo` - Tempo-related descriptors (Slow, Fast, Medium, etc.)

**Dance Tag Categories** (for DANCETAGS and tags within MULTIDANCE):

- `:Other` - General dance descriptors
- `:Tempo` - Tempo-related descriptors for the dance
- `:Style` - Dance style variations (Contemporary, Classic, Traditional, etc.)

> **Note:** The `:Dance` category exists internally but cannot be used in upload files as it has special system meaning.

### Tag Inheritance and Overrides

Tags can be specified at three levels:

1. **Column-level**: Tags in COMMENT, SONGTAGS, GENRE fields (per song)
2. **Upload-level**: Tags specified in the "Tags" form field (applied to all songs)
3. **Combined**: Both column and upload-level tags are merged

### Examples

#### Basic Genre Tags

```text
GENRE
Pop|Contemporary
```

Result: Song gets tags `Pop:Music` and `Contemporary:Music`

#### Mixed Tag Sources

```text
GENRE           | SONGTAGS      | YEAR
Latin Jazz      | Instrumental  | 2018
```

Result: Song gets tags `Latin Jazz:Music`, `Instrumental`, and `2018:Other`

#### Using COMMENT Field

```text
COMMENT
Classic|Smooth|2010s:Pop
```

Result: Song gets tags `Classic`, `Smooth`, and `2010s:Pop` (category preserved)

#### DANCETAGS (Applied to Dance Ratings)

```text
DANCE           | DANCETAGS
West Coast Swing| Contemporary|Slow
```

Result: The West Coast Swing dance rating gets tags `Contemporary` and `Slow`

#### Multi-Dance with Inline Tags

```text
MULTIDANCE
WCS|Contemporary|Slow||CHA|Classic|Upbeat
```

Result:

- West Coast Swing rating with tags `Contemporary` and `Slow`
- Cha Cha rating with tags `Classic` and `Upbeat`

### Tag Best Practices

1. **Consistency**: Use consistent tag names (e.g., always "Contemporary" not "contemporary" or "Contemp")
2. **Categories**: Let GENRE auto-categorize music styles with `:Music`; manually specify categories for SONGTAGS and COMMENT when needed
3. **Multiple Tags**: Use `|` separator, not commas or semicolons
4. **Descriptive**: Tags help with search/filtering, so be descriptive but concise
5. **Dance vs Song**: Use DANCETAGS for dance-specific attributes (with :Style, :Tempo, :Other), use SONGTAGS/GENRE for music attributes (with :Music, :Tempo, :Other)
6. **Fixed Categories**: Only use the predefined categories listed above; custom categories are not supported

---

## Example Header and Data

```tsv
Title	Artist	Album	Dance	Genre	Year	Tempo	Length
Uptown Funk	Mark Ronson	Uptown Special	Cha Cha;West Coast Swing	Pop|Funk	2014	120	4:30
Shape of You	Ed Sheeran	Divide	Rumba;Samba	Pop	2017	96	3:53
Thinking Out Loud	Ed Sheeran	X	Rumba	Pop|Ballad	2014	79	4:41
```

---

## Additional Notes

- **Separator:** Default is tab (`\t`). You can specify a different separator via the `separator` parameter in the upload form.
- **Header Row:** Must match the field names above (case-insensitive matching is performed).
- **Extra Columns:** Columns not in the list above will be ignored without error.
- **Overrides:** You can override artist, album, tags, and user for all songs via parameters in the upload form (applied to every song in the file).
- **Required Fields:** At minimum, `Title` is required for each song. Artist is highly recommended.
- **Empty Fields:** Empty fields are generally skipped; no default values are assigned.

---

## Upload Form Parameters

The upload form provides additional fields that apply to ALL songs in the upload:

- **User**: Associate all songs with a specific user
- **Dances**: Apply these dances to all songs (in addition to per-song DANCE column)
- **Artist**: Override or set artist for all songs
- **Album**: Override or set album for all songs
- **Tags**: Add these tags to all songs (merged with per-song tags)
- **Separator**: Character used to separate fields (default: tab)
- **Header**: Custom header row (if not present in file)

---

## Special Behaviors

- **Dance tags and song tags** are normalized and can be auto-categorized (e.g., "Pop" becomes "Pop:Music" when in GENRE field).
- **MultiDance** allows associating multiple dances and tags in a single field, useful for songs that work for multiple dances.
- **Title+Artist** can be used instead of separate Title and Artist columns when your source data combines them.
- **Purchase fields** (Amazon, iTunes) are used for linking to purchase information and affiliate programs.
- **Tag merging**: Tags from multiple sources (columns, form fields) are merged without duplication.

---

## References

- See `Song.BuildHeaderMap` and `Song.CreateFromRow` in the codebase for implementation details.
- Tag processing logic in `Song.CreateFromRow` and tag normalization methods.

---
