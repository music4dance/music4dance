# Plan: Re-attribute 2015 Beat-Counter Edits from `dwgray` to `tempo-bot`

## Background

In 2015 a beat-counter algorithm was run against the personal music collection and the results
were merged into the music4dance catalog. Those edits were committed under the user account
`dwgray`, which means the system treats them as human editorial judgements rather than
algorithmic data. The goal is to re-attribute those edit blocks to `tempo-bot` so the
system can handle them appropriately (e.g., algorithmic confidence weighting, override rules).

The work is split into two phases:

1. **Phase 1** — Build a search that returns only songs that were edited by `dwgray` within a
   specified date range, so the affected songs can be identified and reviewed.
2. **Phase 2** — Extend `BatchAdminModify` so it can change the `User` field inside edit blocks
   that fall within a given date range, and wire up the UI to invoke it.

---

## Phase 1: Filtered Post-Search ("EditedBy" Search)

### Approach

Generalise `VoteSearch` into a reusable `PostSearch` helper that accepts a filter predicate
(`Func<Song, bool>`) instead of hard-wiring vote logic. Then implement a new predicate that
checks whether a song contains an edit block attributed to a given user within a date range.

### Step 1 — Refactor `VoteSearch` into `PostSearch`

**File:** `m4d/Services/SongSearch.cs`

Add a private constant for the batch fetch limit so it is easy to change:

```csharp
private const int PostSearchBatchSize = 500;
```

Extract the generic pattern:

```csharp
// NEW private helper
private async Task<SearchResults> PostSearch(SearchOptions options, Func<Song, bool> predicate)
{
    var offset = options.Skip ?? 0;
    options.Skip = 0;
    options.Size = PostSearchBatchSize;

    SearchResults results;
    try
    {
        results = await SongIndex.Search(Filter.SearchString, options, Filter.CruftFilter);
    }
    catch (InvalidOperationException ex) when (
        ex.Message.Contains("Azure Search service is unavailable") ||
        ex.Message.Contains("Client registration requires a TokenCredential"))
    {
        ServiceHealth?.MarkUnavailable("SearchService", $"Client error: {ex.Message}");
        return new SearchResults(Filter.SearchString ?? "", 0, 0, 1, PageSize ?? 25, [],
            new Dictionary<string, IList<Azure.Search.Documents.Models.FacetResult>>());
    }

    var songs = results.Songs.Where(predicate).ToList();
    return new SearchResults(results, [.. songs.Skip(offset).Take(PageSize ?? 25)], songs.Count);
}
```

Rewrite `VoteSearch` to delegate:

```csharp
public async Task<SearchResults> VoteSearch(SearchOptions options)
{
    var userQuery = Filter.UserQuery;
    var vote = userQuery.IsUpVoted ? 1 : -1;
    var user = userQuery.IsIdentity ? UserName : userQuery.UserName;
    return await PostSearch(options,
        s => Filter.DanceQuery.Dances.Any(d => s.NormalizedUserDanceRating(user, d.Id) == vote));
}
```

### Step 2 — Add `Song.WasEditedBy`

**File:** `m4dModels/Song.cs`

Add a method that inspects edit-block headers using the existing `SongPropertyBlockParser`:

```csharp
/// <summary>
/// Returns true if this song contains at least one edit (or create) block that is
/// attributed to <paramref name="userName"/> and whose timestamp falls within
/// [<paramref name="from"/>, <paramref name="to"/>] (inclusive).
/// </summary>
public bool WasEditedBy(string userName, DateTime from, DateTime to)
{
    var blocks = SongPropertyBlockParser.ParseBlocks(SongProperties,
        a => a == EditCommand || a == CreateCommand);

    return blocks.Any(b =>
        string.Equals(b.User, userName, StringComparison.OrdinalIgnoreCase) &&
        b.Timestamp.HasValue &&
        b.Timestamp.Value >= from &&
        b.Timestamp.Value <= to);
}
```

### Step 3 — Expose `EditedBy` Post-Search from `SongSearch`

**File:** `m4d/Services/SongSearch.cs`

Add a public method that `Search()` can delegate to when the filter signals an "edited by"
query (see Step 4):

```csharp
public async Task<SearchResults> EditedBySearch(SearchOptions options, string editorUser,
    DateTime from, DateTime to)
{
    return await PostSearch(options, s => s.WasEditedBy(editorUser, from, to));
}
```

### Step 4 — Wire into `SongSearch.Search()`

Decide on a filter representation. The simplest approach that does not require schema changes is
to reuse the `UserQuery` `T` modifier (meaning "tagged by / edited by"), combined with a new
date-range sub-filter. Two options:

**Option A (minimal, out-of-band):** Extend `SongSearch.Search()` to detect when `Filter.User`
has the `T` modifier and `Filter.DateRange` (a new nullable `(DateTime From, DateTime To)?` field
on `SongFilter`) is populated, then invoke `EditedBySearch`.

**Option B (URL-encodable):** Encode the date range into the `SongFilter` serialisation format
so it round-trips through query strings (useful for bookmarking or scripted runs).

**Recommendation:** Option A is sufficient for this one-time cleanup task and avoids touching
the `SongFilter` serialisation format. If date-range filtering becomes a recurring need, revisit
with Option B.

Changes needed for Option A:

- Add `DateRangeFrom` and `DateRangeTo` nullable `DateTime?` properties to `SongFilter` (server
  model only; not serialised to Azure).
- In `SongSearch.Search()`, after building `p = SongIndex.AzureParmsFromFilter(...)`, add:

```csharp
if (userQuery.IsTagged && Filter.DateRangeFrom.HasValue && Filter.DateRangeTo.HasValue)
{
    var user = userQuery.IsIdentity ? UserName : userQuery.UserName;
    return await EditedBySearch(p, user, Filter.DateRangeFrom.Value, Filter.DateRangeTo.Value);
}
```

- Add a minimal admin-only search page or use the existing Advanced Search page with query-string
  parameters to build this filter for review purposes.

### Deliverable for Phase 1

A URL such as:

```
/song/search/?user=%2Bdwgray%7CT&dateFrom=2015-01-01&dateTo=2015-12-31
```

returns only songs that have at least one edit block attributed to `dwgray` in 2015.

---

## Phase 2: Re-attribute Edit Blocks via `BatchAdminModify`

### Goal

After reviewing the songs found in Phase 1, re-write the `User=dwgray` property inside those
edit blocks (and only inside blocks whose `Time` falls in 2015) to `User=tempo-bot`.

### Approach

Extend `SongModifier` with an optional date-range constraint. When the range is present,
`Song.AdminModify` restricts its mutations to property blocks whose `Time` header falls within
the range. This is the narrowest possible change with no impact on existing callers.

### Step 1 — Extend `SongModifier`

**File:** `m4dModels/SongModifier.cs`

Add two nullable date fields:

```csharp
public class SongModifier
{
    public List<string> ExcludeUsers { get; set; }
    public List<PropertyModifier> Properties { get; set; }
    public DateTime? FromDate { get; set; }   // NEW
    public DateTime? ToDate { get; set; }     // NEW
}
```

### Step 2 — Extend `Song.AdminModify` with block-scoped filtering

**File:** `m4dModels/Song.cs`

When `songMod.FromDate` or `songMod.ToDate` is set, restrict mutations to properties that
belong to an edit block whose timestamp is within the range.

```csharp
public async Task<bool> AdminModify(string modInfo, DanceMusicCoreService database)
{
    var changed = false;
    var songMod = SongModifier.Build(modInfo);
    ...
    _ = await ExpandTags(database);

    IEnumerable<SongProperty> candidateSource = songMod.FromDate.HasValue || songMod.ToDate.HasValue
        ? GetPropertiesInDateRange(songMod.FromDate, songMod.ToDate, songMod.ExcludeUsers)
        : FilteredProperties(songMod.ExcludeUsers);

    var props = candidateSource.ToList();
    ...
}

private IEnumerable<SongProperty> GetPropertiesInDateRange(
    DateTime? from, DateTime? to, IEnumerable<string> excludeUsers)
{
    var blocks = SongPropertyBlockParser.ParseBlocks(SongProperties,
        a => a == EditCommand || a == CreateCommand);

    var filteredBlocks = blocks.Where(b =>
        (!from.HasValue || (b.Timestamp.HasValue && b.Timestamp.Value >= from.Value)) &&
        (!to.HasValue   || (b.Timestamp.HasValue && b.Timestamp.Value <= to.Value)));

    if (excludeUsers != null)
    {
        var eu = excludeUsers as HashSet<string>
            ?? new HashSet<string>(excludeUsers, StringComparer.OrdinalIgnoreCase);
        filteredBlocks = filteredBlocks.Where(
            b => !eu.Contains(b.User ?? string.Empty));
    }

    return filteredBlocks.SelectMany(b => b.Properties);
}
```

### Step 3 — Add a `BatchAdminModify` invocation for the reattribution

With the extensions above, the following `SongModifier` JSON, submitted via
`BatchAdminModify` against the Phase 1 filter, will change every `User=dwgray` property
in 2015-era edit blocks to `User=tempo-bot`:

```json
{
  "fromDate": "2015-01-01T00:00:00",
  "toDate": "2015-12-31T23:59:59",
  "properties": [
    {
      "action": "ReplaceValue",
      "name": "User",
      "value": "dwgray",
      "replace": "tempo-bot"
    }
  ]
}
```

This JSON can be pasted into the **"Bulk Admin Modify"** form in `AdminFooter` with the
Phase 1 filter active.

### Step 4 (optional) — UI convenience

Optionally add a dedicated **"Reattribute Edits"** form section to `AdminFooter.vue` that
exposes `fromUser`, `toUser`, `fromDate`, and `toDate` fields and constructs the JSON above
automatically, to avoid manual JSON composition.

---

## Summary of Files to Change

| File                                | Change                                                                                  |
| ----------------------------------- | --------------------------------------------------------------------------------------- |
| `m4d/Services/SongSearch.cs`        | Extract `PostSearch`, refactor `VoteSearch`, add `EditedBySearch`, call from `Search()` |
| `m4dModels/Song.cs`                 | Add `WasEditedBy`, add `GetPropertiesInDateRange`, update `AdminModify`                 |
| `m4dModels/SongModifier.cs`         | Add `FromDate` / `ToDate` fields                                                        |
| `m4dModels/SongFilter.cs`           | Add `DateRangeFrom` / `DateRangeTo` server-side fields (not serialised)                 |
| `m4d/Controllers/SongController.cs` | Pass date-range from request into `SongFilter` for the search endpoint                  |
| `m4d/Components/AdminFooter.vue`    | (Optional) Add reattribute form                                                         |

---

## Open Questions

1. **`SongPropertyBlockParser.ParseBlocks` timestamp parsing** — confirm that `SongPropertyBlock.Timestamp`
   is populated by the parser (it has the field; verify via tests that the `Time=MM/dd/yyyy hh:mm:ss tt`
   format is parsed correctly).
2. **Scope of 2015 edits** — do all beat-counter imports fall within a single calendar year, or
   do they span into early 2016? Verify using the Phase 1 search before running Phase 2.
3. **`tempo-bot` user record** — confirm that a `tempo-bot` application user already exists in
   the database (it is referenced in `BatchCorrectTempo`; it creates one with
   `new ApplicationUser("tempo-bot", true)` if not found).
4. **Test coverage** — add unit tests for `Song.WasEditedBy` and the date-range filtering in
   `AdminModify` before running Phase 2 against production data.
