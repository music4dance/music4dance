# Shared Id Review

**Type:** Reference
**Status:** Current
**Last verified:** 2026-11-04 (written with the code)
**Code:** `m4dModels/SharedIdLog.cs`, `m4dModels/SharedIdCandidate.cs`, `m4d/Utilities/MusicServiceManager.cs` (`UpdateFromTracks`, `GetISRCData`), `m4d/Controllers/AdminController.cs` (`SharedIds` actions), `m4d/Views/Admin/SharedIds.cshtml`

## Overview

A Spotify track id, an iTunes track id and an ISRC each identify one recording, so two catalog
songs holding the same one are almost always the same song. New songs are matched on these ids
before they're saved ([song-merge-algorithm](song-merge-algorithm.md) § Matching new songs by
service id). This doc covers the other half: enrichment of songs already in the catalog, and the
admin queue where the pairs it finds, along with the pairs that already shared an id, are merged
or marked as different songs.

## The enrichment guard

Before enrichment attaches ids to a catalog song, it calls `SharedIdLog.CheckIds(song, claims,
source)`. The claims are the ids it's about to add, spelled the way the `ServiceIds` index field
spells them (`S:…`, `I:…`, `R:…`), each with a description of the service track it came from.

- Ids the song already holds are ignored.
- The rest go to `SongIndex.FindSongsByServiceIds` in one query. Each id another song holds is
  returned as *held*, and the pair is recorded as a candidate.
- A song that isn't in the catalog yet (`FindSong` misses) is never blocked. Those are the add
  paths, which merge the new song into the holder instead.
- If recording fails, the id is still skipped. Only the review entry is lost.

| Caller | Source | What's skipped |
| --- | --- | --- |
| `MusicServiceManager.UpdateFromTracks`, behind `UpdateSongAndServices`, `ConditionalUpdateSongAndService` (`BatchUpdateService`, catalog commit), `CreateSong` and `AttachTracksToSong` | `Spotify lookup`, `iTunes lookup` | A track whose own id is held is dropped. A track whose ISRC alone is held is attached without the ISRC (`UpdateMusicServiceFromTrack(includeIsrc: false)`). |
| `MusicServiceManager.GetISRCData` (`BatchISRC`, and `UpdateAudioData` when a song has no ISRC) | `ISRC lookup` | The held ISRC. |

The playlist viewer's ISRC fallback (`SongController`) also attaches a playlist track to only the
first song holding its ISRC. When two songs share an ISRC, that pair is already a candidate.

## The queue

`SharedIdCandidates` (SQL, EF entity `SharedIdCandidate`) holds one row per pair of songs:

| Column | Notes |
| --- | --- |
| `SongA`, `SongB` | The pair, lower id first. Unique together. |
| `ServiceIds` | The shared ids, comma-separated and sorted. A pair found again adds its ids here. |
| `Source` | `Seed`, or the enrichment step that found it. |
| `Detail` | The service track the id came from (service, id, name, artist, album, length). Empty for seeded rows. |
| `Created` | When it was first recorded. |
| `Status` | `Pending`, `Merged`, `Rejected` (different songs), or `Deleted` (one song deleted without a merge). |
| `ResolvedBy`, `ResolvedAt` | Who resolved it and when. Empty when resolved because the songs changed elsewhere. |

Only a pending row picks up new ids. A rejected pair stays rejected, so enrichment keeps leaving
the id where it is and doesn't queue the pair again.

**Seeding.** `SharedIdLog.Seed` streams every song's `ServiceIds` (`SongIndex.StreamSongServiceIdsAsync`),
and adds a row for each pair of songs that share one. Running it again adds only new pairs. It
covers the same ground as `scripts/song_snapshot.py duplicate-ids --qualifiers SS,IS,RS`.
Amazon ids aren't in `ServiceIds`, so they aren't seeded. On the 2026-11-03 snapshot that
query found 3,748 songs in 1,754 clusters.

## The review page

`/Admin/SharedIds` (dbAdmin; linked from the admin index). `SharedIdLog.PendingClusters` groups
pending rows into clusters of connected songs (union-find), oldest first, 20 a page. Each cluster
shows its songs side by side: title, artist, length, tempo, dances, albums with their song-level
ids (shared ones highlighted), and created date, plus each row's source and detail.

Loading a page follows any song that was merged or deleted since it was queued
(`LoadSongs` → `SongIndex.FindMergedSong`) and re-points its rows, so merges made through the
title-based tools or **Merge and edit** resolve themselves.

| Action | What it does |
| --- | --- |
| **Merge checked** (`POST /Admin/MergeSharedIds`) | `SongIndex.SimpleMergeSongs` on the checked songs, which keeps all their history. Rows between them become `Merged`; their rows with other songs, pending or rejected, move to the merged song. Unchecking a song leaves it in the queue paired with the merged song. |
| **Not the same** (`POST /Admin/RejectSharedIds`) | Marks every pending row in the cluster `Rejected`. |
| **Merge and edit** | Posts the songs to `/Song/BulkEdit` (`action=Merge`) in a new tab, which merges them as **Merge checked** does and opens the merged song in the editor to pick its title, artist, tempo and length. |
| **Merge all with matching title and artist** (`POST /Admin/MergeMatchingSharedIds`) | A background task (watch `/Admin/AdminStatus`). It merges every pending cluster whose songs all share the same title and the same non-empty artist (`Song.CreateTitleHash`, as the title-based merge candidates compare them). Every cluster shares an id by construction. It skips clusters with a rejected pair inside them, or with a `.NoMerge` song. |
| **Queue existing shared ids** (`POST /Admin/SeedSharedIds`) | Runs the seed as a background task. |

## Future improvements

- "Not the same" rejects the whole cluster. To split a cluster, merge the songs that belong
  together first, then reject what's left.
- Moving a wrong id to the right song (e.g. an ISRC attached to a different recording) needs the
  song editor.

## History

- 2026-11: Enrichment guard, `SharedIdCandidates` table, seed, and the `/Admin/SharedIds` review page.

## Related

- [song-merge-algorithm](song-merge-algorithm.md): title-based duplicate detection, and matching new songs by service id
- [service-track-lookup](service-track-lookup.md): `CreateSong` and the exact `ServiceIds` lookup
- [music-service-model](../music-services/music-service-model.md): purchase ids and `ServiceIds`
- [data-layer](../infrastructure/data-layer.md): migrations
