# Saved Searches Architecture

## Overview

Saved searches allow authenticated (and optionally anonymous) users to have their song search queries automatically saved, tracked for how often they are revisited, and displayed in a "My Searches" page. The feature lets dancers quickly return to searches they perform frequently.

---

## Data Model

**Entity**: `m4dModels.Search` ([Search.cs](../m4dModels/Search.cs))

| Column              | Type                    | Notes                                                                         |
| ------------------- | ----------------------- | ----------------------------------------------------------------------------- |
| `Id`                | `long` (PK, identity)   | Auto-increment                                                                |
| `ApplicationUserId` | `string` (FK, nullable) | `null` for anonymous users                                                    |
| `Name`              | `string` (nullable)     | User-defined label (currently unused in UI)                                   |
| `Query`             | `string` (required)     | Normalized serialized `SongFilter` string — **page is always stripped**       |
| `Favorite`          | `bool`                  | Reserved for future use                                                       |
| `Count`             | `int`                   | Number of times this search has been visited                                  |
| `Created`           | `DateTime`              | Timestamp of first visit                                                      |
| `Modified`          | `DateTime`              | Timestamp of most recent visit                                                |
| `MostRecentPage`    | `int?` (nullable)       | Last page the authenticated user was on; `null` or `1` → start from beginning |

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

1. Capture the current page from the filter before normalization (for `MostRecentPage` tracking).
2. Normalize the filter (strips page, resolves identity).
3. Look up an existing `Search` row by `(ApplicationUserId, normalized Query)`.
4. **If found**: increment `Count`, update `Modified` to now, and set `MostRecentPage` to the captured page (stored as `null` when page ≤ 1 or absent).
5. **If not found**: insert a new row with `Count = 1`, `Created = now`, `Modified = now`, `MostRecentPage = null`.
6. All database writes are enqueued to `IBackgroundTaskQueue` to avoid blocking the HTTP request.

### For anonymous users

The same code path runs, but `userId` is `null`. Anonymous searches are stored with a `null` `ApplicationUserId` and are not shown on any user's "My Searches" page.

---

## Display: SearchesController

**Controller**: `m4d.Controllers.SearchesController` ([SearchesController.cs](../m4d/Controllers/SearchesController.cs))

### `Index` action

Accepts:

- `user` — which user's searches to display (defaults to current user; `"all"` shows everyone for admins)
- `sort` — `"recent"` for Modified-descending; anything else for Count-descending (**Most Popular**)
- `showDetails` — `bool` flag to show extra columns (admin-only toggle in the view)
- `spotifyOnly` — `bool` flag to filter the list to only searches that have a linked Spotify playlist

Processing:

1. Resolve the user (defaults to current authenticated user; `UserQuery.IdentityUser` sentinel → current user).
2. Authenticate: non-admins can only see their own searches.
3. Query `Searches` table filtered by user, ordered by `Modified` or `Count`, capped at the top 250 rows (hard-coded `Take(250)`). Users who have logged more than 250 searches will not see the older ones.
4. If showing a single user's searches, call `SetSpotify` to populate the runtime `Spotify` property for each row.
5. If `spotifyOnly`, filter the in-memory list to rows with a non-empty `Spotify` value.
6. Pass bag values `Sort`, `ShowDetails`, `SpotifyOnly`, `SongFilter`, `User` to the view.

### `Resume` action

Redirects the current authenticated user directly into their most-recently-modified saved search, restoring the page they were on. Looks up the user's most recently `Modified` search, reconstructs the filter, applies `MostRecentPage` if > 1, then redirects to `Song/Index`.

### Authentication / Authorization

- The controller has `[Authorize]` — all actions require login.
- Non-admin users can only view/delete their own searches (checked via `Authenticate(user)` which throws `AuthenticationException` on mismatch).
- The `dbAdmin` role bypasses the ownership check.

### `Delete` and `DeleteConfirmed` actions

Standard GET/POST delete with anti-forgery token. After deletion, redirects back to `Index` with original `sort` / `showDetails` / `spotifyOnly` / `user` parameters preserved.

---

## Display: \_SearchesCore Partial View

**Partial**: `m4d/Views/Shared/_SearchesCore.cshtml` ([\_SearchesCore.cshtml](../m4d/Views/Shared/_SearchesCore.cshtml))

Rendered from:

- `m4d/Views/Searches/Index.cshtml` — the main "My Searches" page
- `m4d/Views/ApplicationUsers/_userDetails.cshtml` — embedded in the admin user-detail page (always sorted by Count)

### Column behavior

| Condition                       | Columns shown                                                                                        |
| ------------------------------- | ---------------------------------------------------------------------------------------------------- |
| `showDetails = false` (default) | Two columns: (Search buttons + Spotify icon + description) and (Count or Modified depending on sort) |
| `showDetails = true`            | All columns: Search, Query, User, Count, Created, Modified                                           |

When `showDetails = false`, the second column always shows the active ranking value — `Count` on Most Popular, `Modified` on Most Recent — so users can see relevance at a glance.

The **Search** button always goes to page 1. When `MostRecentPage > 1`, an additional **Page N** button (`btn-outline-success`) appears beside it, linking directly to the last-visited page.

For non-Azure filters, the action is forced to `"Advanced"` before building either link.

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
userName\tname\tquery\tfavorite\tcount\tcreated\tmodified\tMostRecentPage
```

The 8th field (`MostRecentPage`) was added when that column was introduced. `DanceMusicService.LoadSearches()` reads this field if present, leaving `MostRecentPage = null` when loading older 7-field backup data. Both bulk and incremental import modes are supported. This is used by the admin backup/restore workflow and the index backup streaming feature.

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

**Migrations run automatically at startup** via a background `Task.Run` in `Program.cs` (after a 2-second delay). This applies to all environments — local dev, staging, and production. No manual SQL scripts or `dotnet ef database update` steps are needed on deploy.

### Migration History

| Migration                             | Date       | Change                        |
| ------------------------------------- | ---------- | ----------------------------- |
| `20191123233600_CreateSchema`         | 2019-11-23 | Created `Searches` table      |
| `20211128222327_ActivityLog`          | 2021-11-28 | Unrelated                     |
| `20211223033025_CardTracking`         | 2021-12-23 | Unrelated                     |
| `20240224015103_UsageLog`             | 2024-02-24 | Unrelated                     |
| `20240311190320_UsageLogReferral`     | 2024-03-11 | Unrelated                     |
| `20260326002120_SearchMostRecentPage` | 2026-03-26 | Added `MostRecentPage` column |

### Generating a new migration

```bash
dotnet ef migrations add <MigrationName> --project m4dModels --startup-project m4d
```

### Rollback

If a migration must be reversed after deployment:

```bash
dotnet ef database update <PreviousMigrationName> --project m4dModels --startup-project m4d
```

Then remove the migration file and redeploy.

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

---

## Future Improvements

- **Pagination** — The My Searches list is currently capped at 250 rows. Power users who log many unique searches will silently lose visibility into older ones. The list should be paginated (server-side or client-side) so all searches are accessible.
- **Vue front-end** — The My Searches page (`Index.cshtml` + `_SearchesCore.cshtml`) is a classic Razor/Bootstrap view with full-page reloads for every sort, filter, and delete operation. Moving it to a Vue 3 component (backed by a JSON API endpoint) would enable:
  - In-place filtering and sorting without page reloads
  - Inline delete with optimistic UI updates
  - Easier integration with the existing client-side filter model (`SongFilter`, `DanceQueryItem`, etc.)
  - Co-location with other Vue pages and the main client-side routing model
