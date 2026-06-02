# Renewed Amazon Music Support

## Background

Amazon Music support in music4dance has degraded to a near-legacy state:

- Amazon IDs (ASINs) are stored on songs that were catalogued when the feature was active
- `AmazonService.IsSearchable = false` — Amazon is never queried during song ingestion
- `GetMusicServiceTrack` explicitly skips Amazon at the infrastructure level
- The existing affiliate product link (`/gp/product/{ASIN}`) points directly to an ASIN, which may have been delisted, re-pressed under a different ASIN, or migrated to a different storefront — **ASINs are not stable over time**
- No UI entry point exists in `ServiceMatcher` for users to input Amazon IDs
- The "Available on Amazon" filter still works but the data behind it is increasingly stale

The goal of this document is to describe options for restoring meaningful Amazon Music coverage.

---

## Option 1: Amazon Search Links (No Direct API)

Replace per-ASIN product links with Amazon digital music **search links** constructed from the song's title and artist. This is the approach used by [Copperknob](https://www.copperknob.co.uk/) and similar sites.

**Link format:**

```
https://www.amazon.com/s?i=digital-music&k={artist}+{title}
```

Or with affiliate tag:

```
https://www.amazon.com/s?i=digital-music&k={artist}+{title}&tag=msc4dnc-20
```

### What changes

- `AmazonService.BuildPurchaseLink` would construct a search URL from the song's artist and title instead of a product URL from the ASIN
- The link could be generated on demand (no stored ID needed), so every song automatically gets an Amazon link
- Existing Amazon ASINs in the database are no longer needed and can be cleaned up in a separate migration
- The `Purchase` index field would either no longer include `"Amazon"` (removing the filter capability) or be pre-populated for all songs (always-on, no filter needed)
- `AmazonPurchaseInfo` on the client would change `link` to build the search URL from title/artist (available on `SongHistory`/`PurchaseEncoded`) rather than from `songId`

### Pros

- **No API key / registration required** — Amazon's public search URL is open
- **No staleness problem** — the link searches live Amazon inventory on click
- **Near-zero backend effort** — mostly a change to `BuildPurchaseLink` and the client `AmazonPurchaseInfo`
- **Universal coverage** — every song in the catalog gets an Amazon link immediately
- **No rate limit or credential management concerns**
- **Data cleanup optional** — existing ASINs can be retained harmlessly or removed at leisure

### Cons

- **Search result quality varies** — the user lands on a results page, not a specific track; Amazon's search ranking may surface irrelevant results for generic titles
- **Affiliate attribution is weaker** — affiliate cookies for search-result visits convert at a lower rate than direct product pages
- **No "Available on Amazon" filter** — we can no longer reliably indicate that a song is sold on Amazon; the filter would need to be removed or changed to "search Amazon" (always shown)
- **Not a purchase verification** — we cannot confirm the specific recording is actually available; the search link is a best-effort redirect
- **Marketplace fragmentation** — amazon.com vs. amazon.co.uk vs. other marketplaces; a single link only targets one storefront

---

## Option 2: Amazon Product Advertising API v5

Amazon provides the [Product Advertising API 5.0](https://webservices.amazon.com/paapi5/documentation/) (PA-API 5) which supports product search and direct item lookups by ASIN.

### What changes

- Implement a new `AmazonService.ParseSearchResults` / `ParseTrackResults` pair (or a separate `AmazonApiClient`)
- Set `AmazonService.IsSearchable = true`
- Add AWS credentials to configuration (Associate ID + access key + secret key)
- Implement request signing (PA-API 5 uses AWS Signature Version 4)
- Implement search via `SearchItems` operation filtering on `Music` category or `DigitalMusic`
- Implement track lookup via `GetItems` operation by ASIN
- Add `AmazonService` to `ServiceMatcher` on the client so users can enter Amazon URLs/ASINs

### Pros

- **Exact product links** — links point to the specific digital track or album
- **Affiliate revenue maximised** — direct product links convert better
- **"Available on Amazon" filter restored** — we know definitively whether the specific recording is on Amazon
- **Track metadata available** — title, artist, album, duration, ASIN, image from the API

### Cons

- **Registration and approval required** — PA-API 5 requires an active Amazon Associates account with recent qualifying sales; access is not guaranteed and can be revoked
- **Eligibility threshold** — Amazon requires three qualifying sales within 180 days to maintain API access; low-traffic periods could result in loss of access
- **Request signing complexity** — AWS Signature Version 4 is non-trivial compared to the simple Bearer-token model used by Spotify and iTunes
- **Rate limits** — PA-API 5 has per-second and per-day rate limits (varies by Associates tier)
- **Geographic fragmentation** — each marketplace (amazon.com, amazon.co.uk) has its own API endpoint and affiliate programme; supporting multiple regions multiplies credential management
- **Digital music catalogue gaps** — not all tracks have a digital MP3 purchase option; streaming availability through Amazon Music Unlimited is not exposed by PA-API 5
- **ASIN stability** — digital track ASINs can still change if a label pulls and re-releases a recording; slightly better than historical data but not immune
- **Ongoing maintenance** — credential rotation, access monitoring, handling API changes

---

## Option 3: Amazon Music Embed / Streaming Links

Amazon Music Unlimited offers a web player and embeddable player. Some tracks have canonical streaming URLs of the form:

```
https://music.amazon.com/albums/{albumId}?trackAsin={asin}
```

These URLs are accessible to Amazon Music subscribers. Constructing them still requires knowing the ASIN, so this is essentially the same as Option 2 from a data-acquisition standpoint, but targets streaming rather than purchase.

### Pros / Cons

Similar to Option 2 but targets the streaming audience rather than purchase. PA-API 5 does not reliably expose Amazon Music streaming availability, so this would require Amazon's separate music-specific APIs (which are not publicly documented and require a partnership).

**Not recommended** as a standalone option given the API access barriers.

---

## Option 4: Combined Approach — Search Links with ID Preservation

A middle ground: switch to search links for display (Option 1) while retaining existing ASINs in the database and leaving the infrastructure in place to accept ASINs if they are supplied manually or via a future API integration. This hedges against committing to full removal of the Amazon ID model.

### How it works

- `AmazonService.BuildPurchaseLink` returns a search link when no valid ID is stored, or a direct product link when a known ASIN is present
- Songs with existing (potentially stale) ASINs gradually get their links cleaned up or verified by an admin tooling pass
- The `IsSearchable` flag remains `false` — no automatic lookup is attempted
- A new admin action could be added later to re-verify or re-populate ASINs via PA-API if Amazon Associates access is obtained

### Pros

- Immediate improvement (every song gets some Amazon link)
- Preserves optionality for future API integration
- Does not require API credentials to deploy

### Cons

- Split behaviour (some songs show direct links, some show search links) may confuse users
- Requires some frontend logic to distinguish the two cases

---

## Recommendation

**Option 1 (Amazon Search Links)** is the recommended first step.

It delivers immediate user value with minimal risk and no external dependencies. Every song in the catalog immediately gets a working Amazon link. The main tradeoff — losing the "Available on Amazon" filter — is acceptable given that the existing filter data is already stale.

If affiliate revenue from Amazon becomes meaningful, or if accurate "available on Amazon" filtering is needed, **Option 2 (PA-API 5)** can be layered on afterward. The `MusicService` base class is designed to accommodate it — `IsSearchable`, `ParseSearchResults`, and `ParseTrackResults` are the extension points — and the credential and rate-limit handling patterns already established for Spotify and iTunes provide a template.

---

## Implementation Plan for Option 1

### 1. Update `AmazonService.BuildPurchaseLink`

Change the base URL from a product page to a search URL. The method currently delegates to `base.BuildPurchaseLink` after stripping the ID prefix — instead, generate a search URL from the song's title/artist.

**Problem:** `BuildPurchaseLink` currently receives `album` and `song` ID strings, not title/artist metadata. For a search URL we need the song's title and artist.

**Resolution options:**

- Add a new virtual `BuildSearchLink(string title, string artist)` method to `MusicService` with a default implementation that returns `null`; call it from the song display pipeline when `BuildPurchaseLink` returns `null`
- Or: move the search link construction to the client (`AmazonPurchaseInfo.link`) where `SongHistory.Title` and `SongHistory.Artist` are available — this is simpler and keeps the backend model unchanged

The client-side approach is simpler. `AmazonPurchaseInfo` already overrides `link`; it can be changed to:

```typescript
public get link(): string {
  // Search link — always works even without a stored ASIN
  const q = encodeURIComponent(`${this.artist} ${this.name}`);
  return `https://www.amazon.com/s?i=digital-music&k=${q}&tag=msc4dnc-20`;
}
```

This requires `PurchaseInfo` to carry artist/title fields, or for `AmazonPurchaseInfo` to be constructed differently. Alternatively, the search link can be constructed in the component that renders purchase buttons rather than in the model.

### 2. Handle the "Available on Amazon" filter

Since every song would now have an Amazon link, the filter becomes meaningless. Options:

- Remove Amazon from the service filter UI
- Change the filter label to "Search Amazon" and always show it (hide the filter option)
- Keep the filter but only apply it if there is a stored ASIN (existing behaviour) — new songs without ASINs don't show in the filter

### 3. Stale ASIN cleanup

Existing Amazon ASINs in the database are not harmful, but they may generate dead links if the current per-ASIN link format is retained for songs that have an ID. A follow-up migration could:

- Clear all Amazon Song/Album IDs from `AlbumDetails`
- Remove the `Purchase` index entries for Amazon from the Azure Search index

This is an optional step that can be deferred.

### 4. Update `ServiceMatcher` (optional)

If we want to allow users to manually enter Amazon URLs (e.g., from a copied product page), a regex for `https://www.amazon.com/dp/{ASIN}` or `https://music.amazon.com/` could be added. This only makes sense if direct ASIN links are still used; with Option 1, it is not needed.

---

## Files to Change (Option 1 — client-side approach)

| File                                               | Change                                                           |
| -------------------------------------------------- | ---------------------------------------------------------------- |
| `m4d/ClientApp/src/models/Purchase.ts`             | `AmazonPurchaseInfo.link` — return search URL using title/artist |
| `m4d/ClientApp/src/…` (filter components)          | Adjust or hide the "Available on Amazon" filter option           |
| (Optional) `m4dModels/AlbumDetails.cs` / migration | Clear stale Amazon ASINs from the database                       |
