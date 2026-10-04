# Content Pages

**Type:** Reference
**Status:** Current
**Last verified:** 2026-10-03
**Code:** `m4d/Controllers/DanceController.cs`, `m4d/Controllers/CustomSearchController.cs`, `m4d/Controllers/SongController.cs` (`NewMusic`), `m4d/Controllers/HomeController.cs` (`SpotifyExplorer`), `m4d/ClientApp/src/pages/{dance-index,dance-details,ballroom-index,competition-category,country,wedding-dance-music,custom-search,new-music,spotify-explorer}/`

## Overview

The "content pages" are the mostly read-only landing pages that sit between the home page and the
song catalog: the dance style index, per-dance pages, competition category pages, wedding music,
the seasonal custom searches, New Music, and the Spotify Explorer tool. Each one is an MVC action
that calls the shared `Vue3(title, description, name, model, ...)` helper in `DMController`, which
renders `Views/Shared/Vue3.cshtml` with the Vue page `src/pages/<name>/App.vue` and serializes the
model into the global `model_`. Pages that pass `danceEnvironment: true` (or call
`BuildEnvironment3` themselves) also get `window.danceDatabaseJson`, which the client reads through
`safeDanceDatabase()`. Every page wraps its template in `PageFrame`.

Two other reference pages are documented separately: [tempo-list-page](tempo-list-page.md)
(`/Home/Tempi`) and [tempo-counter-page](tempo-counter-page.md) (`/Home/Counter`).

| URL | Action | Vue page | Model |
| --- | --- | --- | --- |
| `/dances` | `DanceController.Index` (no `dance`) | `dance-index` | none (dance database only) |
| `/dances/{dance-or-group}` | `DanceController.Index` | `dance-details` | `DanceModel` |
| `/dances/ballroom-competition-categories` | `DanceController.Index` | `ballroom-index` | `CompetitionGroup` (Ballroom) |
| `/dances/{category}` e.g. `american-rhythm` | `DanceController.Index` | `competition-category` | `CompetitionGroupModel` |
| `/dances/country` | `DanceController.Index` | `country` | `CompetitionGroup` (Country) |
| `/dances/wedding-music` | `DanceController.Index` | `wedding-dance-music` | `TagMatrix` |
| `/customsearch?name=...&dance=...&page=...` | `CustomSearchController.Index` | `custom-search` | `CustomSearchModel` |
| `/song/newmusic?type=...&page=...` | `SongController.NewMusic` | `new-music` | `SongListModel` |
| `/home/spotifyexplorer?link=...` | `HomeController.SpotifyExplorer` | `spotify-explorer` | none |

All of these except Spotify Explorer are listed in the site map tree in `SiteMapInfo.cs` (see
[blog-help-sitemap](blog-help-sitemap.md)).

## Routing for `/dances/...`

`M4dApplicationExtensions` maps three routes ahead of the default MVC route:

- `dances/{group}/{dance}` goes to `DanceController.GroupRedirect`, a permanent redirect to
  `/dances/{dance}` (the old two-level URL shape).
- `dances/edit` goes to `dance/edit`.
- `dances/{dance?}` goes to `DanceController.Index`.

`DanceController.Index(string dance)` is one dispatcher for all the slug-based pages. It calls
`BuildEnvironment3(..., danceEnvironment: true)` up front, then checks the slug in this order:

1. Empty: the style index (`dance-index`, help page `dance-styles`).
2. `ballroom-competition-categories`: `ballroom-index`.
3. `country`: `country`.
4. `wedding-music`: `wedding-dance-music`.
5. A Ballroom competition category whose `CompetitionCategory.CanonicalName` matches the slug
   (`BuildCanonicalName` lowercases and replaces spaces with hyphens, so `International Standard`
   becomes `international-standard`): `competition-category`, help page `dance-category`.
6. Otherwise a dance or dance group, looked up with `DanceStatsInstance.FromName`, which compares
   `DanceObject.SeoFriendly(name)` against each dance's and group's `SeoName`. Not found (or not in
   `DanceStatsManager.Instance.Map`, so a degraded database doesn't cause a query) returns a 404
   through `ReturnError`. A dance with `SongCount == 0` falls back to the Razor view
   `Views/Dance/emptydance.cshtml` (a "performance dance placeholder" page). Everything else renders
   `dance-details`, help page `dance-details`.

Because the special slugs are checked first, a dance or group can never be named
`country`, `wedding-music` or a Ballroom category name.

## Dance style index (`dance-index`)

A static page around `components/DanceTable.vue`: a `NameFilterInput` plus `DanceList` over
`dances.flatGroups`, filtered by `DanceDatabase.filterByName` and limited to groups and dances with
`getSongCount(id) > 0`. Below it are fixed links to the four Ballroom categories, the Ballroom
index, wedding music and the Tempi tool. No server model.

## Dance details (`dance-details`)

**Data.** `DanceModel` (`m4d/ViewModels/DanceModel.cs`, a `SongListModel`) is built from the
cached `DanceStats`: `Description`, `DanceLinks`, `SongTags`, `DanceTags`, and the top songs.
`DanceStats.RefreshTopSongs` resolves the stored `SongIds` from the stats song cache; if that
works, `Histories` holds those songs and `SpotifyPlaylist` holds the id of the
`SpotifyFromSearch` playlist whose name equals the dance name (attached when `DanceStatsInstance`
loads). No search query runs for this page.

**Rendering.** The client resolves the dance in the dance database by `model.danceId` and switches
on `DanceGroup.isGroup`:

- A **dance** shows the description, `TopTen` (a `SongTable` with a `SongFilter` of that dance
  sorted by `Dances`), the Spotify player, `DanceReference`, a `CompetitionCategoryTable` of
  `competitionDances` when there are any, reference links, and dance-tag and song-tag clouds.
- A **group** (e.g. `/dances/swing`) skips the top ten and lists its member dances with
  `DanceList`.

`DanceContents.vue` is the sidebar table of contents. It also links to the full song list
(`model.filter.url`) and to the dance's blog tag (`https://music4dance.blog/tag/{blogTag}`).

**Editing.** When `menuContext.isAdmin` (the `dbAdmin` role) is true, Edit/Save/Cancel buttons
appear. Save sends `PATCH /api/dances/{id}` with the description and links
(`APIControllers/DancesController.Patch`, which rechecks `dbAdmin` and calls
`DanceMusicCoreService.EditDance`).

## Competition pages

All three use the shared components `CompetitionDanceList`, `CompetitionCategoryTable` (with a
BPM/MPM toggle), `TempiLink` and `BlogTagLink`. The data is the static `DanceLibrary`
`CompetitionGroup` / `CompetitionCategory` objects, deserialized on the client into the matching
classes in `src/models/Competition.ts`.

- **`ballroom-index`** (`/dances/ballroom-competition-categories`) loops over
  `group.categories`, with one table per category and a heading that links to `/dances/{canonicalName}`.
- **`competition-category`** (`/dances/international-standard`, `international-latin`,
  `american-smooth`, `american-rhythm`): the server sends `CompetitionGroupModel`
  (`CurrentCategoryName` plus the whole `Group`). The client class derives `currentCategory` and
  `otherCategories` from those, shows the round table and an optional `extras` table, and links the
  sibling categories with `LinkCategory`.
- **`country`** (`/dances/country`) shows the single category of the `Country` group, with
  UCWDC / WORLDCDF / ACDA sourcing text. Only the Ballroom group is searched for category slugs, so
  there are no per-category Country pages.

## Wedding music (`wedding-dance-music`)

`DanceController.BuildWeddingTagMatrix` builds a `TagMatrix` (`m4dModels/TagMatrix.cs`) with four
columns (`Wedding:Other`, `First Dance:Other`, `Mother Son:Other`, `Father Daughter:Other`) and one
row group per dance group in `stats.Groups`, with child rows per dance. Each cell is
`DanceStats.SongTags.TagCount(tag)`, and rows with all zeros are dropped. The page renders it with
`TagMatrixTable`, where every count links to a song search for that dance plus that tag. The
introductory text has hard-coded search links (first dance, father/daughter, Castle and Slow
Foxtrot wedding songs).

## Custom searches (`custom-search`)

`/customsearch?name=holiday|christmas|halloween|broadway[&dance=...]` serves the seasonal and genre
landing pages. How the filter is built (`SongFilter.CreateCustomSearchFilter`, a raw OData filter
with action `customsearch`) and how the controller runs `SongSearch` directly are covered in
[song-filter: CustomSearchController](../search/song-filter.md#customsearchcontroller-canned-raw-filters).
What is specific to the page:

- `CustomSearchModel` (in `m4d/ViewModels/SongListModel.cs`) adds `Name`, `Description` (the quoted
  tag names, set by a `switch` in the controller), `Dance` and `PlayListId`.
- With a `dance`, the controller looks up the `SpotifyFromSearch` playlist named
  `"{Title} {DanceName}"` (for example "Holiday Slow Waltz") and the page embeds it with
  `SpotifyPlayer`. Those playlists are created and refreshed as described in
  [spotify-playlist-automation](../music-services/spotify-playlist-automation.md).
- `CustomSearchDanceChooser` shows one button per dance in the dance database, linking to
  `/customsearch?name=...&dance=...`. `CustomSearchHelp` (ways to contribute songs) appears when a
  dance-specific search has no results or fewer than 15.
- Degraded search: if `IsSearchAvailable()` is false, or `SongSearch` throws a search-service
  `InvalidOperationException`, the page renders with an empty list. The client also hides the list
  when `menuContext.searchHealthy === false`.
- `/Song/HolidayMusic` is a permanent redirect to this controller.

## New Music (`new-music`)

`SongController.NewMusic(type, page)` sets `Filter.Action = "newmusic"` on the ambient filter,
uses `type` as the sort order (defaulting to `Created`), and runs the standard `DoAzureSearch`
pipeline. `SongFilter.VueName` maps the `newmusic` action to the `new-music` page. See
[song-search-results](../search/song-search-results.md). The page has three toggle buttons,
Recently Added / Changed / Commented (`SortOrder.Created`, `Modified`, `Comments`), that navigate to
`/song/newmusic?type=...`. It shows a `SongTable` with `show-history` and sorting hidden, plus
`SongFooter` paging and `AdminFooter`.

## Spotify Explorer (`spotify-explorer`)

A beta tool, linked from the main menu only for beta or admin users
(`context.isBeta || context.isAdmin`), although the action itself has no authorization attribute.
The page has no server model. Everything happens on the client:

- One input takes a Spotify URL or id. `ServiceMatcher` classifies it as a playlist, user or track.
  `?link=` pre-fills it on load (`PlaylistLink.vue` uses this to drill from a user into a
  playlist).
- **Playlist**: `ServiceMatcher.findSpotifyPlaylist` calls `GET /api/serviceplaylist/s{id}`
  (`[Authorize]`; `ServicePlaylistController` looks it up with
  `MusicServiceManager.LookupPlaylistWithAudioData` and keeps it in a static in-memory cache).
  `PlaylistViewer` shows the tracks. For admins it also shows a form that `POST`s
  `/api/serviceplaylist?id=s{id}&tags=...` (`dbAdmin`, enforced on the server too) to register the
  playlist as a `SongsFromSpotify` playlist. This creates a pseudo-user for the owner if needed (see
  [playlist-management](../music-services/playlist-management.md)).
- **User**: `findSpotifyUser` calls `GET /api/serviceuser/s{id}` (`ServiceUserController`, also
  statically cached). `ServiceUserViewer` lists the user's playlists and can build a tab-separated
  export of playlists the user owns that aren't yet in music4dance.
- **Track**: hands off to `useDropTarget().checkServiceAndWarn`, the same lookup that ordinary
  search boxes use (see [drop-target-lookup](../songs/drop-target-lookup.md)).

## Future improvements

- The wedding page (`wedding-dance-music/App.vue`) and `TagMatrixTable.vue` build `?filter=`
  strings by hand, against the CLAUDE.md rule to use `SongFilter` / `DanceQueryItem` / `Tag`.
- `CustomSearchController.Index` repeats the `Vue3(...)` call and the empty `CustomSearchModel` in
  three branches, and keeps a second `switch` over `name` (for `Description`) that has to stay in
  sync with `CreateCustomSearchFilter`. An unknown `name` throws a plain `Exception` from
  `CreateCustomSearchFilter`, and an unknown `dance` isn't checked before
  `Database.DanceStats.FromName(dance).DanceName` is dereferenced, so both become 500s instead of
  404s.
- The `s_cache` dictionaries in
  `ServicePlaylistController` and `ServiceUserController` are unbounded, non-thread-safe statics.
- Spotify Explorer has small bugs: `clearForm` calls `document.getElementById("form")`, but the
  `BForm` has only `ref="form"`, so Clear would throw. The default-case error message is missing its
  `$` interpolation, and there's a `palceholder` typo.
- `DanceController.Index` calls `BuildEnvironment3` unconditionally, and then most branches pass
  `danceEnvironment: true` to `Vue3` as well, so the work is done twice.
- `dance-details/App.vue` gives both tag-cloud headings `id="tags"`.

## History

- 2023-12-16, Azure DevOps PR 443: the wedding dance page was converted to Vue 3.
- 2023-12-31, Azure DevOps PR 445: the Country dance test page was added.
- 2024-09-16, Azure DevOps PR 510: `holidaymusic` was refactored into `CustomSearchController` / `custom-search`.
- 2025-05-19, Azure DevOps PR 563: the Dance Style page was improved.
- 2025-11-01, #59: the Country Western competition page (`/dances/country`) was added; ACDA followed in #71.
- 2025-12-26, #95: degraded-mode handling for the dance and custom-search pages (dance stats `Map` check, empty results when search is unavailable).
- 2026-01-14, #99: `CustomSearchController` started catching search-service `InvalidOperationException`s and rendering an empty list.
- 2026-10-01: this doc was created.
- 2026-10-03: `POST /api/serviceplaylist` requires `dbAdmin` on the server, not just in the client.

## Related

- [tempo-list-page](tempo-list-page.md) and [tempo-counter-page](tempo-counter-page.md): the other dance reference pages
- [song-filter](../search/song-filter.md): the custom-search raw filter
- [song-search-results](../search/song-search-results.md): the `DoAzureSearch` pipeline behind New Music
- [blog-help-sitemap](blog-help-sitemap.md): `SiteMapInfo`, which lists these pages
- [spotify-playlist-automation](../music-services/spotify-playlist-automation.md): the `SpotifyFromSearch` playlists embedded on dance and custom-search pages
- [playlist-management](../music-services/playlist-management.md): `SongsFromSpotify` playlists created from Spotify Explorer
