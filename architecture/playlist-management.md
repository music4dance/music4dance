# Playlist Management

## Overview

music4dance supports two types of playlists that bridge the internal song catalog with Spotify:

- **SongsFromSpotify** — An admin imports a Spotify playlist; tracks are matched to (or created as) songs in the catalog.
- **SpotifyFromSearch** — A search filter drives a Spotify playlist; the playlist is periodically refreshed from the current search results.

Playlist management is currently an admin-only feature (`[Authorize(Roles = "dbAdmin")]`).

---

## Data Model (`m4dModels/PlayList.cs`)

```csharp
public class PlayList
{
    public string Id { get; set; }       // Spotify playlist ID
    public string User { get; set; }     // m4d user ID (owner)
    public PlayListType Type { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public string Data1 { get; set; }    // see type-specific aliases below
    public string Data2 { get; set; }    // see type-specific aliases below
    public DateTime Created { get; set; }
    public DateTime? Updated { get; set; }
    public bool Deleted { get; set; }    // soft-delete flag
}
```

### `PlayListType` enum

| Value               | Description                                    |
| ------------------- | ---------------------------------------------- |
| `Undefined`         | Unused default                                 |
| `Music4Dance`       | Reserved / not actively used                   |
| `SongsFromSpotify`  | Songs are sourced from a Spotify playlist      |
| `SpotifyFromSearch` | Playlist content driven by a m4d search filter |

### `BulkCreateFlavor` enum

| Value       | Description                        |
| ----------- | ---------------------------------- |
| `TopN`      | Top-N most popular songs per dance |
| `Holiday`   | Holiday-themed songs per dance     |
| `Halloween` | Halloween-themed songs per dance   |

### Type-specific field aliases (`[NotMapped]`)

| Type                | `Data1` alias                              | `Data2` alias                      |
| ------------------- | ------------------------------------------ | ---------------------------------- | --- | --- | -------------------------------------------------------- |
| `SongsFromSpotify`  | `Tags` — dance/song tag string split by `" |                                    |     | "`  | `SongIds` — pipe-delimited m4d song IDs already imported |
| `SpotifyFromSearch` | `Search` — serialized search filter string | `Count` — target track count (int) |

`Tags` for `SongsFromSpotify` uses the format `<danceTags>|||<songTags>` where each half is a standard m4d tag string.

### Supporting model classes

| Class                                      | Purpose                                                                                                   |
| ------------------------------------------ | --------------------------------------------------------------------------------------------------------- |
| `GenericPlaylist`                          | Transport object for a loaded service playlist with tracks                                                |
| `PlaylistMetadata`                         | Lightweight info from Spotify (id, name, description, link, count)                                        |
| `PlaylistCreateInfo` / `SpotifyCreateInfo` | View models for user-facing playlist creation (with subscription gate on count > 100)                     |
| `ExportInfo`                               | View model for exporting a search to a playlist                                                           |
| `PlayListIndex`                            | Index view model: type, list of playlists, `ShowDeleted` flag, optional `FilteredUser` (null = all users) |

---

## Controller (`m4d/Controllers/PlayListController.cs`)

All actions require `dbAdmin` role. The controller extends `DanceMusicController` and receives dependencies via primary-constructor injection (DI).

### CRUD actions

| Action               | Method   | Route                     | Description                                                                                                                                                                                          |
| -------------------- | -------- | ------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `Index`              | GET      | `/PlayList`               | Lists playlists filtered by type and optional `user`. Renders via `Vue3()` helper as a client-side SPA. Both active and deleted playlists are sent to the client; visibility is toggled client-side. |
| `Details`            | GET      | `/PlayList/Details/{id}`  | Shows full details using the `_playlistDetails` partial.                                                                                                                                             |
| `Create`             | GET/POST | `/PlayList/Create`        | Creates a new playlist record. Sets `Created = DateTime.Now`, `Deleted = false`.                                                                                                                     |
| `Edit`               | GET/POST | `/PlayList/Edit/{id}`     | Edits Name, Description, and data fields.                                                                                                                                                            |
| `Delete`             | GET      | `/PlayList/Delete/{id}`   | Shows delete confirmation page. Accepts optional `user` query param; threads it through the form and Back to List link.                                                                              |
| `DeleteConfirmed`    | POST     | `/PlayList/Delete/{id}`   | **Soft-deletes**: sets `Deleted = true`, `Updated = DateTime.Now`. Redirects to active index, preserving `user` filter.                                                                              |
| `Undelete`           | GET      | `/PlayList/Undelete/{id}` | Shows undelete confirmation page. Accepts optional `user` query param; threads it through the form and Back to List link.                                                                            |
| `UndeleteConfirmed`  | POST     | `/PlayList/Undelete/{id}` | Restores a soft-deleted playlist: sets `Deleted = false`, `Updated = DateTime.Now`. Redirects to deleted index, preserving `user` filter.                                                            |
| `DeleteAll`          | GET      | `/PlayList/DeleteAll`     | Shows confirmation page listing all active playlists for `user` + `type`. Requires non-empty `user`; redirects to Index if missing.                                                                  |
| `DeleteAllConfirmed` | POST     | `/PlayList/DeleteAll`     | Soft-deletes all non-deleted playlists for the given `user` + `type`. Redirects to user-filtered active index.                                                                                       |

### Sync / update actions

| Action        | Trigger                 | Description                                                                                                                                 |
| ------------- | ----------------------- | ------------------------------------------------------------------------------------------------------------------------------------------- |
| `Update`      | GET                     | Kicks off async update for a single playlist via `AdminMonitor`. Skips deleted playlists.                                                   |
| `UpdateAll`   | GET                     | Kicks off async update for all non-deleted playlists of a given type.                                                                       |
| `UpdateBatch` | GET (anonymous + token) | Scheduled/automated entry point; uses `TokenRequirement.Authorize` for access control.                                                      |
| `BulkCreate`  | GET                     | Creates multiple `SpotifyFromSearch` playlists at once (`BulkCreateFlavor` enum). After creation, redirects to `Index` (SpotifyFromSearch). |
| `Restore`     | GET                     | Re-populates `SongsFromSpotify` playlist `SongIds` from the live Spotify playlist. Skips deleted playlists.                                 |
| `RestoreAll`  | GET                     | Restores all non-deleted `SongsFromSpotify` playlists.                                                                                      |
| `Statistics`  | GET                     | Shows live Spotify playlist metadata (calls Spotify API).                                                                                   |

### Core private helpers

- `DoUpdate(id, email, dms, principal)` — dispatches to `UpdateSongsFromSpotify` or `UpdateSpotifyFromSearch`.
- `UpdateSongsFromSpotify` — loads Spotify tracks, creates/merges songs into the catalog, updates `Name`/`Description`.
- `UpdateSpotifyFromSearch` — executes a search filter, pushes track list to Spotify via `MusicServiceManager.SetPlaylistTracks`.
- `GetTags(playlist)` — splits `Tags` on `"|||"` into dance tags and song tags.
- `DoRestore` — relinks existing catalog songs to a playlist by Spotify track ID.
- `SafeLoadPlaylist(id, dms)` — throws `ArgumentNullException` / `ArgumentOutOfRangeException` if not found.
- `GetIndex(type, showDeleted)` / `GetUserIndex(type, user, showDeleted)` — build `PlayListIndex` from EF query, filtering by `Deleted == showDeleted`. Used by non-Index actions (e.g., confirmation pages).
- `UserEmail(playlists)` — builds a `userId → email` map (needed to authenticate Spotify calls).

---

## Persistence / Serialization (`m4dModels/DanceMusicService.cs`)

Playlists participate in the tab-delimited backup/restore system used for index snapshots.

**Serialize format** (one playlist per line):

```
{User}\t{Type}\t{Data1}\t{Id}\t{Created}\t{Updated}\t{Deleted}\t{Data2}\t{Name}\t{Description}
```

**`SerializePlaylists(withHeader, from)`** — emits all playlists updated/created since `from`, preceded by a `PlaylistBreak` header line.

**`LoadPlaylists(lines)`** — parses the serialized format and upserts playlist records. It supports both legacy 3-4 column rows (bare `userId, tags, spotifyUrl`) and the current 5+ column format. The `Deleted` flag is round-tripped correctly.

---

## Views (`m4d/Views/PlayList/`)

All views are Razor (`.cshtml`), Bootstrap-styled, using `Html.ActionLink` / `Html.EditorFor` patterns.

| View                      | Model                    | Notes                                                                                                                                                          |
| ------------------------- | ------------------------ | -------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| ~~`Index.cshtml`~~        | _(removed)_              | **Replaced by the Vue SPA page** at `src/pages/playlist/App.vue`. The controller `Index` action now calls `Vue3()` with a `PlayListPageModel` DTO.             |
| `Details.cshtml`          | `PlayList`               | Uses `_playlistDetails` partial.                                                                                                                               |
| `_playlistDetails.cshtml` | `PlayList`               | Renders all fields in two-column rows.                                                                                                                         |
| `Edit.cshtml`             | `PlayList`               | Full form (Id, User, Type, Data1, Data2, Name, Description).                                                                                                   |
| `Delete.cshtml`           | `PlayList`               | Confirmation page. Hidden `user` field carries filter through POST. Back link returns to user-filtered active index.                                           |
| `Undelete.cshtml`         | `PlayList`               | Confirmation page. Hidden `user` field carries filter through POST. Back link returns to user-filtered deleted index.                                          |
| `DeleteAll.cshtml`        | `PlayListIndex`          | Bulk-delete confirmation. Shows count and full list of playlists to be deleted. Hidden `user` and `type` fields. Cancel returns to user-filtered active index. |
| `Create.cshtml`           | `PlayList`               | New playlist form.                                                                                                                                             |
| `Statistics.cshtml`       | `List<PlaylistMetadata>` | Table of live Spotify playlist metadata.                                                                                                                       |

---

## Spotify Integration

Spotify authentication is handled by `SpotifyAuthorization()` (calls `HttpContext.AuthenticateAsync` with the Spotify scheme). The Spotify API scope `playlist-modify-public` is requested at login time (`AuthenticationBuilderExtensions`).

The `UpdateBatch` endpoint is the automated/scheduled entry point and is protected by `TokenRequirement.Authorize` rather than cookie auth.

---

## Vue Index Page (`src/pages/playlist/`)

The `PlayList/Index` route is now a Vue SPA following the same pattern as `ApplicationUsers/Index`.

**Server side**

- `m4d/ViewModels/PlayListPageModel.cs` — `PlayListSummary` (per-row DTO) and `PlayListPageModel` (top-level). The `Index` controller action loads all playlists for the given type (both active and deleted) and calls `Vue3("Playlist Index", "Admin: Playlist list", "playlist", model)`.
- `type` remains a server query parameter (page reloads on type switch).
- `user` is an optional server query parameter that pre-populates the client-side filter.

**Client side** (`src/pages/playlist/`)

- `PlayListPageModel.ts` — TypedJSON-decorated models matching the C# DTOs.
- `App.vue` — Vue 3 Composition API page with:
  - `showDeleted` ref — client-side active/deleted toggle (no server round-trip)
  - `userFilter` ref — text filter on user/name/id; pre-populated from `model.filteredUser`
  - `filteredPlaylists` computed — applies both filters
  - Sidebar: Create New, Restore All (SongsFromSpotify), Update All, Show Active/Deleted toggle, Delete All (when user-filtered and active rows exist)
  - Type switcher links (SongsFromSpotify / SpotifyFromSearch) → trigger server reload with `?type=N`
  - BulkCreate links (SpotifyFromSearch only), Statistics link
  - `BTable id="playlist-table"` with sortable columns; deleted rows highlighted with `table-danger`
  - Per-row action links: Update \| Edit \| Details \| Delete (active) / Undelete (deleted); Restore shown when `updated && !data2`

---

## Known Gaps / Planned Improvements
