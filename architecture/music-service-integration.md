# Music Service Integration Architecture

music4dance integrates with three external music services (Spotify, iTunes/Apple Music, Amazon Music) to link songs to streaming/purchase options, auto-populate metadata when songs are added, and support playlist management.

## Service Registry (`MusicService` class)

`m4dModels/MusicService.cs` is the central base class and registry for all music service integrations.

### Base Class Properties

| Property        | Description                                                   |
| --------------- | ------------------------------------------------------------- |
| `Id`            | `ServiceType` enum value (`Amazon`, `ITunes`, `Spotify`, …)   |
| `CID`           | Single character key used in serialization (`A`, `I`, `S`, …) |
| `Name`          | Display name                                                  |
| `Target`        | HTML `target` attribute for links                             |
| `Description`   | Alt text / tooltip                                            |
| `AssociateLink` | URL template for purchase/play links                          |
| `SearchRequest` | URL template for searching by artist/title                    |
| `TrackRequest`  | URL template for looking up a specific track by ID            |
| `IsSearchable`  | Whether the service is used during automatic song ingestion   |
| `ShowInProfile` | Whether the service appears in user profile displays          |

### Registered Services

| Service                    | CID | Searchable | Notes                                                |
| -------------------------- | --- | ---------- | ---------------------------------------------------- |
| Amazon                     | `A` | No         | Link-only, no search URL                             |
| iTunes                     | `I` | Yes        | REST search + track lookup                           |
| Spotify                    | `S` | Yes        | REST search, track lookup, playlists, audio features |
| EMusic                     | `E` | No         | Stub, historical                                     |
| Pandora                    | `P` | No         | Stub, historical                                     |
| AMG (American Music Group) | `M` | No         | Hidden stub                                          |

Services are registered at static-class initialization time and looked up by either `ServiceType` enum or `CID` character.

## Per-Service Implementation

### Spotify (`m4dModels/SpotifyService.cs`)

- **Links:** `https://open.spotify.com/track/{id}`
- **Search:** `https://api.spotify.com/v1/search?q={artist+title}&type=track`
- **Track lookup:** `https://api.spotify.com/v1/tracks/{id}`
- **Auth:** OAuth 2.0 via `AdmAuthentication.GetServiceAuthorization`. App credentials are in configuration (`Authentication:Spotify:ClientId/ClientSecret`). User-scoped operations (playlist creation/modification) additionally require the user's OAuth token passed as `IPrincipal`.

**Capabilities:**

- Full `ParseSearchResults` / `ParseTrackResults` — populates title, artist, album, duration, track number, album art, genres, preview URL, playability
- `BuildLookupRequest` — handles track, album, and playlist URLs
- `BuildPlayListLink` — constructs playlist URLs for a given user/playlist ID
- `GetNextRequest` — follows Spotify's paginated `tracks.next` links

**Audio features** (tempo, danceability, energy, valence, meter):

- `MusicServiceManager.LookupEchoTrack` → `GET /v1/audio-features/{id}`
- `MusicServiceManager.FillEchoTracks` → `GET /v1/audio-features?ids=…` (batched in groups of 10)

**Playlist operations** (all in `MusicServiceManager`):

| Method               | API endpoint                                                      |
| -------------------- | ----------------------------------------------------------------- |
| `GetPlaylists`       | `GET /v1/playlists`                                               |
| `GetUserPlaylists`   | `GET /v1/me/playlists`                                            |
| `LookupServiceUser`  | `GET /v1/users/{id}`                                              |
| `CreatePlaylist`     | `POST /v1/users/{id}/playlists` + `PUT /v1/playlists/{id}/images` |
| `SetPlaylistTracks`  | `PUT /v1/playlists/{id}/tracks`                                   |
| `AddTrackToPlaylist` | `POST /v1/playlists/{id}/tracks`                                  |
| `LookupPlaylist`     | `GET` on the URL from `BuildLookupRequest`                        |

---

### iTunes / Apple Music (`m4dModels/ITunesService.cs`)

- **Links:** `https://itunes.apple.com/album/id{albumId}?i={songId}&uo=4&at=11lwtf` (requires both IDs)
- **Search:** `https://itunes.apple.com/search?term={artist+title}&media=music&entity=song&limit=200`
- **Track lookup:** `https://itunes.apple.com/lookup?id={songId}&entity=song`
- **Auth:** No user-level OAuth. Rate limited by Apple; handled in `MusicServiceManager.GetMusicServiceResults` — HTTP 403 triggers a 60-second retry loop up to 5 times, then pauses the service for 15 minutes via `_pauseITunes`.

**Capabilities:**

- Full `ParseSearchResults` / `ParseTrackResults` — populates title, artist, album, track number, duration, image, genre, preview URL
- Filters results to `kind == "song"` only
- No pagination support (returns up to 200 results in a single call)
- Used as fallback source for sample audio URLs when Spotify doesn't have a preview

**Limitation:** The purchase link requires _both_ the track ID (`songId`) and the album/collection ID (`albumId`). If only one is present, no link is generated.

---

### Amazon Music (`m4dModels/AmazonService.cs`)

- **Links:** `http://www.amazon.com/gp/product/{ASIN}?…&tag=msc4dnc-20` (affiliate link)
- **Search:** None — `IsSearchable = false`, no search URL
- **Track lookup:** None — `GetMusicServiceTrack` explicitly skips Amazon (`if (service.Id != ServiceType.Amazon)`)
- **Auth:** None currently used

**ID format:**
Amazon IDs use a namespaced prefix:

- `D:ASIN` — digital track (MP3 store)
- `A:ASIN` — physical album

`NormalizeId` adds `D:` if no prefix is present. `Strip` removes the prefix before embedding in URLs or affiliate links. On the client side (`Purchase.ts`), `AmazonPurchaseInfo.cleanId` strips `D:` or `A:` before constructing the link URL.

**Current state:** Link-only. No automated lookup or enrichment. Existing ASINs in the database may be stale.

---

## HTTP Infrastructure

All service calls go through `MusicServiceManager.GetMusicServiceResults` (read) or `MusicServiceAction` (write):

- HTTP client is a shared singleton (`HttpClientHelper.Client`)
- `Authorization` header is populated by `AdmAuthentication.GetServiceAuthorization` which selects the appropriate token type per `ServiceType`
- Rate limit headers (`X-RateLimit-Remaining`, `X-RateLimit-Used`, `X-RateLimit-Limit`) are monitored; near-limit calls sleep 3 seconds, HTTP 429 sleeps 15 seconds and retries
- A simple in-process dictionary cache (`s_trackCache`) avoids re-fetching the same track ID within a process lifetime (cleared when it exceeds 10,000 entries)
- Karaoke results are filtered out from all search results before ranking

---

## Song Enrichment Pipeline

When a new song is added, `UpdateSongAndServices` is called, which iterates `MusicService.GetSearchableServices()` (currently iTunes and Spotify):

1. **`MatchSongAndService`** — searches by title/artist, falls back to cleaned strings (punctuation/parentheses stripped), filters by title/artist match, then narrows by album name or duration proximity, then clusters by dominant length
2. **`UpdateFromTracks`** — writes track/album/purchase IDs back to the song via `EditSong`
3. **`UpdateAudioData`** — fetches Spotify audio features (tempo, danceability, meter) and sample preview URL (Spotify first, iTunes fallback)
4. **`ValidateAndCorrectTempo`** (Spotify only) — validates detected BPM against dance-specific rules for single-dance songs; applies corrections or flags for manual review

`ConditionalUpdateSongAndServices` skips services already tried (tracks a "failed" marker on the song) — used for bulk ingestion to avoid re-querying.

---

## Purchase ID Storage

Purchase IDs are stored on `AlbumDetails` (one per album occurrence on a song) as a dictionary keyed by a two-character code:

| Key  | Meaning                |
| ---- | ---------------------- |
| `AS` | Amazon Song ID (ASIN)  |
| `AA` | Amazon Album ID (ASIN) |
| `IS` | iTunes Song ID         |
| `IA` | iTunes Collection ID   |
| `SS` | Spotify Track ID       |
| `SA` | Spotify Album ID       |

The `Song.GetPurchaseIds(service)` method returns all IDs for a given service across all albums. `AlbumDetails.GetPurchaseIdentifier(serviceType, purchaseType)` retrieves a single ID.

---

## Purchase Filtering

`SongFilter.Purchase` is a string of CID characters (e.g., `"S"` = Spotify only, `"AS"` = Amazon or Spotify). A leading `!` negates the filter. This is translated to an OData expression for Azure Search:

```
Purchase/any(t: t eq 'Spotify') or Purchase/any(t: t eq 'Amazon')
```

The `Purchase` index field in Azure Search is a `Collection(Edm.String)` storing service names. This is how the "Available on X" filter in the song browser works.

---

## Client-Side (`ServiceMatcher.ts`, `Purchase.ts`)

**`ServiceMatcher`** (`src/helpers/ServiceMatcher.ts`) recognizes service IDs entered by users:

| Service           | Patterns recognized                                                     |
| ----------------- | ----------------------------------------------------------------------- |
| Apple Music (`i`) | 7–10 digit number, or `https://music.apple.com/…?i={id}` URL            |
| Spotify (`s`)     | 22-character alphanumeric, or `https://open.spotify.com/track/{id}` URL |
| Amazon            | Not in ServiceMatcher (no UI entry point)                               |

`findSong(serviceString)` → `GET /api/servicetrack/{service}{id}` to look up or create the song.
`findSpotifyPlaylist(url)` → `GET /api/serviceplaylist/s{id}` to import a playlist.

**`PurchaseInfo` hierarchy** (`src/models/Purchase.ts`) — frontend classes for rendering purchase links:

- `SpotifyPurchaseInfo.link` → `https://open.spotify.com/track/{songId}`
- `ItunesPurchaseInfo.link` → `https://itunes.apple.com/album/id{albumId}?i={songId}&uo=4&at=11lwtf`
- `AmazonPurchaseInfo.link` → `https://www.amazon.com/gp/product/{cleanSong}?…&tag=msc4dnc-20` (strips `D:`/`A:` prefix)

`PurchaseEncoded` is the serialized form transferred from backend to frontend (two-char keys `aa`, `as`, `ia`, `is`, `sa`, `ss`).

---

## Spotify OAuth Configuration

Spotify OAuth is configured in `m4d/Configuration/AuthenticationBuilderExtensions.cs` via `AddSpotifyWithResilience`. Configuration keys: `Authentication:Spotify:ClientId` and `Authentication:Spotify:ClientSecret`. Successful registration marks `SpotifyOAuth` healthy in `ServiceHealthManager`.

Batch/service-account operations use pseudo-users named `batch-s` (Spotify) and `batch-i` (iTunes) for audit trails in `EditSong` calls.
