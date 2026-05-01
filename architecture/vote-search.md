# VoteSearch: In-Memory Post-Filter Search

## Overview

`VoteSearch` is a search strategy in `SongSearch` that supplements Azure AI Search's server-side
filtering by streaming all matching songs and applying a custom filter predicate in application
memory. It is used to find songs a user has voted for (up or down) on specific dances, because
vote data embedded in song properties cannot be directly expressed as an Azure Search OData filter.

`VoteSearch` is a specific use of the shared `PostSearch` helper, which also powers
`EditedBySearch`. See [admin-search-bulk-modify.md](admin-search-bulk-modify.md) for the admin
context.

---

## Entry Point

`VoteSearch` is called from `SongSearch.Search()` when both conditions are true:

1. The user query type is voted (`userQuery.IsVoted` — modifier `d` or `x` in the `UserQuery`)
2. At least one dance is specified in the filter (`Filter.DanceQuery.Dances.Any()`)

```csharp
// m4d/Services/SongSearch.cs
if (userQuery.IsVoted && Filter.DanceQuery.Dances.Any())
{
    return await VoteSearch(p);
}
```

---

## Implementation

```csharp
// m4d/Services/SongSearch.cs
public async Task<SearchResults> VoteSearch(SearchOptions options)
{
    var userQuery = Filter.UserQuery;
    var vote = userQuery.IsUpVoted ? 1 : -1;
    var user = userQuery.IsIdentity ? UserName : userQuery.UserName;
    return await PostSearch(options,
        s => Filter.DanceQuery.Dances.Any(d => s.NormalizedUserDanceRating(user, d.Id) == vote));
}

// Shared post-filter helper:
private async Task<SearchResults> PostSearch(SearchOptions options, Func<Song, bool> predicate)
{
    var offset = options.Skip ?? 0;
    var matched = new List<Song>();

    await foreach (var song in SongIndex.StreamAll(
        Filter.SearchString, options, Filter.CruftFilter))
    {
        if (predicate(song)) matched.Add(song);
    }

    var page = matched.Skip(offset).Take(PageSize ?? 25).ToList();
    return new SearchResults(
        Filter.SearchString ?? "", page.Count, matched.Count,
        offset / (PageSize ?? 25) + 1, PageSize ?? 25, page,
        new Dictionary<string, IList<FacetResult>>());
}
```

### Key Design Points

| Aspect           | Detail                                                                                                                                                                                                                        |
| ---------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Result set       | Unbounded — all songs Azure returns for the given filter are inspected                                                                                                                                                        |
| Azure pre-filter | Dance filter is applied by Azure, so only songs for the requested dance(s) are fetched                                                                                                                                        |
| Peak memory      | Non-matching songs are released page-by-page (one Azure page of 1000 in transit at a time); matched songs accumulate in a list for the duration of the call — peak memory scales with match count, not total Azure result set |
| Pagination       | Offset applied after in-memory filter; page size from `options.Size`, falling back to `SongSearch.PageSize`, then 25                                                                                                          |
| Vote direction   | `IsUpVoted` → score `+1`; otherwise score `-1` (down-voted)                                                                                                                                                                   |
| User resolution  | `IsIdentity` resolves to the currently logged-in user; otherwise uses the `UserName` from the query                                                                                                                           |
| Error handling   | Azure Search unavailability returns an empty `SearchResults` and marks service unhealthy via `ServiceHealth`                                                                                                                  |

---

## `SongIndex.StreamAll`

`StreamAll` is the paging primitive underlying `PostSearch`. It yields songs from Azure one
page at a time (max 1000 per request, the Azure API limit):

```csharp
// m4dModels/SongIndex.cs
public virtual async IAsyncEnumerable<Song> StreamAll(
    string search, SearchOptions parameters, CruftFilter cruft = CruftFilter.NoCruft)
{
    const int azurePageSize = 1000;

    // Preserve caller's options — DoSearch (via AddCruftInfo) mutates Filter,
    // and IncludeTotalCount is unneeded for streaming.
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

- `parameters.Filter` is reset before each page because `AddCruftInfo()` (called inside `DoSearch`) appends to the filter; without the reset, the cruft clause would compound across pages.
- `IncludeTotalCount` is forced to `false` because Azure computes the total on every page request, and `StreamAll`/`PostSearch` never use it.
- All mutated fields are restored in `finally`, so the caller's `SearchOptions` is unchanged after the call.

`SearchAll` (used in admin bulk operations) is a simple wrapper over `StreamAll` that collects
the full sequence into a `List<Song>`.

---

## Supporting Types

### `UserQuery` vote modifiers (from `m4dModels/UserQuery.cs`)

| Modifier | Meaning                     |
| -------- | --------------------------- |
| `d`      | Up-voted (IsUpVoted)        |
| `x`      | Down-voted (IsDownVoted)    |
| `l`      | Liked (IsLike)              |
| `h`      | Disliked / blocked (IsHate) |
| `a`      | Any opinion (IsAny)         |

`IsVoted` is `true` when the modifier is `d` or `x`.

### `Song.NormalizedUserDanceRating(string userName, string danceId)`

Returns `-1`, `0`, or `+1` based on the user's raw dance rating for the given song and dance.

---

## Constraints

1. **Single dance requirement**: `VoteSearch` is only triggered when at least one dance is specified.
   Vote-only searches without a dance use the standard Azure path.
2. **Total count**: `TotalCount` in `SearchResults` reflects the number of matched songs, not the
   Azure total — consistent with how pagination is re-applied after the in-memory filter.

---

## Related Code

| File                         | Purpose                                                       |
| ---------------------------- | ------------------------------------------------------------- |
| `m4d/Services/SongSearch.cs` | `VoteSearch`, `PostSearch`, and `Search` entry point          |
| `m4dModels/SongIndex.cs`     | `StreamAll` and `SearchAll`                                   |
| `m4dModels/UserQuery.cs`     | Vote modifier parsing (`IsVoted`, `IsUpVoted`, `IsDownVoted`) |
| `m4dModels/Song.cs`          | `NormalizedUserDanceRating`                                   |
| `m4dModels/SongFilter.cs`    | `UserQuery` property, `DanceQuery` property                   |
