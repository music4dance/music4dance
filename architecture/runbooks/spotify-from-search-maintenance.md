# Maintain SpotifyFromSearch Playlists

**Type:** Runbook
**Status:** Current
**Last verified:** 2026-10-08 (service account and scheduled refreshes added)

## When to use

- The **Action Needed: Reconnect the Spotify service account** email arrived, or you're setting
  up the scheduled refreshes for the first time: see
  [Steps: connect the service account](#steps-connect-or-reconnect-the-service-account) and
  [Steps: set up the scheduled refreshes](#steps-set-up-the-scheduled-refreshes).
- You've added dances or a season is coming up: the **Bulk Create**, **Statistics** and
  **Update All** links on `/PlayList/Index?type=3`. These write to Spotify as whichever Spotify
  account your site login is connected to, so sign in to the site through the music4dance
  Spotify account.

Routine refreshes run on their own: weekly for the per-dance "Top 100" playlists and monthly for
the Holiday/Halloween ones, as the stored service account. See
[spotify-playlist-automation](../music-services/spotify-playlist-automation.md) for how.

## Steps: connect or reconnect the service account

Spotify ends the connection six months after it's made. From 14 days before then, the admins
get a daily **Action Needed** email; they also get one at once if Spotify rejects the token.

1. In a browser, sign in to **Spotify** (open.spotify.com) as the music4dance account, or be
   ready to switch to it. Spotify's dialog in step 3 offers "Not you?" either way.
2. Sign in to the **site** as a dbAdmin (any login) and open `/Admin/SpotifyServiceAccount`
   (Administration → Spotify Service Account).
3. Choose **Connect** (or **Reconnect**). In Spotify's dialog, make sure it names the
   music4dance account, then **Agree**.
4. You land back on the page with "Connected the Spotify account ...". Check that the account
   is music4dance, the scopes include `playlist-modify-public`, and **Expires** is six months
   out.
5. Optional: refresh one playlist now with the Logic App's **Run trigger**, or
   `GET /PlayList/UpdateBatch?type=SpotifyFromSearch&seasonal=true` with the token header, then
   check `/Admin/AdminStatus`.

Your own site login doesn't change. Connecting the wrong account is harmless: its writes fail
with `Failed to set playlist` (Spotify checks ownership); reconnect with the right one.

## Steps: set up the scheduled refreshes

One-time Azure setup, after the first connect. Copy the existing **UpdatePlaylists** Logic App
twice (same HTTP polling trigger, `Authorization: Token ...` header and key), and change each
copy's URL and recurrence:

| Logic App | URL | Recurrence |
| --- | --- | --- |
| RefreshTopNPlaylists | `https://www.music4dance.net/playlist/updatebatch?type=SpotifyFromSearch&seasonal=false` | Weekly. Pick a day and hour that miss the daily UpdatePlaylists run and the 6-hourly UpdateSongStats run by an hour or more. |
| RefreshSeasonalPlaylists | `https://www.music4dance.net/playlist/updatebatch?type=SpotifyFromSearch&seasonal=true` | Monthly, at an hour that doesn't collide with the others (including the weekly run). |

All four jobs share the single `AdminMonitor` slot. A run that collides gets `409` and shows as a
failed run; the next scheduled run tries again. A `424` means the service account needs
connecting. Poll `GET /playlist/updatebatchstatus` (same header) for a run's outcome.


## How the pieces fit together

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

## What Bulk Create actually does

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
   | no | yes | **Creates a new Spotify playlist but doesn't update the row.** The row still points at the old id, and the new playlist is orphaned. It's never recorded, so the next Bulk Create makes another one (issue 8 in [spotify-playlist-automation § Known issues](../music-services/spotify-playlist-automation.md#known-issues)). |

5. The new rows get `User` = your site username. TopN rows get `Search` = the dance with
   `-Fake:Tempo`, and `Count` = 50 when the dance has < 50 songs, otherwise 100. Holiday and
   Halloween rows get `CreateCustomSearchFilter(occasion, dance)` and `Count = -1` (treated as
   100). Then it saves and redirects back to the SpotifyFromSearch index.

The Spotify owner is the account linked to your site login (`GetLoginKey("Spotify")`), and the
token comes from your cookie. Both need to be the music4dance Spotify account.

## Update All / Update

These are the manual equivalents of the scheduled refreshes, and run as your Spotify login, not
the service account.

`UpdateAll?type=3` refreshes **every** active SpotifyFromSearch row: TopN, Holiday and
Halloween. Per-row `Update` (the link in the table) does just one. For each playlist it runs the
row's search, sorted by dance votes, restricted to songs with a Spotify id, and capped at
`Count`. Then it **replaces** the Spotify tracks (`PUT`). It's idempotent, so re-running after a
partial failure is safe.

The work runs in the background: the request returns immediately and redirects to
`/Admin/AdminStatus`. Refresh that page until the task completes. Each playlist reports
`UpdateSpotifyFromSearch {id}: Succeeded`, `Empty Playlist`, `Failed to set playlist` (usually
a token or ownership problem) or `Search service unavailable`.

## Steps: after adding new dances

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
   `FixupStats`) build **name-keyed dictionaries** and will throw on duplicates (issue 7 in [Known issues](../music-services/spotify-playlist-automation.md#known-issues)):
   - no two playlists on the Spotify account share a name;
   - no two SpotifyFromSearch rows share a name. Check with **Show Deleted** too, since deleted
     rows are included.

   Also look for any dance whose row exists but whose Spotify playlist is gone (the last row of
   the table above). Fix that by hand first, either by deleting the stale row or by pointing it
   at a real playlist.
5. **Create TopN** (`BulkCreate?flavor=TopN`). You land back on the index. The new rows have
   today's Created date and are empty on Spotify.
6. **Fill them.** Either click **Update** on just the new rows (fast), or run **Update All**
   (refreshes everything, which is worth doing if it's been a while). Otherwise they stay empty
   until the next scheduled refresh: up to a week for TopN, a month for seasonal.
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

## Verification

The new dance pages link to a Spotify playlist with the expected track count and cover, and
`/Admin/AdminStatus` reports `Succeeded` for each updated playlist.

## Rollback

`UpdateAll` replaces tracks idempotently, so re-run it after fixing a problem. To undo a Bulk
Create, delete the new `PlayList` rows (and, if you want, the empty Spotify playlists on the
account), then refresh the stats with `ClearSongCache`.
