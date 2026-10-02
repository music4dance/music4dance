# Spotify Service-Account Automation

**Type:** Plan
**Status:** Proposed. Step 3 (a Logic App-friendly `UpdateBatch`) shipped in #299. Steps 1, 2, 4
and 5 are not started.
**Last verified:** 2026-10-01

Let scheduled jobs write to the music4dance Spotify account's playlists without an admin browser
session. Background and the current constraint:
[spotify-playlist-automation](../music-services/spotify-playlist-automation.md).

Issue numbers below refer to [spotify-playlist-automation § Known issues](../music-services/spotify-playlist-automation.md#known-issues).

## Goal

A Logic App (or hosted service) can run `UpdateBatch?type=SpotifyFromSearch` and seasonal
creation **as the music4dance Spotify account**, with no admin browser session. The only human
step is re-connecting the account when Spotify's 6-month refresh-token lifetime runs out,
prompted by an alert.

## Step 1: Persist a service-account Spotify token

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

## Step 2: A service-account auth path

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

## Step 3: Fix `UpdateBatch` to be Logic App-friendly (mostly done, #299)

**Shipped in #299:** `UpdateBatch` starts the work and returns `200` right away (the Logic App's
polling trigger needs `200`, not `202`), returns `401` on a bad token and `409` when the slot is
busy, rejects `SpotifyFromSearch` with `400`, and adds `UpdateBatchStatus` for polling.
**Remaining:** chunk `SetPlaylistTracks` (issue 4), and accept `SpotifyFromSearch` once a
service-account token exists. The original step text follows for reference.

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

## Step 4: Expiry monitoring

- Compute `ExpiresAt = AuthorizedAt + 6 months`. Show it on the admin page, next to the
  "Connect service account" button.
- Starting about 14 days before expiry, and whenever a refresh is rejected, send an email to the
  admin through the existing email sender. Also log a warning that Application Insights can alert
  on. The same check can run cheaply inside the existing `recompute/songstats` Logic App call.

## Step 5: Schedule

Add or adjust Logic Apps once steps 1-4 are done:

| Job | Suggested schedule |
| --- | --- |
| `UpdateBatch?type=SongsFromSpotify` (import) | As today |
| `UpdateBatch?type=SpotifyFromSearch` (refresh top-N) | Weekly, offset from the import and `songstats` runs so they don't fight over `AdminMonitor` |
| Seasonal refresh | Holiday/Halloween playlists are `SpotifyFromSearch` rows too, so the weekly run covers them. **Creating** a new season's playlists (`BulkCreate`) can stay a once-a-year manual click, or become a token-gated endpoint if wanted. Either way, creation should be followed by a refresh so the new playlists aren't left empty until the next weekly run. |

## What stays manual

- Re-connecting the service account about every 6 months (alerted).
- `BulkCreate` for brand-new dances and seasons (rare; optional to automate).
- Ad hoc `Statistics` / `Restore`.

## Relationship to Apple Music

The same shape carries over to curated Apple Music playlists (see
`local/itunes-isrc-backfill-plan.md`): a stored service-account **Music User Token** (also
about 6-month lifetime), a service-account auth path, and expiry alerts. Building step 1's
storage and step 4's monitoring generically (a keyed service, not Spotify-specific names)
lets Apple reuse them. The difference is that Apple's playlist API is mostly add-only, so its
"refresh" semantics will differ.

## Open questions

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
