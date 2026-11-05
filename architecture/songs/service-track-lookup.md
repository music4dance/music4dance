# Song Lookup by External Track ID

**Type:** Reference
**Status:** Current
**Last verified:** 2026-11-03 (CreateSong dedup steps traced)
**Code:** `m4d/APIControllers/ServiceTrackController.cs`

## Overview

Several unrelated parts of the UI need to answer the same question — "does the catalog already
have a song for this Spotify/Apple Music track ID, and if not, can we create one?" — starting from
nothing but a service track ID. This document covers that backend mechanism on its own, decoupled
from any particular caller. For the specific UX flows that call into it, see [add-augment-song](add-augment-song.md)
(the deliberate `/song/augment` add/attach flow) and [drop-target-lookup](drop-target-lookup.md) (opportunistic
recognition of pasted IDs/URLs in ordinary search boxes). For the on-disk purchase-ID data model
(`AlbumDetails.Purchase`, the `Purchase:NN:XY` property encoding, multi-ID accumulation), see
[music-service-model](../music-services/music-service-model.md).

## Entry Points

All roads lead to the same endpoint:

```
GET /api/servicetrack/{cid}{id}?localOnly={bool}&danceId={danceId}
```

`{cid}` is the single-character service code (`s` = Spotify, `i` = Apple Music — see
`MusicService.CID` in [music-service-model](../music-services/music-service-model.md)).

| Caller | `localOnly` | Why |
| --- | --- | --- |
| `AugmentLookup.vue` (`/song/augment` "by Id" tab) | `false` | User deliberately wants the song added if it's missing |
| `AugmentSearch.vue` (after picking a service-search result) | `false` | Same — converges on the by-ID path once a track is chosen |
| `useDropTarget.ts` → `checkService` (main search box, advanced search, spotify-explorer) | `true` | Opportunistic — just want to redirect to an existing song if there is one; must never silently create a song from someone typing in a search box |
| `SongButton.vue` (Spotify-explorer admin tool) | `true` | Same non-mutating intent, in an admin browsing context |
| Playlist viewer's "Add unmatched song" links (see [song-search-results](../search/song-search-results.md)) | `false` | Same as Augment by-ID — arrives at `/song/augment?id=...`, which routes through `AugmentLookup.vue` |

## `ServiceTrackController.Get` — the two-step resolution

```csharp
[HttpGet("{id}")]
public async Task<IActionResult> Get(string id, bool localOnly = false, string danceId = null)
```

([ServiceTrackController.cs:23](../../m4d/APIControllers/ServiceTrackController.cs#L23))

### Step 1 — exact ID match (always attempted, cheap, read-only)

```csharp
song = await SongIndex.GetSongFromService(service, id);
```

`SongIndex.GetSongFromService` runs an exact-match filter (`SongIndex.FindSongsByServiceIds`)
on the Azure Search `ServiceIds` field for `{CID}:{id}` (e.g.
`S:4iV5W9uYEdYUVa79Axb7Rh`). `ServiceIds` is populated from
`Song.GetExtendedPurchaseIds()`/`AlbumDetails.GetExtendedPurchaseIds()`, which flattens *every*
accumulated ID in every purchase slot across every service — including ISRC (`R:{isrc}`), since
`ISRCService.IsIndexed` is `true` even though it has no external search API (see
[music-service-model](../music-services/music-service-model.md) § IsIndexed vs CanSearchExternally). So the same method does
an ISRC lookup when passed `MusicService.GetService(ServiceType.ISRC)`, which is what
`MusicServiceManager.FindSongByISRC` does.

If a match is found, resolution stops here — `localOnly` is irrelevant, nothing is created or
modified.

### Step 2 — fetch + dedupe + create (only when Step 1 misses and `!localOnly`)

```csharp
song = await MusicServiceManager.CreateSong(Database, user, id, service, danceId);
```

`MusicServiceManager.CreateSong` ([MusicServiceManager.cs:466](../../m4d/Utilities/MusicServiceManager.cs#L466)):

```csharp
var track = await GetMusicServiceTrack(id, service);      // 1. live fetch from the service API
if (track == null) return null;

var song = await Song.UserCreateFromTrack(dms, user, track, danceId);  // 2. transient candidate

var found = false;
var oldSong = await FindSongByISRC(dms, track.ISRC)        // 3. dedupe — the track's ISRC,
    ?? await dms.SongIndex.FindMatchingSong(song);         //    then title/length
if (oldSong != null) { found = true; song = oldSong; }

_ = await UpdateSongAndServices(dms, song);   // 4. blanket re-search of every searchable service
_ = await UpdateFromTracks(dms, song, [track]);  // 5. apply *this* track's data explicitly
_ = await UpdateAudioData(dms, service, song);   // 6. tempo/danceability/sample/ISRC, if missing

if (!found)                                    // 7. dedupe again on the ids enrichment added
{
    var existing = await dms.SongIndex.FindSongByServiceIds(song);
    if (existing != null) { _ = await UpdateFromTracks(dms, existing, [track]); song = existing; found = true; }
}

if (found) { await dms.SongIndex.SaveSong(song); }  // a new song isn't saved here: see below
return song;
```

A new song is returned unsaved. The add page shows it as "Create Song", and it is saved only when
the user submits it (`POST /api/song` → `SongIndex.CreateOrMergeSong`), which checks its ids once
more first. See [song-merge-algorithm](song-merge-algorithm.md) § Matching new songs by service id.

1. **`GetMusicServiceTrack(id, service)`** — fetches the track's own metadata from the live
   service API (`BuildTrackRequest` → `service.ParseTrackResults`), *not* a catalog lookup. Result
   is memoized in a static, process-lifetime-only cache (`s_trackCache`, keyed `"{CID}:{id}"`,
   cleared wholesale once it exceeds 10,000 entries — not an LRU). For Spotify, `ParseTrackResults`
   already reads `external_ids.isrc` into `ServiceTrack.ISRC`
   ([SpotifyService.cs:182-193](../../m4dModels/SpotifyService.cs#L182)), which step 3 uses.
2. **`Song.UserCreateFromTrack`** — builds a transient, unsaved `Song` from the fetched track (row
   creation attributed to the real user, everything else re-attributed to the service's bot user —
   see [add-augment-song](add-augment-song.md) § "Attribution split").
3. **`FindSongByISRC`, then `FindMatchingSong`** — the track's ISRC first (Spotify only; iTunes
   tracks carry none), then `MergeFromTitle`: searches the catalog by title, then requires
   `MatchType.Exact` or `MatchType.Length` title/artist similarity. Anything weaker leaves the
   transient song from step 2 as a new song, unless step 7 finds it.
4. **`UpdateSongAndServices`** — a blanket "fill in whatever's missing from every searchable
   service" pass (iTunes, Spotify — not just the one being added), independent of the specific
   track being resolved. Mostly a no-op for the service actually in play (step 5 handles that
   explicitly) but fills gaps for other services on a freshly-matched *existing* song.
5. **`UpdateFromTracks`** — see [add-augment-song](add-augment-song.md) § "`UpdateFromTracks` →
   `UpdateMusicServiceFromTrack` → `UpdateMusicService`" for the full diff/persist mechanics. This
   is also where a Spotify track's ISRC gets attached to the matched album
   (`ad?.AddPurchaseId(PurchaseType.Song, ServiceType.ISRC, track.ISRC)` in
   `UpdateMusicServiceFromTrack`), so a song enriched via this path picks up its ISRC immediately —
   it doesn't have to wait for the lazy `GetISRCData` pass in step 6.
6. **`UpdateAudioData`** — tempo/danceability/valence (Spotify audio features), sample preview URL
   (Spotify, falling back to iTunes), and `GetISRCData` (backfills ISRC for *existing* Spotify IDs
   on the song that don't have one yet, in case step 5 didn't cover every album).
7. **`FindSongByServiceIds`** — steps 4 to 6 can give a new song ids (an iTunes match found by
   title, ISRCs) that steps 1 and 3 never saw. If a catalog song already holds one, the track is
   attached to that song instead. When the song being enriched is already in the catalog (a
   step 3 match), steps 4 to 6 instead leave off any id another song holds and queue the pair
   for review ([shared-id-review](shared-id-review.md)).

Step 1 only succeeds if the *literal* id being looked up is already on file. Services, Spotify in
particular, periodically reissue a different track id for the same recording (see
[music-service-model](../music-services/music-service-model.md) § "Multi-ID Accumulation"), which
is why steps 3 and 7 match on the other ids too.

## Related Code

| File | Purpose |
| --- | --- |
| `m4d/APIControllers/ServiceTrackController.cs` | `Get` — Step 1/Step 2 orchestration |
| `m4d/Utilities/MusicServiceManager.cs` | `CreateSong`, `GetMusicServiceTrack`, `UpdateSongAndServices`, `UpdateFromTracks`, `UpdateAudioData`, `GetISRCData` |
| `m4dModels/SongIndex.cs` | `GetSongFromService`/`FindSongsByServiceIds`/`FindSongByServiceIds` (exact `ServiceIds` match), `FindMatchingSong`/`MergeFromTitle` (title dedup) |
| `m4dModels/Song.cs` | `UserCreateFromTrack`, `FindAlbum` |
| `m4dModels/SpotifyService.cs` | `ParseTrackResults` — parses `external_ids.isrc` |
| `m4dModels/MusicService.cs` | `GetService(char)`, service registry |
| `m4d/ClientApp/src/helpers/ServiceMatcher.ts` | Client-side id/URL recognition, `findSong` |
| `m4d/ClientApp/src/pages/augment/components/AugmentLookup.vue` | Deliberate entry point (`localOnly=false`) |
| `m4d/ClientApp/src/composables/useDropTarget.ts` | Opportunistic entry point (`localOnly=true`) |
