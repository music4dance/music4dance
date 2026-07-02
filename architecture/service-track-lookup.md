# Song Lookup by External Track ID

## Overview

Several unrelated parts of the UI need to answer the same question — "does the catalog already
have a song for this Spotify/Apple Music track ID, and if not, can we create one?" — starting from
nothing but a service track ID. This document covers that backend mechanism on its own, decoupled
from any particular caller. For the specific UX flows that call into it, see [[add-augment-song]]
(the deliberate `/song/augment` add/attach flow) and [[drop-target-lookup]] (opportunistic
recognition of pasted IDs/URLs in ordinary search boxes). For the on-disk purchase-ID data model
(`AlbumDetails.Purchase`, the `Purchase:NN:XY` property encoding, multi-ID accumulation), see
[[music-service-model]].

## Entry Points

All roads lead to the same endpoint:

```
GET /api/servicetrack/{cid}{id}?localOnly={bool}&danceId={danceId}
```

`{cid}` is the single-character service code (`s` = Spotify, `i` = Apple Music — see
`MusicService.CID` in [[music-service-model]]).

| Caller | `localOnly` | Why |
| --- | --- | --- |
| `AugmentLookup.vue` (`/song/augment` "by Id" tab) | `false` | User deliberately wants the song added if it's missing |
| `AugmentSearch.vue` (after picking a service-search result) | `false` | Same — converges on the by-ID path once a track is chosen |
| `useDropTarget.ts` → `checkService` (main search box, advanced search, spotify-explorer) | `true` | Opportunistic — just want to redirect to an existing song if there is one; must never silently create a song from someone typing in a search box |
| `SongButton.vue` (Spotify-explorer admin tool) | `true` | Same non-mutating intent, in an admin browsing context |
| Playlist viewer's "Add unmatched song" links (see [[song-search-results]]) | `false` | Same as Augment by-ID — arrives at `/song/augment?id=...`, which routes through `AugmentLookup.vue` |

## `ServiceTrackController.Get` — the two-step resolution

```csharp
[HttpGet("{id}")]
public async Task<IActionResult> Get(string id, bool localOnly = false, string danceId = null)
```

([ServiceTrackController.cs:23](../m4d/APIControllers/ServiceTrackController.cs#L23))

### Step 1 — exact ID match (always attempted, cheap, read-only)

```csharp
song = await SongIndex.GetSongFromService(service, id);
```

`SongIndex.GetSongFromService` ([SongIndex.cs:156](../m4dModels/SongIndex.cs#L156)) runs an exact
phrase search against the Azure Search `ServiceIds` field for `"{CID}:{id}"` (e.g.
`"S:4iV5W9uYEdYUVa79Axb7Rh"`). `ServiceIds` is populated from
`Song.GetExtendedPurchaseIds()`/`AlbumDetails.GetExtendedPurchaseIds()`, which flattens *every*
accumulated ID in every purchase slot across every service — including ISRC (`R:{isrc}`), since
`ISRCService.IsIndexed` is `true` even though it has no external search API (see
[[music-service-model]] § IsIndexed vs CanSearchExternally). So this same method already works
for an ISRC lookup today if a caller passes `MusicService.GetService(ServiceType.ISRC)` — nothing
currently does, which is the gap [[isrc-spotify-fallback-plan]] addresses.

If a match is found, resolution stops here — `localOnly` is irrelevant, nothing is created or
modified.

### Step 2 — fetch + dedupe + create (only when Step 1 misses and `!localOnly`)

```csharp
song = await MusicServiceManager.CreateSong(Database, user, id, service, danceId);
```

`MusicServiceManager.CreateSong` ([MusicServiceManager.cs:466](../m4d/Utilities/MusicServiceManager.cs#L466)):

```csharp
var track = await GetMusicServiceTrack(id, service);      // 1. live fetch from the service API
if (track == null) return null;

var song = await Song.UserCreateFromTrack(dms, user, track, danceId);  // 2. transient candidate

var found = false;
var oldSong = await dms.SongIndex.FindMatchingSong(song);  // 3. dedupe — title/length only
if (oldSong != null) { found = true; song = oldSong; }

_ = await UpdateSongAndServices(dms, song);   // 4. blanket re-search of every searchable service
_ = await UpdateFromTracks(dms, song, [track]);  // 5. apply *this* track's data explicitly
_ = await UpdateAudioData(dms, service, song);   // 6. tempo/danceability/sample/ISRC, if missing

if (found) { await dms.SongIndex.SaveSong(song); }  // new songs are saved earlier, inside UserCreateFromTrack
return song;
```

1. **`GetMusicServiceTrack(id, service)`** — fetches the track's own metadata from the live
   service API (`BuildTrackRequest` → `service.ParseTrackResults`), *not* a catalog lookup. Result
   is memoized in a static, process-lifetime-only cache (`s_trackCache`, keyed `"{CID}:{id}"`,
   cleared wholesale once it exceeds 10,000 entries — not an LRU). For Spotify, `ParseTrackResults`
   already reads `external_ids.isrc` into `ServiceTrack.ISRC`
   ([SpotifyService.cs:182-193](../m4dModels/SpotifyService.cs#L182)) — that data is available
   here but currently unused for dedup (see "Known Gap" below).
2. **`Song.UserCreateFromTrack`** — builds a transient `Song` from the fetched track and
   immediately saves it (row creation attributed to the real user, everything else re-attributed
   to the service's bot user — see [[add-augment-song]] § "Attribution split").
3. **`FindMatchingSong`** — the *only* dedup check. Wraps `MergeFromTitle`
   ([SongIndex.cs:685](../m4dModels/SongIndex.cs#L685)): searches the catalog by title, then
   requires `MatchType.Exact` or `MatchType.Length` title/artist similarity. Anything weaker (or a
   title that doesn't cluster together at all) results in the transient song from step 2 being kept
   as a brand-new catalog entry.
4. **`UpdateSongAndServices`** — a blanket "fill in whatever's missing from every searchable
   service" pass (iTunes, Spotify — not just the one being added), independent of the specific
   track being resolved. Mostly a no-op for the service actually in play (step 5 handles that
   explicitly) but fills gaps for other services on a freshly-matched *existing* song.
5. **`UpdateFromTracks`** — see [[add-augment-song]] § "`UpdateFromTracks` →
   `UpdateMusicServiceFromTrack` → `UpdateMusicService`" for the full diff/persist mechanics. This
   is also where a Spotify track's ISRC gets attached to the matched album
   (`ad?.AddPurchaseId(PurchaseType.Song, ServiceType.ISRC, track.ISRC)` in
   `UpdateMusicServiceFromTrack`), so a song enriched via this path picks up its ISRC immediately —
   it doesn't have to wait for the lazy `GetISRCData` pass in step 6.
6. **`UpdateAudioData`** — tempo/danceability/valence (Spotify audio features), sample preview URL
   (Spotify, falling back to iTunes), and `GetISRCData` (backfills ISRC for *existing* Spotify IDs
   on the song that don't have one yet, in case step 5 didn't cover every album).

### Known Gap: no ISRC-based dedup in Step 3

Step 1 (exact ID match) only succeeds if the *literal* ID being looked up is already on file.
Services — Spotify in particular — periodically reissue a different track ID for what is,
recording-wise, the same song (see [[music-service-model]] § "Multi-ID Accumulation"). When that
happens, Step 1 misses, and Step 3's title-only dedup is the sole remaining defense against
creating a duplicate. A title/artist near-miss (parenthetical differences, alternate spellings,
etc.) that `FindMatchingSong` doesn't recognize results in a duplicate catalog song — even though
the *ISRC* on the freshly-fetched track (available for free as part of Step 2's `GetMusicServiceTrack`
call) may already be sitting on an existing song via a previously-known Spotify ID for the same
recording. There is no code path today that checks the fetched track's ISRC against the catalog
before falling through to title matching. See [[isrc-spotify-fallback-plan]] for the proposed fix.

## Related Code

| File | Purpose |
| --- | --- |
| `m4d/APIControllers/ServiceTrackController.cs` | `Get` — Step 1/Step 2 orchestration |
| `m4d/Utilities/MusicServiceManager.cs` | `CreateSong`, `GetMusicServiceTrack`, `UpdateSongAndServices`, `UpdateFromTracks`, `UpdateAudioData`, `GetISRCData` |
| `m4dModels/SongIndex.cs` | `GetSongFromService` (exact `ServiceIds` match), `FindMatchingSong`/`MergeFromTitle` (title dedup) |
| `m4dModels/Song.cs` | `UserCreateFromTrack`, `FindAlbum` |
| `m4dModels/SpotifyService.cs` | `ParseTrackResults` — parses `external_ids.isrc` |
| `m4dModels/MusicService.cs` | `GetService(char)`, service registry |
| `m4d/ClientApp/src/helpers/ServiceMatcher.ts` | Client-side id/URL recognition, `findSong` |
| `m4d/ClientApp/src/pages/augment/components/AugmentLookup.vue` | Deliberate entry point (`localOnly=false`) |
| `m4d/ClientApp/src/composables/useDropTarget.ts` | Opportunistic entry point (`localOnly=true`) |
