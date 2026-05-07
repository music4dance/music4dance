# PR: Playlist Soft Delete & User Filter Improvements

## Summary

Replaces hard delete with soft delete for admin playlist management, adds per-user filter persistence, and introduces a bulk "Delete All" action.

## Changes

### Models (`m4dModels/PlayList.cs`)

- Added `BulkCreateFlavor` enum (replaces magic string switch in `BulkCreate`)
- Added `ShowDeleted` and `FilteredUser` fields to `PlayListIndex`

### Controller (`PlayListController.cs`)

- **`Index`**: added `showDeleted` and `user` parameters; delegates to `GetUserIndex` when user is provided
- **`Delete` / `DeleteConfirmed`**: soft-delete (`Deleted = true`, `Updated = DateTime.Now`) instead of `Remove()`; accepts and preserves optional `user` filter param
- **`Undelete` / `UndeleteConfirmed`**: new actions to restore a soft-deleted playlist; redirects back to the deleted view preserving user filter
- **`DeleteAll` / `DeleteAllConfirmed`**: new bulk soft-delete for all active playlists belonging to a specific user; requires non-empty user
- **`Update` / `Restore` / `UpdateAllBase` / `RestoreAll`**: guard against operating on deleted playlists
- Fixed string interpolation bug: `"Playlist ${id}"` → `"Playlist {id}"`
- `BulkCreate`: switched from `string flavor` with string-literal cases to `BulkCreateFlavor` enum

### Views

- **`Index.cshtml`**:
  - Header shows filtered user name with "Clear filter" link when a user filter is active
  - Show Deleted/Show Active toggle preserves user filter
  - Per-row Delete and Undelete links carry user filter
  - "Delete All" link appears in sidebar when user-filtered and there are active playlists
  - Deleted rows highlighted with `table-danger`; show Undelete action only
- **`Delete.cshtml`**: hidden `user` field threads filter through POST; Back to List preserves user filter
- **`Undelete.cshtml`**: same user-filter threading; fixed `@ViewBag` → `@ViewBag.Title` in heading
- **`DeleteAll.cshtml`** _(new)_: confirmation page showing user, type, and full list of playlists to be deleted

### Documentation

- Updated `architecture/playlist-management.md` to reflect all new actions, views, and the soft-delete/user-filter design
