# Shared Id Review

**Type:** Reference
**Status:** Current
**Last verified:** 2026-11-18
**Code:** `m4dModels/SharedIdLog.cs`, `m4dModels/SharedIdCandidate.cs`, `m4d/Utilities/MusicServiceManager.cs` (`UpdateFromTracks`, `GetISRCData`), `m4dModels/SharedIdTypes.cs` (clusters, choices, page), `m4dModels/SharedIdMatcher.cs` (match tiers), `m4dModels/FeaturedCleanup.cs`, `m4dModels/SharedIdOwnership.cs` (Sort out ids), `m4dModels/ArtistAliases.cs`, `m4dModels/SharedIdArtists.cs` (Merge artist), `m4d/Controllers/SharedIdsController.cs` (page, background tasks), `m4d/APIControllers/SharedIdsController.cs` (apply, track lookup, owners), `m4d/ViewModels/SharedIdReviewModel.cs` (page model, apply request), `m4d/ClientApp/src/pages/shared-id-review/` (page app), `m4d/Services/SharedIdPreviewStore.cs` (last preview and review sort), `m4d/Views/SharedIds/` (last preview), `e2e/tests/shared-ids.spec.ts`

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

`/SharedIds` (`SharedIdsController`, dbAdmin for every action; linked from the admin index and
the admin menu) is the `shared-id-review` Vue page app.
`SharedIdLog.PendingClusters` groups pending rows into clusters of connected songs
(`SharedIdCluster.Group`, union-find), oldest first, and `SharedIdLog.LoadPage` loads 20 of them a
page, with each cluster's preview and proposed names. `SharedIdReviewModel.Build` flattens the
page for the client (`model_`): each song's effective artists, its albums by `AlbumDetails.Index`
with their extended ids, and the track links are worked out on the server, the same way `Apply`
reads them back, so the client only renders and collects choices. Each cluster
shows its songs side by side: title, artist, length, tempo, dances, albums with their song-level
ids (shared ones highlighted), and created date, plus each row's source and detail. Spotify and
iTunes ids link to the track on the service (`MusicService.GetPurchaseLink`; an iTunes link needs
the album's iTunes id), to check by hand which song an id belongs to. ISRCs have no page; the
Spotify and iTunes ids beside one on the same album are the tracks that reported it. In the
song columns, each album and each id has a box that removes it from that song (see
**Removals** below). A last
**If merged** column shows the song merging all of them would make
(`SharedIdLog.PreviewMerge` → `SongIndex.BuildSimpleMerge`, the build half of
`SimpleMergeSongs`, which saves nothing). Use it to choose between **Merge groups** and
**Merge and edit**.

Each cluster has a choice: **Merge groups**, **Not the same** or **Leave for later**. **Apply
this page** sends them all as JSON (`POST /api/sharedids/apply`, `APIControllers/SharedIdsController`
→ `SharedIdChoiceRequest.ToChoice` → `SharedIdLog.Apply`) and reloads the page with the result's
summary. A cluster whose songs changed while the page loaded has no choice and isn't sent. The
page warns before leaving with choices that differ from the defaults, and says how many do. Clusters with a rejected pair, a `.NoMerge` song or lengths too far apart start as Leave for
later, and the rest start as Merge groups. So a page is reviewed by fixing the clusters that
aren't straight merges and pressing Apply once. Until the next restart, a cluster left for
later goes after every other cluster (`SharedIdPreviewStore.Defer`, kept per song and across
previews), so reloading the page brings up new clusters instead of the same ones. A cluster that
gains a song since has something new and takes its place again. The summary counts the clusters
left for later, and the tier counts leave them out.

**Groups.** Each song column has a group, one letter per song (**A**, **B**, …), or **Leave
out**. Every song starts in A, which merges them all. A cluster that is really two
songs (1 and 2 are one, 3 and 4 the other, linked by an id that 2 and 3 share) is split by
putting 3 and 4 in B. Merge groups then merges each group of two or more songs (`SharedIdChoice.MergeGroups`),
and marks the pending pairs between the groups' songs, after the merges, `Rejected`. A
one-song group is just rejected against the others. A song left out keeps its pairs pending, now
with the song its group-mates were merged into. If any group's merge is stale, nothing is
rejected. When the songs are split, a note under **If merged** says that column no longer shows
what Apply makes.

**Match tiers.** Each cluster's header says how closely its titles and artists agree
(`SharedIdMatcher.Match`, worked out once when a cluster's songs load; closest first):

| Tier | Badge | Test |
| --- | --- | --- |
| `Exact` | Title and artist match | Same `Song.CreateTitleHash` normal form, which already ignores case, accents, punctuation and bracketed text such as "(feat. X)", or the same letters and digits alone (`SharedIdMatcher.SquashedTitle`, so "Ghostbusters (from "Ghostbusters")", whose bracketed text the normal form drops, matches "Ghostbusters - From "Ghostbusters""; `SharedIdMatcher.SameTitle`, and `SameTitleWithoutFeaturing` for the tiers below), with each credit under its preferred name (`ArtistAliases`: a short table in code, so "Pink" is "P!nk", "Lady Antebellum" is "Lady A", "Juan Luis Guerra" is "Juan Luis Guerra 4.40", and the New 101 Strings Orchestra's spellings are "101 Strings Orchestra") and compared without its leader's backing group (`ArtistSplitter.RemoveBackingGroup`: "Benny Goodman and His Orchestra" is "Benny Goodman", and so is "Benny Goodman Sextet" when another song's credit names Benny Goodman, `FeaturedCleanup.WithGroupLeaders`; `WithoutFeaturing` compares credits the same way), and the titles compared once `SharedIdMatcher.WithoutVersionNotes` takes off the notes a service adds to the same recording: "- Remastered", "- Remastered 2011", "(2011 Remaster)", "[Digitally Remastered]", "- Remastered Version", "- Single Version", "- Remasterisée", after a dash, a colon or in brackets. Then, since versions danced the same way are grouped as one song for now, `SharedIdMatcher.WithoutQualifiers` takes off the qualifiers at the end of each title, after a spaced dash or in brackets ("- Radio Edit", "- Big Band Version", "- Last Call Version", "(Remix)", "- Rerecorded", and a live or acoustic take too; a length spread over `LengthTolerance` still keeps them from Merge all), except one that names a dance or a tempo (`Song.NamesADance`, the dance names and BPM markers that `Song.CreateTitleHash` keeps in brackets: "- Salsa Version", "(Cha Cha Mix)", "- 128 BPM") or holds a feat. clause, which is the next tier's. Every tier below compares titles the same way (`SharedIdMatcher.TitleKey`). |
| `WithoutFeaturing` | Differ only by feat. | The same once `ArtistSplitter.RemoveTitleFeaturing` and `RemoveArtistFeaturing` take the feat. clauses out ("Song feat. X", "Y feat. X"). |
| `SameArtists` | Same artists, credited differently | The titles match without feat. and every song names the same individual artists (`FeaturedCleanup.IndividualArtists`: the song's recorded `Artists` plus any it features, else its credit split by `ArtistSplitter.Split`, which drops backing groups). "Teddy Wilson And His Orchestra With Billie Holliday" and "Teddy Wilson; Billie Holliday" are the same. |
| `FewerArtists` | Same lead artist | The titles match without feat. and the songs share their lead artist (`SharedIdMatcher.LeadKey`), but the other artists differ: "X" and "X & Y". Without evidence the splitter can't tell "X & Y" from a duo's name, and "X" may be an old classic whose later version added a collaborator, so these merge only when the songs' own music service tracks confirm them (below). |
| `Close` | Close match | For example a dance version and the plain title ("Song - Salsa Version" and "Song"). Every pair passes `Song.TitleArtistMatch`, the fuzzy match that filters service tracks (artist word overlap, `CleanAlbum` trimming of " - 2011 Remaster" suffixes), as given or with feat. removed. |
| `Different` | none | Anything else. |

**Lengths.** Whatever the tier, a cluster whose songs' lengths are more than
`SharedIdMatcher.LengthTolerance` (15) seconds apart, longest minus shortest among the songs that
have a length, is almost certainly two recordings (a live take, an extended mix). No Merge all
task merges it (skip reason `LengthsDiffer`), and on the review page it gets a "Lengths m:ss
apart" badge and starts as **Leave for later**; it can still be merged by hand. The title-based
merge candidates use a looser 20 seconds from the median (`MergeManager.FilterLength`).

**Preview merges** records each cluster's tier. Until the next restart, the review page sorts
by it: clusters that differ only in their artist credits (`WithoutFeaturing`, `SameArtists`, `FewerArtists`) first, then close matches, then the rest. The tier is
kept for each song (`SharedIdPreviewStore`, a singleton), and a song merged on the review page takes
the best tier of the songs it was merged from, so a cluster keeps its place when part of it is
merged and the rest stays queued. The counts on the page are of the clusters still pending.
`Exact` clusters are merged by **Merge all with matching title and artist** and `WithoutFeaturing`
ones by **Merge all that differ only by feat.**; close and different clusters are reviewed by
hand.

**Choosing the names.** The **Title**, **Artist** and **Individual artists** rows have a radio in
every column. They start on the **If merged** column, which shows `FeaturedCleanup.Propose`: the
merged title without its feat. clause or its remaster and single-version notes
(`SharedIdMatcher.WithoutVersionNotes`, so "Song - 2011 Remaster" lands on "Song"), cut back to
the shortest of the songs' titles (cleaned the same way) that it starts with, word for word ("Song
Name - Salsa Version" merged with "Song Name" lands on "Song Name"; "Lovely Day" isn't cut to
"Love"), an artist credit, and the merged song's individual
artists followed by any artists the other songs feature (in their titles or credits). The credit
is the merged song's unless it leaves some of those artists out and one of the songs credits
them all: "Song (feat. Y)" by X merged with "Song" by "X feat. Y" lands on "Song" by "X feat. Y",
with artists X and Y. Artists a title features (the merged song's or another song's) that the
credit still doesn't name are then added to it after the title's own word, so "Song (ft. Y)" by X
lands on "Song" by "X ft. Y", and "X feat. Z" with Y featured in a title on "X feat. Z & Y". A
credit that already names the artist, split or not ("X & Y"), is left alone. A leader's backing
group isn't an artist of its own: "Benny Goodman and His Orchestra" merged with "Benny Goodman"
lands on the credit "Benny Goodman and His Orchestra" with the individual artist Benny Goodman,
and "Charlie Parker Quartet" with "Charlie Parker" on "Charlie Parker Quartet" with Charlie
Parker. The credit's primary artist and the individual artists go by their preferred names
(`ArtistAliases`): "Pink" merged with "P!nk" lands on "P!nk". A title's feat.
clause starts with feat., ft., featuring or with; "with" counts in brackets or after a dash ("Song
(with Y)", "Song - with Y", landing on "X with Y") but not bare ("Stay With Me") or before a
pronoun or common word ("(With You)", "(With Somebody)", "(with Intro)"). The page sends the
song each name comes from, or null for the proposal (`SharedIdNamePicks`), and the proposal is worked out again from
each group's own merge when it's applied. A group takes a name from a song column only when that
song is in the group; otherwise it gets the proposal for its own merge. So the page disables a song's radios while the
song is left out (putting a pick that was on it back on its default), and, when the songs are
split, marks each radio with the group it names. Names only matter to a merge, so all the radios
are disabled for the other choices. The **If merged** radio has a box holding the proposal's
name, which can be typed over: typing picks that radio, and the page sends the text (`title`,
`artist`, and `artists` split on `|`, in `SharedIdNamePicks.Typed`), which wins over the proposal.
Typing the proposal's own text back, a blank, or **Undo** returns to the proposal. A typed name
applies only while the songs are merged in one group, since it can't fit two merged songs: the
boxes are disabled when the songs are split, and the server ignores typed names for a choice with
more than one group. The **Tempo** row works the same way: its **If merged** column is the tempo
the merge gives on its own, and a pick (`tempoFrom`) or a typed positive number (`tempo`, in
`SongNames.Tempo`) sets the merged song's tempo; a picked song with no tempo leaves the merge's.
When the chosen title, artist, individual artists or tempo aren't what the merge gives on its own
(choosing a single individual artist clears the list back to the credit), `SimpleMergeSongs`
appends an edit by
`music4dance` (`Song.M4dUser`, via `Song.AppendNameEdit`) setting them. When they match, it's a
plain simple merge. The edit is by a non-pseudo user, because `Song.LoadProperties` doesn't let a
pseudo user's title or artist override a real user's. So an `Artists` value written this way also
isn't overridden by artist-bot.

Loading a page follows any song that was merged or deleted since it was queued
(`LoadSongs` → `SongIndex.FindMergedSong`) and re-points its rows, so merges made through the
title-based tools or **Merge and edit** resolve themselves.

| Action | What it does |
| --- | --- |
| **Merge groups** (a choice, applied by **Apply this page**) | For each group, `SongIndex.SimpleMergeSongs` on its songs, which keeps all their history, then the music4dance edit for the chosen names if one is needed. Rows between them become `Merged`; their rows with other songs, pending or rejected, move to the merged song. Then the pairs between groups are rejected. A song left out stays in the queue paired with the merged song. A group with fewer than two songs still in the catalog is counted as stale and skipped. This happens, for example, after **Merge and edit**. |
| **Not the same** (a choice) | Marks every pending row in the cluster `Rejected`. |
| **Leave for later** (a choice) | Changes nothing in the database; the cluster goes to the end of the queue until a restart. |
| **Delete** (a box on each song, applied by **Apply this page** first, whatever the cluster's choice) | `SongIndex.DeleteSong` (a `.Delete` edit by the reviewer, so the history stays), then the song's pending rows are marked `Deleted`. The deleted song is left out of the groups and has nothing removed (`SharedIdChoice.Without`); only the cluster's own songs can be deleted. For songs from some other source that have no dances and only bring their album history, where merging them in isn't wanted. Apply asks first. |
| **Look up on Spotify and iTunes** (a button on each cluster) | `POST /api/sharedids/tracks` with the cluster's Spotify and iTunes ids (`SharedIdServiceTrack.Lookup`, one at a time through `MusicServiceManager.GetMusicServiceTrack` and its cache, at most 50). Each Spotify and iTunes id in the Albums row (the If merged column's too) then gets a tooltip with what the service says it is: title, artist, album, length, release date and ISRC; an id the service doesn't know turns red and says "Not found". Saves nothing; it's there to tell the songs apart before splitting them or removing an id. |
| **Sort out ids** (a button on each cluster whose songs have a Spotify or iTunes id) / **Sort out ids on this page** (by Apply, for the clusters that are probably different songs: lengths too far apart, `Close` or `Different`, a rejected pair or a `.NoMerge` song, one at a time with the progress shown) | `POST /api/sharedids/owners` with the cluster's songs (`SharedIdLog.ProposeOwners` → `SharedIdOwners.Propose`, at most 20 songs). Every Spotify and iTunes id on the songs, shared or not, is looked up (through the track cache, one at a time, the shared ones first, at most `SharedIdOwners.MaxLookups`, 50) and compared with every song in the cluster, not just the ones that have it: by `Song.TitleArtistMatch` (as given or without feat., with the credits under their preferred names, `ArtistAliases`), then by exact title when some of those songs have the track's title, qualifiers and all (`SharedIdMatcher.SameVersionWithoutFeaturing`, so for "Song Name - Acoustic" the acoustic song wins over "Song Name", which the fuzzy match also passes), then by artist when some of those songs name exactly the track's artists (Spotify's list, or the iTunes credit and the title's feat. clause split with the songs' artists as evidence, each without a leader's backing group and under its preferred name; the fuzzy match passes "Tony Bennett" for a track by Tony Bennett and Lady Gaga, so the song credited to both wins), and length (within `SharedIdMatcher.LengthTolerance` of the track's duration; a missing length passes). Exactly one song matching owns the id (`Owned`). No song matching the title and artist makes it bad (`Bad`, the track is something else), and so does a track the service doesn't return, as long as the service returned some of the cluster's other tracks (the lookup returns null both for a missing track and a failed request, so a service that returned none is taken not to have answered). More than one matching, only the length failing, a lookup that throws or a service that returned nothing proposes nothing (`Unsure`). An ISRC moves only with a Spotify or iTunes id that has an owner and whose track reports it, which for clusters that share only an ISRC is one of the songs' own ids; other ids are left alone. An album that loses a Spotify or iTunes id loses its ISRCs as well, shared or not, since they came from that track, unless a Spotify or iTunes id it keeps was looked up and reports the ISRC. The response is the proposal (every shared id, and each of a song's own ids that needs something done, marked `shared: false`), the removals that carry it out (the id off every song but its owner, or off every song when it's bad; an album instead when they'd take all of its song-level ids, since a wrong id usually came in with its album), the additions (`SharedIdAddition`: an owned id the owner doesn't have, from the first album that has it, with the ISRC its track reports) and the tracks. The page checks those removal boxes, lists each addition with a checked box under the owner's albums ("+ Album: S:…, R:…"), fills the ids' tooltips as the lookup does, lists each id's owner or why there's none under the albums, and makes the cluster **Leave for later** when it changes anything, so Apply separates the songs instead of merging them. Nothing is saved until **Apply this page**, and every box can be changed first. |
| **Merge artist** / **Force merge artist** (buttons on each cluster) | `POST /api/sharedids/artists` with the cluster's songs and `force` (`SharedIdLog.CombineArtists` → `SharedIdArtists.Propose`). The songs' individual artists (`FeaturedCleanup.IndividualArtists`, each without a leader's backing group and under its preferred name, `ArtistAliases`) are combined once each, in the order the songs name them, as a credit joined with " & " and as the individual artists. Unless `force`, the songs' Spotify and iTunes ids are looked up, the ones more songs have first and Spotify before iTunes, at most `SharedIdArtists.MaxLookups` (10), and the combination is confirmed by the first track that credits every artist (Spotify's list, or the iTunes credit split with the songs' artists as evidence). The page types a confirmed (or forced) combination over the If merged column's Artist and Individual artists and makes the cluster Merge; otherwise it says which artists the first track found leaves out, or that no track was found, and changes nothing. It can't type when the songs are split into groups or the merge couldn't be previewed. Nothing is saved until **Apply this page**. |
| **Additions and removals** (boxes on albums and ids, and on the ids Sort out ids would move, applied by **Apply this page** before the cluster's choice, whatever it is) | For each song with something checked, edits by the reviewer, saved together. Additions first (`Song.AppendIdAddition`): the id goes on the song's album with the same name that has no id of that service yet, or else on a new album (the next index after every album property the song has, so a removed album's index isn't reused) copying the source album's name, track number and that service's album id, with the track's ISRC; the source album is read from the song that had the id before anything is removed from it, and an addition to or from a song to delete is dropped. Then removals (`Song.AppendIdRemoval`): `Album:NN=` (empty) for an album, `Purchase-:NN:SS=<id>` (or `IS`, `RS`) for one id. Removals of albums or ids the song no longer has are skipped. An edited song that is then merged goes into the merge as saved (`SimpleMergeSongs` with `reload: false`), since the index may not have caught up. Afterwards, the pending rows of the edited songs (or the songs they were merged into) whose songs no longer share any id are marked `Rejected`. So two different songs are kept apart by removing the id from the one it doesn't belong to, even with **Leave for later**. If enrichment later tries to put the id back, the guard skips it, because the other song holds it, and the rejected pair stays resolved. Checking an album covers its ids, so their boxes are disabled and they aren't sent on their own. The **If merged** column doesn't show removals. |
| **Merge and edit** | Takes effect right away. It posts the songs to `/Song/BulkEdit` (`action=Merge`) in a new tab, which merges them as **Merge groups** does with one group and opens the merged song in the editor to pick its title, artist, tempo and length. |
| **Merge all with matching title and artist** (`POST /SharedIds/MergeMatching`) | A background task (watch `/Admin/AdminStatus`). It merges every pending `Exact` cluster: its songs all share the same title and the same non-empty artist (`Song.CreateTitleHash`, as the title-based merge candidates compare them). Every cluster shares an id by construction. It skips clusters with a rejected pair inside them, a `.NoMerge` song, a song with no artist, or lengths too far apart. Each merge gets the feat. cleanup (`FeaturedCleanup.Propose`, applied by the music4dance edit when it changes anything), so a feat. clause both titles carry still moves into the individual artists. |
| **Merge all that differ only by feat.** (`POST /SharedIds/MergeFeaturing`) | The same for the `WithoutFeaturing` clusters (`SharedIdLog.MergeFeaturing`): it merges each with the feat. cleanup, which takes the clause out of the title, adds the featured artists to the individual artists and picks a credit that names them all. It skips the same clusters. |
| **Merge all with artists credited differently** (`POST /SharedIds/MergeCredits`) | The same for the `SameArtists` clusters and the confirmed `FewerArtists` ones (`SharedIdLog.MergeCredits`). A `FewerArtists` cluster is checked against the songs' own ids, not the shared one: the shared id may be the one attached to the wrong version. Each song credited to the lead artist alone (every song, when none is) is looked up by the first of its Spotify ids, then iTunes ids, that no other song in the cluster has and whose track (`MusicServiceManager.GetMusicServiceTrack`, through its process-wide cache of 10,000 tracks, one at a time) has the title without feat. The cluster is confirmed when each of those tracks lists every artist the songs name, with the credits split using the tracks' artists as evidence: Spotify's list of artists, or the iTunes credit split with the songs' lead artists as evidence. As soon as one track leaves artists out, the cluster is skipped as probably a different recording (`ServiceListsFewer`); a song with no such track leaves it unconfirmed and skipped. The preview lists the ids checked and the artists they list. The merge's names come from the feat. cleanup with the same evidence, so "X" and "X & Y" land on "X & Y" with individual artists X and Y. |
| **Merge all with nothing to check against** / **Merge all whose own track leaves artists out** (`POST /SharedIds/MergeLeadArtist`, `listsFewer` false or true; buttons on the preview page only) | The `FewerArtists` clusters that **Merge all with artists credited differently** skips, after an admin has checked a sample: the ones with no track of their own to check (`SharedIdLog.MergeLeadArtist`), or with `listsFewer` the ones whose own track leaves artists out (`ServiceListsFewer`). It runs the same lookups to tell the two apart. Each merge gets the feat. cleanup with the lead artists (`SharedIdMatcher.Lead`) and any tracks' artists as evidence, so "X" and "X & Y" land on "X & Y" with individual artists X and Y when Y is a multi-word name. |
| **Preview merges** (`POST /SharedIds/PreviewMergeMatching`) | A background task that works out what each Merge all task would do, without merging, looking up the tracks the `FewerArtists` clusters need (`SharedIdLog.PlanMergeMatching`, which the merges themselves run first; given a user, it also previews each merge, `BuildSimpleMerge` then `FeaturedCleanup.Propose`, for the names it would land on). It also sorts the review page by tier. **Show preview** (`/SharedIds/Preview`) lists the clusters each task would merge, with what each merge would be called (bold where that isn't any of the songs' title or artist) and a button for the task, then the skipped ones grouped by reason: a rejected pair, a `.NoMerge` song, a song with no artist, lengths too far apart, or titles or artists that are close or differ. After a preview the review page says how many each task would merge and links to it. The result lives in memory until the next preview or a restart. |
| **Queue existing shared ids** (`POST /SharedIds/Seed`) | Runs the seed as a background task. |

## The client and its tests

The page app is `m4d/ClientApp/src/pages/shared-id-review/`. A cluster's choice is a
`ClusterChoice` (no Vue in it, wrapped in `reactive()` by the page): built from the cluster's
defaults, it sets groups, picks names and toggles removals, says which controls apply, and builds
the cluster's entry in the apply request (`toRequest()`). `ClusterCard`, `NameChoiceRow` and
`AlbumIds` render a cluster, a name row and a song's albums (with the lookup in the ids'
tooltips).

| Layer | Tests |
| --- | --- |
| Domain | `SharedIdLogTests`, `SharedIdOwnersTests`, `SharedIdGuardTests`, `SharedIdMatcherTests`, `SharedIdTypesTests`, `SongReviewEditTests`, `FeaturedCleanupTests` (`m4dModels.Tests`) |
| Web | `SharedIdsControllerAuthorizationTests` (both controllers dbAdmin, changes are antiforgery POSTs), `SharedIdPreviewStoreTests`, `SharedIdReviewModelTests` (the request's mapping, the JSON the client sends, the page model, the track lookup with a fake service, the owners response) (`m4d.Tests`) |
| Client | `ClusterChoice.test.ts` (defaults, groups, names, removals, deletions, Sort out ids, requests) and `shared-id-review.test.ts` (the page with a fixture model: badges, split note, disabled controls, deleting and its confirmation, the lookup, Sort out ids on a card and on the page, what Apply posts) |
| End to end | `e2e/tests/shared-ids.spec.ts` against `m4d.Sandbox`: makes two songs sharing an ISRC (`POST /api/song`, then `PATCH /api/song` to add the ISRC, which doesn't pass through the guard), seeds, and checks that **Not the same** stays resolved after another seed and that **Merge groups** merges under a picked title. |

## Future improvements

- A group-aware **If merged**: `POST /api/sharedids/preview` with one cluster's groups and
  removals would return each group's merged song (`BuildSimpleMerge` on each group after applying
  the removals to in-memory copies, saving nothing), so the column shows what Apply will make.
  Worth doing if splits and removals keep coming up in review.

- More automatic tiers, once a pattern in the close and different clusters is clear enough to
  merge without review, and `FeaturedCleanup` in the add-time merge
  ([song-merge-algorithm](song-merge-algorithm.md)).

- Moving a wrong id to the song it belongs to (rather than only removing it) needs the song
  editor.

- A bulk Sort out ids, and a report of the ids found bad: see
  [plans/shared-id-ownership](../plans/shared-id-ownership.md).

## History

- 2026-11: Enrichment guard, `SharedIdCandidates` table, seed, and the `/Admin/SharedIds` review page.
- 2026-11: Merge-all preview. Merge all now also re-plans clusters that changed while it ran, instead of leaving them for the next run.
- 2026-11: **If merged** column and page-at-a-time review (**Apply this page**), which replace the per-cluster Merge checked and Not the same buttons.
- 2026-11: Match tiers (differ only by feat., close match), sorted first after a preview; per-song title and artist choices; the feat. cleanup proposal, applied by a music4dance edit.
- 2026-11: Merge groups, which split a cluster in one Apply. The music4dance edit is no longer by a pseudo user, whose title and artist were ignored. The sort keeps a cluster's place after part of it is merged.
- 2026-11: Removing albums and ids from a cluster's songs on the review page; pairs that no longer share an id are marked not the same.
- 2026-11: Moved out of `AdminController` into `SharedIdsController` (`/Admin/SharedIds…` became `/SharedIds…`). Page loading moved into `SharedIdLog.LoadPage`, the last preview into the `SharedIdPreviewStore` singleton, and the match tiers into `SharedIdMatcher`. Name picks and removals name their song by id instead of column index.
- 2026-11: The review page became the `shared-id-review` Vue page app, applying through `POST /api/sharedids/apply`. Left-out songs' name radios and removed albums' id boxes are disabled, split radios name their group, and a cluster can have as many groups as songs. Client tests and an end-to-end test added.
- 2026-11: Clusters left for later go to the end of the queue until a restart.
- 2026-11: **Delete** on a song, and **Look up on Spotify and iTunes** (`POST /api/sharedids/tracks`).
- 2026-11: **Merge all that differ only by feat.**; both Merge all tasks apply the feat. cleanup, which now picks a credit naming every artist. The preview (renamed **Preview merges** and **Show preview**) shows what each merge would be called, and the review page summarizes it.
- 2026-11: Clusters whose lengths are more than 15 seconds apart aren't merged by either Merge all and start as Leave for later.
- 2026-11: Choosing a group on the review page makes the cluster a merge. **Merge all with artists credited differently**: the `SameArtists` tier, and the `FewerArtists` tier confirmed by the shared Spotify or iTunes track. The lookup tooltips list every Spotify artist.
- 2026-11: The `FewerArtists` check uses the songs' own (unshared) ids instead of the shared one, and skips clusters whose own track leaves artists out.
- 2026-11: The preview page can merge the same-lead-artist clusters the credits merge skips (`MergeLeadArtist`).
- 2026-11: **Sort out ids** (`POST /api/sharedids/owners`), on a card or for the page: looks up the shared ids and checks the removals that give each to the song it belongs to.
- 2026-11: The **If merged** names can be typed over.
- 2026-11: The feat. cleanup adds the artists a title features to the credit, and the tiers ignore remaster and single-version notes, which the cleanup also drops from the title.
- 2026-11: "with" starts a title's feat. clause; the cleanup's title is cut back to the shortest title the merged one starts with; Sort out ids prefers the song with the track's exact title and takes an album's ISRC off with its Spotify or iTunes id.
- 2026-11: A Tempo row picks or types the merged song's tempo. A leader with or without their backing group (an orchestra, a band or a small group: "Charlie Parker Quartet") is the same artist in the `Exact` and `WithoutFeaturing` tiers, and the cleanup credits the group with the leader as the individual artist.
- 2026-11: Titles with the same letters and digits match, and artists' aliases (`ArtistAliases`) are the same artist. Sort out ids looks up every Spotify and iTunes id on the cluster's songs, takes off the ones that belong to no song or that the service doesn't have, and moves an id to the song it belongs to with its album.
- 2026-11: Versions danced the same way ("- Radio Edit", "- Big Band Version") match the plain title; a qualifier that names a dance or a tempo keeps them apart. More artist aliases.
- 2026-11: Sort out ids prefers the song that names exactly the track's artists.
- 2026-11: Merge artist and Force merge artist combine the songs' artists in the If merged column, checked against their tracks unless forced.

## Related

- [song-merge-algorithm](song-merge-algorithm.md): title-based duplicate detection, and matching new songs by service id
- [service-track-lookup](service-track-lookup.md): `CreateSong` and the exact `ServiceIds` lookup
- [music-service-model](../music-services/music-service-model.md): purchase ids and `ServiceIds`
- [data-layer](../infrastructure/data-layer.md): migrations
