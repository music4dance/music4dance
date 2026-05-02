# PR: Tempo-Bot Cleanup — Admin Search, Bulk Modify, and Streaming

## Summary

Adds admin search and bulk song re-attribution tooling (originally needed to clean up songs mis-attributed to the tempo-bot), improves memory efficiency of post-filtered searches, and documents the supporting conventions.

## Changes

### New features

- **AdminSearch** (`GET /Admin/AdminSearch`): Find songs last edited by a given user within an optional date range. Uses `WasEditedBy` post-filter over `SearchAll`.
- **AdminModifyBySearch** (`POST /Admin/AdminModifyBySearch`): Apply a `SongModifier` to all songs returned by an `AdminSearch` in a background task. Used to bulk re-attribute edit blocks (e.g. from `tempo-bot` to `tempo-bot|P`).
- `SuggestedModifierJson` on `AdminSearchModel` pre-fills the re-attribution modifier with `"replace": "batch|P"`.

### `SongIndex.StreamAll` — streaming Azure Search results

Adds `StreamAll` as a `virtual async IAsyncEnumerable<Song>` method that pages Azure Search in 1000-song batches. Non-matching songs transit memory one page at a time and are released before the next fetch.

`SearchAll` now delegates to `StreamAll` (one-line wrapper), so existing callers are unaffected.

### `PostSearch` — streaming post-filter

`VoteSearch` and `EditedBySearch` now stream through `StreamAll` via `await foreach` instead of loading all Azure results into memory at once. Non-matching songs are released page-by-page; matching songs accumulate for in-memory pagination. Removes the previous silent 500-song hard cap on post-filtered results.

### Tests

- Updated `SongSearchPostSearchTests` mocks from `SearchAll → ReturnsAsync(list)` to `StreamAll → Returns(AsAsyncEnumerable(list))`.

### Architecture documentation

- **`admin-search-bulk-modify.md`** (new): Documents the AdminSearch + AdminModifyBySearch mechanism, `SuggestedModifierJson`, the `modifier:'a'` user query convention, and the `batch|P` pseudo-user value.
- **`vote-search.md`** (updated): Reflects unbounded streaming, removes old batch-size limitation text, documents the per-page re-scan behavior and its trade-offs.
- **`bulk-admin-modify.md`** (updated): Adds `FromDate`/`ToDate` field documentation and a section on the `batch|P` pseudo-user convention.
- **`song-details-viewing-editing.md`** (updated): Adds a section on the `|P` pseudo-user suffix — server storage, `isPseudo`/`isSystemTempo` flags, pencil icon visibility, and Song History Viewer filtering.
- **`tempo-bot-reattribution-plan.md`** (deleted): Superseded by `admin-search-bulk-modify.md`.
