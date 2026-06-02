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

**Link format (always include the affiliate tag):**

```
https://www.amazon.com/s?i=digital-music&k={artist}+{title}&tag=msc4dnc-20
```

### What changes

- Every song gets a working Amazon link generated on demand from title and artist — no stored ID needed
- Existing Amazon ASINs are **retained** in the database and left unused for now; this keeps the door open to activating the direct-link behaviour (Option 4) later without a migration
- `AmazonPurchaseInfo.link` on the client is simplified to always return the search URL
- The `Purchase` index field no longer drives an "Available on Amazon" filter; the filter is **removed** from the UI

### Pros

- **No API key / registration required** — Amazon's public search URL is open
- **No staleness problem** — the link searches live Amazon inventory on click
- **Near-zero backend effort** — mostly a change to `BuildPurchaseLink` and the client `AmazonPurchaseInfo`
- **Universal coverage** — every song in the catalog gets an Amazon link immediately
- **No rate limit or credential management concerns**
- **Data cleanup optional** — existing ASINs can be retained harmlessly or removed at leisure

### Cons

- **Search result quality varies** — the user lands on a results page, not a specific track; Amazon's search ranking may surface irrelevant results for generic or common song titles
- **Affiliate conversion lower than direct links** — search-result page visits convert less reliably than landing on a specific product page
- **"Available on Amazon" filter is removed** — without a per-song confirmation, the filter is no longer meaningful and is removed from the UI
- **Not a purchase verification** — the search link is a best-effort redirect; the specific recording may not be available for digital purchase on Amazon
- **Marketplace fragmentation** — amazon.com vs. amazon.co.uk vs. other marketplaces; a single link only targets one storefront (see separate section below)

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

**Pure Option 1 (search links for all songs, Amazon filter removed)** is the chosen first step.

Existing ASINs are stale enough that a consistent search link is a better user experience than a mix of working and dead direct links. Every song gets a working Amazon link immediately. The affiliate tag is included in all links.

Existing Amazon ASINs are **retained in the database unused**. If direct-link behaviour is desirable in the future (Option 4), it can be activated by adding the ASIN fallback back into `AmazonPurchaseInfo.link` without any database migration.

If accurate per-song availability and maximum affiliate conversion become priorities, **Option 2 (PA-API 5)** can be layered on afterward. The `MusicService` base class is designed to accommodate it — `IsSearchable`, `ParseSearchResults`, and `ParseTrackResults` are the extension points — and the credential and rate-limit patterns from Spotify and iTunes provide a template.

---

## Geographic Marketplace Fragmentation

Amazon operates separate regional storefronts (amazon.com, amazon.co.uk, amazon.de, amazon.co.jp, amazon.ca, amazon.com.au, etc.), each with its own catalogue, pricing, and affiliate programme. A link hardcoded to amazon.com may not be optimal for users in other countries. The following approaches are available.

### A: Single storefront (default amazon.com) — simplest

Point all links at amazon.com and let Amazon handle any natural redirects. Amazon does sometimes redirect international visitors to their local storefront, but this behaviour is inconsistent and not guaranteed. Given that the primary music4dance audience is North American, this is a reasonable baseline.

**Verdict:** Acceptable starting point. Low effort, imperfect for non-US visitors.

### B: Amazon OneLink — recommended

Amazon Associates OneLink is a redirect mechanism that routes international visitors to their local Amazon storefront automatically. You keep using the same `amazon.com` links with the same affiliate tag; Amazon's JS snippet handles the redirect on the user's device.

#### How it works

1. Your US account (`msc4dnc-20`) is the "home" store; enrolled countries are configured at `https://affiliate-program.amazon.com/p/stores/globalStore`
2. When a visitor from the UK clicks an `amazon.com` link with your affiliate tag, Amazon routes them to the equivalent `amazon.co.uk` URL
3. Commissions are credited to `msc4dnc-20` for the applicable country

Amazon has two implementations of this routing:

- **Server-side routing (newer):** Amazon handles redirection automatically based on the affiliate tag and the visitor's IP/locale. No code change required on your site. This is the current default for most accounts — enrollment alone may be sufficient.
- **JS snippet (older):** A small publisher-side script rewrites links before navigation. Available via Associates Central → Tools → OneLink if the page offers it, or at the global store page (`/p/stores/globalStore`) under a "Script" or "Get Code" section. Note: Amazon has been migrating accounts away from this model.

**To verify the routing is active:** click one of your own Amazon links through a VPN exit node in the UK and confirm it lands on amazon.co.uk.

#### What changes in the codebase

Link URLs do not change — you continue to generate `https://www.amazon.com/s?i=digital-music&k=...&tag=msc4dnc-20`. If Amazon is using server-side routing for your account, **no code change is needed at all**. If a JS snippet is offered and required, it goes in `_Layout.cshtml` as a single `<script>` tag.

#### Requirements

- An active, approved US Amazon Associates account (the `msc4dnc-20` tag implies one already exists)
- Enrolled countries configured in the global store (`/p/stores/globalStore`) — **done**
- Optionally: the OneLink JS snippet placed in `_Layout.cshtml` if Amazon's server-side routing is not sufficient for your account

#### Where to find the documentation

Amazon's OneLink help is inside Associates Central (login required). The relevant areas are:

- **OneLink tool**: Associates Central → Tools → OneLink (`https://affiliate-program.amazon.com/home/tools/onelink`)
- **Help overview**: Associates Central → Help → search "OneLink" — the canonical topic is listed under the _Tools_ section
- **International program links**: Each regional Associates program has its own help centre:
  - UK: `https://associates.amazon.co.uk`
  - CA: `https://associates.amazon.ca`
  - AU: `https://associates.amazon.com.au`

Note: Amazon does not publish stable public deep-link URLs for their Associates help articles — the paths change periodically and the pages require login — so navigating through the Associates Central UI is the most reliable approach.

#### Commission structure

- Commissions are earned **per marketplace**; a UK purchase through the UK affiliate tag earns a UK-rate commission credited to the UK account
- Routing works even for marketplaces where you haven't registered, but you earn no commission from those clicks
- UK and CA are the highest-priority registrations given the English-language ballroom audience overlap

**Verdict:** Strongly recommended. The JS snippet is the only code change needed. Register UK and CA marketplace accounts as soon as the US account is confirmed active and generating qualifying sales.

### C: Browser locale detection (client-side, no permission required)

`navigator.language` (or `navigator.languages[0]`) returns a BCP-47 tag like `"en-US"`, `"en-GB"`, `"de-DE"`. This can be mapped to a regional storefront:

```typescript
function amazonDomain(): string {
  const lang = navigator.language || "en-US";
  const region = lang.split("-")[1]?.toUpperCase();
  const map: Record<string, string> = {
    GB: "amazon.co.uk",
    DE: "amazon.de",
    FR: "amazon.fr",
    JP: "amazon.co.jp",
    CA: "amazon.ca",
    AU: "amazon.com.au",
    IN: "amazon.in",
    ES: "amazon.es",
    IT: "amazon.it",
  };
  return map[region] ?? "amazon.com";
}
```

**Caveats:**

- Language preference ≠ geographic location — a German speaker in the US, or a US expat in the UK, will get the wrong storefront
- VPN and incognito users are unaffected (no permission required, but also not accurate)
- Each storefront requires its own affiliate tag to earn commissions there
- Requires per-country affiliate registration to be worthwhile

**Verdict:** Adds complexity for modest gain if OneLink is in place. Useful only if you want to display the country-specific product price or title, which we do not.

### D: Server-side IP geolocation

The server can infer the user's country from their IP address and either return a country-specific Amazon URL in the API response, or set a cookie/header consumed by the frontend.

- Azure Front Door (already in use per `architecture/front-door-implementation.md`) provides the `X-Azure-ClientIp` header and [geolocation headers](https://docs.microsoft.com/azure/frontdoor/front-door-http-headers-protocol) including `X-FD-ClientCountry` (populated by the WAF/Front Door)
- Country code could be stored in a JS global set during page render (e.g., in the page model) and consumed by `AmazonPurchaseInfo`

**Verdict:** Most accurate, but adds complexity. Only worth pursuing if OneLink proves insufficient or if we need to display country-specific data beyond just the Amazon domain.

### D: User preference (profile setting)

Allow users to select their preferred Amazon marketplace in their account profile. This is accurate and requires no geolocation, but most users won't set it.

**Verdict:** Useful as an override on top of any of the above methods, not as a primary solution.

### Summary

| Approach                       | Effort                   | Accuracy | Commission support           |
| ------------------------------ | ------------------------ | -------- | ---------------------------- |
| Single storefront (amazon.com) | None                     | US-only  | US only                      |
| **OneLink**                    | Low (account setup only) | Good     | Per registered marketplace   |
| Browser locale                 | Medium                   | Moderate | Requires per-country account |
| Server-side IP geo             | High                     | Best     | Requires per-country account |
| User preference                | Medium                   | Exact    | Requires per-country account |

**Recommended:** OneLink is now configured for US, Canada, France, Germany, Italy, Netherlands, Poland, Spain, Sweden, and United Kingdom — covering the major English- and Western-European-language markets for ballroom content. Amazon may route visitors server-side with no further code change; verify with a UK VPN test before assuming a JS snippet is needed (see §4 of the implementation plan).

---

## Implementation Plan

### 1. Update `AmazonPurchaseInfo.link` (client-side)

`BuildPurchaseLink` on the server receives album/song IDs, not title/artist, making it awkward to generate a search URL there. The client-side `AmazonPurchaseInfo` is the better change point — `SongHistory.title` and `SongHistory.artist` are available in the component context.

For pure Option 1, the getter always returns a search URL regardless of whether an ASIN is stored:

```typescript
public get link(): string {
  const q = encodeURIComponent(`${this.artist} ${this.name}`);
  return `https://www.amazon.com/s?i=digital-music&k=${q}&tag=msc4dnc-20`;
}
```

`PurchaseInfo` does not currently carry `artist`/`name` fields. Options:

- Add `artist` / `name` to `PurchaseInfo` (populated from `SongHistory` when building the purchase list)
- Or construct the search link in the component that renders the purchase button, passing title/artist as props
- Or add a `searchLink` property alongside `link` on `AmazonPurchaseInfo`, set by the component after construction

**Future Option 4 activation:** To switch to the hybrid (direct link when ASIN is present), change the getter to:

```typescript
public get link(): string {
  if (this.cleanSong) {
    return `https://www.amazon.com/dp/${this.cleanSong}?tag=msc4dnc-20`;
  }
  const q = encodeURIComponent(`${this.artist} ${this.name}`);
  return `https://www.amazon.com/s?i=digital-music&k=${q}&tag=msc4dnc-20`;
}
```

No database migration is needed to make that switch — the ASINs are already there.

### 2. Remove the "Available on Amazon" filter

With all songs now showing a search link, the filter no longer has a meaningful predicate. Remove Amazon from the service filter UI entirely. Songs that do have ASINs in the database will still generate working (search) links; the filter just isn't useful.

### 3. ASINs — retained, unused

Existing Amazon ASINs are kept in the database. No migration, no column removal. They serve as a breadcrumb for Option 4 and may be useful if ASINs are ever re-verified via PA-API.

### 4. Verify Amazon OneLink routing (and add script if needed)

OneLink is enrolled for 10 countries. Amazon may be routing visitors server-side with no code change required.

**Verify first:** Click one of your own Amazon links through a VPN exit node in the UK (or ask a UK user to test). If the link lands on amazon.co.uk, routing is active and no code change is needed.

**If a JS snippet is required:** Retrieve it from Associates Central → Tools → OneLink (or the global store page under a "Script" / "Get Code" section) and add it to `_Layout.cshtml` as a single `<script>` tag.

### 5. Update `ServiceMatcher` (optional future work)

Deferred. If users need to input Amazon URLs manually, a regex for `amazon.com/dp/{ASIN}` could be added then.

---

## Files to Change

| File                                                   | Change                                                                                                             |
| ------------------------------------------------------ | ------------------------------------------------------------------------------------------------------------------ |
| `m4d/ClientApp/src/models/Purchase.ts`                 | `AmazonPurchaseInfo.link` — always return search URL using title/artist; include affiliate tag                     |
| `m4d/ClientApp/src/…` (filter/purchase components)     | Pass title/artist to `AmazonPurchaseInfo`, or construct search link in the component                               |
| `m4d/ClientApp/src/…` (filter UI)                      | Remove Amazon from the service filter                                                                              |
| `m4d/Views/Shared/_Layout.cshtml` (or `PageFrame.vue`) | Add OneLink `<script>` tag **only if needed** — verify server-side routing first (see §4 of implementation plan)  |
| Amazon Associates account (external)                   | **Done** — OneLink configured for US, CA, FR, DE, IT, NL, PL, ES, SE, GB; add AU/JP accounts if those markets grow |
