# Apple Music API Migration (and iTunes ISRC Backfill)

**Type:** Plan
**Status:** Proposed. An Apple Developer Program membership is available (confirmed 2026-11-03), so the Apple Music API route (option A) is unblocked.
**Last verified:** 2026-11-03
**Code:** `m4dModels/ITunesService.cs`, `m4d/Utilities/MusicServiceManager.cs` (`GetISRCData`, the iTunes 403 pause near the `iTunesCalls` counter), `m4d/Utilities/AdmAuthentication.cs`, `m4d/Controllers/SongController.cs` (`BatchISRC`), `m4d/ClientApp/src/models/Purchase.ts`, `m4d/ClientApp/src/components/AdminFooter.vue`

**Context:** This started (2026-09-23) as a plan to backfill ISRCs for iTunes-only songs. It
turned out that the API we use for iTunes can't supply ISRCs, which led to a broader proposal: move
the whole iTunes surface from the legacy iTunes Search/Lookup API to the Apple Music API. A
follow-up (2026-09-25) covers curated playlists, customer export, link format and referral
commissions.

## Goal

Of the 33,101 songs with an iTunes purchase id, 851 have no ISRC (`R`) (counts as of
2026-11-04). The Spotify backfill
(`BatchISRC` → `MusicServiceManager.GetISRCData`) doesn't reach them because it only reads
ISRCs from Spotify track metadata. We want ISRCs on these songs so that ISRC-based identity
(`FindSongByISRC`, the playlist-viewer ISRC fallback, `R:{isrc}` in `ServiceIds`) works for them.

Candidate set: the 851 songs in purchase filter `INR` (on iTunes, no ISRC, may be on Spotify).
`INSR` (also not on Spotify) is a subset of it. The distinction mattered only for sources that
match by search. The Apple Music API looks up by the iTunes id, so stage 0 can take all of `INR`
in one pass.

## The key finding: the API we use for iTunes has no ISRCs

We call the public **iTunes Search/Lookup API** (`itunes.apple.com/lookup?id=…`, see
`m4dModels/ITunesService.cs`). According to Apple's documentation, it:

- **doesn't return ISRC.** The result-key table has no `isrc` field.
- **can't look up by ISRC.** Lookups work only by iTunes id, UPC/EAN, AMG id, or ISBN.
- is limited to **"approximately 20 calls per minute (subject to change)"**. That's the only
  documented number. There's no per-tier limit: the Search API has no tiers or keys, so the
  limit applies per caller/IP. Our code already treats iTunes `403` as throttling. It retries
  after 60s, then pauses iTunes for 15 min and throws `AbortBatchException`
  (`MusicServiceManager.cs`, next to the `iTunesCalls` counter).

So slowing down iTunes calls won't fix this. Even at 20/min (851 songs ≈ 43 min, easy to spread
out), the responses contain no ISRCs. The ISRCs have to come from a different source.

## ISRC sources considered

### A. Apple Music API (MusicKit): exact match (chosen)

- `GET https://api.music.apple.com/v1/catalog/us/songs?ids={id1},{id2},…`
  - Apple Music catalog song ids are the same numeric ids as iTunes Store `trackId`s, so we
    can pass the iTunes ids we already store. No fuzzy matching.
  - Song `attributes.isrc` is returned. The batch endpoint accepts up to 300 ids per
    request, so **851 songs take about 3 requests**, and throttling stops mattering.
  - Reverse lookup `?filter[isrc]=…` also exists (known issue: it returns 504 when combined with
    `include=albums`).
- Needs **Apple Developer Program membership** ($99/yr, which we have) plus a MusicKit private key
  to sign an ES256 JWT developer token (valid up to 6 months). The Performance Partners
  (affiliate) account we use for the `at=11lwtf` links isn't enough on its own.
- Rate limit: Apple doesn't publish a number. The docs only say requests over the limit get
  `429 Too Many Requests` and that this "resolves itself shortly after the request rate has
  reduced." Our 429 handler (15s back-off) already covers this.
- Storefront caveat: some ids may be region-specific or delisted. Missing ids are silently
  dropped from the `data` array, so record those as "not found" and move on.

Implementation sketch:

1. Add config `AppleMusic:TeamId`, `AppleMusic:KeyId`, `AppleMusic:PrivateKey` (user secrets /
   Key Vault) and a small token provider that signs and caches the JWT. It plugs in the same way
   `AdmAuthentication.GetServiceAuthorization` supplies Spotify auth.
2. `MusicServiceManager.GetAppleISRCData(dms, IEnumerable<Song>)` works like `GetISRCData`:
   collect the iTunes track ids, call `songs?ids=` in chunks of 300, map `id → isrc`, and attach
   `AddPurchaseId(PurchaseType.Song, ServiceType.ISRC, isrc)` to the album entry that carries
   that iTunes id. Edit as a batch user (for example `batch-i|P`, as `GetSampleData` does).
3. Add a `BatchISRCApple` admin action (and a menu item next to `ISRC` in `AdminFooter.vue`).
   `BatchProcess` handles one song at a time, so the action should either buffer songs into
   groups of 300 or just use the per-song `songs/{id}` endpoint (851 calls at a gentle 1/sec is
   about 15 min).

### B. Deezer public API: not part of this plan

Deezer could supply ISRCs by fuzzy title/artist match, but its developer terms allow only
non-commercial use, and music4dance doesn't qualify even at its current revenue. It isn't a
fallback for this plan. See
[Possible future: Deezer ISRC resolver](#possible-future-deezer-isrc-resolver) for how it would
work and what it would take.

### C. Retry Spotify search for these songs

- These songs probably failed Spotify matching already (`FailedLookup` contains `S`). A fresh
  `batchUpdateService?serviceType=S` with the "skip already-tried" behavior cleared might pick
  up some that Spotify added since then. When it matches, we get the Spotify id and the ISRC
  through the existing path at no extra cost.
- Coverage is likely low, but it's zero new code. It's worth running first to shrink the set.

### D. MusicBrainz

- 1 request/sec, requires a User-Agent. Search by recording/artist returns ISRCs when the
  community has entered them, but coverage is patchy for exactly the long tail that's missing
  from Spotify. Not recommended as the primary source.

### E. "Drip-feed" iTunes over time

- This was the original idea. It doesn't work because the iTunes API returns no ISRCs (see
  above). The pacing mechanisms below are still relevant if a slower source (D, or a future B)
  ever needs to be spread out.

## Pacing, if we need it

If a source does need to be spread out, prefer the options at the top of this list:

1. **Admin batch with a count.** `BatchProcess` already takes `count`. Filter to
   `INSR`, run with `count=N` every so often, and each run picks up the songs still missing an
   ISRC, because they drop out of the filter once they have one. No new infrastructure.
   Songs that were tried and not found need a marker so they don't get retried forever. Reuse
   the `FailedLookup` pattern with a distinct code, or keep a skip list in the batch.
2. **Per-call delay inside the resolver.** For example, `Task.Delay` sized to the documented
   limit: 3s for iTunes-class limits, 100-200ms for Deezer.
3. **Hosted-service drip.** A `BackgroundService` (like `DanceStatsHostedService`) processes k
   songs per hour. This is overkill for 851 songs, and it runs in every environment unless
   gated.

## Backfill risks

- **Duplicate discovery.** Once these songs have ISRCs, some will collide with existing Spotify
  songs that carry the same ISRC. Those are real duplicates that title/artist matching missed.
  Log collisions (ISRC already on another `SongId`) to a report instead of auto-merging, and
  review them separately.
- **Multiple ISRCs per song.** A song can have several iTunes album entries (compilations,
  remasters) with different ISRCs. Attach each ISRC to the album entry that carries its iTunes
  id, and dedupe with a `seenISRCs` set as `GetISRCData` does.
- **Search index.** `R:` ids are indexed in `ServiceIds`, so edits must go through
  `SongIndex.EditSong` / `SaveSongsImmediate` as in existing batches.

## Moving the whole iTunes surface to the Apple Music API

### Is a developer account enough?

Yes, for this purpose. There are two token types:

- **Developer token.** A JWT signed with a MusicKit private key. It needs a paid Apple
  Developer Program membership, and it's all that catalog endpoints (`/v1/catalog/...`: songs,
  albums, search, and the ISRC filter) require.
- **Music User Token.** Needed only for a *user's* data (library, playlists, recommendations).
  That's the part that involves an Apple Music subscription, and none of the staged plan below
  uses it.

Setup: Certificates, Identifiers & Profiles → create a Media ID with MusicKit enabled → create a
key with MusicKit → download the `.p8` (you can download it only once) and note the Key ID and
Team ID. The membership has to stay current. If it lapses, token signing still works but Apple
rejects the tokens, so keep the old iTunes path as a fallback (see stage 1).

### Why migrate

| | iTunes Search API (today) | Apple Music API |
| --- | --- | --- |
| Rate limit | ~20/min documented, hit often, 403 + 15-min pause | Unpublished, but practically far higher; 429 back-off |
| ISRC | not returned | `attributes.isrc` returned |
| Reverse ISRC lookup | none | `songs?filter[isrc]=…` |
| Batch lookup | one id per call | many ids per call (`?ids=`) |
| Auth | none | developer token (JWT, up to 6 months) |
| Ids | iTunes `trackId` / `collectionId` | same numeric ids, so **no data migration** |

The biggest new capability is the reverse ISRC lookup. We could attach iTunes ids to
Spotify-only songs by *exact* ISRC match instead of title/artist search, which should raise
iTunes coverage across the catalog at low risk.

Costs: key storage in secrets, JWT signing/caching code, a new response shape to parse, and
rate-limit behavior that we need to measure because it isn't documented.

### Staged plan

Keep `ServiceType.ITunes` / code `I` throughout. Stored ids don't change, so this is a transport
swap, not a new service.

0. **Token plumbing plus the ISRC backfill.** Add an `AppleMusic:*` config section and a
   developer-token provider behind `AdmAuthentication.GetServiceAuthorization` for
   `ServiceType.ITunes`. Only the new code path uses it at first. Ship `BatchISRCApple`. This
   proves the key, token, and rate behavior on 851 songs with low blast radius.
1. **Id lookup.** Point `ITunesService`'s track-lookup template at
   `api.music.apple.com/v1/catalog/{storefront}/songs/{id}` (plus `?include=albums` for the
   collection id), and rewrite `InternalParseTrackResults` for the `data[].attributes` shape:
   `name`, `artistName`, `albumName`, `durationInMillis`, `trackNumber`, `genreNames`,
   `releaseDate`, `previews[0].url`, `artwork.url` (template with `{w}x{h}`), `isrc`. Populate
   `ServiceTrack.ISRC` so `UpdateAudioData` can fill ISRCs for iTunes the same way it does for
   Spotify. Gate the switch with a config flag and fall back to the legacy endpoint when no
   token is configured (dev environments, contributors without keys).
2. **Reverse ISRC enrichment.** Add a new batch that attaches iTunes ids to songs that have
   ISRCs but no `I`, via `filter[isrc]`. This is new value and doesn't change existing flows.
3. **Search.** Switch title/artist search to `/v1/catalog/{sf}/search?types=songs&term=…`.
   This is the riskiest stage, because match quality feeds `MatchSongAndService` and the
   drop-target lookup. Run old and new side by side on a sample and compare before flipping.
4. **Cleanup.** Retire the iTunes 403 pause and `iTunesCalls` special-casing (replace them with
   the generic 429 path), and fold the as-built design into
   [music-service-integration](../music-services/music-service-integration.md) and
   [music-service-api-calls](../music-services/music-service-api-calls.md). Links are optional:
   the `itunes.apple.com/album/...` links in `ITunesService` and `Purchase.ts` already redirect,
   and `attributes.url` (`music.apple.com`) accepts the same `at=` affiliate token if we want to
   switch (see [Purchase links](#purchase-links-itunes--apple-music)).

### Things to verify early (in stage 0)

- The maximum `ids=` batch size, and that every stored iTunes `trackId` resolves as a catalog
  song id in the `us` storefront.
- Observed rate limit: log 429s and throughput during the backfill.
- Whether `filter[isrc]` returns multiple songs per ISRC (it does for compilations). Stage 2
  needs a rule for choosing among them, probably preferring the album we already have.
- Storefront choice: `us` everywhere, or configurable.

## Beyond the catalog API

### Curated music4dance playlists on Apple Music

- There's no brand or curator account available to us. Apple's old curator program is
  invitation-only. The equivalent of the Spotify `music4dance` account is an **ordinary Apple
  Music subscriber account** (about $11/month) with a public profile named music4dance.
  Playlists created there can be shared, which gives each one a public `music.apple.com` link.
  Anyone can view it; only subscribers can play full tracks.
- **A developer account alone isn't enough here.** Writing playlists needs a Music User Token,
  which comes from a subscriber signing in.
- Automated upkeep, mirroring the Spotify server playlists: the music4dance account signs in
  once through MusicKit JS, and we store its Music User Token (valid about 6 months, so it needs
  a re-auth reminder). The server can then create playlists and add tracks with
  `POST /v1/me/library/playlists` and `…/{id}/tracks`.
- **Big limitation: the API is mostly add-only.** Removing tracks and deleting playlists are
  poorly supported or broken from the web API (Apple forum threads report 401s on DELETE, and
  item removal only on Apple platforms). A "refresh" therefore probably means creating a new
  playlist and re-sharing it, or doing periodic manual cleanup. It isn't the in-place replace
  we do on Spotify. Decide this before designing the sync.
- Sharing or publishing a library playlist may have to be done by hand in the Music app. The
  API doesn't obviously expose it. Verify this.

### Customer export to Apple Music (like the Spotify export)

- It's feasible. MusicKit JS handles the customer's Apple sign-in in the browser, and then we
  create a playlist in their library and add songs by catalog id. Those are our stored iTunes
  ids, so no matching step is needed.
- Limits: the customer needs an Apple Music subscription. Updating an existing export is
  add-only, so it's mostly a new playlist each time. Songs without an `I` id are skipped, which
  is a reason to do stage 2 (reverse ISRC enrichment) first.
- The developer token is exposed to the browser for MusicKit JS. That's normal for MusicKit JS,
  but restrict it with the `origin` claim.

### Purchase links: iTunes → Apple Music

- Existing `itunes.apple.com/album/id{album}?i={song}` links already redirect to
  `music.apple.com`, so nothing is broken.
- Switch to `https://music.apple.com/us/album/{albumId}?i={songId}&at=11lwtf`. It saves a
  redirect, works with the same ids, and needs no data change. Consider relabeling "Buy it on
  ITunes" to "Listen on Apple Music"; the page still offers purchase where it's available.
- Scope: `ITunesService.cs` (`AssociateLink`), `Purchase.ts` (`ItunesPurchaseInfo.link`),
  song page snapshots, and the `ITunes-logo.png` icon/alt text. This is independent of the API
  migration and can ship any time.

### Referral commissions

- The Apple Services Performance Partners program (our existing `at=11lwtf` token) pays a
  **one-time commission of 100% of the first month** for new Apple Music subscriptions within
  30 days of a click. The payout threshold is $30, paid about 60-90 days after month end.
  Confirm the current terms in the partner dashboard.
- Every new Apple Music surface (shared playlist links, export prompts, relabeled links) should
  carry `at=11lwtf`.
- Check whether free-trial sign-ups only pay out after conversion to paid.

## Recommendation and ordering

For the ISRC backfill:

1. Optionally, run a fresh Spotify pass (C) over `INSR` first. It's free, but A makes it
   unnecessary for ISRCs; its value is picking up new Spotify ids.
2. Do **A** (Apple Music API) as stage 0 of the migration. It matches on the exact id, needs
   about 3-15 minutes of calls, and needs no pacing infrastructure.
3. Record the songs A can't resolve (delisted or region-locked ids) as "not found", and size
   that leftover set. Fix it by hand if it's small. Deezer isn't an option without a
   commercial agreement (see
   [Possible future: Deezer ISRC resolver](#possible-future-deezer-isrc-resolver)).

Overall order (the Spotify playlist cleanup in
[spotify-playlist-automation](../music-services/spotify-playlist-automation.md) comes first):

1. Link switch.
2. Stage 0 (tokens plus ISRC backfill).
3. Stages 1-2.
4. Customer export.
5. Curated account playlists. This needs the subscription and a decision on add-only refresh.

## Possible future: Deezer ISRC resolver

Not implemented, and not planned. This section records the design and the licensing blocker in
case the Apple Music API leaves a large unresolved set, or Deezer becomes useful for something
else.

### How it would work

Deezer's public API needs no key or account for catalog reads.

1. **Search:** `GET https://api.deezer.com/search?q=artist:"…" track:"…"`. The advanced syntax
   also takes `album:"…"` and duration bounds (`dur_min` / `dur_max`, in seconds). Search results
   are short track records without an ISRC.
2. **Detail:** `GET https://api.deezer.com/track/{id}` returns the full record, including `isrc`,
   `duration`, album and artist.

That's 2 calls per song. Deezer's FAQ gives the limit as about 50 requests per 5 seconds per IP.
The 851-song set would take a few minutes even with conservative pacing. Deezer says it doesn't
grant rate-limit exemptions without a commercial agreement.

It would be a thin ISRC resolver, not a full `MusicService`.

### Why matching is the hard part

Deezer doesn't know our iTunes ids, so a lookup is a title/artist search, not an exact match.

- **Match on:** normalized title and artist (reuse the merge code's title normalization),
  duration within about ±2s, and album when we have one.
- **Ambiguity:** remasters, live versions, compilations and covers by the same artist all come
  back as separate hits, often with different ISRCs.
- **A wrong ISRC is worse than none:** `FindSongByISRC` would then merge-match unrelated songs.
  Safeguards:
  - a strict confidence threshold, with no ISRC written below it;
  - a `preview`/`log` pass through `BatchProcess` that gets reviewed before anything is saved;
  - a collision report (ISRC already on another `SongId`) instead of auto-merging.

### Licensing: Deezer's terms don't allow music4dance's use (checked 2026-11-03)

The [Deezer for Developers terms of use](https://developers.deezer.com/termsofuse) (undated;
the posted version is the binding one) grant free API access for "personal web pages, blog" and
"personal applications". Section IV says use is "strictly limited for a non-commercial purpose
and in a non-commercial environment", and the developer "shall not perceive, receive, generate,
benefit or create directly or indirectly, any moneys, incomes, revenues" (or data or other
consideration) from the Services or Content.

- **There's no revenue threshold.** "Commercial" isn't defined by size. The test is whether any
  money, data or other consideration is made, directly or indirectly. Having well under $1,000 a
  year in revenue doesn't make a site non-commercial under these terms.
- **music4dance has revenue streams that count:** paid premium subscriptions (Stripe),
  affiliate links (Apple `at=11lwtf`, Amazon) and donations. ISRCs pulled from Deezer would be
  stored in the catalog and used to improve matching, which would *indirectly* benefit a site that
  earns revenue. Using them only for a back-office batch doesn't change that.
- **Deezer has no paid API tier.** The developer FAQ says "there is no paid API" and points
  partners to a contact form. In a 2023 community thread, a site with ads asked whether it could
  use the API, and the community manager's only answer was to fill out the business partnership
  form. The terms also say any other use must be "reviewed and approved by DEEZER, at DEEZER's own
  discretion".
- The terms also require following Deezer's trademark guidelines and let Deezer cut off access
  without notice.

Not legal advice, but the plain reading is that music4dance needs a written agreement with
Deezer before using its API, at any revenue level.

### What it would take

1. Size the set that the Apple Music API can't resolve. If it's small, fix it by hand and stop
   here.
2. If it's large, ask Deezer through the partner contact form linked from the developer FAQ.
   Explain the use: a one-off, low-volume ISRC lookup for a small catalog site, with links back
   to Deezer if they want them. Proceed only with written approval.
3. Then build the resolver and safeguards above, behind an admin batch action.

## Open questions

- How many iTunes ids fail to resolve in the Apple Music `us` catalog? That decides whether the
  Deezer route above is ever worth pursuing.
- Curated playlists: is add-only refresh (new playlist, re-share) acceptable?

Resolved:

- An Apple Developer Program membership is available (2026-11-03), so option A is the primary
  ISRC source and the staged migration can start.
- Deezer's terms bar commercial use with no revenue threshold (2026-11-03), so Deezer is out of
  scope unless Deezer approves it in writing.
- Counts (2026-11-04): 33,101 songs have an iTunes id, and 851 of them have no ISRC (`INR`).

## Sources

- iTunes Search API docs (20 calls/min, lookup keys, result fields): https://performance-partners.apple.com/search-api
- Apple Music API: generating developer tokens / request rate limiting: https://developer.apple.com/documentation/applemusicapi/generating-developer-tokens
- Apple forum, ISRC filter + include=albums 504: https://developer.apple.com/forums/thread/750964
- Deezer developer FAQ (rate limit, no paid API, partner contact): https://support.deezer.com/hc/en-gb/articles/360011538897-Deezer-FAQs-For-Developers
- Deezer for Developers terms of use (Section IV, non-commercial use): https://developers.deezer.com/termsofuse
- Deezer community, API on an ad-supported site (2023): https://en.deezercommunity.com/features-feedback-44/can-i-use-deezer-api-for-website-that-runs-ads-78072
- Performance Partners program: https://performance-partners.apple.com/program-overview
- Performance Partners commissions and payments: https://performance-partners.apple.com/commissions-payments
- Apple forum threads on library playlist writes and deletes: https://developer.apple.com/forums/thread/805461, https://developer.apple.com/forums/thread/707759, https://developer.apple.com/forums/thread/107807

## Related

- [music-service-integration](../music-services/music-service-integration.md): service registry and per-service behavior, including iTunes
- [music-service-api-calls](../music-services/music-service-api-calls.md): `MusicServiceManager` HTTP flows and rate-limit handling
- [service-track-lookup](../songs/service-track-lookup.md): resolving a Spotify/Apple track id to a catalog song
- [spotify-playlist-automation](../music-services/spotify-playlist-automation.md): the Spotify playlist model the Apple Music playlists would mirror
