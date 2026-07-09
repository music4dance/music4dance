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

**Failure path:** neither `GetMusicServiceResults` nor `MusicServiceAction` call `EnsureSuccessStatusCode`. On any status other than 200 or 429, `responseString` is never set, so the code falls into `if (responseString == null) throw new WebException(response.ReasonPhrase)`. This constructed `WebException` has no `Response` object, so the `catch (WebException we)` block's `we.Response is HttpWebResponse r` check is always false and the exception is simply rethrown. There is no branch anywhere in this class that inspects the status code for 401/403 and treats it as an auth failure — a bad/expired token surfaces identically to a network error or a 500 from Spotify.

### In-Process Track Cache

```csharp
private static readonly Dictionary<string, ServiceTrack> s_trackCache = [];
```

Key: `"{CID}:{trackId}"` (e.g., `"S:3X2p7fCVH4g5ITBGH8pEtZ"`). Cleared when count exceeds 10,000.

---

## User OAuth Token Lifecycle (Spotify)

`m4d/Utilities/AdmAuthentication.cs` resolves the `Authorization` header for every call. It distinguishes two token types:

- **App-level (client-credentials)**: `SpotAuthentication` — `grant_type=client_credentials`. Used for anonymous search/track-lookup. A single static instance (`s_spotify`), lazily created.
- **User-level (authorization-code)**: `SpotUserAuthentication : SpotAuthentication` — overrides `RequestBody` to `grant_type=refresh_token` and appends `&refresh_token={RefreshToken}`. Required for all playlist writes and any read scoped to the user's library.

### Where the refresh token comes from

1. `AuthenticationBuilderExtensions.AddSpotifyWithResilience` (`m4d/Configuration/AuthenticationBuilderExtensions.cs`) configures the ASP.NET `AddSpotify` OAuth handler with `SaveTokens = true` and scopes `user-read-email`, `playlist-modify-public`, `ugc-image-upload`.
2. On the OAuth callback, `ExternalLoginModel.OnGetCallbackAsync` / `OnPostConfirmationAsync` (`m4d/Areas/Identity/Pages/Account/ExternalLogin.cshtml.cs`) build a fresh `AuthenticationProperties`, call `props.StoreTokens(info.AuthenticationTokens)` (copies `access_token`, `refresh_token`, `expires_at`), set `IsPersistent = true`, and sign the user in via `_signInManager.SignInAsync(user, props, ...)`.
3. This means `access_token`/`refresh_token`/`expires_at` live **inside the ASP.NET Core authentication cookie**, not in the `AspNetUserTokens` Identity table and not in the database at all. They persist as long as the cookie does (subject to cookie auth's configured expiration).

### Per-request resolution (`AdmAuthentication.SetupService`)

```
SetupService(configuration, serviceType, principal, authResult)
    └─ if principal is authenticated:
           if s_users[userName] exists → return it immediately (no re-validation)
           else if authResult.Properties present → TryCreate(...)
                 if TryCreate succeeds → cache in s_users[userName], return it
    └─ else (or if TryCreate returned null) → fall back to the app-level client-credentials auth (s_spotify)
```

`s_users` is a `static Dictionary<string, AdmAuthentication>` — process-lifetime, per-username, **never expires and is never re-validated once populated**. The only way to clear it is `AdmAuthentication.Clear()`, called from the admin-only `ApplicationUsersController.ClearCache()` action, or an app restart.

### `TryCreate(configuration, serviceType, authResult)`

1. Reads `access_token`, `expires_at`, `refresh_token` out of `authResult.Properties` (i.e., the current request's auth cookie).
2. If there's no `refresh_token`, returns `null` (caller falls back to app-level auth — which cannot do playlist writes).
3. Constructs a `SpotUserAuthentication` seeded with `RefreshToken = refreshToken`.
4. If the cookie's `expires_at` is already in the past, calls `auth.GetAccessToken()` immediately (which triggers a refresh-token POST to `https://accounts.spotify.com/api/token`) — **but does not check whether that refresh succeeded**; `auth` is returned (and cached into `s_users`) either way.
5. Otherwise seeds `Token` directly from the cookie's `access_token`/`expires_at` and arms a `Timer` (`AccessTokenRenewer`) to null out `Token` ~60s before it expires (`AccessToken.ExpiresIn`).

### Renewal (`AdmAuthentication.GetAccessToken`)

`GetAccessToken()` returns the cached `Token` if non-null; otherwise calls `CreateToken()`, which POSTs the refresh request and re-arms the timer. `CreateToken()` does not call `EnsureSuccessStatusCode` either — a Spotify error response (e.g. `{"error":"invalid_grant", ...}`) deserializes into an `AccessToken` with every field default/null. The code only logs `"Failed to create Token (null token)"` and **returns that token anyway**. `GetAccessString()` then returns `"Bearer "` (empty), which Spotify's API will reject with 401 — surfacing via the generic failure path described under `MusicServiceAction` above.

> See [§ Known Gap: Spotify Refresh-Token Expiration](#known-gap-spotify-refresh-token-expiration) for the end-to-end failure mode this produces.

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

## Playlist Write Entry Points (User-Facing)

Two user-facing paths create/modify Spotify playlists. Both require the requesting user to have a valid Spotify OAuth login (see [§ User OAuth Token Lifecycle](#user-oauth-token-lifecycle-spotify)) and both go through `MusicServiceManager`'s `principal`-scoped write methods, so both are subject to the same token-refresh behavior and failure mode.

### `SongController.CreateSpotify` (`m4d/Controllers/SongController.cs`) — legacy bulk export

- **`GET CreateSpotify`**: checks `_spotifyAuthService.CanSpotify(User, authResult)`. If the user is authenticated but not currently Spotify-authorized and *does* have a Spotify login on file (`HasSpotifyLogin`), redirects to `GetSpotifyOAuthRedirectUrl` to re-run the OAuth challenge. Otherwise renders the `SpotifyCreateInfo` form (title/description/count/filter) with `CanSpotify`/`IsPremium`/`SubscriptionLevel` flags for the view to gate on.
- **`POST CreateSpotify`**: re-checks `canSpotify`; if false, shows the "Connect your account to Spotify" info view (no redirect — this is a form submission, not a fresh page load). If true:
  1. `MusicServiceManager.CreatePlaylist(service, User, loginKey, title, description, fileProvider)` — `POST /v1/users/{key}/playlists` + `PUT /v1/playlists/{id}/images` (`loginKey` = the Spotify `ProviderKey` from `SpotifyAuthService.GetSpotifyLoginKey`, i.e. the Spotify user ID, not a token).
  2. Loops over search results in pages of 25, calling `MusicServiceManager.SetPlaylistTracks(service, User, metadata.Id, tracks, HttpMethod.Post)` — `POST /v1/playlists/{id}/tracks` — until `info.Count` tracks are added or results run out.
  3. Any exception (including the token-refresh failures described above) is caught by a blanket `catch (Exception e)` and rendered as a generic `"Unable to create a Spotify playlist at this time. Please report the issue. ({e.Message})"` error view — there is no branch that recognizes an auth/token failure and redirects the user to reconnect.

### `SpotifyPlaylistController` (`m4d/APIControllers/SpotifyPlaylistController.cs`) — Vue "add to playlist" widget

- **`GET api/spotify/playlist/user`**: `_spotifyAuthService.ValidateSpotifyAccess` (authenticated + premium + `CanSpotify`) then `MusicServiceManager.GetUserPlaylists` — `GET /v1/me/playlists` (paginated via the response's `next` URL). Validation failures map to 401/402/403 via `HandleValidationError`; any other exception is caught and returns a generic 500.
- **`POST api/spotify/playlist/add`**: same `ValidateSpotifyAccess` gate, then resolves the song by GUID, requires it to already carry a Spotify purchase ID (`song.GetPurchaseId(ServiceType.Spotify)`), and calls `MusicServiceManager.AddTrackToPlaylist(service, User, playlistId, spotifyId)` — `POST /v1/playlists/{id}/tracks`. Logs an `ActivityLog("SpotifyAddTrack", ...)` entry when `ActivityLogging` is enabled.
- Like the MVC path, `ValidateSpotifyAccess` only checks that the user *has* a Spotify login (cookie tokens present) — it never attempts an actual token refresh, so it cannot detect a dead refresh token. A failure during the real `AddTrackToPlaylist`/`GetUserPlaylists` call falls into the controller's blanket `catch (Exception ex)`, logged and returned as a generic 500 (`"Unable to add song to playlist. Please try again later."` / `"Unable to retrieve playlists. Please try again later."`) — again with no reconnect prompt.

---

## Known Gap: Spotify Refresh-Token Expiration

Historically, Spotify user refresh tokens did not expire on their own (only explicit revocation invalidated them), so the failure paths above were rarely exercised. Spotify has announced that refresh tokens will begin expiring; once that lands, the following chain becomes a routine occurrence rather than an edge case:

1. A user's cookie-stored Spotify access token expires (this already happens routinely — access tokens are short-lived, ~1 hour).
2. `AdmAuthentication.TryCreate` or `GetAccessToken` attempts a refresh (`grant_type=refresh_token`). If the refresh token itself has expired/been revoked, Spotify returns an error body (e.g. `invalid_grant`), which `CreateToken()` deserializes into an `AccessToken` with a null `access_token` — logged as a warning, but **not treated as fatal**.
3. That broken `AccessToken` gets cached as `Token` and, in the `SetupService` cache-hit path, **the resulting `SpotUserAuthentication` instance is cached in the static, process-lifetime `s_users` dictionary keyed by username** — returned unconditionally on every subsequent request for that user, without ever re-checking the current auth cookie.
4. Every real API call made with `"Bearer "` (empty) gets a 401 from Spotify, which `GetMusicServiceResults`/`MusicServiceAction` rethrow as a bare exception (no status-code branch for auth failures).
5. That exception is caught by a blanket `catch (Exception)` in `SongController.CreateSpotify` or `SpotifyPlaylistController`, producing a generic "please report the issue" / 500 message — **not** a prompt to reconnect Spotify.
6. Because step 3 poisons the per-username cache for the life of the app process, **the user cannot recover by reconnecting their Spotify account** — `SetupService` returns the cached broken instance before it would ever consult the fresh tokens from a new OAuth round-trip. Only an app restart or the admin-only `ApplicationUsersController.ClearCache()` action clears it.

None of `CanSpotify` / `HasAccess` / `ValidateSpotifyAccess` actually validate the refresh token — they only check that the user's auth cookie *contains* Spotify tokens (`service is SpotUserAuthentication`). So a user who connected Spotify long ago will pass every pre-flight check and only discover a problem when actually creating a playlist or adding a track, with a message that gives no indication reconnecting would help.

**What would need to change**, in rough order of impact:

- Detect a failed refresh (`invalid_grant` / empty `access_token` in `CreateToken()`) as an explicit, distinguishable failure instead of returning a hollow `AccessToken`.
- On that failure, evict the entry from `s_users` (instead of caching it) so a subsequent reconnect can actually take effect.
- Surface a distinct exception/result type through `GetMusicServiceResults`/`MusicServiceAction` for 401 responses, so callers can tell "your Spotify connection expired, please reconnect" apart from a generic server error.
- Have `SongController.CreateSpotify` and `SpotifyPlaylistController` catch that specific case and redirect/respond with the existing reconnect flow (`GetSpotifyOAuthRedirectUrl`) rather than the generic error view/500.

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
