# Admin Pages

Current-state reference for the `dbAdmin`-only admin index pages. All of these were originally
server-rendered Razor tables that became unresponsive in Chromium once the row count grew large
(the cost is DOM node count, not data volume). Each was converted to a pattern where the
controller serialises data as JSON via the `Vue3()` helper and a Vue page renders/filters/sorts/
paginates it client-side.

Auxiliary pages for each controller (Details, Edit, Delete confirmation, ChangeRoles, Create,
Merge, etc.) remain plain Razor views — there's no benefit to converting single-record forms.

## Two paging strategies

| Strategy | Pages | How it works |
| --- | --- | --- |
| **Client paging** | `ApplicationUsers/Index`, `PlayList/Index` | Controller sends the full dataset as JSON once; Vue does all filtering/sorting/paging in-browser |
| **Server paging** | `Searches/Index`, `ActivityLog/Index` | Controller pages at the DB level (`Skip`/`Take`); each page/sort/filter change navigates the browser to a new URL |

Client paging is used where the underlying table is small enough to ship in one payload (users,
playlists). Server paging is used where the table can be very large (searches, activity log).

---

## ApplicationUsers/Index

**Controller**: `ApplicationUsersController.Index()` — builds `AdminUsersModel` (see
`m4d/ViewModels/AdminUsersModel.cs`) from `UserMapper.GetUserNameDictionary()`, the list of role
names, and the known music-service list, then returns
`Vue3("User Administrator", "Admin: User list", "admin-users", model)`.

**DTO security note**: `ApplicationUser` contains `PasswordHash`, `SecurityStamp`,
`ConcurrencyStamp`, etc. The page uses a dedicated `AdminUserSummary` DTO
(`m4d/ViewModels/AdminUsersModel.cs`) that only carries the fields the view needs — the full
entity is never serialized.

**Vue page**: `m4d/ClientApp/src/pages/admin-users/App.vue`, model in `AdminUsersModel.ts`.

### Filtering

The table shows one row per user. Filtering is entirely client-side and built around **mutually
exclusive categories**, each with its own checkbox:

| Category | Membership rule | Default |
| --- | --- | --- |
| Basic | not Pseudo, not Unconfirmed, not Premium (whatever's left) | shown |
| Unconfirmed | `!isPseudo && !emailConfirmed` | hidden |
| Pseudo | `isPseudo` | hidden |
| Premium | `roles.includes("premium") \|\| lifetimePurchased > 0` (mirrors the old `UserMapper.GetPremiumUsers` rule) | shown |

A user is visible if **any** of their matching category checkboxes is checked (an OR across
categories). Because Basic is defined as "none of the others," unchecking Basic isolates
whichever special categories remain checked — e.g. check only Pseudo to see just service/pseudo
accounts, or only Premium to see just paying/premium-role users. This replaced a separate
`/ApplicationUsers/PremiumUsers` Razor page that existed only to show the premium-role-or-paid
user list; that page and `UserMapper.GetPremiumUsers` were removed once Premium became a filter
here.

`Private` is a separate, independent AND-filter (`privacy === 255`), not a category — it narrows
whatever the category checkboxes already produced. Checked (default) shows everyone regardless of
privacy; unchecked hides anyone whose privacy isn't fully public (255).

The checkbox list is ordered with the three on-by-default filters first (Basic, Premium, Private),
followed by the off-by-default ones (Unconfirmed, Pseudo).

A free-text search box filters the category-filtered set further by username or email substring.

### Other page features

- Client-side **sorting** via `<BTable>` column headers (bootstrap-vue-next handles this natively)
- Client-side **pagination** via `<BPagination>` + a per-page `<BFormSelect>` (25/50/100/250)
- Summary stats (Total/Registered/Confirmed/Deleted users, per-role counts, per-login-provider
  counts, service-preference breakdown) are computed from the **unfiltered** user list, so the
  checkboxes only affect the table, not the stats
- Per-row action links (plain `<a>`, no fetch) to the Razor auxiliary pages: Details, Edit,
  Delete, ChangeRoles, ClearPremium, Merge (pseudo users only), plus cross-page links to
  `Song/FilterUser`, `Searches/Index`, `UsageLog/UserLog`, `PlayList/Index` for that user
- Top-of-page links: New Pseudo User (`Create`), Clear Cache, Voting Results

### Notes

- `ClearCache` uses `RedirectToAction("Index")` (not `await Index()`) so it correctly bounces back
  through the Vue-serving action.
- The "Deleted Users" stat counts users whose `UserName` starts with `DEL:`. In current code,
  `ApplicationUsersController.DeleteConfirmed` renames a deleted user to their GUID `Id` and then
  hard-removes the row — no live user ever has a `DEL:`-prefixed name today. This stat (and the
  matching filter behavior, if it's ever revisited) is effectively vestigial; it's left alone
  since fixing it wasn't in scope for the filtering work and no bug was reported against it.

---

## PlayList/Index

**Controller**: `PlayListController.Index(PlayListType type, string user, bool showDeleted)` —
loads playlists of the given `type` (optionally scoped to `user`) and returns them via `Vue3()`.

**Vue page**: `m4d/ClientApp/src/pages/playlist/App.vue`, model in `PlayListPageModel.ts`.

### Controls

- **Type** (`SongsFromSpotify` / `SpotifyFromSearch`) is a server query parameter — switching it
  reloads the page. It is *not* a client-side filter.
- **User** filter is a text input pre-populated from the `user` URL param; it's a client-side
  substring filter over user/name/description/data1/id, not a server round-trip.
- **Show Active / Show Deleted** is fully client-side — both active and deleted playlists are
  always included in the payload; the toggle just switches which subset is displayed.
- `<BTable>` with sortable columns and per-page pagination; deleted rows are styled with
  `table-danger`.
- Per-row actions: Update / Edit / Details / Delete (active rows) or Undelete (deleted rows);
  Restore shown when `updated && !data2`.
- Sidebar actions: Create New, Restore All (SongsFromSpotify only), Update All, Delete All
  (requires a user filter + active rows + count > 0), BulkCreate (SpotifyFromSearch only),
  Statistics.

See `architecture/playlist-management.md` — **Vue Index Page** section — for full detail on the
playlist domain model itself.

---

## Searches/Index

**Controller**: `SearchesController.Index(string user, string sort, bool showDetails, bool
spotifyOnly, int page = 1)` — pages at the DB level (`Skip`/`Take`, page size 100), builds
`SearchesPageModel` (DTOs in `m4d/ViewModels/SearchesPageModel.cs`) with pre-built per-row URLs
(search URL, delete URL — both require `SongFilter` serialization, so they're built server-side),
and returns `Vue3("My Searches", "Search history", "searches", model)`.

**Vue page**: `m4d/ClientApp/src/pages/searches/App.vue`, model in `SearchesPageModel.ts`.

### Searches controls

- **Pagination**: each page click navigates the browser to `/Searches/Index?...&page=N` — there's
  no client-side page state to preserve.
- **Sort** (Most Popular / Most Recent) and **Spotify Only** are toggles that navigate to a
  recomputed URL; `Spotify Only` needs a `<script setup>` handler (`onSpotifyToggle()`) because
  Vue templates don't have `window` in scope for a direct `@change` binding.
- **Toggle Details** (admin-only) adds Query/User/Count/Created/Modified columns and navigates via
  a computed URL.
- Pagination UI: `BPagination` + a page-jump input (`Page [n] of N`), matching `SongFooter.vue`'s
  pattern. `total-rows="totalPages" per-page="1"` maps page numbers directly onto BPagination;
  `@page-click` intercepts BVN's internal routing and sets `window.location.href` to the
  server-rendered URL for the clicked page instead.

---

## ActivityLog/Index

**Controller**: `ActivityLogController.Index(int page = 1)` — same DB-level paging pattern as
Searches, builds `ActivityLogPageModel` (`m4d/ViewModels/ActivityLogPageModel.cs`), returns
`Vue3("Activity Log", "Admin: Activity log", "activity-log", model)`.

**Vue page**: `m4d/ClientApp/src/pages/activity-log/App.vue`, model in `ActivityLogPageModel.ts`.
Same `BPagination` + page-jump pattern as Searches; no client-side filtering.

---

## Shared design decisions

1. **Date serialisation**: dates cross the wire as camelCase JSON strings; TypeScript models
   declare them `string` and format with plain `Date` parsing at render time.
2. **Browser history**: filter/toggle state on the client-paged pages (admin-users, playlist)
   does not sync to the URL — acceptable for admin-only tooling.
3. **Pagination widgets**: client-paged pages use BVN `<BTable>`'s built-in `current-page`/
   `per-page` props with `<BPagination>`. Server-paged pages (Searches, ActivityLog) use
   `<BPagination total-rows="totalPages" per-page="1">` plus a page-jump input, with `@page-click`
   redirected to a server URL instead of BVN's internal routing.
4. **`window` in templates**: Vue 3's template compiler doesn't expose `window` as a global —
   any navigation needing `window.location.href` must go through a `<script setup>` handler
   function, not an inline template expression.
5. **PageFrame**: every `src/pages/*/App.vue` wraps its content in `<PageFrame>` for nav/header/
   footer chrome. All four pages here comply.

---

## Column Definitions (Admin Users Table)

| Column | Meaning | Notes |
| --- | --- | --- |
| `EC` | Email Confirmed | Check icon means the user has confirmed email; hollow circle means not confirmed. |
| `UserName / Email` | User identity | Username link opens user details; email shown below username. Pseudo users are italicized. |
| `Signed Up` | Account creation date | `StartDate` value, rendered as local date (`M/D/YYYY`). |
| `Signed In` | Last active date | `LastActive` value, rendered as local date. `1900-01-01` sentinel is displayed as `Never`. |
| `PRV` | Privacy flag | Numeric privacy byte (`0` = private / anonymous behavior, `255` = fully public). |
| `Contact Prefs` | Communication permissions | Human-readable decoding of `CanContact` bit flags (not raw numbers): `music4dance promo`, `partner promo`, `surveys/blog`, or combinations. `None` means no communication consent selected. |
| `Services` | Music service interests | Human-readable service names decoded from `ServicePreference` CIDs (for example Spotify, YouTube, Apple Music). `None` means no service preference selected. |
| `CCF` | Credit card failures | `FailedCardAttempts` count. |
| `$` | Lifetime purchases | Total amount purchased by the user. |
| `HC` | Hit count | Total tracked user requests/activity count. |
| `Roles` | Identity roles | ASP.NET Identity roles assigned to the user. |
| `Logins` | External login providers | Linked provider list (for example Microsoft, Google, Spotify, Facebook). |
| `(blank actions column)` | Admin actions | Links for songs/searches/usage/playlists plus role, edit, delete, and premium reset actions. |

### Communication Preference Encoding Reference

`CanContact` is a bit field (`ContactStatus`) with these flags:

- `1` (`0x01`): direct music4dance promotional messages
- `2` (`0x02`): partner promotional messages
- `4` (`0x04`): surveys / feedback / blog participation

Composite values are sums of selected flags (for example `5` = `1 + 4`). The UI presents decoded
text, not the numeric value.
