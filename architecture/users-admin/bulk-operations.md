# Bulk Song Operations (Admin)

**Type:** Reference
**Status:** Current
**Last verified:** 2026-10-01 (code references checked; behavior not re-traced)
**Code:** `m4d/Controllers/SongController.cs` (`BatchAdminExecute`, `BatchAdminEdit`, `BatchAdminModify`),
`m4d/Controllers/AdminController.cs` (`AdminSearch`, `AdminModifyBySearch`, `AdminRefreshBySearch`),
`m4dModels/SongModifier.cs`, `m4dModels/Song.cs` (`AdminModify`),
`m4d/ClientApp/src/components/AdminFooter.vue`

`dbAdmin` tools that change many songs in one background operation. There are two ways to choose
the songs:

- **By `SongFilter`** (any song search): `BatchAdminEdit` appends properties, and
  `BatchAdminModify` mutates existing ones with a `SongModifier`.
- **By who edited, and when** (Admin Search): `AdminModifyBySearch` and `AdminRefreshBySearch`
  act on exactly the edit blocks a user made in a date range.

Both paths share the `SongModifier` descriptor and `Song.AdminModify`.

## Filter-driven bulk operations

The bulk admin operations allow a `dbAdmin` user to apply property changes to every song
that matches a given `SongFilter` in a single background operation. There are two distinct
flavours:

| Endpoint           | Action model                                                                       | Underlying method                                |
| ------------------ | ---------------------------------------------------------------------------------- | ------------------------------------------------ |
| `BatchAdminEdit`   | **Append** raw properties to each song (as a new edit block attributed to a user)  | `SongIndex.AdminAppendSong`                      |
| `BatchAdminModify` | **Structurally mutate** existing properties using a `SongModifier` JSON descriptor | `SongIndex.AdminModifySong` → `Song.AdminModify` |

Both share a common background execution engine: `BatchAdminExecute`.

---

### `BatchAdminExecute` (shared engine)

**Location:** `m4d/Controllers/SongController.cs`

```csharp
private ActionResult BatchAdminExecute(
    SongFilter filter,
    Func<DanceMusicCoreService, Song, Task<bool>> act,
    string name)
```

#### Execution flow

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

#### Limits and caveats

- Maximum batch size is **2 000** songs per run (hard-coded).
- The Azure index is updated in a **single bulk call** after all songs are processed — individual
  song failures do not abort the batch.
- The operation runs on a thread-pool thread; it is not cancellable once started.
- Progress is tracked via `AdminMonitor` and visible on the `Admin/AdminStatus` view.

---

### `BatchAdminEdit` — Append Properties

**Location:** `m4d/Controllers/SongController.cs`
**Route:** `POST /Song/BatchAdminEdit`

```csharp
public async Task<ActionResult> BatchAdminEdit(string properties, string user = null)
```

#### Parameters

| Parameter    | Source               | Description                                                           |
| ------------ | -------------------- | --------------------------------------------------------------------- |
| `properties` | Form body            | Raw property string to append (tab-delimited `Name=Value` pairs)      |
| `user`       | Form body (optional) | Username to attribute the edit to; defaults to the current admin user |
| _(filter)_   | Query string / form  | Standard `SongFilter` determining which songs to edit                 |

#### Behaviour

- Resolves the target user via `Database.FindUser(user ?? UserName)`.
- Appends `properties` to each song via `SongIndex.AdminAppendSong(song, applicationUser, properties)`.
- `AdminAppendSong` → `Song.AdminAppend`: adds a new `.Edit` block with the supplied `User=` and
  auto-generated `Time=` header properties, then saves the song.

#### UI

Exposed in `AdminFooter.vue` as the **"Bulk Admin Edit"** form. The form provides:

- A user name field (pre-filled with the current admin's username).
- A free-text properties field.

---

### `BatchAdminModify` — Structural Property Mutation

**Location:** `m4d/Controllers/SongController.cs`
**Route:** `POST /Song/BatchAdminModify`

```csharp
public ActionResult BatchAdminModify(string properties)
```

#### Parameters

| Parameter    | Source              | Description                                             |
| ------------ | ------------------- | ------------------------------------------------------- |
| `properties` | Form body           | JSON-serialized `SongModifier` object                   |
| _(filter)_   | Query string / form | Standard `SongFilter` determining which songs to modify |

#### Behaviour

1. Eagerly validates the `SongModifier` JSON (via `SongModifier.Build`) and throws
   `ArgumentException` on parse failure before any songs are touched.
2. Calls `BatchAdminExecute` with `dms.SongIndex.AdminModifySong(song, properties)`.

#### UI

Exposed in `AdminFooter.vue` as the **"Bulk Admin Modify"** form with a single JSON properties field.

---

### `SongModifier` — Mutation Descriptor

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

#### `PropertyModifier` actions

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

#### `ExcludeUsers`

If specified, `FilteredProperties(ExcludeUsers)` on `Song` is called first, which silently skips
all `SongProperty` objects whose immediate preceding action block is attributed to one of the
excluded users. This prevents algorithmic edits from inadvertently mutating data attributed to
human editors.

#### `FromDate` / `ToDate`

If either date field is set, `Song.AdminModify` restricts mutations to properties that belong to
edit blocks whose `Time` header falls within the range (inclusive). Blocks outside the date range
are completely skipped. This is used by `AdminModifyBySearch` to re-attribute only the edits in a
specific time window without touching later human edits on the same songs.

Example (re-attribute 2015 edits by one user to the batch pseudo-user):

```json
{
  "fromDate": "2015-01-01T00:00:00",
  "toDate": "2015-12-31T23:59:59",
  "properties": [
    {
      "action": "ReplaceValue",
      "name": "User",
      "value": "dwgray",
      "replace": "batch|P"
    }
  ]
}
```

The Admin Search page generates a `SuggestedModifierJson` that pre-populates these fields from the
search date range (see [Admin Search and modify-by-search](#admin-search-and-modify-by-search)).

#### Pseudo-user value (`batch|P`)

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
[song-details-viewing-editing.md](../songs/song-details-viewing-editing.md#pseudo-user-suffix-p).

---

### `Song.AdminModify` (property-level execution)

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

### Song Property Block Structure

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

## Admin Search and modify-by-search

The Admin Search feature lets `dbAdmin` users find all songs edited by a specific user within a
date range, review the results, and then bulk-modify the matching edit blocks in a single
background operation. It was originally built to re-attribute 2015 beat-counter algorithm edits
from a personal account (`dwgray`) to the system bot identity (`batch|P`), but the mechanism is
fully general.

---

### Admin Search Page

**Route:** `GET /Admin/AdminSearch`
**View model:** `m4d/ViewModels/AdminSearchModel.cs`

The form accepts:

| Field               | Meaning                             |
| ------------------- | ----------------------------------- |
| `EditedBy.UserName` | Username whose edit blocks to find  |
| `EditedBy.From`     | Start of the date range (inclusive) |
| `EditedBy.To`       | End of the date range (inclusive)   |

When all three fields are supplied, the controller:

1. Builds a `SongFilter` constrained to the target user via `UserQuery(userName, include: true, modifier: 'a')`.
   The `'a'` modifier generates an OData filter matching any appearance of the user (ratings, likes, hates).
2. Calls `SongIndex.SearchAll(null, options, CruftFilter.AllCruft)` — which pages through Azure in 1000-song
   batches via `StreamAll` — to retrieve all songs the user has interacted with.
3. Post-filters in memory with `Song.WasEditedBy(userName, from, to)` to find songs with an edit block
   attributed to that user within the date range.
4. Populates `model.EditedBy.Results` (a list of `EditedBySongResult` with `Song` + `EditedAt`).

```csharp
// m4d/Controllers/AdminController.cs — GET AdminSearch
var userFilter = SongFilter.Create(false);
userFilter.User = new UserQuery(model.EditedBy.UserName, include: true, modifier: 'a').Query;
var options = SongIndex.AzureParmsFromFilter(userFilter);
var allSongs = await SongIndex.SearchAll(null, options, CruftFilter.AllCruft);
model.EditedBy.Results = allSongs
    .Select(s => (song: s,
                  ts: s.GetEditTimestamp(model.EditedBy.UserName,
                                        model.EditedBy.From.Value,
                                        model.EditedBy.To.Value)))
    .Where(t => t.ts.HasValue)
    .OrderByDescending(t => t.ts.Value)
    .Select(t => new EditedBySongResult { Song = t.song, EditedAt = t.ts.Value })
    .ToList();
```

#### `SuggestedModifierJson`

The view model exposes a `SuggestedModifierJson` property that pre-populates the **Bulk Admin
Modify** form with a `SongModifier` JSON that re-attributes the found edit blocks:

```csharp
public string SuggestedModifierJson =>
    HasSearch
        ? $$"""
           {
             "fromDate": "{{From:yyyy-MM-ddTHH:mm:ss}}",
             "toDate": "{{To:yyyy-MM-ddTHH:mm:ss}}",
             "properties": [
               {
                 "action": "ReplaceValue",
                 "name": "User",
                 "value": "{{UserName}}",
                 "replace": "batch|P"
               }
             ]
           }
           """
        : null;
```

The `"replace": "batch|P"` value is intentional: the `|P` suffix marks the edit block as
pseudo/algorithmic. See [song-details-viewing-editing.md](../songs/song-details-viewing-editing.md#pseudo-user-suffix-p)
for how this suffix flows from storage to the client UI.

---

### AdminModifyBySearch

**Route:** `POST /Admin/AdminModifyBySearch`
**Controller:** `m4d/Controllers/AdminController.cs`

Accepts the same user/date-range form fields plus a `properties` JSON string (a `SongModifier`).
Runs as a background task:

1. Re-fetches songs for the user by streaming `StreamAll` + `WasEditedBy` in-flight.
2. Calls `Song.AdminModify(properties, dms)` on each matched song.
3. Flushes matched songs to the Azure index in batches of 100 as they are processed.

```csharp
var userFilter = SongFilter.Create(false);
userFilter.User = new UserQuery(userName, include: true, modifier: 'a').Query;
var searchOptions = dms.SongIndex.AzureParmsFromFilter(userFilter);

var indexBatch = new List<Song>();
await foreach (var song in dms.SongIndex.StreamAll(null, searchOptions, CruftFilter.AllCruft))
{
    if (!song.WasEditedBy(userName, capturedFrom, capturedTo)) continue;
    if (await dms.SongIndex.AdminModifySong(song, modifierJson))
        succeededSongIds.Add(song.SongId);
    else
        failedCount++;
    indexBatch.Add(song);
    if (indexBatch.Count >= 100) { await FlushBatchAsync(); }
}
await FlushBatchAsync();
```

This avoids materialising the full candidate list in memory — songs not matching `WasEditedBy` are
discarded page-by-page (see `StreamAll` behaviour in [song-search-service.md](../search/song-search-service.md)).

---

### AdminRefreshBySearch

**Route:** `POST /Admin/AdminRefreshBySearch`
**Controller:** `m4d/Controllers/AdminController.cs`

Same matching/streaming/batching shape as `AdminModifyBySearch`, but instead of applying a
`SongModifier`, it calls `SongIndex.RefreshSong(song)` — which just runs `Song.Reload(database)`
on the song's own existing properties — and pushes the result to the Azure index. No edit block
is added and no property values change; this only re-runs the in-memory parsing/derivation logic
(ratings, tags, etc.) and re-serializes the result, which is useful after a parsing-logic change
that should retroactively apply to already-indexed songs.

The "Refresh" button sits next to "Execute Modify" on the Admin Search page and posts to this
action with just `userName`/`from`/`to` — no modifier JSON required.

Note: a `SongModifier` JSON that replaces a value with itself (e.g. `User: "music4dance"` →
`replace: "music4dance"`) would *also* trigger a reload+reindex under `AdminModifyBySearch`,
because `Song.AdminModify` always calls `Reload` unconditionally and the controller always adds
the song to the index batch regardless of whether anything matched. That behavior is incidental,
though — it depends on `AdminModify`'s internals (which also run `ExpandTags`/`CollapseTags`
unnecessarily) and isn't guaranteed to remain true. `AdminRefreshBySearch` is the explicit,
intentional way to do a pure refresh.

---

### Post-Filter Search Infrastructure

The GET controller uses `SongIndex.SearchAll` (which collects all matches into a list) because
it needs to display the full result set on one page. The POST background task uses
`SongIndex.StreamAll` directly, discarding non-matching songs page-by-page and flushing index
updates in batches of 100, so the full candidate list is never held in memory at once.

`StreamAll` pages through Azure Search in 1000-song batches (the API maximum). It resets the
OData filter before each page (to prevent `AddCruftInfo` from compounding across pages) and
disables `IncludeTotalCount` for the duration (Azure would otherwise compute a total on every
page request unnecessarily).

See [song-search-service.md](../search/song-search-service.md) for a description of the shared `PostSearch`/`StreamAll`
pattern used by `VoteSearch` and `EditedBySearch`, and of `SongSearch.Search()` more generally.

---

### `UserQuery` `modifier: 'a'`

The `'a'` modifier on `UserQuery` generates an OData filter that matches the user in any capacity:

```
Users/any(t: t eq 'username') or
Users/any(t: t eq 'username|l') or
Users/any(t: t eq 'username|h') or
Users/any(t: t eq 'username|d') or
Users/any(t: t eq 'username|x')
```

This ensures songs are found even if the user only voted (and never created/edited a block),
which is important because `WasEditedBy` then performs the accurate in-memory check.

---

## Related Code

| File                                   | Purpose                                                   |
| -------------------------------------- | --------------------------------------------------------- |
| `m4d/Controllers/SongController.cs`    | `BatchAdminEdit`, `BatchAdminModify`, `BatchAdminExecute` |
| `m4d/ClientApp/src/components/AdminFooter.vue`       | UI forms for both batch operations                        |
| `m4dModels/SongModifier.cs`            | `SongModifier` and mutation descriptor                    |
| `m4dModels/PropertyModifier.cs`        | `PropertyModifier` and `PropertyAction` enum              |
| `m4dModels/Song.cs`                    | `AdminModify`, `AdminAppend`, `FilteredProperties`        |
| `m4dModels/SongIndex.cs`               | `AdminAppendSong`, `AdminModifySong`, `AdminEditSong`     |
| `m4dModels/SongPropertyBlockParser.cs` | `ParseBlocks`, `SongPropertyBlock`                        |
| `m4dModels/ChunkedSong.cs`             | `SongChunk` / `ChunkedSong` — chunk-level view of a song  |

| File                                 | Purpose                                                               |
| ------------------------------------ | --------------------------------------------------------------------- |
| `m4d/Controllers/AdminController.cs` | `AdminSearch` GET + `AdminModifyBySearch`/`AdminRefreshBySearch` POST |
| `m4d/ViewModels/AdminSearchModel.cs` | View model with `SuggestedModifierJson`                               |
| `m4dModels/SongIndex.cs`             | `StreamAll`, `SearchAll`, `AzureParmsFromFilter`, `RefreshSong`       |
| `m4dModels/Song.cs`                  | `WasEditedBy`, `AdminModify`, `Reload`, `GetEditTimestamp`            |
| `m4dModels/SongModifier.cs`          | `SongModifier` with `FromDate`/`ToDate`                               |
| `m4dModels/UserQuery.cs`             | `UserQuery` with `modifier: 'a'`                                      |
| `m4d/Views/Admin/AdminSearch.cshtml` | Razor view rendering results and pre-populating modify form           |

## History

- 2026-10-01: Merged from `bulk-admin-modify.md` and `admin-search-bulk-modify.md`. The
  duplicated `SongModifier` date-range section was folded into the `FromDate` / `ToDate`
  subsection.
