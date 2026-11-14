# Song Unmerge

**Type:** Plan
**Status:** In progress (branch `song-unmerge`)
**Last verified:** 2026-11-14 (merge code read: `SongIndex.BuildSimpleMerge`, `RenumberAlbums`, `FindMergedSong`, `UploadIndex`, `Song.GetAltids`, `SongCore.vue` admin section)
**Code:** `m4dModels/SongIndex.cs` (new `UnmergeSong`), `m4dModels/Song.cs` (`.Unmerge` command, `GetAltids`), `m4d/Controllers/SongController.cs` (new `Unmerge` action), `m4d/ClientApp/src/models/SongHistory.ts`, `m4d/ClientApp/src/pages/song/components/SongCore.vue`

## Goal

An admin looking at a song made by a simple merge can pull one or more of its source songs back
out as separate songs. The rest of the merge, and every edit made after it, stays on the merged
song under its current id.

## What a simple merge leaves to work with

From [song-merge-algorithm](../songs/song-merge-algorithm.md) § Merging:

- Every `.Create` and `.Edit` block of every source is stamped with that source's id
  (`.Create={guid}`, `.Edit={guid}`), the blocks are sorted by time, and a final
  `.Merge=id1;id2;…` block is appended. The merged song gets a new id.
- Album properties were renumbered (`RenumberAlbums`) into one shared numbering. Same-named
  albums share an index. `OrderAlbums` was dropped.
- The sources were deleted, and a deleted song leaves the index entirely (`IsNull` → removed
  from Azure). Their history now exists only inside the merged song.
- The merged song's `AlternateIds` (from `Song.GetAltids`, every id in every `.Merge`) do two
  jobs: `FindMergedSong` redirects an old source id to the merged song, and
  `UploadIndex(trackDeleted: true)` deletes every alternate id from the index.
- Blocks added after the merge (the post-merge editor's `.Edit`, the shared-id queue's name
  edit, later votes, `batch-s`/`batch-i` enrichment) have an empty `.Edit=` value, so they
  belong to no source.

So, for the most recent `.Merge`, every block before it carries its owner, and that is enough to
rebuild each source.

## What counts as unmergeable

A song can be unmerged when its **last** `.Merge` block lists at least two ids and each of them has
a `.Create={id}` block. Songs merged by the old field-by-field merge (removed 2026-11) have no
stamps and don't qualify; the UI doesn't offer unmerge for them.

**Only one layer can be undone.** Merging an already-merged song restamps its blocks with its own
id, so the earlier layer's owners are overwritten. Unmerging the outer layer gives back the
intermediate merged song whole, and it can't be split any further. That's acceptable: the usual
case is "this last merge was wrong".

## The operation

`SongIndex.UnmergeSong(ApplicationUser user, Song merged, IReadOnlyCollection<Guid> extract)`,
where `extract` is a non-empty, proper subset of the last `.Merge`'s ids (at least one source has
to stay, so the merged song's id and links survive).

1. Reload the song in full and parse it with `SongPropertyBlockParser.ParseBlocks`.
2. Split the blocks:
   - A `.Create`/`.Edit` block stamped with an id in `extract` goes to that source.
   - Every other block stays on the remainder: blocks of the sources that stay, post-merge
     blocks, and the unstamped `.Merge`/`.NoMerge` blocks of earlier merges. Those carry no owner
     (see [Decisions](#decisions) 3). A block carrying `MergeSource={id}` for an extracted id
     goes to that source.
3. For each extracted source:
   - Clear the stamp (`.Create=`, `.Edit=`) so it looks as it did before the merge.
   - Compact its album indices to 0..n in order of first appearance, the reverse of
     `RenumberAlbums`, so it doesn't carry gaps from the other sources' albums.
   - Append a `.NoMerge` block when the admin asks for it ([Decisions](#decisions) 1).
   - `Song.Create(sourceId, props, …)` with the **original id**. That id isn't in the index
     (the source was deleted), so old links to it go back to working, and its own
     `AlternateIds` come back from any earlier `.Merge` it carried.
4. For the remainder: keep its id and its blocks, and append
   `.Unmerge=idA;idB  User=…  Time=…`. Changing the original `.Merge` would lose the audit
   trail; appending keeps the log append-only.
5. Save all the songs in one `SaveSongs` call, then rebuild dance stats
   (`DanceStatsManager.ClearCache`) the way the merge action does, logging a failed rebuild as a
   warning.

### `.Unmerge` and alternate ids

- Add `Song.UnmergeCommand = ".Unmerge"` as an action. In `Song.Load` it changes no field. Check
  that `SongHistory` and `SongHistoryLog` on the client show it as an ordinary action row.
- `Song.GetAltids()` becomes "ids in any `.Merge`, minus ids in any later `.Unmerge`". **This is
  required, not cosmetic.** Without it, the remainder keeps listing the extracted ids as
  alternates, and the next `UploadIndex(trackDeleted: true)` (a reload or restore) would delete
  the songs we just rebuilt. It would also send a link to them to the remainder once they are
  deleted for some other reason.

### Album properties in post-merge blocks

A post-merge edit can touch an album index that belongs only to a source being extracted. For
example, `batch-s` adds `Purchase:3:SS=…` to album 3, which came from source B. Left on the
remainder, that replays as a nameless album holding B's purchase id. **Proposal:** move
album-indexed properties (`Album`, `Track`, `Publisher`, `Purchase`, `Purchase-`, `PromoteAlbum`)
whose index belongs only to one extracted source into a copy of that block (same user and time)
on that source. Properties on an index two sources share stay on the remainder. Non-album
properties never move.

### Shared ids after the split

If the sources were merged because they shared a Spotify/iTunes/ISRC id (the shared-id queue),
splitting them makes that id shared again. Unmerging means the admin decided they aren't the
same song, so:

- Record each (remainder, extracted) pair that shares an id in `SharedIdCandidates` as
  **Rejected** (by the unmerging admin), so `MergeMatching` won't merge them back.
- List the shared ids on the result so the admin can remove the wrong one. Removing ids
  automatically is out of scope, because we can't tell which song's id is wrong.

## UI

On the song details page, in the existing **Admin Actions** area of `SongCore.vue`, next to
**Undo User Edits**. No edit mode is needed: unmerge commits right away, like Delete and Undo User
Edits, so it's only offered when the page **isn't** in edit mode (no unsaved edits to lose).

- `SongHistory.mergeSources` (new computed getter) returns, for the last `.Merge`, one row per
  source: id, title and artist as of its last stamped block, its `.Create` user and date, and
  how many blocks it owns. It returns empty when the song isn't unmergeable, and the section is
  hidden then.
- The section is titled **Unmerge**. It shows one checkbox per source with a short description,
  for example "Shape of You · Ed Sheeran · added by dwgray 2021-03-04 · 7 edits". Further
  controls: a "Mark unmerged songs NoMerge" checkbox (on by default, see decision 1) and an
  **Unmerge selected** button. The button stays disabled until at least one source is checked
  and at least one is left unchecked.
- It's a plain form post with the anti-forgery token, the same pattern as
  `/song/undoUserChanges`. A confirm `BModal` before submitting lists what will happen.
- `POST /Song/Unmerge(Guid id, Guid[] extract, bool noMerge)`, `[Authorize(Roles = "dbAdmin")]`,
  `UseVue.No`. It validates that the song is unmergeable and `extract` is a proper subset, runs
  `UnmergeSong`, clears the merge-candidate cache for the affected songs, and redirects to the
  remainder's details. A one-time message there links to each restored song and names any shared
  ids. If validation fails, it shows an `Info` view with the reason, the same as the merge
  action's error path.

## Tests

- **Server (`m4dModels.Tests`, alongside `MergeTests`):**
  - Round trip: merge A and B, unmerge B. A's and B's restored properties equal their originals,
    allowing for album compaction and the dropped `OrderAlbums`. The remainder's fields equal A
    replayed plus any post-merge edits.
  - Three sources, extract two: the remainder keeps one source and post-merge edits.
  - A post-merge edit on an album index that only B owns moves to B. One on a shared index stays.
  - `GetAltids` drops the unmerged ids. An `UploadIndex(trackDeleted)` replay keeps the restored
    songs.
  - Rejected: a song that isn't unmergeable (no stamps, or a single-id `.Merge`), extracting
    every source, and an id that isn't in the `.Merge`.
  - Nested merge: unmerging the outer layer returns the intermediate merged song with its own
    `.Merge` still in place.
  - Shared ids: the pair is recorded as Rejected.
- **Controller:** authorization, validation errors, and the redirect target.
- **Client:** `SongHistory.mergeSources` (plain song, merged song, unstamped legacy merge,
  after an earlier `.Unmerge`). For the `SongCore` section: hidden while editing, and the button
  disabled for none-checked and all-checked.
- **E2E:** extend `e2e/tests/merge.spec.ts` to unmerge the song it merged and check that both
  originals are back with their albums.

## Docs to update when it ships

Replace § Future: Unmerge Functionality in
[song-merge-algorithm](../songs/song-merge-algorithm.md) with the as-built design. Add `.Unmerge`
to [song-internal-format](../songs/song-internal-format.md). Mention the admin section in
[song-details-viewing-editing](../songs/song-details-viewing-editing.md). Note the Rejected pairs in
[shared-id-review](../songs/shared-id-review.md). Then delete this plan and its README row.

## Decisions

Reviewed 2026-11-14.

1. **Keeping them apart.** Auto-merge (level 3: title, artist and length) would usually merge the
   restored songs straight back. A "Mark unmerged songs NoMerge" checkbox, on by default, appends
   `.NoMerge` to each restored song. A pairwise "never merge these two" store would be better, but
   it is left for later.
2. **No marker on the restored songs.** The remainder's `.Unmerge` is the only record. Using one
   command with two meanings invites bugs in `GetAltids`.
3. **Unowned blocks from earlier merges.** A source's own `.Merge`, `.NoMerge` and `.FailedLookup`
   blocks aren't stamped, because those commands' values mean something else (`.Merge` holds its id
   list). In songs merged before this change, they stay on the remainder. So when the extracted
   source was itself a merged song, its old grand-source ids redirect to the remainder.
   Going forward, `BuildSimpleMerge` adds a `MergeSource={guid}` property (after `User`/`Time`) to
   each block it doesn't stamp, and a later merge overwrites it the same way it restamps
   `.Create`/`.Edit`. Unmerge sends a block with `MergeSource` to that source and strips the
   property. `Song.Load` ignores it. This ships as a separate commit in the same PR.
4. **Post-merge album properties.** These move to the owning source, as proposed above.

## Related

- [song-merge-algorithm](../songs/song-merge-algorithm.md): how the merge being undone works
- [song-internal-format](../songs/song-internal-format.md): the property log and edit blocks
- [shared-id-review](../songs/shared-id-review.md): the queue whose merges this most often undoes
