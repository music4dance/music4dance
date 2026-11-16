# Shared-id ownership: what's left

**Type:** Plan
**Status:** Partially implemented
**Last verified:** 2026-11-16
**Code:** `m4dModels/SharedIdOwnership.cs`, `m4d/APIControllers/SharedIdsController.cs`, `m4d/ClientApp/src/pages/shared-id-review/`

**Sort out ids** shipped in 2026-11: on a card or for a page of the review, it looks up the ids a
cluster's songs share and checks the removals that give each one to the song it belongs to. It
works out an owner from the track's title, artist and length, treats an id as bad when no song is
its track, and moves an ISRC with the Spotify or iTunes id that reports it. See **Sort out ids**
in [the shared-id review](../songs/shared-id-review.md). What's left:

## Later

- **A bulk task**, like the Merge all ones, for the clusters where every shared id has an owner
  or is bad. Do this once the page-by-page proposals have proved reliable. It would run the same
  `SharedIdOwners.Propose` and apply the removals with Leave.
- **A report of the ids found bad**, grouped by where they came from (the candidates' source), in
  case one source keeps attaching wrong tracks.
- **Clusters that share only an ISRC.** These get no proposal today, because an ISRC can't be
  looked up. The Spotify and iTunes ids beside it on each song's album are the tracks that
  reported it, and looking those up would show which songs' tracks really carry it.

## Related

- [Shared-id review](../songs/shared-id-review.md)
