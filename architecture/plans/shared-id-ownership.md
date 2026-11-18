# Shared-id ownership: what's left

**Type:** Plan
**Status:** Partially implemented
**Last verified:** 2026-11-18
**Code:** `m4dModels/SharedIdOwnership.cs`, `m4d/APIControllers/SharedIdsController.cs`, `m4d/ClientApp/src/pages/shared-id-review/`

**Sort out ids** shipped in 2026-11: on a card or for a page of the review, it looks up every
Spotify and iTunes id on a cluster's songs and checks the removals and additions that give each
one to the song it belongs to. It works out an owner from the track's title, artist and length,
treats an id as bad when no song is its track or the service doesn't have it, moves an id to an
owner that doesn't have it with its album, and moves an ISRC with the Spotify or iTunes id that
reports it (so clusters that share only an ISRC are sorted out by the songs' own ids). See
**Sort out ids** in [the shared-id review](../songs/shared-id-review.md). What's left:

## Later

- **A bulk task**, like the Merge all ones, for the clusters where every shared id has an owner
  or is bad. Do this once the page-by-page proposals have proved reliable. It would run the same
  `SharedIdOwners.Propose` and apply the removals with Leave.
- **A report of the ids found bad**, grouped by where they came from (the candidates' source), in
  case one source keeps attaching wrong tracks.
- **Telling a missing track from a failed lookup.** `MusicServiceManager.GetMusicServiceTrack`
  returns null for both (and caches it), so Sort out ids takes a null as "the service doesn't
  have it" only when the service returned some of the cluster's other tracks. A lookup that says
  which it was would let a lone id be called bad too.

## Related

- [Shared-id review](../songs/shared-id-review.md)
