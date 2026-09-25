# Spotify Playlist Automation

How the recurring Spotify playlist jobs run today, split by what triggers them and which
Spotify identity they run as, plus a plan to remove the remaining browser-driven steps.

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
a year. The plan below makes that a scheduled, alerted chore instead of an every-run
requirement.

---

## Category 1: Server-side, triggered by Azure Logic Apps

### Entry point

`GET /PlayList/UpdateBatch?type={PlayListType}` (`PlayListController.UpdateBatch`)

- `[AllowAnonymous]`, gated by `TokenRequirement.Authorize`: header
  `Authorization: Token {base64(key)}`, where the key is config
  `Authentication:RecomputeJob:Key`. It shares that key with `GET /api/recompute/{id}`
  (`songstats`, `subscription`).
- Takes the single `AdminMonitor` slot (`"UpdateAllPlayLists"`). If another admin task is
  running, it returns `{success:false, …}` without doing anything.
- Calls `UpdateAllBase(type, user: null)`. **`principal` is `null`, so every Spotify call uses
  the app token.**

The Logic App definitions themselves live in Azure, not this repo. The inventory as of
2026-09-25 (the owner also has an unrelated subscription Logic App and a disabled earlier
attempt at `UpdateBatch`):

| Logic App | Call | Schedule | Observed |
| --- | --- | --- | --- |
| UpdatePlaylists | `GET https://www.music4dance.net/playlist/updatebatch` (no `type`, so `SongsFromSpotify`) | Daily | **The first call fails every time, and the retry about 2 minutes later "succeeds".** |
| UpdateSongStats | `GET https://www.music4dance.net/api/recompute/songstats` | Every 6 hours | Succeeds every time |

Nothing calls `type=SpotifyFromSearch`, so the silent-failure path (issue 2) isn't being hit.
Search-driven playlists are only refreshed by the manual runbook below.

**Why UpdatePlaylists fails, then "succeeds":** this is almost certainly issue 1. `UpdateBatch`
holds the request open until every `SongsFromSpotify` playlist has been imported. A Logic App
HTTP action gives up on a synchronous response after about 2 minutes on the Consumption plan
(App Service's own front end cuts off at about 230s), so the first call times out. The server
doesn't stop, though: the import keeps running in the background and holds the
`AdminMonitor` slot. The Logic App's default retry policy then calls again, and that retry gets
one of two answers:

- **The first run is still going:** `UpdateBatch` returns HTTP 200 with
  `{success:false, reason='Another Admin Task is already running'}`. The Logic App counts any
  200 as success, so this is a false "success". The real run finishes, unobserved, later.
- **The first run has finished:** the retry starts a *second* full import. That's mostly
  harmless, since only tracks not already in `SongIds` are processed, but it re-reads every
  playlist from Spotify.

**Confirmed 2026-09-25:** the retry's body is
`{success:false, reason='Another Admin Task is already running'}`, so it's the first case. The
green run is a false success. The import that actually runs is the one the timed-out first call
started, and nothing observes its result. Only `/Admin/AdminStatus` (last task only) and the
server logs show whether it succeeded. Step 3 of the plan below
(return 202 right away, work in the background, 409 when the slot is busy) removes both the
failure and the ambiguity. Until then, the daily import *is* happening; the red first attempt
is noise.

UpdateSongStats every 6 hours also means the dance-page playlist links refresh on their own
(see below). A manual `ClearSongCache` just makes that immediate.

### What each playlist type does under `UpdateBatch`

| `type` | Work | Spotify calls | Works with app token? |
| --- | --- | --- | --- |
| `SongsFromSpotify` (default) | For each active playlist: `LoadServicePlaylist` → `LookupPlaylist` reads the tracks. New tracks (not in `SongIds`) become songs via `SongsFromTracks`, are merged with `MatchSongs(…, Merge)`, and are committed via `CommitCatalog`. `Name`/`Description` are refreshed. | Read only (`GET /playlists/{id}`, tracks, artists/genres) | **Yes**, for public playlists |
| `SpotifyFromSearch` | For each active playlist: run the saved search (`Data1`), take the Spotify ids, then `SetPlaylistTracks` (`PUT /playlists/{id}/tracks`) | **Write** | **No.** The PUT is rejected, `MusicServiceAction` logs it and returns `null`, and the per-playlist result is `"Failed to set playlist"`. The batch still reports `success:true`. |

So the useful server-side automation today is **importing** curated Spotify playlists
(`SongsFromSpotify`) into the catalog. If a Logic App calls `UpdateBatch?type=3`
(`SpotifyFromSearch`), it silently does nothing useful. Check the Logic App list for this.

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
| Playlist statistics | `GET /PlayList/Statistics` | Ad hoc | `MusicServiceManager.GetPlaylists`, which lists the signed-in Spotify account's playlists (id, name, track count, description, link) through `GET /v1/playlists`, paged. Read-only. BulkCreate uses the same call, so this page is a safe pre-flight check (see the runbook below and issue 10). |
| Restore import bookkeeping | `GET /PlayList/Restore` / `RestoreAll` | Ad hoc / repair | Re-derives `SongIds` for `SongsFromSpotify` playlists. Read-only on Spotify, so it doesn't strictly need the user token and could move to category 1. (Issue 6: the missing `[Authorize]` was fixed in PR #296.) |

Customer-facing writes (`SongController.CreateSpotify` export and the
`SpotifyPlaylistController` "add to playlist" widget) also need a user token, but it's the
*customer's* own token and playlists. They're working as designed and aren't part of this
automation problem.

---

## Runbook: manual SpotifyFromSearch maintenance

This is how the Bulk Create / Statistics / Update All links on
`/PlayList/Index?type=3` (the SpotifyFromSearch view) are meant to be used today, written from
the code rather than from memory. It's worth running once by hand before automating anything.

### How the pieces fit together

```text
dance stats (in-memory DanceStatsInstance)
   │  dances with ≥ 25 songs
   ▼
BulkCreate?flavor=TopN|Holiday|Halloween ──► empty Spotify playlist + PlayList row (Search, Count)
                                                  │
UpdateAll?type=3  (or per-row Update) ────────────┘ runs Search, PUTs tracks into the playlist
                                                  │
ClearSongCache → FixupStats ──────────────────────┘ copies playlist id → DanceStats.SpotifyPlaylist
                                                     (the Spotify link on the dance page)
```

Three separate steps each do one thing. **Creating a playlist doesn't fill it, and filling it
doesn't link it from the dance page.**

### What Bulk Create actually does

`BulkCreate` (`PlayListController.cs`) runs synchronously in the request:

1. `SpotifyAuthorization()` primes the user token from your cookie.
2. It builds two name-keyed maps:
   - `oldS`: every playlist on the signed-in Spotify account (the same `GetPlaylists` call as
     Statistics).
   - `oldM`: every `SpotifyFromSearch` `PlayList` row with a name, **including deleted rows**.
3. It loops over `Database.DanceStats.Dances`, which is the *cached* stats instance. It skips
   group entries (no `DanceType`) and any dance with **fewer than 25 songs**. The playlist name
   is the dance name ("Cha Cha") for TopN, or "Holiday Cha Cha" / "Halloween Cha Cha".
4. For each remaining dance:

   | On Spotify (by name) | `PlayList` row (by name) | Result |
   | --- | --- | --- |
   | yes | yes (active *or deleted*) | Skipped. Safe to re-run. |
   | yes | no | Reuses the Spotify playlist and adds the row. |
   | no | no | Creates the Spotify playlist (`POST /users/{spotifyId}/playlists` + cover image), then adds the row. |
   | no | yes | **Creates a new Spotify playlist but doesn't update the row.** The row still points at the old id, and the new playlist is orphaned. It's never recorded, so the next Bulk Create makes another one (issue 8). |

5. The new rows get `User` = your site username. TopN rows get `Search` = the dance with
   `-Fake:Tempo`, and `Count` = 50 when the dance has < 50 songs, otherwise 100. Holiday and
   Halloween rows get `CreateCustomSearchFilter(occasion, dance)` and `Count = -1` (treated as
   100). Then it saves and redirects back to the SpotifyFromSearch index.

The Spotify owner is the account linked to your site login (`GetLoginKey("Spotify")`), and the
token comes from your cookie. Both need to be the music4dance Spotify account.

### Update All / Update

`UpdateAll?type=3` refreshes **every** active SpotifyFromSearch row: TopN, Holiday and
Halloween. Per-row `Update` (the link in the table) does just one. For each playlist it runs the
row's search, sorted by dance votes, restricted to songs with a Spotify id, and capped at
`Count`. Then it **replaces** the Spotify tracks (`PUT`). It's idempotent, so re-running after a
partial failure is safe.

The request is held open until the whole run finishes (issue 1), and then it redirects to
`/Admin/AdminStatus`. With many playlists the browser or App Service may time out first. If
that happens, open `/Admin/AdminStatus` directly. Each playlist reports
`UpdateSpotifyFromSearch {id}: Succeeded`, `Empty Playlist`, `Failed to set playlist` (usually
a token or ownership problem) or `Search service unavailable`.

### Procedure after adding new dances

The answer to "Create first, then Update All?" is yes, with a cache refresh on either side:

1. **Sign in** to the site with the account linked to the music4dance Spotify account. If
   you've been signed in a long time, sign out and back in to get a fresh Spotify token.
2. **Refresh the dance stats**: `/Admin/ClearSongCache` (the default `reloadFromStore=true`
   rebuilds from the search index). Bulk Create reads the cached stats, so a dance added to
   `dances.json` isn't seen until the stats know about it and its song count.
3. **Check that the new dances qualify.** Each needs ≥ 25 songs tagged with it. A dance below
   that is silently skipped. Re-running later, once it has enough songs, is safe.
4. **Pre-flight with Statistics** (`/PlayList/Statistics`). If it lists the music4dance
   playlists, the token and the list call work. Also check two things, because Bulk Create (and
   `FixupStats`) build **name-keyed dictionaries** and will throw on duplicates (issue 7):
   - no two playlists on the Spotify account share a name;
   - no two SpotifyFromSearch rows share a name. Check with **Show Deleted** too, since deleted
     rows are included.

   Also look for any dance whose row exists but whose Spotify playlist is gone (the last row of
   the table above). Fix that by hand first, either by deleting the stale row or by pointing it
   at a real playlist.
5. **Create TopN** (`BulkCreate?flavor=TopN`). You land back on the index. The new rows have
   today's Created date and are empty on Spotify.
6. **Fill them.** Either click **Update** on just the new rows (fast), or run **Update All**
   (refreshes everything, which is worth doing if it's been a while).
7. **Link them from the dance pages**: `/Admin/ClearSongCache` again. `FixupStats` runs as part
   of the stats rebuild and copies each matching row's id into `DanceStats.SpotifyPlaylist` by
   **exact dance name**. Until then, the new dance pages show no Spotify playlist. (The
   UpdateSongStats Logic App rebuilds the stats every 6 hours, so the links also appear on their
   own within 6 hours.)
8. **Verify**: open a new dance's page, follow the Spotify link, and check the track count and
   cover image.

For the seasonal playlists, do the same with `flavor=Holiday` or `flavor=Halloween` before the
season. Creating TopN for the new dances doesn't create their Holiday/Halloween siblings; each
flavor is a separate click. The 25-song gate counts *all* of the dance's songs, not its holiday
songs, so a new dance can get a seasonal playlist that's short, or empty (`Empty Playlist` on
update).

---

## Issues found while documenting

1. **`UpdateBatch` holds the HTTP request for the whole run.** `UpdateAllBase` does
   `await Task.Run(...)` instead of the fire-and-forget `_ = Task.Run(...)` used by
   `SongController.BatchProcess`. A long import can outlast the Logic App / App Service
   request timeout (about 230s on App Service). The work keeps going, but the Logic App
   records a failure. The interactive `Update`/`UpdateAll` actions have the same pattern.
2. **Silent failure for `SpotifyFromSearch` in `UpdateBatch`** (above). It reports
   `success:true` even though every playlist failed. It should fail fast, or not accept that
   type until there's a stored service-account token.
3. **`UpdateBatch` returns 500 on a bad token** (`throw new Exception("Unauthorized access.")`),
   while `RecomputeController` returns 401. It also returns hand-built pseudo-JSON
   (`{success:false, reason='…'}` isn't valid JSON).
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

---

## Plan: automating category 2

### Goal

A Logic App (or hosted service) can run `UpdateBatch?type=SpotifyFromSearch` and seasonal
creation **as the music4dance Spotify account**, with no admin browser session. The only human
step is re-connecting the account when Spotify's 6-month refresh-token lifetime runs out,
prompted by an alert.

### Step 1: Persist a service-account Spotify token

- Add an admin action, `GET /Admin/ConnectSpotifyServiceAccount`. It starts the Spotify OAuth
  flow with the same scopes (`playlist-modify-public`, `ugc-image-upload`; add
  `playlist-modify-private` if we ever want private playlists), using a callback separate from
  normal site sign-in. The admin can then connect the music4dance Spotify account without
  switching their own site login.
- On callback, store the **refresh token**, the Spotify user id, the granted scopes, and
  `AuthorizedAt` (the start of the 6-month clock).
- Storage: encrypt with ASP.NET Data Protection (already configured for cookies) and store it
  in a small table, or as a Key Vault secret. **Don't** put it in `appsettings`. Key Vault is
  simpler if the app already has a managed identity; a DB row is simpler to rotate from the
  admin UI. Recommendation: a DB row with a Data Protection payload.
- **Persist rotated tokens.** Spotify may return a new `refresh_token` on refresh.
  `AdmAuthentication.CreateToken` already swaps it into memory; the service-account path also
  has to write it back to storage.

### Step 2: A service-account auth path

- Add a `SpotServiceAccountAuthentication : SpotUserAuthentication` (or a factory) that loads
  the stored refresh token, and a way to request it that doesn't depend on `principal`. For
  example, a sentinel principal for the `music4dance` service user, or an explicit
  `AdmAuthentication.GetServiceAccountAuthorization(ServiceType.Spotify)` threaded through
  `MusicServiceManager` write methods. Prefer the explicit method: it keeps the "anonymous
  means app token" rule obvious and avoids surprising `s_users` cache behavior.
- In `UpdateAllBase`/`DoUpdate`, use the service-account auth when the playlist's owner
  (`PlayList.User`) is the configured service account and no interactive principal was
  supplied. Interactive admin runs keep working unchanged.
- On `SpotifyAuthExpiredException` from the service account: mark the stored token invalid,
  finish the batch with a clear failure, and raise an alert (step 4).

### Step 3: Fix `UpdateBatch` to be Logic App-friendly

This step doesn't depend on steps 1-2. It fixes the UpdatePlaylists Logic App's daily
fail-then-succeed pattern, so it can go first.

- Fire and forget (`_ = Task.Run(...)`). Return `202 Accepted` with valid JSON. Return `401` on
  a bad token and `409` when the `AdminMonitor` slot is busy. Logic Apps' default retry policy
  retries 408, 429 and 5xx but not 409, so a busy slot shows up as a real failure instead of a
  false success.
- Resolve the scoped services the background work needs (`Database.SearchService`, and so on)
  from a new scope, as `RecomputeController` does, instead of capturing the request's. Once the
  request returns immediately, the request-scoped context is disposed while the work is still
  running.
- Accept `type=SpotifyFromSearch` only when a valid service-account token exists. Otherwise
  return `424`/`503` with a "service account not connected" reason.
- Chunk `SetPlaylistTracks`: `PUT` the first 100 and `POST` the rest, or cap `Count` at 100.
- Optionally add `GET /PlayList/UpdateBatchStatus` (or reuse the AdminMonitor status JSON) so
  a Logic App can poll for completion rather than infer it.

### Step 4: Expiry monitoring

- Compute `ExpiresAt = AuthorizedAt + 6 months`. Show it on the admin page, next to the
  "Connect service account" button.
- Starting about 14 days before expiry, and whenever a refresh is rejected, send an email to the
  admin through the existing email sender. Also log a warning that Application Insights can alert
  on. The same check can run cheaply inside the existing `recompute/songstats` Logic App call.

### Step 5: Schedule

Add or adjust Logic Apps once steps 1-4 are done:

| Job | Suggested schedule |
| --- | --- |
| `UpdateBatch?type=SongsFromSpotify` (import) | As today |
| `UpdateBatch?type=SpotifyFromSearch` (refresh top-N) | Weekly, offset from the import and `songstats` runs so they don't fight over `AdminMonitor` |
| Seasonal refresh | Holiday/Halloween playlists are `SpotifyFromSearch` rows too, so the weekly run covers them. **Creating** a new season's playlists (`BulkCreate`) can stay a once-a-year manual click, or become a token-gated endpoint if wanted. Either way, creation should be followed by a refresh so the new playlists aren't left empty until the next weekly run. |

### What stays manual

- Re-connecting the service account about every 6 months (alerted).
- `BulkCreate` for brand-new dances and seasons (rare; optional to automate).
- Ad hoc `Statistics` / `Restore`.

### Relationship to Apple Music

The same shape carries over to curated Apple Music playlists (see
`local/itunes-isrc-backfill-plan.md`): a stored service-account **Music User Token** (also
about 6-month lifetime), a service-account auth path, and expiry alerts. Building step 1's
storage and step 4's monitoring generically (a keyed service, not Spotify-specific names)
lets Apple reuse them. The difference is that Apple's playlist API is mostly add-only, so its
"refresh" semantics will differ.

---

## Open questions for the owner

- ~~Which Logic Apps exist today?~~ Answered 2026-09-25: see the Category 1 inventory. None
  calls `SpotifyFromSearch`.
- ~~Is the UpdatePlaylists retry's "success" body `success:true` or `success:false`?~~
  Answered 2026-09-25: `success:false … already running`, a false success.
- Which site login do you use for category 2 runs? Is it signed in *through* the music4dance
  Spotify account, or linked to it?
- How often do you run `UpdateAll(SpotifyFromSearch)` by hand today, and is weekly the right
  cadence?
- Should the Holiday/Halloween playlists keep refreshing year-round, or be emptied/skipped out of
  season? Today nothing distinguishes them, so a weekly refresh would update them all year.
