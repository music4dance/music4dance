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

| Header Name          | Internal Field  | Description                                              |
| -------------------- | --------------- | -------------------------------------------------------- |
| DANCE                | DanceRating     | Dance style(s) for the song - uses dance IDs (see below) |
| TITLE                | Title           | Song title                                               |
| ARTIST               | Artist          | Song artist                                              |
| CONTRIBUTING ARTISTS | Artist          | Alternate header for artist                              |
| LABEL                | Publisher       | Publisher/label                                          |
| USER                 | User            | User name (for per-user properties)                      |
| TEMPO                | Tempo           | Tempo in BPM                                             |
| BPM                  | Tempo           | Alternate header for tempo                               |
| BEATS-PER-MINUTE     | Tempo           | Alternate header for tempo                               |
| LENGTH               | Length          | Song length (seconds or mm:ss)                           |
| TRACK                | Track           | Track number                                             |
| ALBUM                | Album           | Album name                                               |
| #                    | Track           | Alternate header for track number                        |
| PUBLISHER            | Publisher       | Alternate header for publisher                           |
| AMAZONTRACK          | Purchase (AS)   | Amazon track ID                                          |
| AMAZON               | Purchase (AS)   | Amazon album ID                                          |
| ITUNES               | Purchase (IS)   | iTunes ID                                                |
| PATH                 | OwnerHash       | File path hash                                           |
| TIME                 | Length          | Alternate header for length                              |
| COMMENT              | Tag+            | Adds tags (see below)                                    |
| COMMENTS             | Tag+            | Alternate header for tags                                |
| RATING               | R               | Used for rating (see below)                              |
| DANCERS              | DancersCell     | Dancer names                                             |
| TITLE+ARTIST         | TitleArtistCell | Combined title and artist (see below)                    |
| DANCETAGS            | DanceTags       | Dance-specific tags                                      |
| SONGTAGS             | SongTags        | General song tags                                        |
| GENRE                | GenreTags       | Genre/style tags                                         |
| YEAR                 | SongYear        | Year (adds a tag)                                        |
| MPM                  | MPM             | Measures-per-minute (tempo)                              |
| MULTIDANCE           | MultiDance      | Multiple dances with tags (see below)                    |
| DANCECOMMENT         | DanceComment    | Comment for a dance                                      |

> **Note:** The internal field is for reference; use the header name in your file.

---

## Special Field Formats

### DANCE / DanceRating

- Specifies the dance(s) for the song using **dance IDs** (not full names).
- Can be a single dance or multiple, separated by `;` or `,`.
- **Example**: `CHA;WCS` (for Cha Cha and West Coast Swing)
- **Common Dance IDs**:
  - `CHA` - Cha Cha
  - `RMB` - Rumba
  - `WCS` - West Coast Swing
  - `SAM` - Samba
  - `WAL` - Waltz
  - `TAN` - Tango
  - `FOX` - Foxtrot
  - `JIV` - Jive
  - `QUI` - Quickstep
  - `VW` - Viennese Waltz

### MULTIDANCE

- Format: `DanceId|tag1|tag2||DanceId2|tag3`
- **Example**: `WCS|Contemporary||CHA|Classic`
- Each group is separated by `||`, with the first part as the **dance ID** (not full name) and the rest as tags.
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
WCS             | Contemporary|Slow
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

Here's a sample file in TSV format:

```tsv
Title	Artist	Album	Dance	Genre	Year	Tempo	Length
Uptown Funk	Mark Ronson	Uptown Special	CHA;WCS	Pop|Funk	2014	120	4:30
Shape of You	Ed Sheeran	Divide	RMB;SAM	Pop	2017	96	3:53
Thinking Out Loud	Ed Sheeran	X	RMB	Pop|Ballad	2014	79	4:41
```

**Field Breakdown for "Uptown Funk":**

- **Title**: `Uptown Funk`
- **Artist**: `Mark Ronson`
- **Album**: `Uptown Special`
- **Dance**: `CHA;WCS` (Cha Cha and West Coast Swing)
- **Genre**: `Pop|Funk` (becomes `Pop:Music` and `Funk:Music`)
- **Year**: `2014` (becomes `2014:Other` tag)
- **Tempo**: `120` BPM
- **Length**: `4:30` (4 minutes 30 seconds)

**Field Breakdown for "Shape of You":**

- **Title**: `Shape of You`
- **Artist**: `Ed Sheeran`
- **Album**: `Divide`
- **Dance**: `RMB;SAM` (Rumba and Samba)
- **Genre**: `Pop` (becomes `Pop:Music`)
- **Year**: `2017` (becomes `2017:Other` tag)
- **Tempo**: `96` BPM
- **Length**: `3:53` (3 minutes 53 seconds)

**Field Breakdown for "Thinking Out Loud":**

- **Title**: `Thinking Out Loud`
- **Artist**: `Ed Sheeran`
- **Album**: `X`
- **Dance**: `RMB` (Rumba)
- **Genre**: `Pop|Ballad` (becomes `Pop:Music` and `Ballad:Music`)
- **Year**: `2014` (becomes `2014:Other` tag)
- **Tempo**: `79` BPM
- **Length**: `4:41` (4 minutes 41 seconds)

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

- **User**: Associate all songs with a specific user - this field is required, since creating records without attributing them to a user breaks the system
- **Dances**: Apply these dances to all songs (in addition to per-song DANCE column)
- **Artist**: Override or set artist for all songs
- **Album**: Override or set album for all songs
- **Tags**: Add these tags to all songs (merged with per-song tags)
- **Separator**: Character used to separate fields (default: tab)
- **Header**: Custom header row (if not present in file)

### Parameter Details

#### User

- **Purpose**: Specifies which user account to associate with the uploaded songs
- **Interaction with File**: Applied to all songs regardless of file content
- **Format**: Username string
- **Example**: `john.doe@example.com`

#### Dances

- **Purpose**: Add these dances to all songs in the upload
- **Interaction with File**: Merged with dances specified in the DANCE column
- **Format**: Semicolon-separated **dance IDs** (not full names)
- **Example**: `CHA;RMB` (for Cha Cha and Rumba)
- **Note**: These dances are added in addition to any dances in the file's DANCE column
- **Common Dance IDs**: See the DANCE field format section above for a list of common IDs

#### Artist

- **Purpose**: Override or set the artist for all songs
- **Interaction with File**:
  - If a song has NO artist in the file, this value is used
  - If a song HAS an artist in the file AND this parameter is provided, the file's artist is preserved but this artist is added as an additional property with a header indicating it's an override
- **Format**: Artist name string
- **Example**: `Various Artists`

#### Album

- **Purpose**: Set the album for all songs that don't have one
- **Interaction with File**:
  - Only applied to songs that have NO album specified in the ALBUM column
  - Songs with existing albums are not modified
- **Format**: Album name string
- **Example**: `Ballroom Dance Classics`

#### Tags

- **Purpose**: Add these tags to all songs
- **Interaction with File**: Merged with tags from SONGTAGS, GENRE, COMMENT, and YEAR columns
- **Format**: Pipe-separated tags
- **Example**: `Competition|2024:Other`
- **Note**: Tags are deduplicated - if a tag exists in both the file and this parameter, it's only added once

#### Separator

- **Purpose**: Specifies the character used to separate fields in the uploaded file
- **Interaction with File**: Required for parsing the file correctly
- **Format**: Single character or escape sequence
- **Default**: Tab character (`\t`)
- **Common Values**:
  - Tab: `\t` or just leave as default
  - Comma: `,`
  - Pipe: `|`
  - Space: ` ` (not recommended)
- **Example**: `,` for CSV files

#### Header (Headers in form)

- **Purpose**: Specify column headers when the file doesn't include them
- **Interaction with File**:
  - If the file's first line is NOT a valid header, this parameter is used
  - If the file's first line IS a valid header, it takes precedence and this parameter is ignored
- **Format**: Comma-separated list of header names
- **Example**: `Title,Artist,Album,Dance,Tempo`
- **Note**: Use this when your data file doesn't have a header row

### Upload Methods

The UploadCatalog endpoint supports two methods of providing data:

#### File Upload

- Use the **File** field to upload a text file
- File should be tab-separated (or use the **Separator** parameter for other delimiters)
- First line should be headers (or provide **Header** parameter)

#### Direct Text Entry

- Use the **Table** field to paste data directly
- Must also provide the **Separator** parameter
- Must also provide the **Header** parameter (since pasted data typically lacks headers)

> **Note**: If both **File** and **Table** are provided, the **File** takes precedence and **Table** is ignored.

### Parameter Override Behavior

When parameters interact with file data:

1. **Additive**: User, Dances, Tags - these are ADDED to what's in the file
2. **Conditional**: Album - only applied if the song has no album
3. **Override with Preservation**: Artist - file artist is kept, form artist is added as additional data
4. **Structural**: Separator, Header - these control how the file is parsed

### Example Usage Scenarios

#### Scenario 1: Bulk Import with Common Album

```text
Form Parameters:
- Album: "Wedding Dance Collection"
- User: "dj@example.com"
- Dances: "CHA;RMB"

File Content:
Title           Artist          Tempo
Happy           Pharrell        120
Thinking Out    Ed Sheeran      79

Result: Both songs get album "Wedding Dance Collection", are associated with user dj@example.com,
and are tagged with Cha Cha and Rumba dances
```

#### Scenario 2: Adding Tags to Existing Data

```text
Form Parameters:
- Tags: "Competition|Professional:Other"

File Content (with GENRE column):
Title           Artist          Genre       Dance
Uptown Funk     Mark Ronson     Funk        CHA

Result: Song gets tags "Funk:Music", "Competition", and "Professional:Other"
```

#### Scenario 3: CSV File with Custom Headers

```text
Form Parameters:
- Separator: ",",
- Header: "Title,Artist,Tempo,Dance"

File Content (no header row):
Uptown Funk,Mark Ronson,120,CHA
Shape of You,Ed Sheeran,96,RMB

Result: System parses the file using the provided headers
```

#### Scenario 4: Combining Form Dances with File Dances

```text
Form Parameters:
- Dances: "WCS;ECS"

File Content:
Title           Artist          Dance
Happy           Pharrell        CHA;SAM

Result: Song "Happy" gets four dances: CHA, SAM (from file) and WCS, ECS (from form)
```

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
