# Admin Pages Performance Improvement Plan

## Problem Statement

Several admin index pages are rendering large HTML tables server-side (via Razor), which causes
Chromium-based browsers to become unresponsive. The root cause is not the data volume itself — it
is the cost of parsing and laying out thousands of DOM nodes at once. Two categories of pages need
different treatments:

| Category                           | Pages                                      | Strategy                                                                                           |
| ---------------------------------- | ------------------------------------------ | -------------------------------------------------------------------------------------------------- |
| **Vue conversion (client paging)** | `ApplicationUsers/Index`, `PlayList/Index` | Plant all data as JSON, page/filter/sort client-side with BVN                                      |
| **Vue conversion (server paging)** | `Searches/Index`, `ActivityLog/Index`      | Server paginates at DB level; Vue renders the current page and windowed server-side pagination nav |

Auxiliary pages (Details, Edit, Delete confirmation, ChangeRoles, etc.) remain as Razor — no
conversion is needed or wanted for these.

---

## Part 1 — Vue Conversion: ApplicationUsers/Index _(Priority 1)_

### Current behavior

`ApplicationUsersController.Index` calls `UserMapper.GetUserNameDictionary()` and passes the
full `IReadOnlyDictionary<string, UserInfo>` to the `Index.cshtml` Razor view. The view does all
filtering, sorting, and rendering inline, producing a single HTML table with one `<tr>` per user.
The table grows linearly with the user count and the page becomes unresponsive.

### Target behavior

The controller serialises a safe, flat DTO array as `model_` JSON (via the existing `Vue3()`
helper). A new Vue page deserialises this and renders the table using `<BTable>` from
bootstrap-vue-next, with:

- Client-side **filtering** (showUnconfirmed / showPseudo / hidePrivate toggles — reactive,
  no page reload)
- Client-side **sorting** (clickable column headers; BVN `<BTable>` handles this natively)
- Client-side **pagination** (BVN `<BPagination>` + `<BTable per-page>`)

Because all data is already on the page (the dataset is manageable), no API calls are needed and
no pagination state needs to survive a navigation.

### Security note

`ApplicationUser` contains `PasswordHash`, `SecurityStamp`, `ConcurrencyStamp`, and other
sensitive identity fields. **The JSON payload must use a dedicated DTO** that exposes only the
fields actually needed by the index view. Never serialize the full `ApplicationUser` entity.

### C# changes

#### 1. New DTO — `AdminUserSummary`

Create in `m4d/ViewModels/AdminUserSummary.cs` (or in a new `Admin/` subdirectory).

Fields needed (cross-referenced with the existing `Index.cshtml` columns and action links):

```csharp
public class AdminUserSummary
{
    // Identity / display
    public string Id { get; set; }          // for Edit/Delete/ChangeRoles links
    public string UserName { get; set; }
    public string Email { get; set; }
    public bool EmailConfirmed { get; set; }
    public bool IsPseudo { get; set; }

    // Activity
    public DateTime StartDate { get; set; }
    public DateTime LastActive { get; set; }
    public int HitCount { get; set; }

    // Subscription / commerce
    public decimal LifetimePurchased { get; set; }
    public int SubscriptionLevel { get; set; }  // serialise enum as int

    // Profile flags
    public byte Privacy { get; set; }
    public string ServicePreference { get; set; }
    public int FailedCardAttempts { get; set; }

    // Roles / logins (already projected lists in UserInfo)
    public List<string> Roles { get; set; }
    public List<string> Logins { get; set; }
}
```

#### 2. New wrapper model — `AdminUsersModel`

```csharp
public class AdminUsersModel
{
    public List<AdminUserSummary> Users { get; set; }
    public List<string> AllRoles { get; set; }   // for role count summary
}
```

#### 3. Modified `Index()` action

```csharp
// GET: ApplicationUsers
public async Task<ActionResult> Index()
{
    var dict = await UserMapper.GetUserNameDictionary(Database.UserManager, ServiceHealth);
    var roles = Context.Roles.Select(r => r.Name).ToList();

    var users = dict.Values.Select(u => new AdminUserSummary
    {
        Id             = u.User.Id,
        UserName       = u.User.UserName,
        Email          = u.User.Email,
        EmailConfirmed = u.User.EmailConfirmed,
        IsPseudo       = u.IsPseudo,
        StartDate      = u.User.StartDate,
        LastActive     = u.User.LastActive,
        HitCount       = u.User.HitCount,
        LifetimePurchased = u.User.LifetimePurchased,
        SubscriptionLevel = (int)u.User.SubscriptionLevel,
        Privacy        = u.User.Privacy,
        ServicePreference = u.User.ServicePreference,
        FailedCardAttempts = u.User.FailedCardAttempts,
        Roles          = u.Roles,
        Logins         = u.Logins,
    }).ToList();

    var model = new AdminUsersModel { Users = users, AllRoles = roles };
    return Vue3("User Administrator", "Admin: User list", "admin-users", model);
}
```

The `showUnconfirmed`, `showPseudo`, `hidePrivate`, and `sort` query parameters are **removed**
from the server action — they become purely client-side state managed by Vue reactive refs.

The `ClearCache()` action remains unchanged (redirects back to `Index`).

### TypeScript / Vue changes

#### New page entry: `src/pages/admin-users/`

Structure mirrors existing Vue pages:

```
src/pages/admin-users/
  main.ts            -- standard Vite entry (same pattern as other pages)
  App.vue            -- root component
  __tests__/
    admin-users.test.ts
    model.ts         -- test fixture data
```

#### TypeScript model (`AdminUsersModel.ts`)

```typescript
@jsonObject
export class AdminUserSummary {
  @jsonMember(String) public id!: string;
  @jsonMember(String) public userName!: string;
  @jsonMember(String) public email?: string;
  @jsonMember(Boolean) public emailConfirmed!: boolean;
  @jsonMember(Boolean) public isPseudo!: boolean;
  @jsonMember(String) public startDate!: string; // serialized as string
  @jsonMember(String) public lastActive!: string;
  @jsonMember(Number) public hitCount!: number;
  @jsonMember(Number) public lifetimePurchased!: number;
  @jsonMember(Number) public subscriptionLevel!: number;
  @jsonMember(Number) public privacy!: number;
  @jsonMember(String) public servicePreference?: string;
  @jsonMember(Number) public failedCardAttempts!: number;
  @jsonArrayMember(String) public roles!: string[];
  @jsonArrayMember(String) public logins!: string[];
}

@jsonObject
export class AdminUsersModel {
  @jsonArrayMember(AdminUserSummary) public users!: AdminUserSummary[];
  @jsonArrayMember(String) public allRoles!: string[];
}
```

#### `App.vue` key features

- Deserialise model with `TypedJSON.parse(model_, AdminUsersModel)`
- Reactive filter state:
  - `showUnconfirmed: ref(false)`
  - `showPseudo: ref(false)`
  - `hidePrivate: ref(false)`
- `filteredUsers` — `computed()` over `model.users` applying the three boolean filters
- Sorting — pass `filteredUsers` to BVN `<BTable :items="filteredUsers" :fields="fields">`; BVN
  handles column-header click sorting natively
- Pagination — `<BTable :per-page="perPage" :current-page="currentPage">` +
  `<BPagination>` component underneath the table
- Summary stats row (total, registered, confirmed, deleted, service breakdown, role counts) —
  computed from the **unfiltered** `model.users` array, same as the current Razor view
- Action links are plain `<a href="...">` anchors (no fetch/API needed) pointing to the existing
  Razor auxiliary pages:
  - `/ApplicationUsers/Details/{id}`
  - `/ApplicationUsers/Edit/{id}`
  - `/ApplicationUsers/Delete/{id}`
  - `/ApplicationUsers/ChangeRoles/{id}`
  - `/ApplicationUsers/ClearPremium/{id}`
  - `/Song/FilterUser?user={userName}` (List Songs)
  - `/Searches/Index?user={userName}&showDetails=true&sort=recent` (List Searches)
  - `/UsageLog/UserLog?user={userName}` (Usage)
  - `/PlayList/Index?user={userName}` (Playlists — defaults to SongsFromSpotify type)

#### Preserved top-of-page links (non-list actions)

- "New Pseudo User" → `/ApplicationUsers/Create`
- "Clear Cache" → `/ApplicationUsers/ClearCache`
- "Voting Results" → `/ApplicationUsers/VotingResults`
- "Premium" → `/ApplicationUsers/PremiumUsers`

### Razor view retirement

`m4d/Views/ApplicationUsers/Index.cshtml` is replaced by the Vue3 shell (rendered by `Vue3()`
via `Views/Shared/Vue3.cshtml`). The old file can be deleted as part of the PR.

---

## Part 2 — Vue Conversion: PlayList/Index _(Priority 2)_

### Current behavior

`PlayListController.Index` returns a `PlayListIndex` model to the Razor view, which renders one
`<tr>` per playlist with no pagination. The unfiltered result includes all playlists of the
selected type.

### Target behavior

Same pattern as the user page: plant `PlayListIndex`-equivalent data as JSON, move
filtering/sorting/pagination to Vue. Filters that currently require a page reload (type, user,
showDeleted) become client-side toggle/select controls.

### Data security

`PlayList` contains no sensitive PII; the `PlayListIndex` model (and `PlayList` records) are
appropriate to serialise. The main concern is not exposing unreleated tables — the model stays
limited to the list being shown.

Because `PlayList.Data1Name` / `Data2Name` are computed properties on `PlayList` (not persisted),
the serialisation should either include them explicitly or the TypeScript model should replicate
the logic. Easiest: include them as plain strings on the C# DTO.

### C# changes

#### Modified `Index()` action

```csharp
[Authorize(Roles = "dbAdmin")]
public IActionResult Index(PlayListType type = PlayListType.SongsFromSpotify,
    string user = null, bool showDeleted = false)
{
    // Load all playlists of the given type (showDeleted will become a client filter)
    // For now, keep the existing query structure and pass the full dataset:
    var model = string.IsNullOrWhiteSpace(user)
        ? GetIndex(type, showDeleted)
        : GetUserIndex(type, user, showDeleted);

    // Wrap in a serialization-friendly container
    return Vue3($"PlayList Admin — {type}", "Admin: Playlist list",
        "admin-playlists", new AdminPlaylistsModel(model));
}
```

`AdminPlaylistsModel` simply flattens `PlayListIndex` into a serialisation-safe form (no
`[NotMapped]` computed property issues). Type enum values are serialised as integers.

Alternatively, start with type fixed to a default and let the Vue page switch types by
reloading the URL with the new `type` query parameter (simplest migration path — only requires
changing how `filteredLists` is computed client-side, not eliminating the server query parameter).

### Vue page: `src/pages/playlist/` _(as built)_

- Deserialise model via TypedJSON (`PlayListPageModel` + `PlayListSummary`)
- Client-side controls:
  - Type links (SongsFromSpotify / SpotifyFromSearch) — trigger a server reload with `?type=N`;
    current type is **not** a client-side filter
  - User filter (text input on user/name/id); pre-populated from the `user` URL param
  - Show Active / Show Deleted toggle (fully client-side; both active and deleted playlists are
    included in the server payload)
- `<BTable id="playlist-table">` with per-page pagination and sortable columns; deleted rows
  styled with `table-danger`
- Per-row action links: Update \| Edit \| Details \| Delete (active) / Undelete (deleted); Restore
  shown when `updated && !data2`
- Sidebar: Create New, Restore All (SongsFromSpotify), Update All, Delete All (user-filtered +
  active + count > 0), BulkCreate links (SpotifyFromSearch only), Statistics

> See `architecture/playlist-management.md` — _Vue Index Page_ section for full detail.

---

## Part 3 — Vue Conversion + Server-Side Paging: Searches/Index _(Priority 3)_

### Current behavior

`SearchesController.Index` fetches up to 250 records (`.Take(250)`). There is no pagination UI.

### Target behavior

Add a `page` parameter. Page at the database level via `.Skip()`/`.Take()`. Render via Vue using
the `Vue3()` helper — server supplies `SearchesPageModel` JSON; Vue renders the table and builds
windowed server-side pagination nav links (no SPA routing — each page click navigates to the server).

### C# changes

```csharp
private const int SearchesPageSize = 100;

public async Task<IActionResult> Index(string user, string sort = null,
    bool showDetails = false, bool spotifyOnly = false, int page = 1)
{
    // ... existing user/auth setup ...

    IQueryable<Search> searches = Database.Searches.Include(s => s.ApplicationUser);
    if (user is not null and not "all")
    {
        // ... existing user lookup ...
        searches = searches.Where(s => s.ApplicationUserId == appUser.Id);
    }

    var ordered = string.Equals(sort, "recent")
        ? searches.OrderByDescending(s => s.Modified)
        : searches.OrderByDescending(s => s.Count);

    var totalCount = await ordered.CountAsync();
    var model = await ordered
        .Skip((page - 1) * SearchesPageSize)
        .Take(SearchesPageSize)
        .ToListAsync();

    // ... existing spotify/spotifyOnly logic ...

    ViewBag.Page = page;
    ViewBag.TotalPages = (int)Math.Ceiling((double)totalCount / SearchesPageSize);
    // ... existing ViewBag assignments ...
    return View(model);
}
```

### C# changes

#### New DTOs — `m4d/ViewModels/SearchesPageModel.cs`

```csharp
public class SearchSummary
{
    public long Id { get; set; }
    public string UserName { get; set; }   // "anonymous" when no user
    public string Query { get; set; }
    public string Description { get; set; }  // filter.Description, computed server-side
    public string SearchUrl { get; set; }    // pre-built; involves SongFilter serialization
    public string SearchPageUrl { get; set; } // link to most-recently-visited page (optional)
    public int? MostRecentPage { get; set; }
    public int Count { get; set; }
    public DateTime Created { get; set; }
    public DateTime Modified { get; set; }
    public string Spotify { get; set; }
    public string DeleteUrl { get; set; }   // pre-built with all current filter params
}

public class SearchesPageModel
{
    public List<SearchSummary> Searches { get; set; }
    public int Page { get; set; }
    public int TotalPages { get; set; }
    public string Sort { get; set; }          // "recent" or null
    public bool ShowDetails { get; set; }     // admin detail view
    public bool SpotifyOnly { get; set; }
    public string User { get; set; }          // "all" or a username
    public bool IsAdmin { get; set; }         // show Toggle Details link
    public bool CanDeleteAll { get; set; }    // false when User == "all"
    public string BasicSearchUrl { get; set; }
    public string AdvancedSearchUrl { get; set; }
    public string DeleteAllUrl { get; set; }
}
```

Search URLs and delete URLs are pre-built server-side because they require `SongFilter`
serialization. Pagination/sort/toggle URLs are built client-side by the Vue component from the
known parameters (user, sort, showDetails, spotifyOnly).

#### Modified `Index()` action

Builds `SearchesPageModel`, projects each `Search` to `SearchSummary` using `Url.Action()` for
per-row URLs, then returns `Vue3("My Searches", "Search history", "searches", viewModel)`.

### Vue page: `src/pages/searches/`

```
src/pages/searches/
  App.vue               -- renders controls + table + pagination nav
  SearchesPageModel.ts  -- TypedJSON-decorated model classes
  __tests__/
    searches.test.ts    -- snapshot + behavior tests
    model.ts            -- test fixture data
```

**Vue design decisions:**

- Server handles paging — each page click navigates to `/Searches/Index?...&page=N`
- Sort toggle (Most Popular / Most Recent) navigates via computed URL, resets to page 1
- Spotify Only switch navigates via an `onSpotifyToggle()` handler function that sets
  `window.location.href` — Vue template expressions do not have `window` in scope, so the
  navigation must happen inside a `<script setup>` function
- Toggle Details navigates via computed URL (admin only)
- Pagination: `BPagination` (bootstrap-vue-next) + page-jump input (`Page [n] of N`), matching
  `SongFooter.vue` style. `total-rows="totalPages" per-page="1"` maps page count directly.
  `@page-click` intercepts BVN's internal routing and sets `window.location.href` to the
  server URL for the selected page
- `BTable` fields are computed based on `showDetails` — detail view adds Query, User, Count,
  Created, Modified columns
- Per-row delete URL and search URLs are pre-built in the model (not computed in Vue)
- All pages wrapped in `<PageFrame>` for consistent nav/header/footer chrome

---

## Part 4 — Vue Conversion + Server-Side Paging: ActivityLog/Index _(Priority 4)_

### Current behavior

`ActivityLogController.Index` takes the 500 most-recent records. No pagination.

### Target behavior

Same paging treatment as Searches/Index. Render via Vue — server supplies `ActivityLogPageModel`
JSON; Vue renders the table and windowed server-side pagination nav.

### C# changes

#### New DTOs — `m4d/ViewModels/ActivityLogPageModel.cs`

```csharp
public class ActivityLogEntry
{
    public int Id { get; set; }
    public DateTimeOffset Date { get; set; }
    public string UserName { get; set; }
    public string Action { get; set; }
    public string Details { get; set; }
}

public class ActivityLogPageModel
{
    public List<ActivityLogEntry> Entries { get; set; }
    public int Page { get; set; }
    public int TotalPages { get; set; }
}
```

#### Modified `Index()` action

Builds `ActivityLogPageModel`, then returns
`Vue3("Activity Log", "Admin: Activity log", "activity-log", viewModel)`.

### Vue page: `src/pages/activity-log/`

```
src/pages/activity-log/
  App.vue                  -- renders table + pagination nav
  ActivityLogPageModel.ts  -- TypedJSON-decorated model classes
  __tests__/
    activity-log.test.ts   -- snapshot + behavior tests
    model.ts               -- test fixture data
```

Pagination follows the same `BPagination` + page-jump input pattern as Searches. All pages
wrapped in `<PageFrame id="app" title="Activity Log">`.

---

## Development Phases

| Phase | PR scope                                              | Status                | Outcome                                                                                                      |
| ----- | ----------------------------------------------------- | --------------------- | ------------------------------------------------------------------------------------------------------------ |
| **1** | ApplicationUsers/Index → Vue (DTO + Vue page + tests) | ✅ Complete (PR #174) | Users page is responsive; all auxiliary pages unchanged                                                      |
| **2** | PlayList/Index → Vue                                  | ✅ Complete           | Playlists page is responsive                                                                                 |
| **3** | Searches/Index server paging (DB-level Skip/Take)     | ✅ Complete           | Searches paginated server-side                                                                               |
| **4** | ActivityLog/Index server paging                       | ✅ Complete           | Activity log paginated server-side                                                                           |
| **5** | Searches/Index + ActivityLog/Index → Vue conversion   | ✅ Complete           | Both pages rendered by Vue with `BPagination` + page-jump input; PageFrame added to all four admin Vue pages |

Each phase is independently deployable. Later phases can be re-prioritised without affecting
earlier ones.

---

## Testing Notes

### Phase 1 (ApplicationUsers Vue)

- **Unit tests** for the new Vue component (`admin-users.test.ts`): snapshot test; verify
  filtering logic (showUnconfirmed, showPseudo, hidePrivate); verify pagination; verify summary
  stat computations.
- **Manual smoke test**: navigate to `/ApplicationUsers` as a dbAdmin user, verify all filter
  toggles work, table renders without browser freeze, all action links reach the correct Razor
  page.
- The existing server-side tests for `ApplicationUsersController` continue to pass without change
  (auxiliary actions are unchanged; `Index()` now returns a `VueModel` but the existing tests
  only verify auxiliary actions like `Edit`, `Delete`, `ChangeRoles`).

### Phase 3 & 4 (Server-Side Paging)

- Integration test for `Index(page)` action verifying correct `Skip`/`Take` behaviour
  (`Index_PaginatesResults_WhenMoreThanPageSizeExists`).
- Manual verification that page 1 loads the first N records and page 2 loads the next N.

### Phase 5 (Vue Conversion of Searches + ActivityLog)

- **Snapshot tests** for both pages: `searches.test.ts`, `activity-log.test.ts`
- **Behavior tests**: sort active state, pagination visibility, delete-all visibility,
  admin Toggle Details visibility, table columns in detail mode, Spotify icon, no-user placeholder
- **Pagination selector**: use `ul.pagination` (the `<ul>` BPagination always renders); do not
  use `nav[aria-label]` — bootstrap-vue-next does not render it in a form reachable by CSS
  attribute selector in JSDOM
- **Spotify icon test**: use `img[alt="Spotify Playlist"]` rather than `a[target="_blank"]` —
  PageFrame's footer also contains an `<a target="_blank">` link
- **Manual smoke test**: navigate to each page, verify pagination nav, sort toggles, delete buttons,
  Spotify links, and (for Searches) the admin Toggle Details link.

---

## Design Decisions

1. **PlayList type switching (Phase 2)**: `type` stays as a server query parameter (causes a page
   reload). `showDeleted` is fully client-side (both active and deleted rows are sent in the
   payload; the toggle just filters the computed list). `user` is also a server query parameter
   but only for pre-populating the client-side text filter — all playlists of the given type are
   always sent.

2. **Date serialisation**: Use camelCase JSON serialiser defaults throughout. Dates arrive as
   strings in the JSON payload; TypeScript models declare them as `string` and format for display
   using standard JS `Date` parsing.

3. **`ClearCache` redirect**: The `ClearCache` action uses `return RedirectToAction("Index")`
   (not `await Index()`) so it correctly returns to the Vue-served page.

4. **Role count summary**: `allRoles` is included in the server model. The Vue component counts
   roles from `model.users` via computed properties, avoiding a separate API call.

5. **Browser history / URL state**: Filter toggles (showUnconfirmed, etc.) do not update the URL.
   This is acceptable for an admin-only page.

6. **Pagination controls (server-paged pages)**: Searches and ActivityLog use `BPagination`
   (bootstrap-vue-next) plus a direct page-jump input (`Page [n] of N`), matching `SongFooter.vue`.
   Config: `total-rows="totalPages" per-page="1"` maps page numbers directly; `@page-click`
   intercepts BVN's client-side routing and instead sets `window.location.href` to the
   server-rendered URL for the selected page. Client-paged pages (admin-users, playlist) continue
   to use BPagination with BTable's built-in `current-page` / `per-page` props.

7. **`window.location.href` in Vue templates**: Vue 3's template compiler does not expose
   `window` as a global. Any navigation that needs `window.location.href` must be wrapped in a
   `<script setup>` handler function (e.g. `onSpotifyToggle()`). Binding `@change="window.location.href = ..."` directly in the template will fail silently at runtime.

8. **PageFrame**: All `src/pages/*/App.vue` root components wrap their content in `<PageFrame>`
   to provide the main navigation bar, service status banner, `<h1>` page title, and site footer.
   `admin-users` and `playlist` (Phases 1–2) were retrofitted with PageFrame alongside the
   Phase 5 work.

---

## Column Definitions (Admin Users Table)

The admin users table now uses descriptive labels for communication-related fields so values are
meaningful at a glance.

| Column                   | Meaning                   | Notes                                                                                                                                                                                       |
| ------------------------ | ------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `EC`                     | Email Confirmed           | Check icon means the user has confirmed email; hollow circle means not confirmed.                                                                                                           |
| `UserName / Email`       | User identity             | Username link opens user details; email shown below username. Pseudo users are italicized.                                                                                                  |
| `Signed Up`              | Account creation date     | `StartDate` value, rendered as local date (`M/D/YYYY`).                                                                                                                                     |
| `Signed In`              | Last active date          | `LastActive` value, rendered as local date. `1900-01-01` sentinel is displayed as `Never`.                                                                                                  |
| `PRV`                    | Privacy flag              | Numeric privacy byte (`0` = private / anonymous behavior, `255` = fully public).                                                                                                            |
| `Contact Prefs`          | Communication permissions | Human-readable decoding of `CanContact` bit flags (not raw numbers): `music4dance promo`, `partner promo`, `surveys/blog`, or combinations. `None` means no communication consent selected. |
| `Services`               | Music service interests   | Human-readable service names decoded from `ServicePreference` CIDs (for example Spotify, YouTube, Apple Music). `None` means no service preference selected.                                |
| `CCF`                    | Credit card failures      | `FailedCardAttempts` count.                                                                                                                                                                 |
| `$`                      | Lifetime purchases        | Total amount purchased by the user.                                                                                                                                                         |
| `HC`                     | Hit count                 | Total tracked user requests/activity count.                                                                                                                                                 |
| `Roles`                  | Identity roles            | ASP.NET Identity roles assigned to the user.                                                                                                                                                |
| `Logins`                 | External login providers  | Linked provider list (for example Microsoft, Google, Spotify, Facebook).                                                                                                                    |
| `(blank actions column)` | Admin actions             | Links for songs/searches/usage/playlists plus role, edit, delete, and premium reset actions.                                                                                                |

### Communication Preference Encoding Reference

`CanContact` is a bit field (`ContactStatus`) with these flags:

- `1` (`0x01`): direct music4dance promotional messages
- `2` (`0x02`): partner promotional messages
- `4` (`0x04`): surveys / feedback / blog participation

Composite values are sums of selected flags (for example `5` = `1 + 4`). The UI should present
decoded text, not the numeric value.
