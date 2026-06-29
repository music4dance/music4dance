# Add / Augment Song

## Overview

"Augment" is the user-facing flow for adding a song to the music4dance catalog, or attaching a
service (Spotify/Apple Music) track to a catalog song that's missing it. It lives at
`/song/augment` (`SongController.Augment`, [SongController.cs:784](../m4d/Controllers/SongController.cs#L784))
and is rendered as a Vue page (`src/pages/augment/`). This document covers that flow end to end:
the two ways a user can locate a track (by service ID, by title/artist), the lookup-time
enrichment/dedup that happens server-side before the user ever sees an edit form, and the
separate, later step where the user's own votes/tags get saved.

This is a different "add songs" surface than the admin playlist-import system
([[playlist-management]]) and different again from the read-only [[song-search-results]] flow —
this one is anonymous-reachable for *searching*, but actually creating/editing requires sign-in.

---

## Entry Point: `/song/augment`

```csharp
public ActionResult Augment(string title = null, string artist = null, string id = null, string dance = null)
```

Renders `AugmentViewModel { Title, Artist, Id, Dance }` into the `augment` Vue page. All four
fields are optional query-string seeds — e.g. the playlist viewer's "add unmatched song" links
(see [[song-search-results]]) land here with just `?id=<spotifyTrackId>`.

### Vue page (`src/pages/augment/App.vue`)

A three-phase state machine (`AugmentPhase`: `lookup` → `results`/`edit`):

- **`lookup`** — shown first. If the viewer is authenticated (`context.userName` set), shows a
  `BTabs` with **by Title**, **by Id**, and (for admins) **Admin** (paste raw property TSV). If
  not authenticated, shows `AugmentInfo` instead — a "you must sign in" gate; anonymous users can
  search but not commit anything.
- **`results`/`edit`** — once a song is found or a new one is started, `SongCore` (the same
  song-detail editor used by `/song/details`) takes over, pre-filled via `SongDetailsModel
  { created, songHistory, filter, userName }`. The page's only job before this point is to obtain
  that `SongDetailsModel`. Saving/voting from here on follows the **normal song-edit save path**,
  not anything Augment-specific.

`computedId`/`computedDance` are stripped to `undefined` once `model.value.id` is cleared (it's
needed so `AugmentLookup` doesn't immediately re-trigger its `onMounted` lookup on remount after a
reset).

---

## Locating a Track

### By ID (`AugmentLookup.vue`)

Used when the caller already has a service ID or URL (the common path coming from the playlist
viewer's "Add" links, or pasting a Spotify/Apple Music URL). `ServiceMatcher`
(`src/helpers/ServiceMatcher.ts`) recognizes the ID shape via regex (bare 22-char Spotify ID, bare
7–10 digit iTunes ID, or either as a full open.spotify.com/itunes URL) and calls:

```
GET /api/servicetrack/{serviceChar}{id}?localOnly=false&danceId=<dance>
```

→ `ServiceTrackController.Get` ([ServiceTrackController.cs:23](../m4d/APIControllers/ServiceTrackController.cs#L23)).

### By Title/Artist (`AugmentSearch.vue`)

Used when the caller doesn't have a specific track ID. Two-step UI:

1. `GET /api/song?title=...&artist=...` (`APIControllers/SongController.Get`, calls
   `SongIndex.SongsFromTitleArtist`) — searches the **catalog** first. If a match is shown and
   picked, the user clicks straight into edit mode (`SongDetailsModel{created:false}`) — no
   service lookup needed at all.
2. If nothing matches (or the user wants a different one), **Search Spotify** / **Search Apple
   Music** buttons call `GET /api/musicservice?service=S|I&title=...&artist=...`
   (`MusicServiceController.Get` → `MusicServiceManager.FindMusicServiceSong`) to search the
   *service's* catalog instead. Picking a result from that list calls
   `GET /api/servicetrack/{service}{trackId}` — i.e. it converges on the same by-ID lookup as
   above, just with a service-search step in front of it.

---

## `ServiceTrackController.Get` — Lookup, Dedup, and Side-Effecting Create

```csharp
[HttpGet("{id}")]
public async Task<IActionResult> Get(string id, bool localOnly = false, string danceId = null)
```

This is a `GET`, but it is **not read-only** — looking up an ID this way can create or modify a
catalog song as a side effect, which is why the controller carries `[ValidateAntiForgeryToken]`.

1. `SongIndex.GetSongFromService(service, id)` — exact lookup: searches the Azure `ServiceIds`
   field for `"{CID}:{id}"` (e.g. `"S:4iV5W9...""`). If found, that's the song — done, no
   enrichment needed, nothing to merge.
2. If not found and `!localOnly`: `MusicServiceManager.CreateSong(Database, user, id, service,
   danceId)` — see below. `localOnly=true` (used by `SongButton.vue` in the Spotify explorer admin
   tool) skips this and just reports "not found" without creating anything.
3. `created` is computed as `await SongIndex.FindSong(song.SongId) == null` — checked *after*
   `CreateSong` has already run (and already saved, if it matched/modified an existing song), so
   for the "matched existing song" case this is always `false` → the client shows "Edit Song"
   ("We found this song in the music4dance catalog..."). For a genuinely new song it's `true` →
   "Create Song".

### `MusicServiceManager.CreateSong` — [MusicServiceManager.cs:457](../m4d/Utilities/MusicServiceManager.cs#L457)

```csharp
public async Task<Song> CreateSong(DanceMusicCoreService dms,
    ApplicationUser user, string id, MusicService service, string danceId = null)
{
    var track = await GetMusicServiceTrack(id, service);          // fetch metadata for *this* id
    if (track == null) return null;

    var song = await Song.UserCreateFromTrack(dms, user, track, danceId);   // transient candidate

    var found = false;
    var oldSong = await dms.SongIndex.FindMatchingSong(song);      // dedup by title (+ length)
    if (oldSong != null)
    {
        found = true;
        song = oldSong;                                            // prefer the existing song
    }

    _ = await UpdateSongAndServices(dms, song);     // re-search ALL services by title/artist
    _ = await UpdateFromTracks(dms, song, [track]); // apply *this* track's data explicitly
    _ = await UpdateAudioData(dms, service, song);  // tempo/danceability/sample, if missing

    if (found)
    {
        await dms.SongIndex.SaveSong(song);          // persist (new songs are saved earlier)
    }

    return song;
}
```

- **Dedup**: `SongIndex.FindMatchingSong` (`MergeFromTitle` under the hood — same general family
  of title-similarity matching as [[song-merge-algorithm]], though that document covers the
  admin merge-candidate UI specifically) only treats `MatchType.Exact` or `MatchType.Length` as a
  match. Anything weaker and `found` stays `false` — a new song gets created, even if it's
  arguably "the same song" the cataloger was thinking of.
- **Attribution split**: `Song.UserCreateFromTrack` attributes the row creation to the real
  `user`, but immediately re-attributes the rest of the initial property block to the *service's*
  bot/pseudo `ApplicationUser` (e.g. `spotify|P`) — the human only gets credit for the implicit
  "like". The enrichment calls below (`UpdateSongAndServices`, `UpdateFromTracks`) attribute their
  edits to the service bot user too (see `UpdateFromTracks` below) — this is why service-driven
  metadata edits show up in song history as `batch-s|P`, `spotify|P`, etc., not the cataloger's
  name.
- **Why `UpdateSongAndServices` runs at all here**: it's a blanket "fill in whatever's missing
  from every service" pass (iTunes, Amazon, etc., not just the one the user searched), independent
  of the literal track being added. It's mostly a no-op for the service actually being added
  (since that work happens next), but fills gaps for the *other* services on a freshly-matched
  existing song.

### `UpdateFromTracks` → `UpdateMusicServiceFromTrack` → `UpdateMusicService`

```
UpdateFromTracks(dms, song, [track])
  → edit = await Song.Create(song, dms)              // independent deep clone, for diffing
  → UpdateMusicServiceFromTrack(dms, edit, track, ref tags)
      → UpdateMusicService(edit, service, name, album, artist, trackId, collectionId, ...)
  → SongIndex.EditSong(serviceBotUser, song, edit, tags)   // diffs edit against song, mutates song in place
```

`UpdateMusicService` ([MusicServiceManager.cs:241](../m4d/Utilities/MusicServiceManager.cs#L241))
fills in `Title`/`Artist` only if currently blank, finds-or-creates the matching `AlbumDetails`
entry (`Song.FindAlbum(album, trackNum)`, matched by cleaned album name + optionally track
number), and calls `UpdateMusicServicePurchase` to stamp the service's track/album IDs onto that
album entry. Length is backfilled from the track's duration if the song doesn't have one.

`SongIndex.EditSong(user, song, edit, tags)` (the 3-arg overload,
[SongIndex.cs:271](../m4dModels/SongIndex.cs#L271)) is what actually performs the diff —
`Song.Edit` → `EditCore` walks `edit.Albums` against the song's current albums by `Index`: a
matching index calls `AlbumDetails.ModifyInfo`/`PurchaseDiff` (changed-property edits); no matching
index calls `AlbumDetails.CreateProperties` (brand-new album, new properties). Either way, the
diff is applied to `song`'s own `SongProperties` *in place* — `CreateSong`'s final
`SongIndex.SaveSong(song)` is what pushes the accumulated in-memory state to the Azure index (and,
via the same code path used elsewhere, the durable property log).

---

## The One-ID-Per-Album-Per-Service Constraint (and the fix for stale IDs)

`AlbumDetails.Purchase` is a `Dictionary<string, string>` keyed by service+type (e.g. `"SS"` =
Spotify Song) — **one value per key**. `UpdateMusicServicePurchase`
([MusicServiceManager.cs:344](../m4d/Utilities/MusicServiceManager.cs#L344)) used to do:

```csharp
var old = ad.GetPurchaseIdentifier(service.Id, pt);
if (old != null && old.StartsWith(trackId)) return;   // already correct, skip
ad.SetPurchaseInfo(pt, service.Id, trackId);            // otherwise: set (overwrite) unconditionally
```

Services — Spotify in particular — periodically reissue a *different* track ID for what is, as
far as the catalog cares, the same recording (same title/artist/album/track number). Before the
fix below, `Song.FindAlbum` would still match that existing album (it matches on album name/track
number, not on the service ID), and `UpdateMusicServicePurchase` would silently **overwrite** the
already-stored ID with the new one — losing the old ID rather than accumulating both. The
practical symptom: a playlist built from the *old* ID would eventually stop matching after the
*new* ID overwrote it, even though the song was otherwise correctly enriched.

The fix (`UpdateMusicService`,
[MusicServiceManager.cs:260](../m4d/Utilities/MusicServiceManager.cs#L260)): before reusing a
matched album, check whether it already carries a *different*, non-empty ID for this exact
service+type:

```csharp
var ad = song.FindAlbum(album, trackNum);
if (ad != null && HasConflictingPurchase(ad, service, PurchaseType.Song, trackId))
{
    ad = null;   // force the "new album" branch below instead of reusing this one
}
```

When there's a conflict, the code falls through to the "no album matched" branch and creates a
**new** `AlbumDetails` entry (own `Index`, own `Purchase` dict) carrying just the new ID. Both the
old and new IDs now live in `song.Albums` (in different entries) and both show up in
`Song.GetExtendedPurchaseIds()` / the Azure `ServiceIds` field (which aggregates **all** albums —
see `SongIndex`'s document-build step), so either ID will match this song from then on. No data is
lost, and no separate re-validation pass is required — this self-heals the next time anyone
augments/looks up the song under the new ID, e.g. via the playlist viewer's "Add" button
([[song-search-results]]).

This only guards `PurchaseType.Song` (the reported case); `PurchaseType.Album` (`collectionId`)
isn't covered by the same check yet — see Known Gaps.

---

## Saving the User's Own Edits

Everything above happens *before* the user has touched anything — it's automatic enrichment
attributed to the service bot user, triggered by a lookup. The user's actual contribution (voting
on dances, adding tags, correcting title/artist) happens in `SongCore` after the lookup, and is
saved through a completely separate path (`SongEditor.ts`, `APIControllers/SongController`):

| Case | Client call | Server action |
| --- | --- | --- |
| Editing an existing song (the common Augment outcome — "Edit Song") | `PATCH /api/song/{id}` | `SongIndex.AppendHistory` — normal user edit, attributed to the signed-in user |
| Brand new song (`created: true`) | `POST /api/song/` | `SongIndex.CreateOrMergeSong` |
| Admin raw edit | `PUT /api/song/{id}` | `SongIndex.AdminEditSong` |

This split matters for the architecture: the service-ID merge/enrichment fix above happens
*regardless* of whether the user ever clicks save on the edit form — simply looking a track up by
ID is enough to trigger it (and persist it), because `ServiceTrackController.Get` is a mutating
`GET`.

---

## Known Gaps / Future Work

- **`PurchaseType.Album` (`collectionId`) conflicts** aren't guarded the same way `Song` IDs are —
  an album-id reissue on an already-matched album would still overwrite. Same fix shape would
  apply if this turns out to matter in practice.
- **No proactive re-validation**: stale IDs are only fixed reactively, when someone happens to
  augment/look up the song under its new ID (e.g. via the playlist viewer's unmatched-song "Add"
  flow). There's no background job that re-checks existing catalog IDs against the service for
  staleness. An additional lookup step during playlist matching itself (matching by title/artist
  as a fallback when the literal ID filter misses) was discussed but not implemented — see
  [[song-search-results]] for the current ID-only matching behavior of the playlist viewer.
- **Dedup strictness**: `FindMatchingSong` only accepts `MatchType.Exact`/`MatchType.Length`. A
  near-miss (e.g. a "(Remastered)" suffix difference that title-cleaning doesn't normalize) results
  in a duplicate song being created rather than merged — a separate concern from the ID-conflict
  fix here, addressable instead via [[song-merge-algorithm]]'s admin merge-candidate tooling after
  the fact.

---

## Related Code

| File | Purpose |
| --- | --- |
| `m4d/Controllers/SongController.cs` | `Augment` action — renders the Vue page |
| `m4d/ClientApp/src/pages/augment/App.vue` | Phase state machine (lookup/results/edit) |
| `m4d/ClientApp/src/pages/augment/components/AugmentLookup.vue` | By-ID lookup tab |
| `m4d/ClientApp/src/pages/augment/components/AugmentSearch.vue` | By-title/artist search tab, with service-search fallback |
| `m4d/ClientApp/src/helpers/ServiceMatcher.ts` | ID/URL recognition, `/api/servicetrack` call |
| `m4d/APIControllers/ServiceTrackController.cs` | By-ID lookup + side-effecting create/merge |
| `m4d/APIControllers/MusicServiceController.cs` | By-title/artist search against a service's own catalog |
| `m4d/APIControllers/SongController.cs` | Catalog title/artist search; PATCH/PUT/POST save endpoints |
| `m4d/Utilities/MusicServiceManager.cs` | `CreateSong`, `UpdateSongAndServices`, `UpdateFromTracks`, `UpdateMusicService`, `UpdateMusicServicePurchase`/`HasConflictingPurchase` |
| `m4dModels/Song.cs` | `UserCreateFromTrack`, `CreateFromTrack`, `FindAlbum`, `Edit`/`EditCore` |
| `m4dModels/AlbumDetails.cs` | `Purchase` dictionary, `PurchaseDiff`/`ModifyInfo`/`CreateProperties` |
| `m4dModels/SongIndex.cs` | `GetSongFromService`, `FindMatchingSong`, `EditSong` overloads, `SaveSong` |
| `m4d/ClientApp/src/models/SongEditor.ts` | Client-side save (`saveChanges`/`create`) |
