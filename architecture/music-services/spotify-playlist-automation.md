# Spotify Playlist Automation

**Type:** Reference
**Status:** Current
**Last verified:** 2026-10-01 (after #299)
**Code:** `m4d/Controllers/PlayListController.cs`, `m4d/Utilities/AdmAuthentication.cs`,
`m4d/Utilities/MusicServiceManager.cs`, `m4d/APIControllers/RecomputeController.cs`

How the recurring Spotify playlist jobs run today, split by what triggers them and which
Spotify identity they run as. The manual procedure is
[runbooks/spotify-from-search-maintenance](../runbooks/spotify-from-search-maintenance.md). The
plan to remove the browser-driven steps is
[plans/spotify-service-account-automation](../plans/spotify-service-account-automation.md).

Companion docs: [playlist-management.md](playlist-management.md) covers the `PlayList` data model,
admin UI, and CRUD. [music-service-api-calls.md](music-service-api-calls.md) covers the HTTP
layer and the Spotify OAuth token lifecycle. This doc covers only **what runs when, and as
whom**.

---

## The constraint that splits everything in two

Every Spotify call gets its `Authorization` header from
`AdmAuthentication.GetServiceAuthorization(configuration, ServiceType.Spotify, principal)`
(`m4d/Utilities/AdmAuthentication.cs`). Which token that returns depends only on `principal`:

| `principal` | Token | Can do |
| --- | --- | --- |
| `null` / anonymous (server jobs) | App token: `SpotAuthentication`, `grant_type=client_credentials`, static `s_spotify` | Read public catalog data and **public playlists**. **Can't write any playlist.** |
| Signed-in user whose auth cookie carries Spotify tokens | User token: `SpotUserAuthentication`, `grant_type=refresh_token`, cached per username in `s_users` | Everything in the granted scopes (`playlist-modify-public`, `ugc-image-upload`, `user-read-email`), **as that Spotify user** |

The user refresh token exists **only inside the ASP.NET auth cookie** (`SaveTokens = true` →
`props.StoreTokens(...)` in `ExternalLogin.cshtml.cs`) and in the in-memory `s_users` cache
built from it. It isn't in the database, Key Vault, or `AspNetUserTokens`. A request with no
browser cookie (Logic App, hosted service) therefore can't act as the music4dance Spotify
account.

So:

- **Category 1 (server/automated)** can only do things the app token can do: **read**.
- **Category 2 (browser/admin)** is every job that **writes** to a playlist owned by the
  music4dance Spotify account. It needs an admin signed in to the site with that Spotify
  account's OAuth tokens in their cookie.

Spotify refresh tokens now **expire 6 months after the original authorization**, and
refreshing doesn't reset the clock (see music-service-api-calls.md § Spotify Refresh-Token
Expiration Handling). Any automation of category 2 still needs a human re-consent about twice
a year. The [service-account plan](../plans/spotify-service-account-automation.md) makes that a scheduled, alerted chore instead of an every-run
requirement.

---

## Category 1: Server-side, triggered by Azure Logic Apps

### Entry point

`GET /PlayList/UpdateBatch?type={PlayListType}` (`PlayListController.UpdateBatch`)

- `[AllowAnonymous]`, gated by `TokenRequirement.Authorize`: header
  `Authorization: Token {base64(key)}`, where the key is config
  `Authentication:RecomputeJob:Key`. It shares that key with `GET /api/recompute/{id}`
  (`songstats`, `subscription`).
- Accepts only `type=SongsFromSpotify` (the default). Any other type gets a `400`, because
  writing to Spotify needs a user token that a Logic App call doesn't have.
- Takes the single `AdminMonitor` slot (`"UpdateAllPlayLists"`). If another admin task holds
  it, the response is `409` with `{success:false, reason:"Another Admin Task is already running"}`.
- Starts `UpdateAllBase(type, user: null)` in the background and returns **`200`**
  `{success:true, reason:"Kicked off Update Batch"}` right away. It returns `200` rather than
  `202` because the Logic App's HTTP *polling trigger* only fires a run on `200`. **`principal`
  is `null`, so every Spotify call uses the app token.**
- `GET /PlayList/UpdateBatchStatus` (same token) returns `{IsRunning, Succeeded, Status}` from
  `AdminMonitor`, for polling the outcome.

The Logic App definitions live in Azure, not in this repo. The inventory as of 2026-09-25 (the
owner also has an unrelated subscription Logic App, and a disabled earlier attempt at
`UpdateBatch`):

| Logic App | Call | Schedule |
| --- | --- | --- |
| UpdatePlaylists | `GET https://www.music4dance.net/playlist/updatebatch` (no `type`, so `SongsFromSpotify`) | Daily |
| UpdateSongStats | `GET https://www.music4dance.net/api/recompute/songstats` | Every 6 hours |

Search-driven playlists are refreshed only by the manual runbook. UpdateSongStats every 6 hours
also means the dance-page playlist links refresh on their own (see below); a manual
`ClearSongCache` just makes that immediate.

### What each playlist type does under `UpdateBatch`

| `type` | Work | Spotify calls | Works with app token? |
| --- | --- | --- | --- |
| `SongsFromSpotify` (default) | For each active playlist: `LoadServicePlaylist` → `LookupPlaylist` reads the tracks. New tracks (not in `SongIds`) become songs via `SongsFromTracks`, are merged with `MatchSongs(…, Merge)`, and are committed via `CommitCatalog`. `Name`/`Description` are refreshed. | Read only (`GET /playlists/{id}`, tracks, artists/genres) | **Yes**, for public playlists |
| `SpotifyFromSearch` | For each active playlist: run the saved search (`Data1`), take the Spotify ids, then `SetPlaylistTracks` (`PUT /playlists/{id}/tracks`) | **Write** | **No.** `UpdateBatch` now rejects this type with `400`. Before #299 the `PUT` failed silently and the batch still reported success. |

So the useful server-side automation today is **importing** curated Spotify playlists
(`SongsFromSpotify`) into the catalog.

`RecomputeController` (`/api/recompute/songstats`, `/api/recompute/subscription`) is also
Logic App-driven and shares the token. It doesn't touch Spotify but does compete for the
`AdminMonitor` slot. Note that `songstats` rebuilds `DanceStatsInstance`, which is what
publishes each dance's `SpotifyPlaylist` id (the `SpotifyFromSearch` playlist whose `Name`
matches the dance name) to the dance pages.

---

## Category 2: Browser-driven, needs the music4dance Spotify login

These all run from the admin UI (`/PlayList`, see playlist-management.md § Vue Index Page) and
pass the signed-in admin's `User` as `principal`. Before starting, each one calls
`SpotifyAuthorization()`, which primes the `s_users` cache from the current cookie. Any
playlist they write to belongs to **whichever Spotify account the admin's cookie is connected
to**, so the admin has to be signed in to the site with the music4dance Spotify account.

| Job | Action | Cadence (owner to confirm) | What it does |
| --- | --- | --- | --- |
| Refresh search-driven playlists | `GET /PlayList/UpdateAll?type=SpotifyFromSearch` (or per-row `Update`) | Periodic | For each `SpotifyFromSearch` playlist: search with the sort forced to dance votes, `Purchase=S`, top `Count` (default 100). Then **replaces** the Spotify playlist's tracks with `PUT /playlists/{id}/tracks` and sets `Updated`. These are the per-dance "Top 100 {Dance}" playlists linked from the dance pages. |
| Create top-N playlists | `GET /PlayList/BulkCreate?flavor=TopN` | When new dances pass 25 songs | For each dance with ≥ 25 songs that's missing its Spotify playlist or its `PlayList` row: `CreatePlaylist` (`POST /users/{id}/playlists` + cover image `PUT`), then adds a `SpotifyFromSearch` row (dance-votes search, `Count` 100, `User` = the signed-in admin). **It doesn't fill in tracks.** They arrive on the next `UpdateAll(SpotifyFromSearch)`. |
| Create seasonal playlists | `GET /PlayList/BulkCreate?flavor=Holiday` / `Halloween` | Yearly, before the season | The same as above for "Holiday {Dance}" / "Halloween {Dance}", using `CreateCustomSearchFilter(occasion, dance)` and `Count = -1` (100). Tracks also come from the next refresh. |
| Playlist statistics | `GET /PlayList/Statistics` | Ad hoc | `MusicServiceManager.GetPlaylists`, which lists the signed-in Spotify account's playlists (id, name, track count, description, link) through `GET /v1/playlists`, paged. Read-only. BulkCreate uses the same call, so this page is a safe pre-flight check (see the [runbook](../runbooks/spotify-from-search-maintenance.md) and issue 10 below). |
| Restore import bookkeeping | `GET /PlayList/Restore` / `RestoreAll` | Ad hoc / repair | Re-derives `SongIds` for `SongsFromSpotify` playlists. Read-only on Spotify, so it doesn't strictly need the user token and could move to category 1. (Issue 6: the missing `[Authorize]` was fixed in PR #296.) |

Customer-facing writes (`SongController.CreateSpotify` export and the
`SpotifyPlaylistController` "add to playlist" widget) also need a user token, but it's the
*customer's* own token and playlists. They're working as designed and aren't part of this
automation problem.

---

## Known issues

1. ~~**`UpdateBatch` held the HTTP request for the whole run.**~~ Fixed in #299. `UpdateAllBase`
   now runs fire-and-forget for `UpdateBatch` and the interactive `UpdateAll`.
2. ~~**Silent failure for `SpotifyFromSearch` in `UpdateBatch`.**~~ Fixed in #299: it's rejected
   with `400`.
3. ~~**`UpdateBatch` returned 500 on a bad token, and pseudo-JSON.**~~ Fixed in #299: it returns
   `401` / `409` / `200` with real JSON.
4. **Track cap.** `SetPlaylistTracks` sends every id in a single `PUT`, and Spotify caps that
   call at 100 URIs. A `SpotifyFromSearch` playlist with `Count > 100` would fail. Nothing
   uses that today, but nothing prevents it either.
5. **Shared-key naming.** `Authentication:RecomputeJob:Key` guards playlist updates too.
   That's fine, but the name is misleading.
6. ~~**`Restore` and `RestoreAll` have no `[Authorize]` attribute.**~~ Fixed in PR #296, which
   adds `[Authorize(Roles = "dbAdmin")]` and a test that every action declares authorization.
   Still open: `Restore` and `Update` call `AdminMonitor.StartTask` before loading the playlist
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
10. **`GetPlaylists` calls `GET /v1/playlists`.** Spotify documents `GET /v1/me/playlists` (which
    `GetUserPlaylists` already uses) for the current user's playlists, not a bare
    `/v1/playlists`. It's been this way since at least January 2025 and hasn't been verified
    against the live API here. If Statistics errors or comes back empty, this is the first
    suspect. A failed call throws before BulkCreate creates anything, so the failure is safe.
    But an empty *successful* result would make BulkCreate think no Spotify playlists exist,
    and it would hit the issue 8 path for every dance, so check Statistics first.

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

## Related

- [runbooks/spotify-from-search-maintenance](../runbooks/spotify-from-search-maintenance.md)
- [plans/spotify-service-account-automation](../plans/spotify-service-account-automation.md)
- [playlist-management](playlist-management.md)
- [music-service-api-calls](music-service-api-calls.md)
- [dance-domain-model](../dances/dance-domain-model.md): what the `songstats` recompute rebuilds
