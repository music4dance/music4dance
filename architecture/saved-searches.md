# Saved Searches Architecture

## Overview

Saved searches allow authenticated (and optionally anonymous) users to have their song search queries automatically saved, tracked for how often they are revisited, and displayed in a "My Searches" page. The feature lets dancers quickly return to searches they perform frequently.

---

## Data Model

**Entity**: `m4dModels.Search` ([Search.cs](../m4dModels/Search.cs))

| Column              | Type                    | Notes                                                                   |
| ------------------- | ----------------------- | ----------------------------------------------------------------------- |
| `Id`                | `long` (PK, identity)   | Auto-increment                                                          |
| `ApplicationUserId` | `string` (FK, nullable) | `null` for anonymous users                                              |
| `Name`              | `string` (nullable)     | User-defined label (currently unused in UI)                             |
| `Query`             | `string` (required)     | Normalized serialized `SongFilter` string — **page is always stripped** |
| `Favorite`          | `bool`                  | Reserved for future use                                                 |
| `Count`             | `int`                   | Number of times this search has been visited                            |
| `Created`           | `DateTime`              | Timestamp of first visit                                                |
| `Modified`          | `DateTime`              | Timestamp of most recent visit                                          |

**Non-mapped (runtime) properties**:

- `Filter` — Deserializes `Query` back to a `SongFilter` object via `SongFilter.Create(false, Query)`
- `Spotify` — Populated at display time if the user has a Spotify playlist linked to this filter

**Database table name**: `Searches`
**Declared in**: `DanceMusicContext.OnModelCreating` — `builder.Entity<Search>().ToTable("Searches")`

---

## The Identity Key: Normalized Query String

The `Query` field is the business identity for a saved search. Two searches are considered the same if their normalized filter strings are equal.

Normalization is performed by `SongFilter.Normalize(userName)` ([SongFilter.cs](../m4dModels/SongFilter.cs)):

1. Action is set to `"Advanced"` if it was `"index"`.
2. The user field is handled based on `UserQuery.IsDefault(userName)`:
   - **Stripped (set to null)** only when the filter is the default "exclude my don't-liked songs" pattern — i.e., the modifier is `h` (hate/don't-like) **and** the username is either `"me"` or matches the current user. This is the baseline filter applied to every logged-in session (`SongFilter.GetDefault`) and carries no meaningful intent worth saving.
   - **Preserved as-is** for every other user-specific filter: likes (`l`), votes (`d`/`x`), any-opinion (`a`), filters referencing other users' usernames, etc. These represent real intentional filters and must survive normalization so the saved search continues to work correctly.
   - This design is correct: user-vote/like filters are meaningful and would produce nonsensical results with the wrong or absent username, so they are never stripped.
3. **`Page` is set to `null`** — page number is intentionally excluded from the identity key so that browsing page 3 of a search produces the same saved-search record as browsing page 1.

The lookup key in the database is always `(ApplicationUserId, Query)`.

---

## How Searches Are Logged

Logging is triggered inside `SongSearch.LogSearch()` ([SongSearch.cs](../m4d/Services/SongSearch.cs)) on every call to `SongSearch.Search()`.

### Conditions for skipping

- The filter has an "empty user" context (`filter.IsEmptyUser(userName)`)
- The filter action is `"customsearch"`

### For authenticated users

1. Normalize the filter (strips page, resolves identity).
2. Look up an existing `Search` row by `(ApplicationUserId, normalized Query)`.
3. **If found**: increment `Count` and update `Modified` to now.
4. **If not found**: insert a new row with `Count = 1`, `Created = now`, `Modified = now`.
5. All database writes are enqueued to `IBackgroundTaskQueue` to avoid blocking the HTTP request.

### For anonymous users

The same code path runs, but `userId` is `null`. Anonymous searches are stored with a `null` `ApplicationUserId` and are not shown on any user's "My Searches" page.

---

## Display: SearchesController

**Controller**: `m4d.Controllers.SearchesController` ([SearchesController.cs](../m4d/Controllers/SearchesController.cs))

### `Index` action

Accepts:

- `user` — which user's searches to display (defaults to current user; `"all"` shows everyone for admins)
- `sort` — `"recent"` for Modified-descending; anything else for Count-descending (**Most Popular**)
- `showDetails` — `bool` flag to show extra columns

Processing:

1. Resolve the user (defaults to current authenticated user; `UserQuery.IdentityUser` sentinel → current user).
2. Authenticate: non-admins can only see their own searches.
3. Query `Searches` table filtered by user, ordered by `Modified` or `Count`, limited to 250 rows.
4. If showing a single user's searches, call `SetSpotify` to populate the runtime `Spotify` property for each row.
5. Pass bag values `Sort`, `ShowDetails`, `SongFilter`, `User` to the view.

### Authentication / Authorization

- The controller has `[Authorize]` — all actions require login.
- Non-admin users can only view/delete their own searches (checked via `Authenticate(user)` which throws `AuthenticationException` on mismatch).
- The `dbAdmin` role bypasses the ownership check.

### `Delete` and `DeleteConfirmed` actions

Standard GET/POST delete with anti-forgery token. After deletion, redirects back to `Index` with original `sort` / `showDetails` / `user` parameters preserved.

---

## Display: \_SearchesCore Partial View

**Partial**: `m4d/Views/Shared/_SearchesCore.cshtml` ([\_SearchesCore.cshtml](../m4d/Views/Shared/_SearchesCore.cshtml))

Rendered from:

- `m4d/Views/Searches/Index.cshtml` — the main "My Searches" page
- `m4d/Views/ApplicationUsers/_userDetails.cshtml` — embedded in the admin user-detail page (always sorted by Count)

### Column behavior

| Condition                       | Columns shown                                                                        |
| ------------------------------- | ------------------------------------------------------------------------------------ |
| `showDetails = false` (default) | Single cell: Search button, Delete button, optional Spotify icon, filter description |
| `showDetails = true`            | All columns: Search, Query, User, Count, Created, Modified                           |

The Search button uses the filter reconstructed from `item.Filter` (the deserialized `Query`). For non-Azure filters, the action is forced to `"Advanced"`.

---

## Filter Serialization Format

`SongFilter.ToString()` serializes a filter as a tilde (`~`) separated string (originally a sub-character):

```
[v2~]action~dances~sortOrder~searchString~purchase~user~tempoMin~tempoMax[~lengthMin~lengthMax]~page~tags~level
```

For saved searches the stored `Query` always has `page` as empty/null (normalized out). On display the Filter is deserialized back and the page is not restored.

---

## Data Serialization / Import-Export

`DanceMusicService.SerializeSearches()` exports searches as tab-delimited rows:

```
userName\tname\tquery\tfavorite\tcount\tcreated\tmodified
```

`DanceMusicService.LoadSearches()` imports this format (both bulk and incremental modes). This is used by the admin backup/restore workflow and the index backup streaming feature.

---

## Databases

| Environment    | Server                            | Database           |
| -------------- | --------------------------------- | ------------------ |
| Local dev      | `(localdb)\mssqllocaldb`          | `m4d`              |
| Staging / Test | `n8a541qjnq.database.windows.net` | `music4dance_test` |
| Production     | `n8a541qjnq.database.windows.net` | `music4dance`      |

Connection strings are managed via:

- Local: `appsettings.json` → `ConnectionStrings:DanceMusicContextConnection`
- Azure: Service Connector → `AZURE_SQL_CONNECTIONSTRING` environment variable (takes precedence)

**Migrations are NOT automatically applied at startup.** They must be applied manually (see [Applying Migrations](#applying-migrations)).

### Migration History

| Migration                         | Date       | Change                   |
| --------------------------------- | ---------- | ------------------------ |
| `20191123233600_CreateSchema`     | 2019-11-23 | Created `Searches` table |
| `20211128222327_ActivityLog`      | 2021-11-28 | Unrelated                |
| `20211223033025_CardTracking`     | 2021-12-23 | Unrelated                |
| `20240224015103_UsageLog`         | 2024-02-24 | Unrelated                |
| `20240311190320_UsageLogReferral` | 2024-03-11 | Unrelated                |

### Applying Migrations

Generate an idempotent SQL script from the latest migration:

```bash
# From the repo root
dotnet ef migrations script --idempotent \
  --project m4dModels \
  --startup-project m4d \
  --output migration.sql
```

Apply locally:

```bash
dotnet ef database update --project m4dModels --startup-project m4d
```

Apply to Azure (staging or production): run the generated SQL script in Azure Portal → SQL Database → Query Editor or via `sqlcmd`/Azure Data Studio against the appropriate database (`music4dance_test` or `music4dance`).

---

## Key Files Reference

| File                                                                               | Role                                                 |
| ---------------------------------------------------------------------------------- | ---------------------------------------------------- |
| [m4dModels/Search.cs](../m4dModels/Search.cs)                                      | Entity model                                         |
| [m4dModels/DanceMusicContext.cs](../m4dModels/DanceMusicContext.cs)                | EF Core DbContext, `Searches` DbSet                  |
| [m4dModels/SongFilter.cs](../m4dModels/SongFilter.cs)                              | Filter serialization, normalization (page stripping) |
| [m4dModels/DanceMusicService.cs](../m4dModels/DanceMusicService.cs)                | Serialize/load searches for backup                   |
| [m4d/Services/SongSearch.cs](../m4d/Services/SongSearch.cs)                        | `LogSearch()` — creates/updates saved search records |
| [m4d/Controllers/SearchesController.cs](../m4d/Controllers/SearchesController.cs)  | CRUD for My Searches page                            |
| [m4d/Views/Searches/Index.cshtml](../m4d/Views/Searches/Index.cshtml)              | My Searches list view                                |
| [m4d/Views/Shared/\_SearchesCore.cshtml](../m4d/Views/Shared/_SearchesCore.cshtml) | Shared search table partial                          |
| [m4dModels/Migrations/](../m4dModels/Migrations/)                                  | EF Core migration history                            |
