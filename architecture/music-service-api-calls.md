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

**Failure path:** neither `GetMusicServiceResults` nor `MusicServiceAction` call `EnsureSuccessStatusCode`. On any status other than 200 or 429, `responseString` is never set, so the code falls into `if (responseString == null) throw new WebException(response.ReasonPhrase)`. This constructed `WebException` has no `Response` object, so the `catch (WebException we)` block's `we.Response is HttpWebResponse r` check is always false and the exception is simply rethrown. There is no branch in this class that inspects an actual Spotify *data*-API response status for 401/403 and treats it as an auth failure — a token that was valid when fetched but rejected by the data endpoint itself would still surface identically to a network error or a 500. In practice this residual gap rarely matters: a dead refresh token is now caught one layer up, in `AdmAuthentication.GetServiceAuthorization` (called to build the `Authorization` header before the request is even sent) — see [§ Spotify Refresh-Token Expiration Handling](#spotify-refresh-token-expiration-handling).

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

`GetAccessToken()` returns the cached `Token` if non-null; otherwise calls `CreateToken()`, which POSTs the refresh request and re-arms the timer. `CreateToken()` does not call `EnsureSuccessStatusCode` — a Spotify error response (e.g. `{"error":"invalid_grant", ...}`) deserializes into an `AccessToken` with every field default/null. For a `SpotUserAuthentication` (user refresh-token) instance, a missing `access_token` is treated as fatal: `CreateToken()` throws `SpotifyAuthExpiredException` rather than returning a hollow token.

> See [§ Spotify Refresh-Token Expiration Handling](#spotify-refresh-token-expiration-handling) for how that exception is caught, how the per-user auth cache is evicted, and how the two playlist-write entry points surface a reconnect prompt instead of a generic error.

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

## Spotify Refresh-Token Expiration Handling

Historically, Spotify user refresh tokens did not expire on their own (only explicit revocation invalidated them). Per Spotify's [refresh-token-expiration announcement](https://developer.spotify.com/blog/2026-06-18-refresh-token-expiration), refresh tokens now expire 6 months after the user's *original* authorization (refreshing the access token does not reset the timer) — new apps are affected immediately, existing apps from **2026-07-20**. Once that lands, the following chain would otherwise become a routine occurrence rather than an edge case:

1. A user's cookie-stored Spotify access token expires (this already happens routinely — access tokens are short-lived, ~1 hour).
2. `AdmAuthentication.TryCreate` or `GetAccessToken` attempts a refresh (`grant_type=refresh_token`). If the refresh token itself has expired/been revoked, Spotify returns an error body (e.g. `invalid_grant`).
3. Without special handling, the broken result would get cached in the static, process-lifetime `s_users` dictionary and returned unconditionally on every subsequent request for that user — surfacing only as a generic error, with no way to recover short of an app restart.

**This is now handled explicitly:**

- `AccessToken` carries the `error`/`error_description` fields from Spotify's token-error response.
- `CreateToken()` (`m4d/Utilities/AdmAuthentication.cs`) checks for a missing `access_token` on a `SpotUserAuthentication` (user-scoped, refresh-grant) instance and throws `SpotifyAuthExpiredException` (`m4d/Utilities/SpotifyAuthExpiredException.cs`) instead of returning a hollow token. App-level (client-credentials) failures are unaffected — that's a server-configuration problem, not a user-reconnect scenario.
- `AdmAuthentication.GetServiceAuthorization` and `HasAccess` both catch `SpotifyAuthExpiredException` and call `EvictUser` to remove the poisoned entry from `s_users`, so a subsequent request (in particular, after the user reconnects Spotify and gets a fresh cookie) re-runs `TryCreate` from scratch instead of returning the dead cached instance.
  - `HasAccess` (used by `CanSpotify`/`ValidateSpotifyAccess` preflight checks) swallows the exception and returns `false` — these are advisory checks, not the API call itself, and returning `false` triggers the existing "reconnect via OAuth" UI paths that already exist for "no Spotify login".
  - `GetServiceAuthorization` (used by the actual API calls in `MusicServiceManager`) rethrows after evicting, since neither `GetMusicServiceResults` nor `MusicServiceAction` wrap that call in a try/catch — the exception propagates naturally out of `CreatePlaylist`/`SetPlaylistTracks`/`AddTrackToPlaylist`/`GetUserPlaylists` to the controllers.
- `SongController.CreateSpotify` (POST) catches `SpotifyAuthExpiredException` ahead of the generic `catch (Exception)` and shows the `Info` view with a "reconnect your Spotify account" message/link (`_spotifyAuthService.GetSpotifyOAuthRedirectUrl`) instead of the generic "please report the issue" error.
- `SpotifyPlaylistController` (`GetUserPlaylists`, `AddTrackToPlaylist`) catches the same exception and returns 403 with `{ message, connectUrl, reauthRequired: true }` — a plain anonymous object, not the `AddToPlaylistResult` model, since this controller's non-success responses bypass the app's `JsonCamelCase()` helper and the default Newtonsoft contract resolver configured in `Program.cs` is PascalCase, not camelCase. (`AddToPlaylistResult.CreateFailure(...)` returned bare via `StatusCode`/`NotFound`/`BadRequest` elsewhere in `AddTrackToPlaylist` has this same PascalCase-vs-camelCase mismatch pre-existing — the client silently falls back to its default toast text for those paths. Out of scope for this fix, but worth knowing if `message` ever appears to be ignored client-side.)

Note this only covers the *user-token* refresh path (`SpotUserAuthentication`). `CanSpotify`/`HasAccess`/`ValidateSpotifyAccess` still only check that the auth cookie *contains* Spotify tokens up front — they don't proactively validate the refresh token before the real API call is made. The fix is reactive: the first write/read that actually hits a dead refresh token discovers it, evicts the cache, and reports "please reconnect" — rather than the previous behavior of a permanently poisoned per-user cache entry surfaced only as a generic error.

### Client-side handling (Vue)

`AddToPlaylistButton.vue`'s `handleError` already routed any 401/402/403 to `SpotifyRequirementsModal` (a checklist of sign-in/premium/Spotify-connection requirements). That modal's `hasSpotifyOAuth` prop is always `false` in practice (`menuContext.hasRole('canSpotify')` — `"canSpotify"` isn't a real seeded Identity role, so the checklist always renders as if Spotify were never connected), which made the existing "connect" messaging technically applicable but potentially confusing for a user who previously connected successfully. The reauth-required case is now distinguished explicitly:

- The 403 response body's `reauthRequired: true` flag (set only for `SpotifyAuthExpiredException`, absent/falsy for the plain "never connected" `NoSpotifyOAuth` case) is read in `handleError` and stored in a `spotifyReauthRequired` ref, passed to `SpotifyRequirementsModal` as `:reauth-required`.
- `SpotifyRequirementsModal` shows an `alert-warning` banner ("Your Spotify connection has expired...") and swaps the CTA button/next-step copy from "Connect Spotify Account" to "Reconnect Spotify Account" when `reauthRequired` is true — same underlying link (`/identity/account/manage/externallogins`), different framing.

### Testing

Spotify provides no sandbox/dev-mode way to force a refresh token to expire on demand; their own guidance is to handle `invalid_grant` defensively and test the reauthorization flow directly. `SelfCrawler/SpotifyAuthRecoveryTests.cs` does exactly that: it calls `AdmAuthentication.TryCreate`/`GetServiceAuthorization` directly against the **real** `https://accounts.spotify.com/api/token` endpoint with a deliberately-invalid refresh token (and fake `ClientId`/`ClientSecret` — no real Spotify app credentials needed, since *any* rejection with no `access_token` in the body exercises the same code path a genuine `invalid_grant` would). One test asserts `SpotifyAuthExpiredException` is thrown; a second (the regression test for the cache-poisoning bug) asserts that a subsequent call for the *same* username with fresh tokens succeeds afterward, proving the `s_users` eviction actually unblocks a reconnect. These are real network calls, so — like the rest of `SelfCrawler` — they're excluded from `Server: Test`/CI (`FullyQualifiedName!~SelfCrawler`) and only run via the `Server: Test SelfCrawler` task.

**Note:** these are calls straight into `AdmAuthentication`, independent of the web app — they don't launch the dev server and don't care which `launchSettings.json` profile (e.g. `m4d-spotify`) it would otherwise run under.

### Manually Testing the Reconnect UI

To click through the actual reconnect user journey (the `SpotifyRequirementsModal` banner, the `SongController.CreateSpotify` "Info" view, the API 403s) against a real signed-in, Spotify-connected account, without waiting for a real token to expire: `AdminController.ExpireMySpotifyConnection` (`dbAdmin`-only; linked from the Admin Diagnostics page's Status section) corrupts the current user's Spotify tokens in both places they live — the in-memory `AdmAuthentication.s_users` cache (via `AdmAuthentication.Clear()`) and the auth cookie itself (re-signs in with a garbage `refresh_token` and an already-past `expires_at` via `HttpContext.SignInAsync`). The next real Spotify action (opening "Add to Playlist", visiting `/song/createspotify`) then fails against Spotify's real token endpoint exactly as it will once a genuine refresh token expires — this exercises the actual production code path, not a mock. Reconnecting Spotify afterward (the normal "Reconnect Spotify Account" link) issues a fresh cookie and clears the corruption, so the same click-through also verifies recovery.

Gated on `IWebHostEnvironment.IsProduction()` (returns 404 there) rather than `IsDevelopment()`, so it also works on the Staging/`m4d-test` cloud instance — allowed everywhere except true production. This is safe to allow broadly: it only ever touches the calling admin's own auth cookie, and `AdmAuthentication.Clear()` clears the process-wide cache for *all* users, but that's harmless for everyone else — it just forces their next request to rebuild their own cached auth from their own still-valid cookie, it doesn't corrupt or reveal anything about their session.

Since Spotify's local OAuth redirect requires `http://127.0.0.1:<port>` rather than `https://localhost:<port>` (Spotify's registered redirect URI only accepts the loopback IP literal over http for local dev, not the `localhost` hostname), driving this manually needs the app running under the `m4d-spotify` launch profile (`applicationUrl: http://127.0.0.1:5000`, `DISABLE_HTTPS_REDIRECT=true`).

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
