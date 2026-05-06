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
    public bool Deleted { get; set; }    // soft-delete flag — present in model but NOT currently used by Delete action
}
```

### `PlayListType` enum

| Value               | Description                                    |
| ------------------- | ---------------------------------------------- |
| `Undefined`         | Unused default                                 |
| `Music4Dance`       | Reserved / not actively used                   |
| `SongsFromSpotify`  | Songs are sourced from a Spotify playlist      |
| `SpotifyFromSearch` | Playlist content driven by a m4d search filter |

### Type-specific field aliases (`[NotMapped]`)

| Type                | `Data1` alias (→ `Tags`)                   | `Data2` alias (→ `SongIds`)        |
| ------------------- | ------------------------------------------ | ---------------------------------- | --- | --- | -------------------------------------------------------- |
| `SongsFromSpotify`  | `Tags` — dance/song tag string split by `" |                                    |     | "`  | `SongIds` — pipe-delimited m4d song IDs already imported |
| `SpotifyFromSearch` | `Search` — serialized search filter string | `Count` — target track count (int) |

`Tags` for `SongsFromSpotify` uses the format `<danceTags>|||<songTags>` where each half is a standard m4d tag string.

### Supporting model classes

| Class                                      | Purpose                                                                               |
| ------------------------------------------ | ------------------------------------------------------------------------------------- |
| `GenericPlaylist`                          | Transport object for a loaded service playlist with tracks                            |
| `PlaylistMetadata`                         | Lightweight info from Spotify (id, name, description, link, count)                    |
| `PlaylistCreateInfo` / `SpotifyCreateInfo` | View models for user-facing playlist creation (with subscription gate on count > 100) |
| `ExportInfo`                               | View model for exporting a search to a playlist                                       |
| `PlayListIndex`                            | Index view model: type + list of playlists                                            |

---

## Controller (`m4d/Controllers/PlayListController.cs`)

All actions require `dbAdmin` role. The controller extends `DanceMusicController` and receives dependencies via primary-constructor injection (DI).

### CRUD actions

| Action            | Method   | Route                    | Description                                                                      |
| ----------------- | -------- | ------------------------ | -------------------------------------------------------------------------------- |
| `Index`           | GET      | `/PlayList`              | Lists playlists filtered by type (and optionally by user).                       |
| `Details`         | GET      | `/PlayList/Details/{id}` | Shows full details using the `_playlistDetails` partial.                         |
| `Create`          | GET/POST | `/PlayList/Create`       | Creates a new playlist record. Sets `Created = DateTime.Now`, `Deleted = false`. |
| `Edit`            | GET/POST | `/PlayList/Edit/{id}`    | Edits Name, Description, and data fields.                                        |
| `Delete`          | GET      | `/PlayList/Delete/{id}`  | Shows delete confirmation page.                                                  |
| `DeleteConfirmed` | POST     | `/PlayList/Delete/{id}`  | **Hard-deletes** the row with `DbSet.Remove`. Does NOT set `Deleted = true`.     |

> **Gap**: The `Deleted` field exists on the model and is displayed in the index table, but the `DeleteConfirmed` action performs a hard delete rather than a soft delete.

### Sync / update actions

| Action        | Trigger                 | Description                                                                                |
| ------------- | ----------------------- | ------------------------------------------------------------------------------------------ |
| `Update`      | GET                     | Kicks off async update for a single playlist via `AdminMonitor`.                           |
| `UpdateAll`   | GET                     | Kicks off async update for all playlists of a given type (admin UI).                       |
| `UpdateBatch` | GET (anonymous + token) | Scheduled/automated entry point; uses `TokenRequirement.Authorize` for access control.     |
| `BulkCreate`  | GET                     | Creates multiple `SpotifyFromSearch` playlists at once (TopN, Holiday, Halloween flavors). |
| `Restore`     | GET                     | Re-populates `SongsFromSpotify` playlist `SongIds` from the live Spotify playlist.         |
| `RestoreAll`  | GET                     | Restores all `SongsFromSpotify` playlists.                                                 |
| `Statistics`  | GET                     | Shows live Spotify playlist metadata (calls Spotify API).                                  |

### Core private helpers

- `DoUpdate(id, email, dms, principal)` — dispatches to `UpdateSongsFromSpotify` or `UpdateSpotifyFromSearch`.
- `UpdateSongsFromSpotify` — loads Spotify tracks, creates/merges songs into the catalog, updates `Name`/`Description`.
- `UpdateSpotifyFromSearch` — executes a search filter, pushes track list to Spotify via `MusicServiceManager.SetPlaylistTracks`.
- `GetTags(playlist)` — splits `Tags` on `"|||"` into dance tags and song tags.
- `DoRestore` — relinks existing catalog songs to a playlist by Spotify track ID.
- `SafeLoadPlaylist(id, dms)` — throws `ArgumentNullException` / `ArgumentOutOfRangeException` if not found.
- `GetIndex` / `GetUserIndex` — build `PlayListIndex` from EF query.
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

| View                      | Model                    | Notes                                                                                                                                                      |
| ------------------------- | ------------------------ | ---------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `Index.cshtml`            | `PlayListIndex`          | Table with Update/Edit/Details/Delete/Restore links per row. Shows `Deleted` column. Restore link only appears when `Updated` is set and `Data2` is empty. |
| `Details.cshtml`          | `PlayList`               | Uses `_playlistDetails` partial.                                                                                                                           |
| `_playlistDetails.cshtml` | `PlayList`               | Renders all fields in two-column rows.                                                                                                                     |
| `Edit.cshtml`             | `PlayList`               | Full form (Id, User, Type, Data1, Data2, Name, Description).                                                                                               |
| `Delete.cshtml`           | `PlayList`               | Confirmation page using `_playlistDetails`. Posts to `DeleteConfirmed`.                                                                                    |
| `Create.cshtml`           | `PlayList`               | New playlist form.                                                                                                                                         |
| `Statistics.cshtml`       | `List<PlaylistMetadata>` | Table of live Spotify playlist metadata.                                                                                                                   |

---

## Spotify Integration

Spotify authentication is handled by `SpotifyAuthorization()` (calls `HttpContext.AuthenticateAsync` with the Spotify scheme). The Spotify API scope `playlist-modify-public` is requested at login time (`AuthenticationBuilderExtensions`).

The `UpdateBatch` endpoint is the automated/scheduled entry point and is protected by `TokenRequirement.Authorize` rather than cookie auth.

---

## Known Gaps / Planned Improvements

### 1. Soft-delete instead of hard-delete

The `Deleted` field is defined, serialized, and displayed in the index table, but `DeleteConfirmed` currently does a hard `DbSet.Remove`. The fix is straightforward:

```csharp
// In DeleteConfirmed:
playList.Deleted = true;
playList.Updated = DateTime.Now;
// Save changes (no Remove)
```

The index query in `GetIndex` should then be updated to filter out deleted playlists by default (with an option to show them), and the serialization/deserialization already handles the flag correctly.

### 2. Vue UX migration

The playlist management views are traditional Razor MVC pages. Migrating to Vue would follow the existing MPA pattern used elsewhere in the app:

- A new `PlaylistManager.vue` page component (mounted on a Razor layout shell)
- API controller (`PlaylistApiController`) exposing JSON endpoints for CRUD and trigger actions
- Leverage existing Vue component patterns: `BTable`, `BButton`, `BModal` from bootstrap-vue-next
- Reactive soft-delete: mark deleted in-place and re-filter the displayed list without a full page reload
- Status feedback for long-running async operations (Update, Restore) using the existing `AdminMonitor` status polling pattern

### 3. Other observations

- **`GetIndex` does not filter `Deleted` playlists** — all rows are shown regardless of the `Deleted` flag.
- **`BulkCreate` uses hard-coded flavor strings** (`"TopN"`, `"Holiday"`, `"Halloween"`) — an enum would be safer.
- **`DeleteConfirmed` uses a string-interpolation format bug**: `$"Playlist ${id} not found."` should be `$"Playlist {id} not found."`.
