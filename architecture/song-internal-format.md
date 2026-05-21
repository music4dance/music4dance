# Song Internal Format

## Overview

Every song in music4dance is persisted as an **append-only log of `SongProperty` records**. The
log is stored as a flat, tab-delimited string in the database. Playback of the log (loading all
properties in sequence) rebuilds the current state of the song. All edits, votes, tag changes, and
comments are represented as additional property records appended to the same log.

This document covers:

- The serialized wire / storage format
- The property name syntax
- Every field name and what it means
- How the log is structured into blocks (action + user + time + payload)
- User identity in the log (real users, pseudo/proxy users, batch/algorithmic users)
- The computed `Song` model that results from replaying the log

Related documents:

- **[SongUploadFormat.md](SongUploadFormat.md)** — bulk CSV/TSV upload field mapping
- **[song-merge-algorithm.md](song-merge-algorithm.md)** — how duplicate songs are consolidated
- **[VOTING_WITH_DANCE_FAMILIES.md](VOTING_WITH_DANCE_FAMILIES.md)** — style-family tag voting

---

## 1. Serialized Format

### 1.1 Full Song String

A song in storage looks like:

```
SongId={guid}\tProp1=Val1\tProp2=Val2\t...
```

Example:

```
SongId={b3f1a2c0-...}\t.Create=\tUser=alice\tTime=01/15/2024 2:30:00 PM\tTitle=Summertime\tArtist=Ella Fitzgerald\tTempo=60.0\tDanceRating=FXT+1\tTag+=Foxtrot:Dance\tTag+=Jazz:Music
```

The `SongId=` prefix is present when the song is stored or transmitted as a standalone string.
When properties are stored as rows in the database, the song GUID is stored separately and the
`SongId=` prefix is omitted from the property blob.

### 1.2 SongProperty Encoding

Each property is serialized as `Name=Value`. Special characters within `Value` are escaped:

| Character     | Escape sequence |
| ------------- | --------------- |
| `\t` (tab)    | `\u001aT`       |
| `\r\n` (CRLF) | `\u001aR`       |
| `=`           | `\\<EQ>\\`      |

Properties are joined by `\t` (tab) in the default format. A `\r\n`-delimited variant exists for
diagnostic/export use.

Command properties (starting with `.`) that have no value serialize as `.CommandName=` (no value
after `=`) or simply `.CommandName` if no `=` was present during load.

---

## 2. Property Name Syntax

```
BaseName[:index[:qualifier]][:danceId]
```

| Segment      | Meaning                                                                                                                                                      |
| ------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `BaseName`   | Field identity (e.g. `Title`, `Tag+`, `DanceRating`)                                                                                                         |
| `:index`     | Zero-based, 2-digit decimal index for multi-valued fields (albums, purchases). `-1` = single-value                                                           |
| `:qualifier` | Optional per-field qualifier — currently used for purchase type (e.g. `AS`, `IS`)                                                                            |
| `:danceId`   | On `Tag+`/`Tag-`, `Comment+`/`Comment-`, `Choreographer+`/`Choreographer-`, and `StepSheetUrl+`/`StepSheetUrl-`: are the properties that dance ID applies to |

Examples:

| Name                 | Meaning                                  |
| -------------------- | ---------------------------------------- |
| `Title`              | Song title (simple scalar)               |
| `Album:00`           | First album name                         |
| `Album:01`           | Second album name                        |
| `Purchase:00:AS`     | Amazon album purchase ID for first album |
| `Purchase:00:IS`     | iTunes purchase ID for first album       |
| `Tag+`               | Song-level tag addition                  |
| `Tag+:CHA`           | Cha Cha–scoped tag addition              |
| `Comment+:CHA`       | Cha Cha–scoped comment addition          |
| `Choreographer+:PTN` | Pattern dance choreographer name         |
| `StepSheetUrl+:PTN`  | Pattern dance step sheet URL             |
| `.Create`            | Action command (no index / qualifier)    |

---

## 3. Field Reference

### 3.1 Scalar Fields

These fields hold a single value; later writes by non-pseudo users win.

| Name           | Type    | Notes                                                                                 |
| -------------- | ------- | ------------------------------------------------------------------------------------- |
| `Title`        | string  | Song title. Required — a song without a title is considered null/deleted              |
| `Artist`       | string  | Performing artist                                                                     |
| `Tempo`        | decimal | Beats per minute, stored as `F1` (one decimal place, e.g. `120.0`). Valid range 5–500 |
| `Length`       | int     | Duration in seconds                                                                   |
| `Sample`       | string  | URL of audio sample. Value `.` means "explicitly no sample"                           |
| `Danceability` | float   | EchoNest/Spotify acoustic danceability score                                          |
| `Energy`       | float   | EchoNest/Spotify energy score                                                         |
| `Valence`      | float   | EchoNest/Spotify valence (emotional positivity) score                                 |

**Bot override rule**: When loading properties, scalar field writes by pseudo users (`|P`) are
silently ignored if the same field was already set by a real (non-pseudo) user. This means human
edits always take precedence over service-imported metadata.

### 3.2 Add/Remove Fields and the `+`/`-` Convention

Several field families use a `+` suffix to add a value and a `-` suffix to remove it. All such
fields support an optional `:danceId` qualifier that scopes the operation to a specific dance
rating rather than the song as a whole.

Field families that follow this pattern:

| Family         | Add field        | Remove field     | Extra behaviors                          |
| -------------- | ---------------- | ---------------- | ---------------------------------------- |
| Tags           | `Tag+`           | `Tag-`           | Categorized, pipe-delimited, `DeleteTag` |
| Comments       | `Comment+`       | `Comment-`       | —                                        |
| Choreographer  | `Choreographer+` | `Choreographer-` | Dance-scoped only                        |
| Step sheet URL | `StepSheetUrl+`  | `StepSheetUrl-`  | Dance-scoped only                        |

Comments, Choreographer, and StepSheetUrl are covered in detail in §3.4. Tag-specific
behaviors are described below.

#### Tag Fields

Tags are key-value pairs stored as `value:Category`.

| Name        | Meaning                                                                          |
| ----------- | -------------------------------------------------------------------------------- |
| `Tag+`      | Add one or more tags to the song (or dance, when `danceId` qualifier is present) |
| `Tag-`      | Remove one or more tags from the song (or dance)                                 |
| `DeleteTag` | Curator-level hard-delete of a tag even if it was added by a later block         |

Tag values within a single property are pipe-delimited: `Pop:Music|Latin:Music|Uptempo:Other`.

**Tag categories:**

| Category | Used on | Meaning                                                                           |
| -------- | ------- | --------------------------------------------------------------------------------- |
| `Dance`  | Song    | Which dances the song is appropriate for (e.g., `Foxtrot:Dance`)                  |
| `Music`  | Song    | Musical genre (e.g., `Jazz:Music`, `Pop:Music`)                                   |
| `Other`  | Song    | General descriptors (e.g., `Instrumental:Other`, `2010s:Other`)                   |
| `Tempo`  | Song    | Meter tags (e.g., `4/4:Tempo`, `3/4:Tempo`)                                       |
| `Style`  | Dance   | Style family for a specific dance (e.g., `International:Style`, `American:Style`) |

Dance-scoped tags (qualifier present, e.g. `Tag+:CHA`) apply only to the `CHA` `DanceRating`
object, not to the song's global tag list.

### 3.3 Dance Rating Fields

| Name          | Value format                 | Meaning                                             |
| ------------- | ---------------------------- | --------------------------------------------------- |
| `DanceRating` | `{DanceId}{+\|-}{magnitude}` | Vote for/against a dance. Example: `CHA+1`, `FXT-1` |

Each `DanceRating` property is a **delta**: positive = vote for, negative = vote against. Deltas
accumulate; the net `Weight` on the `DanceRating` computed object is their sum. A dance rating
with `Weight <= 0` is soft-deleted (removed from the active list) when the log is replayed.

Standard delta magnitudes:

| Constant               | Value | Usage                           |
| ---------------------- | ----- | ------------------------------- |
| `DanceRatingCreate`    | 1     | Initial rating on song creation |
| `DanceRatingInitial`   | 1     | First vote by a user            |
| `DanceRatingIncrement` | 1     | Upvote                          |
| `DanceRatingDecrement` | -1    | Downvote                        |

### 3.4 Comment and Dance-Scoped Metadata Fields

These fields follow the same `+`/`-` add/remove convention introduced in §3.2. Like tags, all
support a `:danceId` qualifier. Unlike tags: each field is a single plain string value (not
pipe-delimited or categorized), and there is no `Delete*` hard-delete variant.

| Name             | C# constant                | Meaning                                                            |
| ---------------- | -------------------------- | ------------------------------------------------------------------ |
| `Comment+`       | `AddCommentField`          | Add a comment to the song, or to a specific dance (with qualifier) |
| `Comment-`       | `RemoveCommentField`       | Remove the most recent comment by the current user                 |
| `Choreographer+` | `AddChoreographerField`    | Add a choreographer name to a dance                                |
| `Choreographer-` | `RemoveChoreographerField` | Remove the choreographer from a dance                              |
| `StepSheetUrl+`  | `AddStepSheetUrlField`     | Add a step sheet URL to a dance                                    |
| `StepSheetUrl-`  | `RemoveStepSheetUrlField`  | Remove the step sheet URL from a dance                             |

**Example** (line dance song with PTN dance rating):

```
DanceRating=PTN+1
Tag+=Pattern:Dance
Comment+:PTN=Boots on the Ground
Choreographer+:PTN=Tre Little (USA) - January 2025
StepSheetUrl+:PTN=https://www.copperknob.co.uk/stepsheets/4GPM8ZG/boots-on-the-ground-with-clacker-fan
```

The upload import (`DANCECOMMENT`, `CHOREOGRAPHER`, `STEPSHEETURL` CSV headers) always uses the
`+` form. The `-` form is available for future UX that removes or replaces these values.

### 3.5 User & Session Fields

These appear at the start of every edit block and establish attribution for the properties that follow.

| Name        | Value format                    | Meaning                                                                            |
| ----------- | ------------------------------- | ---------------------------------------------------------------------------------- |
| `User`      | `{username}` or `{username}\|P` | Identifies the author of the following properties. `\|P` marks a pseudo/proxy user |
| `UserProxy` | same as `User`                  | Variant used for some proxy scenarios (treated identically to `User` during load)  |
| `Time`      | parseable DateTime string       | Timestamp of the edit block                                                        |

### 3.6 Album Fields

Albums are indexed (`:00`, `:01`, …) and each album can have multiple related properties.

| Name                    | Value                   | Meaning                                                           |
| ----------------------- | ----------------------- | ----------------------------------------------------------------- |
| `Album:NN`              | Album name              | Name of the NN-th album                                           |
| `Publisher:NN`          | Publisher name          | Label/publisher for album NN                                      |
| `Track:NN`              | int                     | Track number within album NN                                      |
| `Purchase:NN:qualifier` | service-specific ID     | Purchase link. Qualifier identifies the service and purchase type |
| `PromoteAlbum`          | album index             | Mark an album as the preferred display album                      |
| `OrderAlbums`           | comma-delimited indices | Override the display ordering of albums                           |

Purchase qualifiers combine a service code and purchase type code. Examples:

| Qualifier | Service      | Type             |
| --------- | ------------ | ---------------- |
| `AS`      | Amazon (`A`) | Song (`S`)       |
| `AD`      | Amazon (`A`) | Album/disc (`D`) |
| `IS`      | iTunes (`I`) | Song (`S`)       |
| `IA`      | iTunes (`I`) | Album (`A`)      |

### 3.7 User Preference Fields

These are per-user properties stored within an edit block attributed to the user.

| Name        | Value             | Meaning                                                             |
| ----------- | ----------------- | ------------------------------------------------------------------- |
| `Like`      | `true` or `false` | The current user's like/dislike flag for this song                  |
| `OwnerHash` | hex int           | Hash of the user's local file path — indicates the user owns a copy |

### 3.8 Command / Action Fields

Commands start with `.` and mark the beginning of an edit block. They are not preceded by `=` in
the raw stream if they have no value, though during serialization they use `Name=Value` format
with an empty value.

| Name            | Value                     | Meaning                                                                       |
| --------------- | ------------------------- | ----------------------------------------------------------------------------- |
| `.Create`       | empty                     | First creation of the song                                                    |
| `.Edit`         | empty                     | A subsequent edit                                                             |
| `.Delete`       | `true` or empty           | Marks the song as deleted; causes title and all scalars to be cleared on load |
| `.Undo`         | empty                     | Undo the most recent edit                                                     |
| `.Merge`        | semicolon-delimited GUIDs | Records that this song was merged from the listed source song IDs             |
| `.NoMerge`      | —                         | Prevents this song from being merged with others                              |
| `.FailedLookup` | service char code(s)      | Records that a music-service lookup failed for the given service(s)           |

Pseudo-commands used only during serialization (never persisted in the primary log):

| Name                            | Meaning                                           |
| ------------------------------- | ------------------------------------------------- |
| `.NoSongId`                     | Serialize properties without the `SongId=` prefix |
| `.SerializeDeleted`             | Include even deleted (null-title) songs in output |
| `.Success`, `.Fail`, `.Message` | API response envelope (not part of song log)      |

---

## 4. Edit Block Structure

Properties are organized into **edit blocks**. Each block starts with a command property (`.Create`
or `.Edit`) followed immediately by `User` and `Time` (in either order). The rest of the block is
the payload for that edit.

```
.Create=  User=alice  Time=2024-01-15 14:30:00  Title=Summertime  Artist=Ella  Tempo=60.0  DanceRating=FXT+1  Tag+=Foxtrot:Dance  Tag+=Jazz:Music
.Edit=    User=bob    Time=2024-03-01 10:00:00   DanceRating=FXT+1  Tag+=Foxtrot:Dance
.Edit=    User=alice  Time=2024-03-05 09:15:00   Tempo=58.0
```

In the raw tab-delimited form, the fields above are tab-separated (whitespace shown above for
readability only).

**Valid header orders:**

- `.Command=` → `User=` → `Time=`
- `.Command=` → `Time=` → `User=`

Both orderings are accepted during load and are validated by `CheckHeader()` in `Song.cs`.

---

## 5. User Identity and User Types

Every edit block is attributed to a user. The `User` field value is `{username}` for real users and
`{username}|P` for pseudo/proxy users.

### 5.1 User Types

| Type           | Pattern                                    | Description                                                                                                  |
| -------------- | ------------------------------------------ | ------------------------------------------------------------------------------------------------------------ |
| Real user      | `alice`                                    | Confirmed human account                                                                                      |
| Pseudo / proxy | `ArthurMurrays\|P`                         | A dance-studio or other entity acting as a proxy for real users; votes are counted but displayed differently |
| `batch`        | `batch`                                    | The human-curated catalog import user. Not considered algorithmic                                            |
| `batch\|P`     | `batch\|P`                                 | Pseudo-flagged batch import. Has additional restrictions (see §5.3)                                          |
| Service import | `batch-s`, `batch-a`, `batch-i`, `batch-x` | Automated imports from Spotify, Amazon, iTunes, Xbox Music                                                   |
| Algorithmic    | `tempo-bot`                                | Automated tempo-correction bot                                                                               |

**Display names** (used in history views):

| UserName    | Displayed As     |
| ----------- | ---------------- |
| `batch`     | Anonymous Import |
| `batch-s`   | Spotify          |
| `batch-a`   | Amazon Music     |
| `batch-i`   | iTunes           |
| `batch-x`   | Xbox Music       |
| `batch-e`   | EchoNest         |
| `tempo-bot` | Tempo Bot        |

### 5.2 `IsPseudo` Flag

A user is _pseudo_ if their decorated name ends with `|P`. In `ApplicationUser`, this is
indicated by an email address at `@music4dance.net`. Pseudo users represent human votes proxied
through an organization (e.g. a dance school submitting votes on behalf of its students).

Key behaviors of pseudo users:

- **Scalar field suppression**: writes to scalar fields (`Title`, `Artist`, `Tempo`, etc.) by
  pseudo users are ignored if the field has already been set by a real user.
- **Visible in history**: `SongChange.isPseudo` is true, but pseudo users are _included_ in the
  default history view (`userChanges`). Only the `humanOnly` filter in
  `SongHistory.filterChanges` excludes batch/algorithmic users.
- **`|P` suffix in log**: Stored as `Alice|P` in the `User` property value.

### 5.3 Batch / Algorithmic Users

Batch and algorithmic users (`batch-*`, `tempo-bot`) are **excluded** from the default
`userChanges` history view and from the `humanOnly` history filter.

`batch|P` is additionally considered "invalid" if it carries a `DanceRating` property
(`ChunkedSong.HasInvalidBatch()`). Dance ratings from `batch|P` blocks are removed during catalog
cleanup (`RemoveInvalidBatches()`). The rationale: vote weights should only come from real users
or legitimate service imports.

### 5.4 Decorated Name

The string stored in the `User` property is the _decorated name_:

```
realUser     → "alice"
pseudoUser   → "alice|P"
```

When properties are loaded, the `ModifiedRecord` constructor parses the `|P` flag:

```csharp
var parts = value.Split('|');
UserName = parts[0];           // "alice"
IsPseudo = parts[1] == "P";   // true
```

---

## 6. Computed Song Object

Replaying the property log produces the `Song` object:

| Property                            | Source                                                        | Notes                                            |
| ----------------------------------- | ------------------------------------------------------------- | ------------------------------------------------ |
| `SongId`                            | GUID stored separately or as `SongId=`                        | Immutable once assigned                          |
| `Title`                             | Last `Title` write by non-pseudo user                         | Falls back to last write by any user if none     |
| `Artist`                            | Same as Title                                                 |                                                  |
| `Tempo`                             | Same as Title                                                 | Decimal, stored as `F1`                          |
| `Length`                            | Same as Title                                                 | Seconds                                          |
| `Sample`                            | Same as Title                                                 | URL                                              |
| `Danceability`, `Energy`, `Valence` | Same as Title                                                 | Float, from EchoNest/Spotify                     |
| `Created`                           | First `Time` value in the log                                 |                                                  |
| `Modified`                          | Last `Time` value in the log                                  |                                                  |
| `Edited`                            | Last `Time` from a non-pseudo user                            | Tracks when a human last changed the song        |
| `DanceRatings`                      | Accumulated `DanceRating` deltas                              | Objects with `DanceId`, `Weight`, tags           |
| `ModifiedBy`                        | One `ModifiedRecord` per unique username                      | Contains `IsPseudo`, `Owned`, `Like`             |
| `Albums`                            | Grouped by index from `Album:NN`, `Track:NN`, `Purchase:NN:Q` | Rebuilt on every load                            |
| `SongProperties`                    | Raw log (all properties)                                      | The source of truth; everything above is derived |
| `TagSummary`                        | Built from `Tag+` / `Tag-` operations                         | Indexed tag sets by category                     |

### 6.1 Dance Rating Computation

- Each `DanceRating=DID+N` or `DanceRating=DID-N` property contributes a delta to the running
  total for dance `DID`.
- If the accumulated `Weight` drops to ≤ 0, the `DanceRating` is soft-deleted (removed from
  `DanceRatings` and a negative `Dance` tag is injected: `!DanceName:Dance`).
- Dance-scoped tags (`Tag+:CHA=International:Style`) are stored on the `DanceRating` object for
  `CHA`, not on the song-level `TagSummary`.

#### Per-User ±1 Net Vote Cap

When replaying the log, a **±1 net cap** is enforced per (user, dance) pair for all non-batch
users — this includes both real users and pseudo (`|P`) users. The cap prevents any single user
from contributing more than ±1 net to a dance's `Weight`.

**Exempt from the cap** (may accumulate any magnitude):

- Usernames starting with `batch` (bulk import accounts)
- `tempo-bot` (algorithmic tempo service)
- Null user (legacy/system records with no attributed author)

**Effect on raw property values**: The raw `SongProperties` log stores deltas exactly as written
(e.g., a raw value of `CHA+2` is possible if a user downvoted then upvoted the same dance,
producing intermediate deltas of `-1, +1, +1` in the log). The cap applies only during
computation of the in-memory `DanceRatings` list, not to the stored log entries.

**Why this matters for serialization**: When two rows with the same title/artist are merged via
`MergeRow`, the merge reads from the computed `DanceRatings` (cap-applied) rather than raw
properties. Serialized output from a merged song therefore reflects capped values.

### 6.2 `ModifiedRecord` and Ownership

`ModifiedRecord` accumulates per-user metadata across all edit blocks attributed to that user:

| Field       | Set by                        | Meaning                                              |
| ----------- | ----------------------------- | ---------------------------------------------------- |
| `UserName`  | `User` property               | Canonical username                                   |
| `IsPseudo`  | `\|P` suffix on `User` value  | Whether this is a proxy/pseudo user                  |
| `Owned`     | `OwnerHash` property          | Whether (and which) local file is owned by this user |
| `Like`      | `Like` property               | The user's like/dislike flag                         |
| `IsCreator` | Position in `ModifiedBy` list | `true` for the first entry (original creator)        |

### 6.3 Deleted Songs

A `.Delete=` property (or `.Delete=true`) in any block causes the song to be treated as deleted:
all scalar fields are cleared and `IsNull` returns `true`. A deleted song still has a log and a
GUID, allowing undeletion via `.Undo`.

---

## 7. Block-Level Operations

### 7.1 ChunkedSong

The `ChunkedSong` helper splits the flat property log into named `SongChunk` objects, one per
edit block. This is used for validation and for per-user analysis:

```csharp
var chunked = new ChunkedSong(song);
// chunked.Chunks     — all blocks in order
// chunked.UserChunks — dictionary keyed by decorated username
```

Each `SongChunk` exposes the command, user, timestamp, and payload properties of one block.

### 7.2 SongPropertyBlockParser

`SongPropertyBlockParser.ParseBlocks()` is a lower-level alternative used for date-range queries
and admin modifications. It returns `SongPropertyBlock` objects that preserve the action command,
user, timestamp, and a mutable property list for that block.

---

## 8. History Model

The `SongHistory` class (client-side TypeScript) provides a view over the same property log. It
groups properties into `SongChange` objects that represent one edit block each.

### 8.1 `SongChange`

| Field           | Meaning                                                                     |
| --------------- | --------------------------------------------------------------------------- |
| `action`        | The command name (without `.`), e.g. `"Create"`, `"Edit"`                   |
| `user`          | Decorated username (e.g. `"alice"` or `"ArthurMurrays\|P"` if pseudo)       |
| `date`          | Block timestamp                                                             |
| `properties`    | Payload properties (excludes command, user, time)                           |
| `isBatch`       | `true` when `user === "batch\|P"` or `user.startsWith("batch-")`            |
| `isPseudo`      | `true` when the user value contains `\|P` (pseudo/proxy account)            |
| `isAlgorithmic` | `true` when `user` is one of the known service/bot names (`tempo-bot` etc.) |

### 8.2 History Filters

| Getter             | Includes                                                        |
| ------------------ | --------------------------------------------------------------- |
| `userChanges`      | Human and pseudo users only; excludes `batch-*` and `tempo-bot` |
| `inclusiveChanges` | All users (human, pseudo, batch, algorithmic)                   |
| `systemTagKeys`    | Tag keys whose last modification was by a non-human user        |

### 8.3 TypeScript vs. C# Property Names

TypeScript uses camelCase (`PropertyType` enum), C# uses PascalCase constants (`Song.UserField`).
They map to the same string values:

| TypeScript `PropertyType` | C# `Song.*` constant       | String value       |
| ------------------------- | -------------------------- | ------------------ |
| `userField`               | `UserField`                | `"User"`           |
| `timeField`               | `TimeField`                | `"Time"`           |
| `titleField`              | `TitleField`               | `"Title"`          |
| `artistField`             | `ArtistField`              | `"Artist"`         |
| `tempoField`              | `TempoField`               | `"Tempo"`          |
| `lengthField`             | `LengthField`              | `"Length"`         |
| `addedTags`               | `AddedTags`                | `"Tag+"`           |
| `removedTags`             | `RemovedTags`              | `"Tag-"`           |
| `danceRatingField`        | `DanceRatingField`         | `"DanceRating"`    |
| `likeTag`                 | `LikeTag`                  | `"Like"`           |
| `userProxy`               | `UserProxy`                | `"UserProxy"`      |
| `createCommand`           | `CreateCommand`            | `".Create"`        |
| `editCommand`             | `EditCommand`              | `".Edit"`          |
| `deleteCommand`           | `DeleteCommand`            | `".Delete"`        |
| `mergeCommand`            | `MergeCommand`             | `".Merge"`         |
| `addCommentField`         | `AddCommentField`          | `"Comment+"`       |
| `removeCommentField`      | `RemoveCommentField`       | `"Comment-"`       |
| —                         | `AddChoreographerField`    | `"Choreographer+"` |
| —                         | `RemoveChoreographerField` | `"Choreographer-"` |
| —                         | `AddStepSheetUrlField`     | `"StepSheetUrl+"`  |
| —                         | `RemoveStepSheetUrlField`  | `"StepSheetUrl-"`  |
| `ownerHash`               | `OwnerHash`                | `"OwnerHash"`      |

---

## 9. Annotated Examples

### 9.1 Simple Song Creation

```
SongId={guid}
.Create=
User=alice
Time=01/15/2024 2:30:00 PM
Title=Summertime
Artist=Ella Fitzgerald
Tempo=60.0
DanceRating=FXT+1
Tag+=Foxtrot:Dance
Tag+=Jazz:Music
Tag+=4/4:Tempo
```

### 9.2 Edit by a Second User

```
...continuation of §9.1...
.Edit=
User=bob
Time=03/01/2024 10:00:00 AM
DanceRating=FXT+1
Tag+=Foxtrot:Dance
```

### 9.3 Service Import + Human Edit (Pseudo User)

```
.Create=
User=alice
Time=01/15/2024 2:30:00 PM
Like=true
.Edit=
User=batch-s
Time=01/15/2024 2:30:00 PM
Title=Summertime
Artist=Ella Fitzgerald
Album:00=Ella & Louis
Track:00=3
Purchase:00:AS=B00123456
```

Here `batch-s` (Spotify) imports metadata. If `alice` later edits `Title`, the human value wins
because `batch-s` is algorithmic (pseudo by email convention) and the bot override rule applies.

### 9.4 Style-Family Vote (Dance Families Feature)

```
.Edit=
User=charlie
Time=04/10/2024 9:00:00 AM
DanceRating=CHA+1
Tag+=Cha Cha:Dance
Tag+:CHA=American:Style|International:Style
```

The `Tag+:CHA=American:Style|International:Style` property adds style-family tags scoped
specifically to the Cha Cha dance rating for this user's vote. The dance rating itself (`CHA+1`)
remains dance-level — it contributes to the total Cha Cha vote count regardless of style family.

### 9.5 Proxy (Pseudo) User Vote

```
.Edit=
User=ArthurMurrays|P
Time=07/30/2023 9:48:04 AM
DanceRating=CHA+1
Tag+=Cha Cha:Dance
Tag+:CHA=American:Style
```

The `|P` suffix marks this as a proxy vote from the Arthur Murray dance studio organization. The
vote counts toward the global dance rating. In the history UI, this appears under `userChanges`
(not filtered out) but `SongChange.isPseudo === true`.

---

## 10. Validation Invariants

The following are enforced by `Song.CheckPropertiesInternal()`:

1. Every `.Create` or `.Edit` block must be followed immediately by `User` and `Time` (in either order).
2. Dance rating deltas must have magnitude 1 or 2 (values `+1`, `-1`, `+2`, `-2`).
3. No `DanceRating` on a song-level `batch|P` block (that is a corrupt import).
4. No genre (`:Music`) tags directly on a `DanceRating` — those belong on the song level.
5. No competition group IDs (e.g. `SWG`, `FXT`) as actual dance ratings — only individual dance types.
