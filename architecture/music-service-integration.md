# Music Service Integration Architecture

music4dance integrates with three external music services (Spotify, iTunes/Apple Music, Amazon Music) to link songs to streaming/purchase options, auto-populate metadata when songs are added, and support playlist management.

> This is the feature-level overview: registered services, purchase filtering, and client-side rendering. For the step-by-step HTTP call sequences see [music-service-api-calls.md](music-service-api-calls.md). For the class hierarchy and on-disk data encoding see [music-service-model.md](music-service-model.md).

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

| Service                    | CID | Searchable | Notes                                                                                    |
| -------------------------- | --- | ---------- | ---------------------------------------------------------------------------------------- |
| Amazon                     | `A` | No         | Search links only; always shown per song; filter removed from UI                         |
| iTunes                     | `I` | Yes        | REST search + track lookup                                                               |
| Spotify                    | `S` | Yes        | REST search, track lookup, playlists, audio features                                     |
| EMusic                     | `E` | No         | Stub, historical                                                                         |
| Pandora                    | `P` | No         | Stub, historical                                                                         |
| AMG (American Music Group) | `M` | No         | Hidden stub                                                                              |
| ISRC                       | `R` | No         | Not a storefront — recording codes read from Spotify track metadata; hidden from profile |

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

- **Links:** `https://www.amazon.com/s?i=digital-music&k={artist}+{title}&tag=msc4dnc-20` (affiliate search link — see below)
- **Search:** None — `IsSearchable = false`
- **Track lookup:** None — `GetMusicServiceTrack` explicitly skips Amazon (`if (service.Id != ServiceType.Amazon)`)
- **Auth:** None currently used

**Link strategy:** Rather than direct ASIN product links (which go stale as labels reprice or re-release recordings), every song is linked to an Amazon digital music search for its artist + title. This gives users a live, always-working link to Amazon's current catalogue at the cost of landing on a search results page rather than a specific product. The affiliate tag `msc4dnc-20` is included in all links.

**ID format (retained, unused):**
Existing Amazon IDs remain stored in the database using a namespaced prefix:

- `D:ASIN` — digital track (MP3 store)
- `A:ASIN` — physical album

`NormalizeId` adds `D:` if no prefix is present. `Strip` removes the prefix. On the client side (`Purchase.ts`), `AmazonPurchaseInfo.cleanSong`/`cleanAlbum` strip the prefix — retained for a potential future switch to direct ASIN links without a DB migration.

**Current state:** Search-link-only. No automated lookup or enrichment. Existing ASINs are retained in the database but not used in link generation. The "Available on Amazon" filter has been removed from the search UI (see Purchase Filtering below).

---

## HTTP Infrastructure

All service calls go through `MusicServiceManager.GetMusicServiceResults` (read) or `MusicServiceAction` (write), using a shared `HttpClientHelper.Client` singleton, per-service auth via `AdmAuthentication.GetServiceAuthorization`, and rate-limit backoff/retry. Karaoke results are filtered out of all search results before ranking. See [music-service-api-calls.md § HTTP Layer](music-service-api-calls.md#http-layer) for the rate-limit table, retry/pause behavior, and the track-ID cache.

---

## Song Enrichment Pipeline

When a new song is added, `UpdateSongAndServices` iterates `MusicService.GetSearchableServices()` (iTunes and Spotify) to match the song against each service, write back track/album/purchase IDs, pull Spotify audio features (tempo, danceability, meter) and a sample preview URL, and validate/correct the detected tempo. `ConditionalUpdateSongAndServices` skips services already tried on a song — used for bulk ingestion. See [music-service-api-calls.md § Song Enrichment Pipeline](music-service-api-calls.md#song-enrichment-pipeline) for the full call sequence.

---

## Purchase ID Storage

Purchase IDs are stored on `AlbumDetails` (one per album occurrence on a song) as a dictionary keyed by a two-character code (service `CID` + purchase-type char, e.g. `SS` = Spotify Track ID). Each dictionary entry is a single string but can hold _multiple_ comma-separated IDs — `AddPurchaseId` appends rather than overwrites, since services (Spotify in particular) periodically reissue a new ID for the same recording. See [music-service-model.md § Multi-ID Accumulation](music-service-model.md#multi-id-accumulation) and [§ Purchase Dictionary Key Format](music-service-model.md#purchase-dictionary-key-format) for the full key table and the underlying `SongProperty` encoding.

`AlbumDetails.GetPurchaseIdentifiers(serviceType, purchaseType)` returns all IDs in a slot; `GetPurchaseIdentifier` returns only the first (primary) one, used for link generation. `Song.GetPurchaseIds(service)` returns all IDs for a given service across all of a song's albums.

---

## Purchase Filtering

`SongFilter.Purchase` is a string of CID characters (e.g., `"S"` = Spotify only, `"IS"` = iTunes or Spotify). A leading `!` negates the filter. This is translated to an OData expression for Azure Search:

```
Purchase/any(t: t eq 'Spotify') or Purchase/any(t: t eq 'ITunes')
```

The `Purchase` index field in Azure Search is a `Collection(Edm.String)` storing service names. This is how the "Available on X" filter in the song browser works.

**Amazon filter removed from UI:** The "Available on Amazon" checkbox has been removed from the advanced search form (`advanced-search/App.vue`). Because every song now shows an Amazon search link regardless of whether an ASIN is stored, filtering by Amazon presence is no longer meaningful. The OData mechanism still supports `'Amazon'` in the filter string for backward compatibility (existing saved searches that encoded `A` will not break), but the filter is not offered in the UI.

**Default "published" filter:** All default searches prepend `Purchase/any()` to the OData filter (via `SongIndex.AddCruftInfo` with `CruftFilter.NoCruft`). This requires a song to have at least one entry in the Azure Search `Purchase` collection — i.e., to be linked to at least one service (Amazon, iTunes, or Spotify). Because existing Amazon ASINs were retained in the database, songs that previously had ASINs still pass this filter. Songs that were never associated with any purchase ID remain excluded from default searches, preserving the pre-change behavior.

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
- `AmazonPurchaseInfo.link` → `https://www.amazon.com/s?i=digital-music&k={artist}+{title}&tag=msc4dnc-20`
  - `artist` and `songTitle` are set by `Song.getPurchaseInfos()` after construction (not serialized)
  - `cleanSong`/`cleanAlbum`/`cleanId` are retained for a potential future Option 4 direct-link switch

`PurchaseEncoded` is the serialized form transferred from backend to frontend (two-char keys `aa`, `as`, `ia`, `is`, `sa`, `ss`).

**`Song.getPurchaseInfos()`** always includes an `AmazonPurchaseInfo` entry, even for songs with no stored ASIN. For iTunes and Spotify, an entry is only included if a matching ID is present in the song's albums. This means the Amazon search link appears on every song page regardless of whether the song has ever been associated with Amazon.

---

## Spotify OAuth Configuration

Spotify OAuth is configured in `m4d/Configuration/AuthenticationBuilderExtensions.cs` via `AddSpotifyWithResilience`. Configuration keys: `Authentication:Spotify:ClientId` and `Authentication:Spotify:ClientSecret`. Successful registration marks `SpotifyOAuth` healthy in `ServiceHealthManager`.

For how the user's OAuth/refresh token is stored, resolved per-request, and renewed — including the user-facing playlist creation (`SongController.CreateSpotify`) and playlist track-add (`SpotifyPlaylistController`) entry points, and the known gap around Spotify's refresh-token expiration — see [music-service-api-calls.md § User OAuth Token Lifecycle](music-service-api-calls.md#user-oauth-token-lifecycle-spotify) and [§ Playlist Write Entry Points](music-service-api-calls.md#playlist-write-entry-points-user-facing).

Batch/service-account operations use pseudo-users named `batch-s` (Spotify) and `batch-i` (iTunes) for audit trails in `EditSong` calls.
