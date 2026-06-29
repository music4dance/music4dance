# SongController — Search & Song-List Results

## Overview

`SongController` (`m4d/Controllers/SongController.cs`) is the entry point for everything that
returns a **list of songs** to the browser, as well as song editing/details/admin/export actions.
This document covers only the **list-returning** surface: every action whose job is to produce a
page (or raw payload) of songs. Editing, details, merge, and batch-admin actions are stubbed at the
bottom for future coverage.

There are two structurally distinct families of list results:

1. **Filter-driven search** — the vast majority of actions. They mutate the ambient `SongFilter`
   (`Filter` property, inherited from `ContentController`) and funnel through `DoAzureSearch()` →
   `SongSearch.Search()` → `SongIndex.Search`/`VoteSearch`/`PostSearch` → `FormatResults()` →
   `FormatSongList()` → `Vue3(..., new SongListModel {...})`. The result is paginated, filter-aware,
   and rendered by the `song-index` (or `new-music`/`holiday-music`) Vue page.
2. **One-off, non-filter list results** — actions that build a list of songs by some other means
   (a Spotify playlist, an uploaded file of IDs, an in-memory merge-candidate scan, a list of GUIDs)
   and either reuse `FormatSongList()` with the ambient `Filter` just along for the ride, or render
   an entirely separate model/Vue page (`Playlist` → `PlaylistViewerModel` / `playlist-viewer`).

---

## The Ambient `SongFilter`

`ContentController.OnActionExecutionAsync` runs before every action and sets:

```csharp
ViewBag.SongFilter = Filter = GetFilterFromContext(context);
```

`GetFilterFromContext` (`DMController.cs`) reads a serialized filter string from the `filter` query
param (or POST form field) and rehydrates it via `Database.SearchService.GetSongFilter(filterString)`;
if absent, it falls back to `SongFilter.GetDefault(user)`. This means **every** action on the
controller — search or not — starts with a `Filter` already populated from the request, and most
search actions only need to mutate a few fields on it before re-running the search.

`SongFilter` carries the full query state: `Dances`, `SearchString`, `Tags`, `User` (a serialized
`UserQuery`, supports identity/vote queries), `Purchase` (service filter), `TempoMin/Max`,
`LengthMin/Max`, `Page`, `Level` (bonus-content bitmask), `SortOrder`, plus `Action` (drives
`VueName`/help-page selection) and `CruftFilter`.

---

## Core Pipeline (Filter-Driven Search)

```
Action (mutates Filter) → DoAzureSearch() → SongSearch.Search() → FormatResults() → FormatSongList() → Vue3()
```

### `DoAzureSearch()` — [SongController.cs:201](../m4d/Controllers/SongController.cs#L201)

- Bails out early to an empty `FormatSongList` if `IsSearchAvailable()` is false (Azure Search
  marked unhealthy by `ServiceHealthManager`).
- Throws `RedirectException("BotFilter", Filter)` for spider/bot user agents (unless the filter is
  already an "empty bot" filter), caught by `HandleRedirect`.
- Delegates the actual search to a new `SongSearch` (`m4d/Services/SongSearch.cs`), passing the
  ambient `Filter`, current user, premium status, `SongIndex`, and admin flag.
- Catches `InvalidOperationException` search-service errors and degrades to an empty result list
  rather than throwing.

### `SongSearch.Search()` — the next layer down

`DoAzureSearch` hands off to `SongSearch.Search()` for the actual query: premium gating, resolving
`Filter.UserQuery` (identity vs. named user vs. vote query, with anonymize/deanonymize on either
side), building the Azure `SearchOptions`, firing the fire-and-forget `LogSearch` analytics write
(see [[saved-searches]]), and dispatching to either a direct `SongIndex.Search` call or the
in-memory `VoteSearch`/`PostSearch` post-filter path (used when vote data — unfilterable in the
Azure schema — has to be checked song-by-song after streaming candidates via `StreamAll`).

**Full writeup**: [[song-search-service]] — covers `Search()` step-by-step, `VoteSearch`,
`EditedBySearch`, the shared `PostSearch`/`StreamAll` mechanics, `Page()`, and `LogSearch`.

### `FormatResults()` / `FormatSongList()` — [SongController.cs:235](../m4d/Controllers/SongController.cs#L235)

- `FormatResults(SearchResults)` is a thin adapter: unwraps `Songs`/`TotalCount`/`RawCount` and
  calls `FormatSongList`.
- `FormatSongList` is the single place that turns a `IReadOnlyCollection<Song>` into the page model:
  - Anonymizes the ambient `Filter` for display if a user is logged in (`Filter.Anonymize(user)`).
  - Converts every `Song` to a `SongHistory` via `AnonymizeSongs` (anonymizes vote/edit attribution
    based on viewer identity — see [[user-name-visibility]]).
  - Optionally attaches `SearchRequestDiagnostics` (raw Azure query/filter/sort) for users in the
    `DiagRole`.
  - Renders `Vue3(title, description, Filter.VueName, new SongListModel {...}, danceEnvironment: true)`.
  - `Filter.VueName` picks the Vue page component: `new-music`, `holiday-music`, or the default
    `song-index` for everything else (advanced search, raw search, tag browsing, plain index, etc.)
    — they're all variations rendered by the same underlying list component with different
    title/filter wiring.

`SongListModel` (`m4d/ViewModels/SongListModel.cs`) is the payload: `Histories`, a sparse
`SongFilterSparse` (serializable view of the current filter, used by the client to build follow-up
requests), `Count` (matched total), `RawCount` (pre-cruft-filter total), and optional
`HiddenColumns` (used by admin views like `MergeCandidates` to suppress irrelevant columns).

---

## Filter-Driven Search Actions

All of these end in `DoAzureSearch()` (directly or via a redirect/alias) and share the pipeline above.
They differ only in *how* they mutate `Filter` before searching.

| Action | Route | Auth | Filter mutation |
| --- | --- | --- | --- |
| `Search` | `/Song/Search` | anon | Delegates to `AzureSearch` |
| `AzureSearch` | `/Song/AzureSearch` | anon | Sets `Dances`/`SearchString`/`Page`; resets `Purchase`/`TempoMin`/`TempoMax` |
| `Index` | `/` (Song/Index) | anon | Default landing page: sets `Dances` from route `id`, `Page`, defaults `User` to "my songs" if authenticated and filter is empty, sets `Purchase` |
| `Advanced` | `/AdvancedIndex` | anon | Alias for `Index` (TODO: confirm still reachable) |
| `AdvancedSearch` | `/Song/AdvancedSearch` | anon | Full advanced-search form handler: searchString, dances, tags, services (→`Purchase`), tempo range, user, sort, bonus-content level — each resets `Page` to 1 if changed |
| `RawSearch` | `/Song/RawSearch` | anon | Rebuilds `Filter` entirely from a `RawSearch` model (raw OData/Lucene query) bound from the form |
| `FilterSearch` | `/Song/FilterSearch` | anon | No mutation — re-runs search on whatever `Filter` the query string already encodes |
| `Sort` | `/Song/Sort` | anon | Sets `SortOrder` via `SongSort` |
| `FilterUser` | `/Song/FilterUser` | anon | Sets/clears `User` |
| `FilterService` | `/Song/FilterService` | anon | Sets `Purchase` from selected services |
| `FilterTempo` | `/Song/FilterTempo` | anon | Sets `TempoMin`/`TempoMax` |
| `Tags` / `AddTags` / `RemoveTags` | `/Song/Tags` etc. | anon | Set/merge/subtract `Tags` (via `TagList`) |
| `NewMusic` | `/Song/NewMusic` | anon | Sets `Action = "newmusic"`, `SortOrder` (default `Created`), `Page`, and a fixed curator `User` query |
| `HolidayMusic` | `/Song/HolidayMusic` | anon | Permanent redirect to `CustomSearchController` — not actually handled here |

`BulkEdit`'s default `switch` arm and `MergeCandidates` also produce song lists but **don't** go
through `DoAzureSearch`/`SongSearch` — see below.

---

## Non-Filter-Pipeline List Results

These return song lists but bypass `SongSearch` (and in some cases bypass Azure Search entirely).

### `Playlist(string id)` — [SongController.cs:132](../m4d/Controllers/SongController.cs#L132)

The endpoint this document was requested to set up for generalization. **Distinct from the admin
playlist CRUD system** documented in [[playlist-management]] — that system manages persisted
`PlayList` rows and periodic Spotify sync; this action is a stateless, anonymous, read-only viewer
for an *arbitrary* Spotify playlist URL, matched on the fly:

1. Looks up the Spotify playlist by ID via `MusicServiceManager.LookupPlaylist(spotify, "/playlist/{id}")`
   (live Spotify API call, not the catalog).
2. Builds an OData filter directly (`ServiceIds/any(id: search.in(id, 'S:track1,S:track2,...'))`)
   matching catalog songs whose Spotify service ID appears in the playlist's track list.
3. Calls `SongIndex.Search(null, options, CruftFilter.NoCruft)` directly — no `SongFilter`, no
   `SongSearch`, no pagination (`Size = 1000`, i.e. the whole playlist in one page).
4. Re-sorts the matched catalog songs into the **Spotify playlist's track order** (matched catalog
   songs only — tracks with no catalog match are silently dropped, and a song matching multiple
   playlist tracks resolves to its first match).
5. Builds a `PlaylistViewerModel` (`Id`, `Histories`, playlist `Name`/`Description`/`Owner*`,
   `TotalCount` = playlist track count, **not** matched-song count) and renders it via the
   `playlist-viewer` Vue component — not `song-index`, and not `SongListModel`.

Because this bypasses `SongFilter`/`SongSearch` entirely, it has none of: paging, sort order,
dance/tempo/tag filtering, premium gating, or search logging. It's effectively a single hard-coded
"give me whatever's in this Spotify playlist" view. Generalizing it (e.g. supporting other services,
exposing it for arbitrary embedding, or merging its read path with the filter-driven pipeline) is
the next piece of work.

### `List(IFormFile fileUpload)` — [SongController.cs:502](../m4d/Controllers/SongController.cs#L502)

POST-only. Parses an uploaded file of song IDs (`UploadFile`), calls `SongIndex.List(ids)` (a
dedicated Azure `search.in(SongId, ...)` lookup, `Size = 1000`, no `SongFilter` involved), then
`FormatResults` → same `SongListModel`/`song-index` rendering as the main pipeline.

### `MergeCandidates(...)` — [SongController.cs:1330](../m4d/Controllers/SongController.cs#L1330), admin-only

Doesn't query Azure Search at all — calls `Database.MergeManager.GetMergeCandidates(...)`, an
in-memory/SQL duplicate-detection scan, optionally runs `AutoMerge`, then pages the result list
in-process (`Skip`/`Take` on the in-memory `List<Song>`) before `FormatSongList`. See
[[song-merge-algorithm]] for the candidate-detection logic.

### `BulkEdit` default arm — [SongController.cs:1418](../m4d/Controllers/SongController.cs#L1418), admin-only

Looks up an explicit set of `Guid[] selectedSongs` via `SongIndex.FindSongs`, and if `action` isn't
one of the merge/delete/cleanup commands, just calls `FormatSongList(list, list.Count)` to redisplay
them as a song list (used as the "preview selection" no-op path).

### `DownloadJson(SongFilter filter, ...)` — [SongController.cs:1896](../m4d/Controllers/SongController.cs#L1896), admin-only

Takes an explicit `filter` parameter (not the ambient `Filter`), runs `SongIndex.Search` directly,
and returns raw camelCase JSON of anonymized `SongHistory` — no Vue rendering, no `SongListModel`.
Only the `"H"` (history) type is implemented; anything else returns `null`.

---

## Stubbed for Future Coverage (Not Search-Result-Returning)

The remaining actions on `SongController` edit data, show single-song detail pages, or run
admin/batch jobs. Listed here as placeholders only:

- **Song detail**: `Details`, `Album`, `Artist`, `Augment`, `UpdateSongAndServices`,
  `UpdateRatings`, `CleanupAlbums`, `CleanMusicServices`.
- **Editing/deletion**: `Delete`/`DeleteConfirmed`, `UndoUserChanges`.
- **Merge workflow**: `MergeResults`, `SimpleMerge`, `Merge`/`SongMerge` (preview),
  `ClearMergeCache`. See [[song-merge-algorithm]].
- **Spotify playlist creation** (distinct again from both `Playlist` above and
  [[playlist-management]]): `CreateSpotify` (GET form + POST handler) builds a *new* Spotify
  playlist from the current search filter, pushing tracks page-by-page via
  `MusicServiceManager.SetPlaylistTracks`.
- **CSV export**: `ExportPlaylist` (GET form + POST handler), backed by `PlaylistExport`.
- **Batch admin jobs** (`BatchCorrectTempo`, `BatchAdminEdit`, `BatchAdminModify`,
  `BatchUpdateService`, `BatchCleanService`, `BatchCleanupProperties`, `BatchReloadSongs`,
  `CheckProperties`, `BatchSamples`, `BatchEchoNest`) — all funnel through the private
  `BatchProcess`/`BatchAdminExecute` helpers, which page through Azure Search themselves
  (independent of `SongSearch`) to drive async fire-and-forget mutation jobs tracked via
  `AdminMonitor`.
