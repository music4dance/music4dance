# Admin Search and Bulk Modify by User

## Overview

The Admin Search feature lets `dbAdmin` users find all songs edited by a specific user within a
date range, review the results, and then bulk-modify the matching edit blocks in a single
background operation. It was originally built to re-attribute 2015 beat-counter algorithm edits
from a personal account (`dwgray`) to the system bot identity (`batch|P`), but the mechanism is
fully general.

---

## Admin Search Page

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

### `SuggestedModifierJson`

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
pseudo/algorithmic. See [song-details-viewing-editing.md](song-details-viewing-editing.md#pseudo-user-suffix-p)
for how this suffix flows from storage to the client UI.

---

## AdminModifyBySearch

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
discarded page-by-page (see `StreamAll` behaviour in [vote-search.md](vote-search.md)).

---

## `SongModifier` Date-Range Fields

The standard `SongModifier` JSON (used in `BatchAdminModify`) has been extended with optional
date-range fields:

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

When `fromDate` / `toDate` are set, `Song.AdminModify` restricts mutations to properties that
belong to edit blocks whose `Time` header falls within the range. Properties in blocks outside
the range are left untouched.

See [bulk-admin-modify.md](bulk-admin-modify.md) for the full `SongModifier` reference.

---

## Post-Filter Search Infrastructure

The GET controller uses `SongIndex.SearchAll` (which collects all matches into a list) because
it needs to display the full result set on one page. The POST background task uses
`SongIndex.StreamAll` directly, discarding non-matching songs page-by-page and flushing index
updates in batches of 100, so the full candidate list is never held in memory at once.

`StreamAll` pages through Azure Search in 1000-song batches (the API maximum). It resets the
OData filter before each page (to prevent `AddCruftInfo` from compounding across pages) and
disables `IncludeTotalCount` for the duration (Azure would otherwise compute a total on every
page request unnecessarily).

See [vote-search.md](vote-search.md) for a description of the shared `PostSearch`/`StreamAll`
pattern used by `VoteSearch` and `EditedBySearch`.

---

## `UserQuery` `modifier: 'a'`

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

| File                                 | Purpose                                                     |
| ------------------------------------ | ----------------------------------------------------------- |
| `m4d/Controllers/AdminController.cs` | `AdminSearch` GET + `AdminModifyBySearch` POST              |
| `m4d/ViewModels/AdminSearchModel.cs` | View model with `SuggestedModifierJson`                     |
| `m4dModels/SongIndex.cs`             | `StreamAll`, `SearchAll`, `AzureParmsFromFilter`            |
| `m4dModels/Song.cs`                  | `WasEditedBy`, `AdminModify`, `GetEditTimestamp`            |
| `m4dModels/SongModifier.cs`          | `SongModifier` with `FromDate`/`ToDate`                     |
| `m4dModels/UserQuery.cs`             | `UserQuery` with `modifier: 'a'`                            |
| `m4d/Views/Admin/AdminSearch.cshtml` | Razor view rendering results and pre-populating modify form |
