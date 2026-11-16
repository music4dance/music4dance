# Shared-id ownership: give each shared id to the song it belongs to

**Type:** Plan
**Status:** Proposed
**Last verified:** 2026-11-16
**Code:** `m4dModels/SharedIdOwnership.cs`, `m4d/APIControllers/SharedIdsController.cs`, `m4d/ClientApp/src/pages/shared-id-review/`

## Problem

After the Merge all tasks of the [shared-id review](../songs/shared-id-review.md), most of what's
left in the queue is clusters of songs that really are different. These are the clusters whose
lengths are more than 15 seconds apart, and the close or different titles and artists. Their
songs share an id only because enrichment or an import attached a track to the wrong song. Each
shared id should belong to at most one of them. Removing it from the others resolves the pairs:
the review already marks a pair not the same once its songs no longer share an id.

## Design

### Picking an owner for each shared id

For each id two or more of the cluster's songs hold:

- **Spotify and iTunes ids** are looked up (`MusicServiceManager.GetMusicServiceTrack`, through
  its cache, one at a time), and each song holding the id is compared with the track:
  - title and artist, with `Song.TitleArtistMatch` (the fuzzy match that filters service tracks),
    as given or without feat. clauses;
  - length, within `SharedIdMatcher.LengthTolerance` (15 seconds) of the track's duration. A
    song or track without a length passes.
- **Exactly one song matches** → the id is that song's: it's removed from the others.
- **No song matches the title and artist** (the track is something else entirely) → the id is
  bad: it's removed from every song.
- **More than one song matches**, **only the lengths fail**, or **the service doesn't find the
  track** → unsure: nothing is proposed.
- **ISRCs** (`R:`) identify a recording, so two releases of one recording legitimately share
  one. An ISRC moves only with a Spotify or iTunes id that has an owner and whose track
  reports that ISRC. Otherwise it's left alone.
- Other ids (no track lookup) are left alone.

### Albums

When the removals take every song-level id off one of a song's albums, the album itself is
removed: a wrong id usually came in with its album.

### Applying

The proposal is just the review page's existing removal boxes, checked. Apply removes them with
an edit by the reviewer (`Song.AppendIdRemoval`), so the change is logged and can be undone like
any other edit. A cluster with a proposal switches to **Leave for later**, so Apply doesn't
merge songs it has just separated. Anything still shared keeps the pair in the queue.

### The page

- **Sort out ids** on a card looks up that cluster's shared ids (`POST /api/sharedids/owners`).
  It checks the proposed boxes and lists each shared id with its owner, or why there's none,
  and what the service says the track is. It also fills the ids' tooltips, as the existing
  lookup does.
- **Sort out ids on this page** does the same for each cluster on the page that's probably
  different songs: lengths too far apart, close or different titles and artists, a rejected pair
  or a .NoMerge song. It works one cluster at a time and shows its progress.
- The reviewer can change any box before **Apply this page**.

## Later

- Once the proposals prove reliable, a bulk task (like the Merge all ones) for the clusters where
  every shared id has an owner or is bad.
- Report the ids found bad, in case one source keeps attaching wrong tracks.

## Related

- [Shared-id review](../songs/shared-id-review.md)
