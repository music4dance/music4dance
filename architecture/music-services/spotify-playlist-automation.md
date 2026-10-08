# Spotify Playlist Automation

**Type:** Reference
**Status:** Current
**Last verified:** 2026-10-08 (service account shipped)
**Code:** `m4d/Controllers/PlayListController.cs`, `m4d/Utilities/AdmAuthentication.cs`,
`m4d/Utilities/ServiceAccountPrincipal.cs`, `m4d/Services/ServiceAccountTokenStore.cs`,
`m4d/Services/ServiceAccountMonitor.cs`, `m4d/Utilities/MusicServiceManager.cs`,
`m4d/APIControllers/RecomputeController.cs`

How the recurring Spotify playlist jobs run, split by what triggers them and which Spotify
identity they run as. The manual procedures, including connecting the service account and
setting up the Logic Apps, are in
[runbooks/spotify-from-search-maintenance](../runbooks/spotify-from-search-maintenance.md).

Companion docs: [playlist-management.md](playlist-management.md) covers the `PlayList` data model,
admin UI, and CRUD. [music-service-api-calls.md](music-service-api-calls.md) covers the HTTP
layer and the Spotify OAuth token lifecycle. This doc covers only **what runs when, and as
whom**.

---

## Three Spotify identities

Every Spotify call gets its `Authorization` header from
`AdmAuthentication.GetServiceAuthorization(configuration, ServiceType.Spotify, principal)`
(`m4d/Utilities/AdmAuthentication.cs`). Which token that returns depends only on `principal`:

| `principal` | Token | Can do |
| --- | --- | --- |
| `null` / anonymous | App token: `SpotAuthentication`, `grant_type=client_credentials`, static `s_spotify` | Read public catalog data and **public playlists**. **Can't write any playlist.** |
| Signed-in user whose auth cookie carries Spotify tokens | User token: `SpotUserAuthentication`, `grant_type=refresh_token`, cached per username in `s_users` | Everything in the granted scopes (`playlist-modify-public`, `ugc-image-upload`, `user-read-email`), **as that Spotify user** |
| `ServiceAccountPrincipal` (server jobs that write) | Service-account token: `SpotServiceAccountAuthentication`, `grant_type=refresh_token`, refresh token loaded from the `ServiceAccountTokens` table, cached in `s_serviceAccounts` | The same scopes, **as the music4dance Spotify account**, with no browser session |

A user's refresh token exists only inside their ASP.NET auth cookie (`SaveTokens = true` →
`props.StoreTokens(...)` in `ExternalLogin.cshtml.cs`) and in the in-memory `s_users` cache, so
a request with no cookie can't act as that user. The **service account** fills that gap for the
music4dance Spotify account: an admin connects it once, and the refresh token is stored in the
database.

### The service account

- **Connecting.** `/Admin/SpotifyServiceAccount` (dbAdmin) shows the connection and a
  Connect/Reconnect button. `ConnectSpotifyServiceAccount` challenges the normal `Spotify` OAuth
  handler with an `m4d:service-account` item on the `AuthenticationProperties`, which makes the
  handler add `show_dialog=true` (so the admin can switch to the music4dance Spotify account) and
  record the granted scopes. The handler's callback is the usual `/signin-spotify`, so no new
  redirect URI has to be registered with Spotify. It signs the result in to the Identity
  *external* cookie and redirects to `SpotifyServiceAccountCallback`, which reads the tokens,
  clears the external cookie and stores the connection. The admin's own site login doesn't
  change.
- **Storage.** `ServiceAccountTokenStore` (`IServiceAccountTokenStore`, singleton) keeps one
  `ServiceAccountTokens` row per `ServiceType`: the refresh token as an ASP.NET Data Protection
  payload (purpose `m4d.ServiceAccountToken.v1`), the Spotify account id and name, the granted
  scopes, `AuthorizedAt` (the start of the 6-month clock) and who connected it, plus
  `InvalidatedAt`/`InvalidReason` and `LastAlertSent`. `ExpiresAt` is `AuthorizedAt + 6 months`.
  If the Data Protection keys change so the token can't be decrypted, it's marked invalid.
- **Auth path.** Pass `new ServiceAccountPrincipal(ServiceType.Spotify, store)` (or
  `ServiceAccountMonitor.SpotifyPrincipal`) as the principal to any `MusicServiceManager` call.
  `SetupService` checks for that type **before** anything else. The principal's identity is
  unauthenticated, so it never touches `s_users`, and it never falls back to the app token: when
  the account isn't connected or is invalid, the call throws `SpotifyAuthExpiredException`.
- **Rotation.** If Spotify returns a new `refresh_token` on refresh, `CreateToken` swaps it into
  memory as before and calls `OnRefreshTokenRotated`, which the service-account auth overrides to
  write it back to the store. Rotation doesn't restart the expiry clock.
- **Rejection.** When Spotify rejects the refresh token, `GetServiceAuthorization` drops the
  cached service-account auth and marks the row invalid. `UpdateSpotifyFromSearch` lets
  `SpotifyAuthExpiredException` propagate, so the batch stops with a failed `AdminMonitor` status
  instead of failing every playlist one by one, and then runs the monitor check, which emails
  right away.
- **Expiry monitoring.** `ServiceAccountMonitor.CheckSpotify` runs after every `songstats`
  recompute (every 6 hours) and after a batch that hit a rejected token. It confirms Spotify still
  accepts the refresh token (only a real network call when no access token is cached), then,
  from 14 days before `ExpiresAt`, after it, or once the token is invalid, logs a warning and
  sends an **Action Needed** admin email through `ServiceHealthNotifier` (the
  `ServiceHealth:AdminNotifications` recipients). It emails at most once a day, except that a new
  rejection is reported at once.

Spotify refresh tokens expire **6 months after the original authorization**, and refreshing
doesn't reset the clock (see music-service-api-calls.md § Spotify Refresh-Token Expiration
Handling), so reconnecting the service account about twice a year is the remaining manual step.

---

## Category 1: Server-side, triggered by Azure Logic Apps

### Entry point

`GET /PlayList/UpdateBatch?type={PlayListType}&seasonal={true|false}` (`PlayListController.UpdateBatch`)

- `[AllowAnonymous]`, gated by `TokenRequirement.Authorize`: header
  `Authorization: Token {base64(key)}`, where the key is config
  `Authentication:RecomputeJob:Key`. It shares that key with `GET /api/recompute/{id}`
  (`songstats`, `subscription`).
- Accepts `type=SongsFromSpotify` (the default) and `type=SpotifyFromSearch`; any other type gets
  a `400`.
- `SpotifyFromSearch` runs as the service account. If it isn't connected, or its token was
  rejected, the response is **`424`** with `{success:false, reason:"The Spotify service account
  isn't connected"}` (or "needs to be reconnected"). 424 isn't in the Logic App's default retry
  list, so it fails once instead of retrying.
- `seasonal` applies only to `SpotifyFromSearch` (`400` otherwise). Omitted, it refreshes every
  active row. `seasonal=false` refreshes only the non-seasonal rows (the per-dance top-N lists),
  `seasonal=true` only the seasonal ones. `PlayList.IsSeasonal` is true for a name that starts
  with a non-TopN `BulkCreateFlavor` and a space ("Holiday Salsa", "Halloween Rumba"), which is
  how `BulkCreate` names them.
- Takes the single `AdminMonitor` slot (`"UpdateAllPlayLists"`). If another admin task holds
  it, the response is `409` with `{success:false, reason:"Another Admin Task is already running"}`.
- Starts `UpdateAllBase` in the background and returns **`200`**
  `{success:true, reason:"Kicked off Update Batch"}` right away. It returns `200` rather than
  `202` because the Logic App's HTTP *polling trigger* only fires a run on `200`.
- `GET /PlayList/UpdateBatchStatus` (same token) returns `{IsRunning, Succeeded, Status}` from
  `AdminMonitor`, for polling the outcome.

The Logic App definitions live in Azure, not in this repo:

| Logic App | Call | Schedule |
| --- | --- | --- |
| UpdatePlaylists | `GET https://www.music4dance.net/playlist/updatebatch` (no `type`, so `SongsFromSpotify`) | Daily |
| UpdateSongStats | `GET https://www.music4dance.net/api/recompute/songstats` | Every 6 hours |
| Refresh top-N playlists (to create) | `GET .../playlist/updatebatch?type=SpotifyFromSearch&seasonal=false` | Weekly, offset from the other two so they don't fight over `AdminMonitor` |
| Refresh seasonal playlists (to create) | `GET .../playlist/updatebatch?type=SpotifyFromSearch&seasonal=true` | Monthly, year round, offset from the weekly run |

The owner chose weekly for the top-N lists and monthly, year round, for the Holiday/Halloween
lists (2026-10-08). The seasonal lists stay public all year, so a monthly refresh keeps them
current without churning them every week. Creating the two new Logic Apps is a step in the
[runbook](../runbooks/spotify-from-search-maintenance.md#steps-set-up-the-scheduled-refreshes).
UpdateSongStats every 6 hours also means the dance-page playlist links refresh on their own (see
below) and the service account's expiry is checked four times a day.

### What each playlist type does under `UpdateBatch`

| `type` | Work | Spotify calls | Identity |
| --- | --- | --- | --- |
| `SongsFromSpotify` (default) | For each active playlist: `LoadServicePlaylist` → `LookupPlaylist` reads the tracks. New tracks (not in `SongIds`) become songs via `SongsFromTracks`, are merged with `MatchSongs(…, Merge)`, and are committed via `CommitCatalog`. `Name`/`Description` are refreshed. | Read only (`GET /playlists/{id}`, tracks, artists/genres) | App token (public playlists) |
| `SpotifyFromSearch` | For each active playlist (filtered by `seasonal`): run the saved search (`Data1`), take the Spotify ids, then `SetPlaylistTracks` | **Write**: `PUT /playlists/{id}/tracks` for the first 100, `POST` for the rest | Service account |

`SetPlaylistTracks` splits the tracks into calls of at most 100 (Spotify's per-call limit): a
replace sends the first 100 with a `PUT` and appends the rest with `POST`s. An empty replace
still sends one `PUT`, which clears the playlist. Spotify itself enforces ownership: a row whose
playlist isn't owned by the service account reports `Failed to set playlist` and the batch goes
on to the next row.

`RecomputeController` (`/api/recompute/songstats`, `/api/recompute/subscription`) is also
Logic App-driven and shares the token. It competes for the `AdminMonitor` slot. `songstats`
rebuilds `DanceStatsInstance`, which publishes each dance's `SpotifyPlaylist` id (the
`SpotifyFromSearch` playlist whose `Name` matches the dance name) to the dance pages, and then
runs the service-account expiry check.

---

## Category 2: Browser-driven admin actions

These run from the admin UI (`/PlayList`, see playlist-management.md § Vue Index Page) and pass
the signed-in admin's `User` as `principal`. Before starting, each one calls
`SpotifyAuthorization()`, which primes the `s_users` cache from the current cookie. Any playlist
they write to belongs to **whichever Spotify account the admin's cookie is connected to**, so the
admin has to be signed in to the site through the music4dance Spotify account. (The owner signs
in to the site with that Spotify account for these.)

| Job | Action | Cadence | What it does |
| --- | --- | --- | --- |
| Refresh search-driven playlists by hand | `GET /PlayList/UpdateAll?type=SpotifyFromSearch` (or per-row `Update`) | Ad hoc; the scheduled runs above cover the routine refresh | The same work as `UpdateBatch?type=SpotifyFromSearch`, as the signed-in admin. These are the per-dance "Top 100 {Dance}" playlists linked from the dance pages, plus the seasonal ones. |
| Create top-N playlists | `GET /PlayList/BulkCreate?flavor=TopN` | When new dances pass 25 songs | For each dance with ≥ 25 songs that's missing its Spotify playlist or its `PlayList` row: `CreatePlaylist` (`POST /users/{id}/playlists` + cover image `PUT`), then adds a `SpotifyFromSearch` row (dance-votes search, `Count` 100, `User` = the signed-in admin). **It doesn't fill in tracks.** Fill them with `Update`/`UpdateAll`, or wait for the next scheduled refresh. |
| Create seasonal playlists | `GET /PlayList/BulkCreate?flavor=Holiday` / `Halloween` | Yearly, before the season | The same as above for "Holiday {Dance}" / "Halloween {Dance}", using `CreateCustomSearchFilter(occasion, dance)` and `Count = -1` (100). |
| Playlist statistics | `GET /PlayList/Statistics` | Ad hoc | `MusicServiceManager.GetPlaylists`, which lists the signed-in Spotify account's playlists (id, name, track count, description, link) through `GET /v1/me/playlists`, paged. Read-only. BulkCreate uses the same call, so this page is a safe pre-flight check. |
| Restore import bookkeeping | `GET /PlayList/Restore` / `RestoreAll` | Ad hoc / repair | Re-derives `SongIds` for `SongsFromSpotify` playlists. Read-only on Spotify. |

`BulkCreate` stays manual: it's rare, and it's where the duplicate-name and orphan issues below
bite, so it's worth a human look each time.

Customer-facing writes (`SongController.CreateSpotify` export and the
`SpotifyPlaylistController` "add to playlist" widget) also need a user token, but it's the
*customer's* own token and playlists. They're working as designed and aren't part of this
automation.

---

## Known issues

1. ~~**`UpdateBatch` held the HTTP request for the whole run.**~~ Fixed in #299.
2. ~~**Silent failure for `SpotifyFromSearch` in `UpdateBatch`.**~~ Fixed in #299 (rejected with
   `400`), and since superseded: it now runs as the service account.
3. ~~**`UpdateBatch` returned 500 on a bad token, and pseudo-JSON.**~~ Fixed in #299.
4. ~~**Track cap.**~~ Fixed with the service account: `SetPlaylistTracks` sends at most 100
   tracks per call.
5. **Shared-key naming.** `Authentication:RecomputeJob:Key` guards playlist updates too.
   That's fine, but the name is misleading.
6. ~~**`Restore` and `RestoreAll` have no `[Authorize]` attribute.**~~ Fixed in PR #296. Still
   open: `Restore` and `Update` call `AdminMonitor.StartTask` before loading the playlist
   and checking `Deleted`, so a bad or deleted id leaks the admin task slot.
7. **Name-keyed `ToDictionary` throws on duplicate names.** `BulkCreate` (both the Spotify list
   and the `PlayList` rows, deleted rows included) and `DanceStatsInstance.FixupStats` key by
   name. Two same-named playlists or rows throw `ArgumentException`, so BulkCreate fails with a
   500. On the `ClearSongCache` → `BuildInstance` path, FixupStats doesn't catch it (it only
   catches `SqlException`), so the cache rebuild fails too. The startup path, loading from
   AppData, logs it and skips the rest of FixupStats.
8. **BulkCreate orphans playlists when a row survives its Spotify playlist.** If the row exists
   but the Spotify playlist doesn't, `metadata ??= CreatePlaylist(...)` makes a new Spotify
   playlist, then `if (m4dExists) continue;` skips updating the row. The row keeps the dead id,
   and each later run creates yet another orphan.
9. **Dead branch in TopN sizing.** Dances with `SongCount < 25` are skipped at the top of the
   loop, so the later `if (ds.SongCount < 25) count = 25;` never runs.
10. ~~**`GetPlaylists` calls `GET /v1/playlists`.**~~ Fixed in #333: it calls `GET /v1/me/playlists`.
11. ~~**Token responses are logged.**~~ Fixed in #338: `AdmAuthentication` no longer logs the raw
    token response, rotated refresh tokens, or the access token, so the service account's tokens
    stay out of the logs too. Tokens logged before #338 remain in App Service log history.

## Future improvements

- Apple Music curated playlists can reuse the same shape: `ServiceAccountTokens` and the store
  are keyed by `ServiceType`, and `ServiceAccountPrincipal` carries the service. Apple would need
  its own connect flow (a Music User Token, also about a 6-month lifetime), a
  `SetupServiceAccount` branch, and different "refresh" semantics, since Apple's playlist API is
  mostly add-only.
- A token-gated `BulkCreate`, if creating new seasons' playlists by hand becomes a chore.

## History

- 2026-09-25: Documented from the code; Logic App inventory taken. The daily UpdatePlaylists
  run "failed, then succeeded on retry". The first call timed out because `UpdateBatch` held the
  request for the whole import. The retry got `200 {success:false, "Another Admin Task is
  already running"}`, which the Logic App counted as success, while the real import finished
  unobserved.
- 2026-09-29 (#299): `UpdateBatch` returns immediately with real status codes, rejects
  `SpotifyFromSearch`, and gained `UpdateBatchStatus`. Interactive `UpdateAll` also runs in the
  background now.
- PR #296: `[Authorize(Roles = "dbAdmin")]` added to `Restore` / `RestoreAll`, with a test that
  every action declares authorization.
- 2026-10-01: The runbook and the automation plan moved to their own documents.
- 2026-10-08: The Spotify service account shipped (the plan is folded in here and deleted):
  `ServiceAccountTokens` table, the admin connect flow, `ServiceAccountPrincipal`, expiry
  alerts from `songstats`, `UpdateBatch?type=SpotifyFromSearch&seasonal=`, and chunked
  `SetPlaylistTracks`. Schedules chosen: weekly top-N, monthly seasonal.

## Related

- [runbooks/spotify-from-search-maintenance](../runbooks/spotify-from-search-maintenance.md)
- [playlist-management](playlist-management.md)
- [content-pages](../pages/content-pages.md): where `SpotifyFromSearch` playlists are embedded (dance details, custom searches)
- [music-service-api-calls](music-service-api-calls.md)
- [background-work-and-startup](../infrastructure/background-work-and-startup.md): `RecomputeController`, the shared token, and `AdminMonitor`
- [service-resilience](../infrastructure/service-resilience.md): `ServiceHealthNotifier` and the admin notification emails
- [payments-and-premium](../users-admin/payments-and-premium.md): what `/api/recompute/subscription` does
- [dance-domain-model](../dances/dance-domain-model.md): what the `songstats` recompute rebuilds
