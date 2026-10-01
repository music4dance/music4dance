# Maintain SpotifyFromSearch Playlists

**Type:** Runbook
**Status:** Current
**Last verified:** 2026-10-01 (against `PlayListController` after #299)

## When to use

You've added dances, a season is coming up, or the per-dance "Top 100" Spotify playlists need
refreshing. These are the **Bulk Create**, **Statistics** and **Update All** links on
`/PlayList/Index?type=3`. They write to Spotify as the music4dance Spotify account, so they can't
run from a Logic App yet. See [spotify-playlist-automation](../music-services/spotify-playlist-automation.md)
for why, and [plans/spotify-service-account-automation](../plans/spotify-service-account-automation.md)
for the fix.


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

## Verification

The new dance pages link to a Spotify playlist with the expected track count and cover, and
`/Admin/AdminStatus` reports `Succeeded` for each updated playlist.

## Rollback

`UpdateAll` replaces tracks idempotently, so re-run it after fixing a problem. To undo a Bulk
Create, delete the new `PlayList` rows (and, if you want, the empty Spotify playlists on the
account), then refresh the stats with `ClearSongCache`.
