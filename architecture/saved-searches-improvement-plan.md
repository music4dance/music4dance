# Saved Searches Improvement Plan

## Background

See [saved-searches.md](./saved-searches.md) for the current architecture.

This document covers two planned improvements to the saved-search feature:

1. **Remember most recent page** — Track which page the user was on when they last visited a saved search, and use that page when they click the Search button.
2. **Always show the ranking column** — Show the `Count` column on Most Popular view and the `Modified` column on Most Recent view, even when `showDetails` is off.

---

## Phase 0: Get Existing Code Under Test

Before implementing any new functionality, establish integration tests for the existing saved-search code paths. This safety net will catch regressions as the feature work proceeds and clarify the current behaviour (since none of the existing search logic has dedicated tests today — `FunctionalTests.LoadDatabase` only asserts a row count of 16).

---

### 0.1 Search Serialization Tests (`m4dModels.Tests`)

Create `m4dModels.Tests/SearchesTests.cs`. These tests use the existing `DanceMusicTester.CreateServiceWithUsers` / `CreatePopulatedService` infrastructure (EF Core in-memory database) and exercise `DanceMusicService.LoadSearches`, `SerializeSearches`, and `ParseSearchEntry` directly.

**Tests to write:**

| Test                                                    | What it verifies                                                                                                |
| ------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------- |
| `LoadSearches_LoadsAllRecords`                          | Load the 16-record `test-searches.txt` fixture via `CreatePopulatedService`; verify the count equals 16         |
| `LoadSearches_Incremental_UpdatesCountNotDuplicate`     | Load the same search entry twice; the second load should increment `Count`, not add a second row                |
| `LoadSearches_AnonymousUser_UsesNullUserId`             | Load a record with empty username field; `ApplicationUserId` is `null`, row is stored                           |
| `SerializeSearches_RoundTrip`                           | Call `SerializeSearches` then `LoadSearches` on the result; verify row count and key field values are preserved |
| `SerializeSearches_FromDateFilter_ExcludesOlderRecords` | Set a `from` date after some records; verify only recent records appear in the output                           |

**Setup needed:** None beyond what already exists — `DanceMusicTester.CreateServiceWithUsers(name)` and `CreatePopulatedService(name)` are sufficient. No new test infrastructure required for this part.

---

### 0.2 Song Search Log Tests (`m4d.Tests`)

Create `m4d.Tests/Services/SongSearchLogTests.cs`. These tests exercise `SongSearch.LogSearch` end-to-end:

1. Construct a `SongSearch` with `TestBackgroundTaskQueue`.
2. Call `LogSearch`.
3. Execute the enqueued background task against a real in-memory `DanceMusicContext`.
4. Query the context to verify the expected `Search` row was created or updated.

**Infrastructure challenge:** `LogSearch` enqueues a task whose delegate receives `IServiceScopeFactory` as its first argument (the standard `IBackgroundTaskQueue` contract). To execute it in tests, we need a `ServiceProvider` that contains a `DanceMusicContext`. Since EF Core's in-memory provider shares state by database name, we can create a second `ServiceCollection` that registers `DanceMusicContext` under the **same** database name used by `DanceMusicTester.CreateService`, and both contexts will share the same data store.

Add a helper in `m4d.Tests/TestHelpers/` (or inline in the test class for now):

```csharp
private static ServiceProvider BuildTaskServiceProvider(string dbName)
{
    var services = new ServiceCollection();
    services.AddDbContext<DanceMusicContext>(options =>
        options.UseInMemoryDatabase(dbName));
    return services.BuildServiceProvider();
}
```

Call it in tests like:

```csharp
var serviceProvider = BuildTaskServiceProvider(dbName);
await queue.ExecuteAllAsync(serviceProvider);
```

**Tests to write:**

| Test                                        | What it verifies                                                                                                                    |
| ------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------- |
| `LogSearch_FirstVisit_CreatesRow`           | Authenticated user, page 1; one `Search` row created with `Count=1`                                                                 |
| `LogSearch_SecondVisit_IncrementsCount`     | Same user + query called twice; `Count=2` after second call and task execution                                                      |
| `LogSearch_PageOne_StoresNullPage`          | Page 1; no page stored (null / clean storage), baseline for Phase 1 assertion                                                       |
| `LogSearch_AnonymousUser_CurrentBehavior`   | `userId = null`; verify and document whether a row is created — the test name records whatever the actual behaviour turns out to be |
| `LogSearch_CustomsearchAction_SkipsLogging` | Filter with `Action = "customsearch"`; no task enqueued, no row created                                                             |

The `MostRecentPage` assertions (Phase 1) will be added to the existing `LogSearch_SecondVisit_IncrementsCount` and `LogSearch_PageOne_StoresNullPage` tests once the `MostRecentPage` column exists.

---

### 0.3 Running the New Tests

```bash
# Model / serialization tests only:
dotnet test m4dModels.Tests/m4dModels.Tests.csproj

# Controller / service tests only:
dotnet test m4d.Tests/m4d.Tests.csproj

# All tests (excluding SelfCrawler):
dotnet test --filter "FullyQualifiedName!~SelfCrawler"
```

---

## Feature 1: Remember most recent page

### Goal

When a logged-in user is browsing page 3 of a particular search and comes back the next day, the Search button for that saved search should take them directly to page 3 rather than always starting at page 1.

### Constraints

- Page is **not** part of the search identity key. The `Query` column (normalized, page stripped) continues to deduplicate searches. This behavior does not change.
- Only applies to **authenticated users**. Anonymous searches do not track page.
- `MostRecentPage` of `1` or `null` both mean "start from the beginning" and should link to the first page (i.e., no page parameter in the URL, or page=1 — either is fine).

---

### 1.1 Database Change

Add a nullable integer column `MostRecentPage` to the `Searches` table.

**Default**: `null` for all existing rows (semantically equivalent to page 1).

#### EF Core migration

Create a new migration named `SearchMostRecentPage`:

```bash
dotnet ef migrations add SearchMostRecentPage \
  --project m4dModels \
  --startup-project m4d
```

The generated migration will look like:

```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    migrationBuilder.AddColumn<int>(
        name: "MostRecentPage",
        table: "Searches",
        type: "int",
        nullable: true);
}

protected override void Down(MigrationBuilder migrationBuilder)
{
    migrationBuilder.DropColumn(
        name: "MostRecentPage",
        table: "Searches");
}
```

After adding the migration, update `DanceMusicContextModelSnapshot.cs` automatically.

#### Search.cs change

Add the property to the entity:

```csharp
public int? MostRecentPage { get; set; }
```

No `DanceMusicContext.OnModelCreating` configuration needed — nullable `int?` defaults to a nullable column.

---

### 1.2 Search Logging Change (`SongSearch.cs`)

In `LogSearch`, the current filter (before normalization) has the real page number. When updating an existing row we should capture the current page.

**Change**: When updating an existing `Search` row, also write the page from the **original** (pre-normalization) filter to `MostRecentPage`. Only write a page value when `userId != null` (authenticated users only). Page `1` and `null` are equivalent — store `null` when the value is ≤ 1 or absent, to keep the column clean.

```csharp
// Inside the BackgroundTaskQueue lambda, in the "if (old != null)" branch:
old.Modified = now;
old.Count += 1;
old.MostRecentPage = (mostRecentPage.HasValue && mostRecentPage.Value > 1) ? mostRecentPage : null;
```

The `mostRecentPage` value should be captured before the lambda (from `filter.Page`) since `filter` should not be captured by reference in the closure.

**No change** to the new-record path — new rows start with `MostRecentPage = null`.

---

### 1.3 View Change (`_SearchesCore.cshtml`)

Currently the Search button uses:

```cshtml
var t = item.Filter;   // Deserialized from Query — page is always null
```

Change to set the page on the filter before building the link:

```cshtml
var t = item.Filter;
if (item.MostRecentPage.HasValue && item.MostRecentPage.Value > 1)
{
    t.Page = item.MostRecentPage;
}
if (t.IsAzure == false)
{
    t.Action = "Advanced";
}
```

This ensures the Search button URL includes the page parameter when `MostRecentPage > 1`.

---

### 1.4 Serialization / Import-Export

The backup format in `DanceMusicService.SerializeSearches` is a **positional** tab-delimited line. Adding `MostRecentPage` requires extending this format.

**Current format** (7 fields):

```
userName\tname\tquery\tfavorite\tcount\tcreated\tmodified
```

**New format** (8 fields — append `MostRecentPage` at end):

```
userName\tname\tquery\tfavorite\tcount\tcreated\tmodified\tMostRecentPage
```

`ParseSearchEntry` must be updated to read the optional 8th field (to remain backward-compatible with existing backup data that has only 7 fields).

`SerializeSearches` must be updated to emit the 8th field.

When loading 7-field records (old backups), `MostRecentPage` is left as `null`.

---

## Feature 2: Always Show Ranking Column

### Goal

When browsing My Searches without `showDetails`, the table currently shows only a single column with the search description. Users navigating by Most Popular can't see how popular each search is, and users on Most Recent can't see when they last ran each search.

**Change**: Always show one extra column alongside the search description:

- **Most Popular** view (default / `sort != "recent"`): show `Count`
- **Most Recent** view (`sort == "recent"`): show `Modified`

The `showDetails` flag continues to control all the other columns (Query, User, Created, and the other non-active ranking column).

---

### 2.1 View Change (`_SearchesCore.cshtml`)

Read the `sort` value (already available in the partial) and conditionally render a second column.

**Header row** — add a column header when `showDetails = false`:

```cshtml
@if (!showDetails)
{
    if (string.Equals(sort, "recent"))
    {
        <th scope="col">@Html.DisplayNameFor(model => model.Modified)</th>
    }
    else
    {
        <th scope="col">@Html.DisplayNameFor(model => model.Count)</th>
    }
}
```

**Row cells** — add the corresponding data cell when `showDetails = false`:

```cshtml
@if (!showDetails)
{
    if (string.Equals(sort, "recent"))
    {
        <td>@Html.DisplayFor(modelItem => item.Modified)</td>
    }
    else
    {
        <td>@Html.DisplayFor(modelItem => item.Count)</td>
    }
}
```

This requires making `sort` available inside the partial. It is already read from `ViewData` at the top of the partial:

```cshtml
var sort = ViewData.ContainsKey("Sort") ? ViewBag.Sort : null;
```

So no additional plumbing is needed — `sort` is already in scope.

Note: the `_SearchesCore` partial is also used from `_userDetails.cshtml` (admin view), where `sort` will be `null`, resulting in the Count column being shown by default — which is the correct behavior for that context.

---

## Files to Change

| File                                                                                | Change                                                                                                     |
| ----------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------- |
| [`m4dModels/Search.cs`](../m4dModels/Search.cs)                                     | Add `public int? MostRecentPage { get; set; }` property                                                    |
| [`m4dModels/Migrations/`](../m4dModels/Migrations/)                                 | Add `SearchMostRecentPage` migration (generated by EF tooling)                                             |
| [`m4dModels/DanceMusicService.cs`](../m4dModels/DanceMusicService.cs)               | Update `SerializeSearches` (emit `MostRecentPage`) and `ParseSearchEntry` (read optional `MostRecentPage`) |
| [`m4d/Services/SongSearch.cs`](../m4d/Services/SongSearch.cs)                       | Capture `filter.Page` before lambda, update `old.MostRecentPage` in existing-record branch                 |
| [`m4d/Views/Shared/_SearchesCore.cshtml`](../m4d/Views/Shared/_SearchesCore.cshtml) | Set `t.Page` from `item.MostRecentPage` (Feature 1); add conditional column header + cell (Feature 2)      |

---

## Database Migrations

Migrations run **automatically at startup** via a background `Task.Run` in `Program.cs` (after a 2-second delay to let the app begin accepting HTTP requests). This applies to all environments — local dev, staging, and production. In development, role-seeding also runs after migrations.

The relevant startup code (see `m4d/Program.cs`):

```csharp
_ = Task.Run(async () =>
{
    await Task.Delay(TimeSpan.FromSeconds(2));
    // ...
    db.Migrate();   // runs pending migrations in all environments

    if (isDevelopment)
        await UserManagerHelpers.SeedData(...);   // dev only
});
```

**Deploying this change**: just deploy the app normally. The `SearchMostRecentPage` migration will be applied on first startup against each database. No manual SQL scripts or `dotnet ef database update` steps are needed.

### Generating a new migration (for future schema changes)

```bash
dotnet ef migrations add <MigrationName> --project m4dModels --startup-project m4d
```

The generated migration is picked up automatically on next deploy.

### Rollback

If a migration must be reversed after deployment, run the EF Down method manually:

```bash
dotnet ef database update <PreviousMigrationName> --project m4dModels --startup-project m4d
```

Then remove the migration file from source and redeploy.

---

## Testing Plan

### Server-side

- Add integration tests in `m4d.Tests` that verify:
  - `LogSearch` records `MostRecentPage = 3` when called with a filter on page 3 (authenticated user).
  - `LogSearch` does not update `MostRecentPage` for anonymous users.
  - Revisiting a search on page 1 sets `MostRecentPage` to `null` (or 1 treated as null).
  - Serialization round-trip: a `Search` with `MostRecentPage = 5` serializes and deserializes correctly.
  - Loading a 7-field (old format) backup line sets `MostRecentPage = null` (backward compatibility).

### Manual / browser

- Log in, search for a dance, page to page 3, navigate away, go to My Searches — the Search button should return you to page 3.
- Check Most Popular view (no details): a Count column should appear.
- Check Most Recent view (no details): a Modified column should appear.
- Enable showDetails: all columns should appear as before, no duplication.
- Verify the admin `_userDetails` page still renders correctly (Count column shown by default).

---

## Open Questions / Decisions

1. **MostRecentPage on new records**: Should a brand-new search record (first visit) store the page, or only store it on updates? The current plan stores `null` on creation (first visit always starts at page 1 by definition) and only updates on subsequent visits. An alternative is to always store it. Either is straightforward to implement.

2. **Page in Spotify link**: The `Spotify` icon links to a playlist, not the search — page has no relevance there. No change needed.

3. **Sort parameter in `_userDetails` context**: The partial is called without setting `ViewBag.Sort` in `_userDetails`. With the proposed change, `sort` will be `null` there, which maps to the "Most Popular" behavior (Count column). This is consistent with the fact that the admin view always sorts by Count. No extra work needed.
