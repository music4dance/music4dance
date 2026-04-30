# VoteSearch: Client-Side Post-Filter Search

## Overview

`VoteSearch` is a search strategy in `SongSearch` that supplements Azure AI Search's server-side
filtering by fetching a large batch of songs and applying a custom filter predicate in application
memory. It is used today to find songs a user has voted for (up or down) on specific dances, because
vote data embedded in song properties cannot be directly expressed as an Azure Search OData filter.

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
    var offset = options.Skip ?? 0;
    options.Skip = 0;
    options.Size = 500;          // Fetch a large batch

    // 1. Fetch up to 500 songs from Azure Search using the regular filter
    var results = await SongIndex.Search(Filter.SearchString, options, Filter.CruftFilter);

    // 2. Determine vote direction and user identity
    var userQuery = Filter.UserQuery;
    var vote = userQuery.IsUpVoted ? 1 : -1;
    var user = userQuery.IsIdentity ? UserName : userQuery.UserName;

    // 3. Apply the in-memory filter predicate
    var songs = results.Songs
        .Where(s => Filter.DanceQuery.Dances
            .Any(d => s.NormalizedUserDanceRating(user, d.Id) == vote))
        .ToList();

    // 4. Re-apply pagination from the original offset
    return new SearchResults(results, [.. songs.Skip(offset).Take(options.Size ?? 25)], songs.Count);
}
```

### Key Design Points

| Aspect | Detail |
|---|---|
| Batch size | 500 (hard-coded) |
| Pagination | Offset applied after in-memory filter; `Size` in the returned results uses the original `options.Size` (default 25) |
| Vote direction | `IsUpVoted` → score `+1`; otherwise score `-1` (down-voted) |
| User resolution | `IsIdentity` resolves to the currently logged-in user; otherwise uses the `UserName` from the query |
| Error handling | Azure Search unavailability returns an empty `SearchResults` and marks service unhealthy via `ServiceHealth` |

---

## Supporting Types

### `UserQuery` vote modifiers (from `m4dModels/UserQuery.cs`)

| Modifier | Meaning |
|---|---|
| `d` | Up-voted (IsUpVoted) |
| `x` | Down-voted (IsDownVoted) |
| `l` | Liked (IsLike) |
| `h` | Disliked / blocked (IsHate) |
| `a` | Any opinion (IsAny) |

`IsVoted` is `true` when the modifier is `d` or `x`.

### `Song.NormalizedUserDanceRating(string userName, string danceId)`

Returns `-1`, `0`, or `+1` based on the user's raw dance rating for the given song and dance.

---

## Limitations

1. **Maximum result set**: Only songs in the first 500 Azure Search results are eligible. Songs ranked
   beyond position 500 are silently excluded even if they match the vote filter.
2. **No total count**: The `songs.Count` passed to `SearchResults` is the post-filter count, but
   Azure Search's `TotalCount` still reflects the unfiltered batch.
3. **Single dance requirement**: `VoteSearch` is only triggered when at least one dance is specified.
   Vote-only searches without a dance use the standard Azure path.
4. **Pagination limit**: Because pagination is applied after the in-memory filter, deep pages may
   be empty if the batch of 500 does not contain enough matching songs.

---

## Related Code

| File | Purpose |
|---|---|
| `m4d/Services/SongSearch.cs` | `VoteSearch` and `Search` entry point |
| `m4dModels/UserQuery.cs` | Vote modifier parsing (`IsVoted`, `IsUpVoted`, `IsDownVoted`) |
| `m4dModels/Song.cs` | `NormalizedUserDanceRating` |
| `m4dModels/SongFilter.cs` | `UserQuery` property, `DanceQuery` property |
