# MusicServiceManager API Call Flows

`m4d/Utilities/MusicServiceManager.cs` is the single class responsible for all external music-service HTTP calls. It is injected as a scoped service (takes `IConfiguration` in its constructor).

> For registered services, the purchase-ID storage format, and client-side rendering see [music-service-integration.md](music-service-integration.md). For the class hierarchy and data encoding see [music-service-model.md](music-service-model.md).

---

## HTTP Layer

All reads go through `GetMusicServiceResults`; all writes go through `MusicServiceAction`.

### `GetMusicServiceResults(request, service, principal?)`

1. Checks `CheckPaused(service)` — if iTunes is currently paused (after repeated 403s), returns `null` immediately.
2. Builds an `HttpRequestMessage(GET, request)` with `Accept: application/json`.
3. Calls `AdmAuthentication.GetServiceAuthorization(Configuration, service.Id, principal)` to get the `Authorization` header value.
   - For Spotify with no `principal`: uses the app-level client-credentials token.
   - For Spotify with a `principal`: uses the user's OAuth token.
   - For iTunes: no auth header (public API).
4. Sends via the shared `HttpClientHelper.Client` singleton.
5. On success (200): reads response body, calls `service.PreprocessResponse` (a no-op for iTunes/Spotify), deserializes with `JsonConvert.DeserializeObject` (dynamic).
6. Increments `iTunesCalls` or `spotifyCalls` counters.

**Rate limit handling:**

| Condition                           | Action                                                  |
| ----------------------------------- | ------------------------------------------------------- |
| `X-RateLimit-Remaining` 1–19        | Sleep 3 s (pre-emptive throttle)                        |
| HTTP 429 (Too Many Requests)        | Sleep 15 s, retry (infinite loop)                       |
| iTunes HTTP 403, retries > 0        | Sleep 60 s, decrement retry counter (up to 5 attempts)  |
| iTunes HTTP 403, retries exhausted  | Set `_pauseITunes = DateTime.Now`, throw `AbortBatchException` |
| `_pauseITunes` set < 15 min ago     | `CheckPaused` returns true → entire service call skipped |

### `MusicServiceAction(request, input, method, service, principal, contentType?)`

Used for Spotify write operations (create playlist, set tracks, upload image). Requires a valid `principal` with Spotify OAuth. Returns deserialized JSON or null on failure.

### In-Process Track Cache

```csharp
private static readonly Dictionary<string, ServiceTrack> s_trackCache = [];
```

Key: `"{CID}:{trackId}"` (e.g., `"S:3X2p7fCVH4g5ITBGH8pEtZ"`). Cleared when count exceeds 10,000.

---

## Song Enrichment Pipeline

### Entry Points

| Method                             | When used                                       |
| ---------------------------------- | ----------------------------------------------- |
| `UpdateSongAndServices`            | New song added; always re-queries all services  |
| `ConditionalUpdateSongAndServices` | Bulk commit; skips services already tried       |

Both iterate `MusicService.GetSearchableServices()` (iTunes then Spotify — services with `CanSearchExternally = true`) and call the same inner methods. `ISRCService` is intentionally excluded from this loop (`CanSearchExternally = false`); its IDs are populated by `GetISRCData` rather than by the general enrichment path.

### Inner Flow

```
UpdateSongAndService(dms, sd, service)
    └─ MatchSongAndService(sd, service)          → IList<ServiceTrack>
    └─ UpdateFromTracks(dms, sd, tracks)         → bool changed

UpdateAudioData(dms, service, sd)
    └─ [Spotify only] GetEchoData(dms, sd)
           └─ ValidateAndCorrectTempo(dms, sd)
    └─ [Spotify or iTunes] GetSampleData(dms, sd)
    └─ [Spotify only, if no ISRC yet] GetISRCData(dms, sd)
```

---

## Search Flow

### `MatchSongAndService(song, service)`

1. Calls `FindMusicServiceSong(service, song)` with the raw title/artist.
2. If no results, retries with `Song.CleanString` applied to title and artist (removes punctuation and parenthetical content).
3. Applies `song.TitleArtistFilter(tracks)` — weak filter: keeps only results whose title and artist fuzzy-match.
4. Narrows to an exact album match if the song has no length yet, or clusters by `DurationFilter` (±6 s) if it does.
5. If still empty and the song has no "real" albums, picks the "dominant" cluster by `FindDominantTrack`.
6. Appends any track IDs already stored on the song for this service (reconstructed locally, no extra API call).

### `FindMusicServiceSong(service?, song?, title?, artist?, album?)`

Dispatcher: calls `DoFindMusicServiceSong` for one service (or all searchable services if `service` is null), then applies `FilterKaraoke` and `song.RankTracks` / `Song.RankTracksByCluster`.

`FilterKaraoke` excludes any track whose `Name` or `Album` contains "karaoke", "in the style of", or "a tribute to".

### `DoFindMusicServiceSong` → `FindMSSongGeneral`

```
service.BuildSearchRequest(artist, title)
    └─ iTunes: https://itunes.apple.com/search?term={artist+title}&media=music&entity=song&limit=200
    └─ Spotify: https://api.spotify.com/v1/search?q={artist+title}&type=track

GetMusicServiceResults(request, service)
    └─ HTTP GET → dynamic JSON

service.ParseSearchResults(results, getResult, excludeTracks)
    └─ returns List<ServiceTrack>
```

After returning, `ComputeTrackPurchaseInfo` decorates each `ServiceTrack` with `AlbumLink`, `SongLink`, and `PurchaseInfo` strings.

---

## Track Lookup

### `GetMusicServiceTrack(id, service)`

1. Strips any `[…]` suffix from the ID.
2. Checks `s_trackCache`.
3. Calls `service.BuildTrackRequest(id)`:
   - iTunes: `https://itunes.apple.com/lookup?id={id}&entity=song`
   - Spotify: `https://api.spotify.com/v1/tracks/{id}`
4. Calls `GetMusicServiceResults`, then `service.ParseTrackResults`.
5. Stores result (including null) in cache.

Amazon is explicitly skipped — `GetMusicServiceTrack` returns null immediately for `ServiceType.Amazon`.

---

## Spotify Audio Features (Echo)

### `GetEchoData(dms, song)`

1. Gets all Spotify track IDs from `song.GetPurchaseIds(spotify)`.
2. For each ID, calls `LookupEchoTrack(id, service)` until one succeeds.
3. If no track found: writes `Danceability = NaN` as a sentinel and returns.
4. If found: applies to a `Song.Create` edit copy:
   - `Tempo` ← `track.BeatsPerMinute`
   - `Danceability`, `Energy`, `Valence` ← respective fields
   - Meter tag ← `track.Meter` appended to the batch-s user's tag set as `"4/4:Tempo"` etc.
5. Commits via `dms.SongIndex.EditSong` attributed to the `batch-s` pseudo-user.
6. On success, calls `ValidateAndCorrectTempo`.

### `LookupEchoTrack(id, service)`

```
GET https://api.spotify.com/v1/audio-features/{id}
    └─ EchoTrack.BuildEchoTrack(results)
           └─ maps time_signature, tempo, danceability, energy, valence
```

### `FillEchoTracks(playlist)` (playlist import path)

Batches 10 track IDs per request:

```
GET https://api.spotify.com/v1/audio-features?ids={id1},{id2},...
    └─ results.audio_features[j] → EchoTrack.BuildEchoTrack → track.AudioData
```

### `ValidateAndCorrectTempo(dms, song)`

Only runs when the song has exactly one dance rating. Uses `dance.ValidateTempo(bpm, meter)` against WDC/NDCA tempo rules. If correction is needed, commits a tempo edit under the `tempo-bot` pseudo-user. If the meter looks wrong, adds `check-accuracy:Tempo` tag for manual review. See [tempo-validation-rules.md](tempo-validation-rules.md) for the per-dance rules.

---

## ISRC Enrichment

### `GetISRCData(dms, song)`

1. Iterates all Spotify track IDs on `song.GetPurchaseIds(spotify)`.
2. For each, calls `GetMusicServiceTrack(id, spotify)` to retrieve `track.ISRC` from the Spotify tracks endpoint (`GET /v1/tracks/{id}`).
3. Maintains a `seenISRCs` set (pre-seeded from any ISRCs already on the song) to deduplicate across re-releases that share the same recording.
4. Finds the `AlbumDetails` in the edit copy whose Spotify track ID matches and calls `album.AddPurchaseId(PurchaseType.Song, ServiceType.ISRC, isrc)`.
5. Commits via `dms.SongIndex.EditSong` attributed to `batch-s` (the Spotify pseudo-user, since the data originates from Spotify).

`GetISRCData` is called by `UpdateAudioData` when a song has Spotify IDs but no ISRC yet. The `BatchISRC` admin batch action calls it in bulk via `BatchProcess`/`StreamAll`, skipping songs that already have any ISRC (`song.GetPurchaseId(ServiceType.ISRC) != null`).

---

## Sample Audio URL

### `GetSampleData(dms, song)`

1. Tries each Spotify track ID via `GetMusicServiceTrack(id, spotify)` → `track.SampleUrl`.
2. If Spotify has no preview URL, tries each iTunes track ID the same way.
3. Writes `song.Sample = sampleUrl ?? "."` (`.` is the sentinel meaning "tried, none found").
4. Committed under `batch-s` (if Spotify found it) or `batch-i` (if iTunes found it) or `batch-s` as fallback.

---

## Playlist Lookup

### `LookupPlaylist(service, url, oldTrackList?, principal?)`

1. Calls `service.BuildLookupRequest(url)` to translate a user-facing URL to an API URL.
   - Spotify album URL → `GET /v1/albums/{id}`
   - Spotify playlist URL → `GET /v1/playlists/{id}` (or `/v1/users/{user}/playlists/{id}`)
2. Parses name, description, owner from top-level response.
3. Calls `service.ParseSearchResults` for the track list (items array), filtering out `oldTrackList` IDs.
4. Paginates via `NextMusicServiceResults` → `service.GetNextRequest(last)` → Spotify's `tracks.next`.
5. Calls `ComputeTrackPurchaseInfo` to add purchase links.

`LookupPlaylistWithAudioData` extends this by calling `FillEchoTracks` for Spotify playlists.

---

## Spotify Pagination

`SpotifyService.GetNextRequest(last)` extracts `last.tracks.next` (or `last.next`). `NextMusicServiceResults` calls this and, if non-null, fetches the next page. This handles all paginated endpoints: search results, playlist tracks, user playlists.

iTunes has no pagination — it returns up to 200 results in one response.

---

## UpdateFromTracks (Writing Back to Song)

After `MatchSongAndService` returns results:

```
UpdateFromTracks(dms, sd, tracks)
    └─ Song.Create(sd, dms)          — clone of current song as edit target
    └─ foreach track in tracks:
           UpdateMusicServiceFromTrack(dms, edit, track, ref tags)
               └─ UpdateMusicService(song, service, name, album, artist,
                                     trackId, collectionId, altId, duration, trackNum)
                       └─ song.FindAlbum(album, trackNum)      — match or create AlbumDetails
                       └─ AlbumDetails.AddPurchaseId(Song, trackId)
                       └─ AlbumDetails.AddPurchaseId(Album, collectionId)
                       └─ sets song.Length if missing
           dms.NormalizeTags(genres)   — adds genre tags
    └─ dms.SongIndex.EditSong(batchUser, sd, edit, tags)
```

The edit is committed as the batch pseudo-user for the winning service (e.g., `batch-s` for Spotify).
