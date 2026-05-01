# Bulk Admin Edit and Bulk Admin Modify

## Overview

The bulk admin operations allow a `dbAdmin` user to apply property changes to every song
that matches a given `SongFilter` in a single background operation. There are two distinct
flavours:

| Endpoint           | Action model                                                                       | Underlying method                                |
| ------------------ | ---------------------------------------------------------------------------------- | ------------------------------------------------ |
| `BatchAdminEdit`   | **Append** raw properties to each song (as a new edit block attributed to a user)  | `SongIndex.AdminAppendSong`                      |
| `BatchAdminModify` | **Structurally mutate** existing properties using a `SongModifier` JSON descriptor | `SongIndex.AdminModifySong` → `Song.AdminModify` |

Both share a common background execution engine: `BatchAdminExecute`.

---

## `BatchAdminExecute` (shared engine)

**Location:** `m4d/Controllers/SongController.cs`

```csharp
private ActionResult BatchAdminExecute(
    SongFilter filter,
    Func<DanceMusicCoreService, Song, Task<bool>> act,
    string name)
```

### Execution flow

1. **Validates** `ModelState` and that the filter is non-empty.
2. Calls `StartAdminTask(name)` to register the operation with `AdminMonitor`.
3. Acquires a **transient (scoped) `DanceMusicCoreService`** via `Database.GetTransientService()`.
4. Fires `Task.Run(...)` so control returns to the caller immediately; the browser is redirected
   to the `AdminStatus` view.
5. Inside the background task:
   - Calls `dms.SongIndex.Search(filter, 2000, CruftFilter.AllCruft)` to fetch up to **2 000 songs**.
   - Iterates over each song, calling `act(dms, song)`.
   - Accumulates `succeeded` / `failed` lists.
   - After the loop, calls `dms.SongIndex.UpdateAzureIndex(succeeded ∪ failed, dms)` once.
   - Reports results through `AdminMonitor.CompleteTask`.
6. The transient service is disposed in `finally`.

### Limits and caveats

- Maximum batch size is **2 000** songs per run (hard-coded).
- The Azure index is updated in a **single bulk call** after all songs are processed — individual
  song failures do not abort the batch.
- The operation runs on a thread-pool thread; it is not cancellable once started.
- Progress is tracked via `AdminMonitor` and visible on the `Admin/AdminStatus` view.

---

## `BatchAdminEdit` — Append Properties

**Location:** `m4d/Controllers/SongController.cs`
**Route:** `POST /Song/BatchAdminEdit`

```csharp
public async Task<ActionResult> BatchAdminEdit(string properties, string user = null)
```

### Parameters

| Parameter    | Source               | Description                                                           |
| ------------ | -------------------- | --------------------------------------------------------------------- |
| `properties` | Form body            | Raw property string to append (tab-delimited `Name=Value` pairs)      |
| `user`       | Form body (optional) | Username to attribute the edit to; defaults to the current admin user |
| _(filter)_   | Query string / form  | Standard `SongFilter` determining which songs to edit                 |

### Behaviour

- Resolves the target user via `Database.FindUser(user ?? UserName)`.
- Appends `properties` to each song via `SongIndex.AdminAppendSong(song, applicationUser, properties)`.
- `AdminAppendSong` → `Song.AdminAppend`: adds a new `.Edit` block with the supplied `User=` and
  auto-generated `Time=` header properties, then saves the song.

### UI

Exposed in `AdminFooter.vue` as the **"Bulk Admin Edit"** form. The form provides:

- A user name field (pre-filled with the current admin's username).
- A free-text properties field.

---

## `BatchAdminModify` — Structural Property Mutation

**Location:** `m4d/Controllers/SongController.cs`
**Route:** `POST /Song/BatchAdminModify`

```csharp
public ActionResult BatchAdminModify(string properties)
```

### Parameters

| Parameter    | Source              | Description                                             |
| ------------ | ------------------- | ------------------------------------------------------- |
| `properties` | Form body           | JSON-serialized `SongModifier` object                   |
| _(filter)_   | Query string / form | Standard `SongFilter` determining which songs to modify |

### Behaviour

1. Eagerly validates the `SongModifier` JSON (via `SongModifier.Build`) and throws
   `ArgumentException` on parse failure before any songs are touched.
2. Calls `BatchAdminExecute` with `dms.SongIndex.AdminModifySong(song, properties)`.

### UI

Exposed in `AdminFooter.vue` as the **"Bulk Admin Modify"** form with a single JSON properties field.

---

## `SongModifier` — Mutation Descriptor

**Location:** `m4dModels/SongModifier.cs`

```csharp
public class SongModifier
{
    public List<string> ExcludeUsers { get; set; }
    public List<PropertyModifier> Properties { get; set; }
    public DateTime? FromDate { get; set; }   // Optional: restrict to edit blocks on/after this date
    public DateTime? ToDate { get; set; }     // Optional: restrict to edit blocks on/before this date
}
```

`SongModifier.Build(string json)` deserialises the JSON and automatically injects additional
`PropertyModifier` entries for tag renaming whenever a `DanceRating` value is replaced (so that
`Tag+:OLD` / `Tag-:OLD` properties are renamed to `Tag+:NEW` / `Tag-:NEW` in tandem).

### `PropertyModifier` actions

| `PropertyAction` | Effect                                                           |
| ---------------- | ---------------------------------------------------------------- |
| `ReplaceValue`   | Replace the `Value` of every matching property                   |
| `ReplaceName`    | Replace the property `Name` (key)                                |
| `Replace`        | Remove the matched property and insert `Properties` in its place |
| `Append`         | Insert `Properties` immediately _after_ the matched property     |
| `Prepend`        | Insert `Properties` immediately _before_ the matched property    |
| `Remove`         | Delete the matched property entirely                             |

A modifier matches a property when:

- `modifier.Name == prop.Name` (case-insensitive), **AND**
- `modifier.Value == prop.Value` (case-insensitive), **OR**
- the property is a `DanceRating` and the value starts with `modifier.Value`, **OR**
- the property is a `Tag*` property and the action is `ReplaceName`.

### `ExcludeUsers`

If specified, `FilteredProperties(ExcludeUsers)` on `Song` is called first, which silently skips
all `SongProperty` objects whose immediate preceding action block is attributed to one of the
excluded users. This prevents algorithmic edits from inadvertently mutating data attributed to
human editors.

### `FromDate` / `ToDate`

If either date field is set, `Song.AdminModify` restricts mutations to properties that belong to
edit blocks whose `Time` header falls within the range (inclusive). Blocks outside the date range
are completely skipped. This is used by `AdminModifyBySearch` to re-attribute only the edits in a
specific time window without touching later human edits on the same songs.

The `AdminSearch` page generates a `SuggestedModifierJson` that pre-populates these fields
from the search date range. See [admin-search-bulk-modify.md](admin-search-bulk-modify.md).

### Pseudo-user value (`batch|P`)

When re-attributing edits to a bot identity, the `replace` value should include the `|P` suffix:

```json
{
  "action": "ReplaceValue",
  "name": "User",
  "value": "dwgray",
  "replace": "batch|P"
}
```

The `|P` suffix marks the edit block as algorithmic. On the client, `ModifiedRecord.fromValue`
parses this suffix into `isPseudo = true`, which prevents the property from being counted as a
human edit. This in turn drives `isSystemTempo` (tempo set only by bots) and controls whether the
pencil-icon tempo override is offered to `canTag` users. See
[song-details-viewing-editing.md](song-details-viewing-editing.md#pseudo-user-suffix-p).

---

## `Song.AdminModify` (property-level execution)

**Location:** `m4dModels/Song.cs`

```csharp
public async Task<bool> AdminModify(string modInfo, DanceMusicCoreService database)
```

1. Builds a `SongModifier` from the JSON string.
2. Calls `ExpandTags(database)` to normalise tag properties before matching.
3. Calls `FilteredProperties(songMod.ExcludeUsers)` to get the candidate property list.
4. For each `PropertyModifier`, scans the candidate list for matches and applies the action.
5. Calls `Reload(SongProperties, database)` then `CollapseTags(database)` to re-index computed
   fields.
6. Returns `true` if at least one property was changed.

---

## Song Property Block Structure

Each edit block within a song's property list begins with an **action property** (`.Create` or
`.Edit`) and is immediately followed by metadata:

```
.Edit=            (action)
User=dwgray       (editor username)
Time=01/15/2015 03:22:00 PM   (edit timestamp)
Tempo=120.0       (changed properties ...)
```

The `SongPropertyBlockParser` class provides `ParseBlocks()` to split a flat property list into
`SongPropertyBlock` instances, each exposing:

- `ActionCommand` (`.Create` or `.Edit`)
- `User` — the editor's username
- `Timestamp` — parsed `DateTime`
- `Properties` — all remaining properties in the block

---

## Related Code

| File                                   | Purpose                                                   |
| -------------------------------------- | --------------------------------------------------------- |
| `m4d/Controllers/SongController.cs`    | `BatchAdminEdit`, `BatchAdminModify`, `BatchAdminExecute` |
| `m4d/Components/AdminFooter.vue`       | UI forms for both batch operations                        |
| `m4dModels/SongModifier.cs`            | `SongModifier` and mutation descriptor                    |
| `m4dModels/PropertyModifier.cs`        | `PropertyModifier` and `PropertyAction` enum              |
| `m4dModels/Song.cs`                    | `AdminModify`, `AdminAppend`, `FilteredProperties`        |
| `m4dModels/SongIndex.cs`               | `AdminAppendSong`, `AdminModifySong`, `AdminEditSong`     |
| `m4dModels/SongPropertyBlockParser.cs` | `ParseBlocks`, `SongPropertyBlock`                        |
| `m4dModels/ChunkedSong.cs`             | `SongChunk` / `ChunkedSong` — chunk-level view of a song  |
