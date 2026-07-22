# SongSearch: Search Orchestration Service

## Overview

`SongSearch` (`m4d/Services/SongSearch.cs`) is the layer between `SongController` and `SongIndex`
for every filter-driven search. `SongController.DoAzureSearch()` constructs one per request and
calls `Search()`; see [[song-search-results]] for how the controller actions get there and what
happens to the result afterward (`FormatSongList`, `SongListModel`, Vue rendering). This document
covers what `SongSearch` itself does: premium gating, user-query resolution/anonymization, search
logging, and the three search strategies it can dispatch to (direct Azure query, `VoteSearch`,
`EditedBySearch`). For how `Filter` itself is built (Advanced Search form → wire format → OData
filter/`SearchOptions`), see [[song-filter]].

`SongSearch` is constructed fresh per call (primary constructor, not a singleton/scoped service):

```csharp
public class SongSearch(SongFilter filter, string userName, bool isPremium, SongIndex songIndex,
    UserManager<ApplicationUser> userManager, IBackgroundTaskQueue backgroundTaskQueue,
    ServiceHealthManager serviceHealth = null, int? pageSize = null, bool isAdmin = false)
```

`filter` is **re-parsed** (`songIndex.DanceMusicService.SearchService.GetSongFilter(filter.ToString())`)
rather than stored by reference, so the `SongFilter` instance `SongSearch` mutates internally
(`Filter.User`, `Filter.Page` via `Page()`) never leaks back to the controller's ambient `Filter`.

---

## `Search()` — Entry Point

```csharp
// m4d/Services/SongSearch.cs
public async Task<SearchResults> Search()
```

In order:

1. **Premium gate** — `Filter.Level != null && Filter.Level != 0 && !IsPremium` throws
   `RedirectException("RequiresPremium")`, caught by `ContentController.HandleRedirect`.
2. **User-query resolution** (`Filter.UserQuery`, see [[user-name-visibility]] for the anonymization
   model this builds on):
   - Empty → no-op, search runs unscoped by user.
   - `IsIdentity` (e.g. "my songs") → resolves to the current caller if authenticated, else
     `RedirectException("Login", Filter)`.
   - A different named user → `UserMapper.AnonymizeFilter` rewrites `Filter.User` to an anonymized
     token **unless** the caller `isAdmin` — admins can view any user's filter unscoped, regardless
     of that user's privacy setting.
3. **Azure parameter build** — `SongIndex.AzureParmsFromFilter(await UserMapper.DeanonymizeFilter(Filter, ...), PageSize)`,
   with `IncludeTotalCount = true`. `DeanonymizeFilter` is the inverse of step 2: it resolves any
   anonymized user token in the filter back to a real user ID for the server-side query, after the
   anonymized token has already been chosen for client-facing display.
4. **`LogSearch(Filter)`** — fire-and-forget via `IBackgroundTaskQueue`; see [[saved-searches]] for
   the `Searches` table this writes to (upserts by normalized query string, tracks visit `Count` and
   `MostRecentPage`). Skipped for empty-user filters, `customsearch` action, or unhealthy DB.
5. **Dispatch**:

   | Condition | Strategy |
   | --- | --- |
   | `userQuery.IsVoted && Filter.DanceQuery.Dances.Any()` | `VoteSearch(p)` |
   | otherwise | `SongIndex.Search(Filter.SearchString, p, Filter.CruftFilter)` — direct Azure query |

6. **Error handling** — Azure client errors (`"Azure Search service is unavailable"` or
   `"Client registration requires a TokenCredential"`) are caught, mark `ServiceHealth` unavailable,
   and degrade to an empty `SearchResults` rather than propagating a 500.

`EditedBySearch` is a second strategy with the same `PostSearch` shape as `VoteSearch`, but it is
**not** wired into `Search()`'s dispatch — it's called directly from admin code that already knows
it wants edited-by-user semantics. See [[admin-search-bulk-modify]].

---

## `VoteSearch` — Vote-Based Post-Filter

`VoteSearch` finds songs a user has voted for (up or down) on specific dances. Vote data lives in
song properties and cannot be expressed as an Azure Search OData filter directly, so it's applied
as an in-memory predicate after streaming candidates from Azure.

```csharp
public async Task<SearchResults> VoteSearch(SearchOptions options)
{
    var userQuery = Filter.UserQuery;
    var vote = userQuery.IsUpVoted ? 1 : -1;
    var user = userQuery.IsIdentity ? UserName : userQuery.UserName;
    var voteDances = Dances.Instance
        .ExpandGroups(Filter.DanceQuery.Dances)
        .GroupBy(d => d.Id)
        .Select(g => g.First())
        .ToList();
    return await PostSearch(options,
        s => voteDances.Any(d => s.NormalizedUserDanceRating(user, d.Id) == vote));
}
```

- **Constraint**: only triggered when at least one dance is specified (`Search()`'s dispatch
  condition) — a vote-only query with no dance uses the standard Azure path instead.
- `voteDances` expands dance *groups* (e.g. "Latin") to their member dances and de-duplicates by ID
  before checking each one against the song's rating.
- Vote direction: `IsUpVoted` → score `+1`; otherwise (`IsDownVoted`) → `-1`.
- User resolution: `IsIdentity` → current caller; otherwise the named `UserName` from the query.

## `EditedBySearch` — Edit-Attribution Post-Filter

```csharp
public async Task<SearchResults> EditedBySearch(SearchOptions options, string editorUser,
    DateTime from, DateTime to)
{
    return await PostSearch(options, s => s.WasEditedBy(editorUser, from, to));
}
```

Returns songs where at least one edit block is attributed to `editorUser` with a timestamp in
`[from, to]`. Same underlying mechanism as `VoteSearch` — edit attribution isn't an indexed/filterable
field, so it's checked in memory via `Song.WasEditedBy`. Used by the admin bulk-edit-by-user flow;
see [[admin-search-bulk-modify]] for the controller-level usage.

## `PostSearch` — Shared In-Memory Post-Filter Helper

```csharp
private async Task<SearchResults> PostSearch(SearchOptions options, Func<Song, bool> predicate)
{
    var offset = options.Skip ?? 0;
    var pageSize = options.Size ?? PageSize ?? 25;

    var matched = new List<Song>();
    await foreach (var song in SongIndex.StreamAll(Filter.SearchString, options, Filter.CruftFilter))
    {
        if (predicate(song)) matched.Add(song);
    }

    var page = matched.Skip(offset).Take(pageSize).ToList();
    return new SearchResults(
        Filter.SearchString ?? "", page.Count, matched.Count,
        offset / pageSize + 1, pageSize, page,
        new Dictionary<string, IList<FacetResult>>());
}
```

| Aspect | Detail |
| --- | --- |
| Result set | Unbounded — every song Azure returns for the given filter is inspected |
| Azure pre-filter | Whatever the OData filter already narrows (e.g. dance) is applied server-side first; only the unindexable predicate runs in memory |
| Peak memory | Non-matching songs are released page-by-page (one Azure page of 1000 in transit at a time); matched songs accumulate for the duration of the call — peak memory scales with match count, not total Azure result set |
| Pagination | Offset/page size applied **after** the in-memory filter, so `TotalCount` reflects matched-song count, not the Azure total |
| Error handling | Azure Search unavailability returns an empty `SearchResults` and marks `ServiceHealth` unavailable, same as the direct-query path in `Search()` |

### `SongIndex.StreamAll` — Paging Primitive

`StreamAll` is the paging primitive underlying `PostSearch` (and `SearchAll`, used by admin bulk
operations to collect the full sequence into a `List<Song>`). It yields songs from Azure one page
at a time (max 1000 per request, the Azure API limit):

```csharp
// m4dModels/SongIndex.cs
public virtual async IAsyncEnumerable<Song> StreamAll(
    string search, SearchOptions parameters, CruftFilter cruft = CruftFilter.NoCruft)
{
    const int azurePageSize = 1000;

    var originalFilter = parameters.Filter;
    var originalSize = parameters.Size;
    var originalSkip = parameters.Skip;
    var originalIncludeTotalCount = parameters.IncludeTotalCount;

    parameters.Size = azurePageSize;
    parameters.IncludeTotalCount = false;
    var skip = 0;

    try
    {
        while (true)
        {
            parameters.Filter = originalFilter;  // reset before each page so cruft isn't appended repeatedly
            parameters.Skip = skip;
            var response = await DoSearch(search, parameters, cruft);
            var page = await CreateSongs(response.GetResults());
            if (page == null || page.Count == 0) yield break;
            foreach (var song in page) yield return song;
            if (page.Count < azurePageSize) yield break;
            skip += azurePageSize;
        }
    }
    finally
    {
        parameters.Filter = originalFilter;
        parameters.Size = originalSize;
        parameters.Skip = originalSkip;
        parameters.IncludeTotalCount = originalIncludeTotalCount;
    }
}
```

Key implementation notes:

- `parameters.Filter` is reset before each page because `AddCruftInfo()` (called inside `DoSearch`)
  appends to the filter; without the reset, the cruft clause would compound across pages.
- `IncludeTotalCount` is forced to `false` because Azure computes the total on every page request,
  and `StreamAll`/`PostSearch` never use it.
- All mutated fields are restored in `finally`, so the caller's `SearchOptions` is unchanged after
  the call.

---

## `Page()`

```csharp
public void Page()
{
    if (Filter.Page.HasValue) Filter.Page += 1;
    else Filter.Page = 2;
}
```

Advances the internal `Filter` to the next page. Used by callers that drive `Search()` in a loop
against the same `SongSearch` instance — e.g. `SongController.CreateSpotify` paging through results
25 at a time while building a Spotify playlist.

---

## `LogSearch`

```csharp
internal async Task LogSearch(SongFilter filter)
```

Fire-and-forget background task (`IBackgroundTaskQueue`) that upserts a `Searches` row keyed by
`(ApplicationUserId, Query)`, where `Query` is `filter.Normalize(UserName).ToString()`. Increments
`Count` and updates `Modified`/`MostRecentPage` on repeat visits, or inserts a new row. Skipped
entirely when `filter.IsEmptyUser(UserName)`, `filter.Action == "customsearch"`, or the database is
marked unhealthy. See [[saved-searches]] for the full data model and normalization rules this feeds.

---

## Supporting Types

### `UserQuery` vote modifiers (`m4dModels/UserQuery.cs`)

| Modifier | Meaning |
| --- | --- |
| `d` | Up-voted (`IsUpVoted`) |
| `x` | Down-voted (`IsDownVoted`) |
| `l` | Liked (`IsLike`) |
| `h` | Disliked / blocked (`IsHate`) |
| `a` | Any opinion (`IsAny`) |

`IsVoted` is `true` when the modifier is `d` or `x`.

### `Song.NormalizedUserDanceRating(string userName, string danceId)`

Returns `-1`, `0`, or `+1` based on the user's raw dance rating for the given song and dance.

### `Song.WasEditedBy(string editorUser, DateTime from, DateTime to)`

Scans the song's edit blocks for one attributed to `editorUser` with a `Time` header in range.

---

## Related Code

| File | Purpose |
| --- | --- |
| `m4d/Controllers/SongController.cs` | `DoAzureSearch` constructs `SongSearch` and calls `Search()`; see [[song-search-results]] |
| `m4d/Services/SongSearch.cs` | `Search`, `VoteSearch`, `EditedBySearch`, `PostSearch`, `Page`, `LogSearch` |
| `m4dModels/SongIndex.cs` | `StreamAll`, `SearchAll`, `Search`, `AzureParmsFromFilter` |
| `m4dModels/UserQuery.cs` | Vote modifier parsing (`IsVoted`, `IsUpVoted`, `IsDownVoted`, `IsIdentity`) |
| `m4dModels/Song.cs` | `NormalizedUserDanceRating`, `WasEditedBy` |
| `m4dModels/SongFilter.cs` | `UserQuery` property, `DanceQuery` property, `Normalize` |
| `m4d/Utilities/UserMapper.cs` | `AnonymizeFilter` / `DeanonymizeFilter` |
