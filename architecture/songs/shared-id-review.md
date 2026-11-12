# Shared Id Review

**Type:** Reference
**Status:** Current
**Last verified:** 2026-11-11 (written with the code)
**Code:** `m4dModels/SharedIdLog.cs`, `m4dModels/SharedIdCandidate.cs`, `m4d/Utilities/MusicServiceManager.cs` (`UpdateFromTracks`, `GetISRCData`), `m4d/Controllers/AdminController.cs` (`SharedIds` actions), `m4d/Views/Admin/SharedIds.cshtml`, `m4d/Views/Admin/SharedIdsPreview.cshtml`, `m4dModels/FeaturedCleanup.cs`

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
ids (shared ones highlighted), and created date, plus each row's source and detail. A last
**If merged** column shows the song merging all of them would make
(`SharedIdLog.PreviewMerge` → `SongIndex.BuildSimpleMerge`, the build half of
`SimpleMergeSongs`, which saves nothing). Use it to choose between **Merge checked** and
**Merge and edit**.

The page is one form. Each cluster has a choice: **Merge checked**, **Not the same** or
**Leave for later**. **Apply this page** (`POST /Admin/ApplySharedIds` → `SharedIdLog.Apply`)
carries out all of them. Clusters with a rejected pair or a `.NoMerge` song start as Leave for
later, and the rest start as Merge checked. So a page is reviewed by fixing the clusters that
aren't straight merges and pressing Apply once. Clusters left for later stay at the top of page 1.

**Match tiers.** Each cluster's header says how closely its titles and artists agree
(`SharedIdLog.Match`, closest first):

| Tier | Badge | Test |
| --- | --- | --- |
| `Exact` | Title and artist match | Same `Song.CreateTitleHash` normal form, which already ignores case, accents, punctuation and bracketed text such as "(feat. X)". |
| `WithoutFeaturing` | Differ only by feat. | The same once `ArtistSplitter.RemoveTitleFeaturing` and `RemoveArtistFeaturing` take the feat. clauses out ("Song feat. X", "Y feat. X"). |
| `Close` | Close match | Every pair passes `Song.TitleArtistMatch`, the fuzzy match that filters service tracks (artist word overlap, `CleanAlbum` trimming of " - 2011 Remaster" suffixes), as given or with feat. removed. |
| `Different` | none | Anything else. |

**Preview merge all** records each cluster's tier. Until the next restart, the review page sorts
by it: clusters that differ only by feat. first, then close matches, then the rest. Only `Exact`
clusters are merged by **Merge all**; the other tiers are reviewed by hand while the cleanup is
refined.

**Choosing the names.** The **Title**, **Artist** and **Individual artists** rows have a radio in
every column. They start on the **If merged** column, which shows `FeaturedCleanup.Propose`: the
merged title without its feat. clause, the merged artist credit, and its individual artists
followed by any artists the other songs feature (in their titles or credits). When the chosen
title, artist or individual artists aren't what the merge gives on its own (choosing a single
individual artist clears the list back to the credit), `SimpleMergeSongs` appends an edit by
the `music4dance` account (`Song.M4dUser`, via `Song.AppendNameEdit`) setting them. When they
match, it's a plain simple merge. An `Artists` value written this way isn't overridden by
artist-bot.

Loading a page follows any song that was merged or deleted since it was queued
(`LoadSongs` → `SongIndex.FindMergedSong`) and re-points its rows, so merges made through the
title-based tools or **Merge and edit** resolve themselves.

| Action | What it does |
| --- | --- |
| **Merge checked** (a choice, applied by **Apply this page**) | `SongIndex.SimpleMergeSongs` on the checked songs, which keeps all their history, then the music4dance edit for the chosen names if one is needed. Rows between them become `Merged`; their rows with other songs, pending or rejected, move to the merged song. Unchecking a song leaves it in the queue paired with the merged song. If fewer than two checked songs are still in the catalog, it's counted as stale and skipped. This happens, for example, after **Merge and edit**. |
| **Not the same** (a choice) | Marks every pending row in the cluster `Rejected`. |
| **Leave for later** (a choice) | Changes nothing. |
| **Merge and edit** | Takes effect right away. It posts the songs to `/Song/BulkEdit` (`action=Merge`) in a new tab, which merges them as **Merge checked** does and opens the merged song in the editor to pick its title, artist, tempo and length. |
| **Merge all with matching title and artist** (`POST /Admin/MergeMatchingSharedIds`) | A background task (watch `/Admin/AdminStatus`). It merges every pending cluster whose songs all share the same title and the same non-empty artist (`Song.CreateTitleHash`, as the title-based merge candidates compare them). Every cluster shares an id by construction. It skips clusters with a rejected pair inside them, or with a `.NoMerge` song. |
| **Preview merge all** (`POST /Admin/PreviewMergeMatchingSharedIds`) | A background task that works out what **Merge all with matching title and artist** would do, without merging (`SharedIdLog.PlanMergeMatching`, which the merge itself runs first). **Last preview** (`/Admin/SharedIdsPreview`) lists the clusters it would merge, then the skipped ones grouped by reason: a rejected pair, a `.NoMerge` song, a song with no artist, or titles or artists that differ only by feat., are close, or differ. The result lives in memory until the next preview or a restart. |
| **Queue existing shared ids** (`POST /Admin/SeedSharedIds`) | Runs the seed as a background task. |

## Future improvements

- Once the feat. cleanup and the close-match tier have been refined on this queue, use
  `FeaturedCleanup` in **Merge all** and in the add-time merge ([song-merge-algorithm](song-merge-algorithm.md)).

- "Not the same" rejects the whole cluster. To split a cluster, merge the songs that belong
  together first, then reject what's left.
- Moving a wrong id to the right song (e.g. an ISRC attached to a different recording) needs the
  song editor.

## History

- 2026-11: Enrichment guard, `SharedIdCandidates` table, seed, and the `/Admin/SharedIds` review page.
- 2026-11: Merge-all preview. Merge all now also re-plans clusters that changed while it ran, instead of leaving them for the next run.
- 2026-11: **If merged** column and page-at-a-time review (**Apply this page**), which replace the per-cluster Merge checked and Not the same buttons.
- 2026-11: Match tiers (differ only by feat., close match), sorted first after a preview; per-song title and artist choices; the feat. cleanup proposal, applied by a music4dance edit.

## Related

- [song-merge-algorithm](song-merge-algorithm.md): title-based duplicate detection, and matching new songs by service id
- [service-track-lookup](service-track-lookup.md): `CreateSong` and the exact `ServiceIds` lookup
- [music-service-model](../music-services/music-service-model.md): purchase ids and `ServiceIds`
- [data-layer](../infrastructure/data-layer.md): migrations
