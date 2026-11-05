# Partner API: Standalone Host Independent of the Web App

**Type:** Plan
**Status:** In progress. Spike complete 2026-10-29 (see [Spike results](#spike-results-2026-10-29)); auth decided and PR 1 (skeleton + test environment) deployed 2026-11-01; PR 2 (resolve) deployed to test 2026-11-01; PR 3 (API keys and limits) deployed to test and PR 4 (key issuing pages) checked end to end against it 2026-11-04.
**Last verified:** 2026-11-04
**Code:** `m4d-api/` (Azure Functions project: `/v1/health`, and `/v1/songs/resolve` behind partner API keys, a per-client rate limit and a daily cap), with infrastructure and the deploy script in `m4d-api/infra/` and unit tests in `m4d-api.Tests/`. Reads the index built by `m4dModels/SongIndex.cs`, guarded by `m4dModels.Tests/PartnerApiIndexContractTests.cs`; the existing in-app API foundation is under `m4d/PublicApi/`.

**Context:** The likely first integration is a third-party app that sells its own subscription,
with the partner developer's backend calling music4dance server to server using a single
confidential credential. That takes the per-user OAuth design in
[public-api-authorization.md](public-api-authorization.md) off the critical path. This document
asks whether that thinner API can run **outside the ASP.NET app**, so a music4dance.net outage
(deploy, memory pressure, attack mitigation, SQL trouble) doesn't take the partner's app down
with it.

**Service level:** music4dance offers no SLA, to site subscribers or to partners, and won't
offer anything stronger than best effort. This design doesn't change that. Its purpose is to
make best effort *credible* for a paying partner: shrink what the API depends on, so ordinary
web-app trouble stops being partner-visible downtime.

**Short answer:** yes, and it's cheap. The Azure Search index already holds a denormalized,
read-optimized copy of everything the resolve endpoint returns. A small Azure Functions app on
the Flex Consumption plan can query it directly with managed identity. It needs no SQL, no
`Song` materialization, and no call back into the web app. The one design trap is
authentication: issuing tokens from the web app's OpenIddict server would re-create the
dependency we're trying to remove.

---

## Goals and non-goals

**Goals**

- Partner lookups keep working when `msc4dnc` (the production App Service) is down, restarting,
  or being redeployed.
- Keep the web app's attack surface and load separate from partner traffic, in both directions.
- Near-zero fixed cost at MVP volume.
- Record what settlement and fair-use need: per-client call counts by day and match method,
  plus unmatched identifiers. Record them without writing to SQL.

**Non-goals**

- An SLA of any kind. The partner agreement should say "best effort" and describe the client
  behavior we expect (see [Client expectations](#client-expectations)).
- Surviving an **Azure Search** outage. Search is the data. See
  [Failure modes](#failure-modes).
- Per-subscriber identifiers. The partner won't send them; settlement uses call volume or the
  partner's own reporting.
- Library import or other bulk resolution. The MVP is live recognition, one track per call.
  Bulk use is a separate conversation with its own limits.
- Per-user accounts, `/v1/me`, voting, or anything that reads `ApplicationUser`. Those need
  the web app and stay in the OpenIddict design for a later account-linking phase.
- A general search API. This covers resolution only, per the MVP scope.

---

## What the API actually depends on

An inventory of what `POST /v1/songs/resolve` needs, and where each piece lives today:

| Need | Today (in-app design) | Standalone equivalent | App-independent? |
| --- | --- | --- | --- |
| Song lookup by ISRC / Apple / Spotify id | `SongIndex.Search` with `ServiceIds/any(id: search.in(...))`, same as [SongController.cs:229](../../m4d/Controllers/SongController.cs#L229) | Same OData filter sent from the function | Yes |
| Fuzzy title + artist | `SongIndex.SongsFromTitleArtist`, [SongIndex.cs:1339](../../m4dModels/SongIndex.cs#L1339) | Same query (`searchFields=Title,Artist`); ranking logic must be shared or ported | Yes |
| Song fields in the response | `CreateSongs` decompresses `Properties` and replays the property log into `Song` | **Read the denormalized index fields directly** (below) | Yes |
| Dance names | `Dances.Instance`, loaded by `DanceStatsManager` via `FileManager` | Bundle `dances.json` into the function and load it with `DanceLib`'s `Dances.Load` | Yes |
| Index name | `SEARCHINDEX` / `SEARCHINDEXVERSION` + `appsettings.json` sections | A Search **index alias** (`songs-api-prod`) | Yes |
| Client authentication | OpenIddict bearer (per-user tokens) | API key or Entra ID client credentials, **not** m4d-issued tokens | Yes, if designed that way |
| Metering / global cap | `RateLimitingMiddleware` (per IP, in memory) | In-function limiter + Table Storage counters | Yes |
| Usage attribution | `UsageLog` in SQL | Table Storage aggregates | Yes |
| Unmatched-id review queue | (proposed) SQL table | Storage queue or table, consumed by the web app when it's up | Yes |

### The index already carries the response

`DocumentFromSong` ([SongIndex.cs:2135](../../m4dModels/SongIndex.cs#L2135)) writes these
top-level fields: `SongId`, `Title`, `Artist`, `Length`, `Tempo`, `ServiceIds`, `TempoTags`, and
a complex field per dance, `dance_{ID}`, with `Votes`, `Tempo`, `TempoTags`, `StyleTags`, and
`OtherTags`. That covers the whole MVP response in
[public-api-authorization.md § API Surface](public-api-authorization.md#api-surface):

| Response field | Index source |
| --- | --- |
| `title`, `artist`, `length` | Top-level fields |
| `beatsPerMinute` | Top-level `Tempo`, which is stored as **beats per minute** (`Song.Tempo`; imports map a `BPM` column to it). Returned unconverted; see [Tempo units](#exact-vs-fuzzy-matches-return-evidence-let-the-partner-decide). |
| `meter` | Derived from `TempoTags` containing `4/4`, `3/4`, or `2/4`. This mirrors `Song.Meter` ([Song.cs:4682](../../m4dModels/Song.cs#L4682)). |
| `danceRatings[].weight` | `dance_{ID}/Votes`. `-1` is the unconfirmed-only sentinel ([SongIndex.cs:2246](../../m4dModels/SongIndex.cs#L2246)); exclude it. |
| `danceRatings[].beatsPerMinute` | `dance_{ID}/Tempo`, also beats per minute (already falls back to song tempo at index time) |
| `danceRatings[].styles` | `dance_{ID}/StyleTags` |
| `match.method` | Which requested id appears in the returned `ServiceIds` |
| Fuzzy-match signals (`lengthSeconds`, etc.) | `Length` (seconds, already filterable for the site's length range), `Title`, `Artist`, search `@search.score` |
| `songUrl` | Built from `SongId`: a "View on music4dance" deep link the partner app can show |

So the function never touches `Properties`, the compressed property log. That matters for
three reasons:

1. It removes the heaviest dependency: `m4dModels`, `DanceMusicService`, `DanceStats`, and EF.
2. `Properties`, `Users`, and `Comments` contain usernames and user text. A strict `$select`
   whitelist means they never leave the search service, which is a real privacy improvement
   over materializing `Song`.
3. The function is a few hundred lines, not a second copy of the domain model.

**Constraint this accepts:** the API contract is limited to what the index holds. A future
response field that exists only in the property log needs an index field first (additive, via
`AddMissingIndexFields`, per [individual-artists.md](../songs/individual-artists.md) §5).

---

## Hosting options

| Option | Fixed cost | Independence from app | Fit | Verdict |
| --- | --- | --- | --- | --- |
| **A. Azure Functions, Flex Consumption** (.NET 10 isolated) | ~$0 on demand; optional always-ready instance | Full: own plan, own identity, own hostname | C# and `DanceLib` reuse, managed identity to Search, scale to zero | **Recommended** |
| B. Azure Container Apps (consumption) | ~$0, scale to zero | Full | Same code as a minimal ASP.NET Core API in a container. More moving parts (registry, image builds). | Good fallback if Functions chafes |
| C. APIM Consumption, policy-only proxy to Search | ~$0 + per-call | Full | No code at all, but response shaping in policy XML is awkward. Consumption **lacks `rate-limit-by-key` and `quota-by-key`** (they need Basic v2+ at a real monthly cost). | No: wrong tool at this scale |
| D. Give the partner developer a Search **query key** (optionally to a slim dedicated index) | $0 | Full | Zero compute. But their app couples to our Azure schema and OData; no metering, no response shaping; a full-index key exposes `Properties`/`Users`/`Comments`. | No: no contract, no control |
| E. Second App Service / slot running m4d | Plan cost | Partial: same code, same failure modes | Shields against platform problems but not app bugs or SQL outages | No: doesn't solve the actual worry |
| F. Keep it in the web app (the current plan) | $0 | None | Simplest | Baseline for comparison |

### Why Flex Consumption

Checked against the current docs on 2026-09-29:

- It's Microsoft's recommended serverless plan. The older Linux Consumption plan gets no new
  features, and migrating to Flex means a new app, so start on Flex.
- It supports .NET 8, 9, and **10** in the isolated worker model, which matches the solution's
  `net10.0`.
- On-demand billing has a monthly free grant of GB-seconds and executions. Check the
  [pricing page](https://azure.microsoft.com/pricing/details/functions/) for the current
  numbers. At MVP volumes (thousands to low hundreds of thousands of calls a month) it should
  cost close to nothing.
- **Cold starts** are the tradeoff. A scaled-to-zero .NET isolated app takes on the order of a
  second or two for its first request. Live recognition makes that visible, since a person is
  waiting on the result. Traffic will be bursty (evenings, weekends, dance events), so the
  first lookup after a quiet spell will be slow. If the spike shows it's noticeable, set **one
  always-ready instance** for the HTTP group. That's a small fixed monthly charge (always-ready
  has no free grant), still well under a Basic App Service plan.
- Its limits (one app per plan, no deployment slots, 30-second host start timeout) don't bite
  here. Rolling-update zero-downtime deploys are in preview; a partner API can tolerate a few
  seconds of deploy blip, and the partner's retry logic should expect 503s anyway.

---

## Authentication: don't route token issuance through the web app

OpenIddict `client_credentials` is the obvious choice for a server-to-server client, and it
works when the API lives in the web app. **For a standalone host it defeats the purpose:** the
token endpoint is `/connect/token` on music4dance.net. With one-hour access tokens, a web app
outage longer than an hour (or any outage that hits a token refresh) takes the API down anyway.

Two credential schemes that don't depend on the app:

| | **API key** (chosen) | **Entra ID client credentials** |
| --- | --- | --- |
| What the partner holds | A random 256-bit key, e.g. `m4d_live_…` | A client secret or certificate for an app registration in our tenant |
| Validation | Function hashes the presented key (SHA-256) and compares against stored hashes in constant time | Function validates Entra JWTs: issuer, audience, `roles` claim `Songs.Read`. Built-in App Service auth (Easy Auth) can do this before code runs. |
| Issuer availability | No issuer | Microsoft's identity platform (independent of m4d) |
| Rotation | Two active keys per client; add new, partner switches, delete old | Standard secret/cert rollover |
| Revocation | Disable or delete the key's vault secret; takes effect within the 5-minute refresh | Disable the app registration or remove the role assignment |
| Build cost | ~50 lines | Configuration, plus the partner adopts MSAL or an equivalent library |
| Standards story | Common for server-to-server APIs (Stripe-style); fine when the key never ships to devices | Full OAuth 2.0 client credentials |

**Decision: API keys.** Entra stays available if a later partner prefers it. Either way the
credential never goes in the partner's client app: anything shipped to devices can be
extracted.

Header: `Authorization: Bearer m4d_live_…`, not a query string, so keys stay out of access
logs. Reject anything that looks like a key in the URL.

### Key format and generation

```txt
m4d_live_7KQ2M9XA4PZT_hR3v…(43 chars)
└──┬───┘ └────┬─────┘ └─────┬──────┘
 prefix     keyId         secret
```

- **Secret:** 32 bytes from `RandomNumberGenerator`, base64url (256 bits).
- **keyId:** a short random id. Not secret; it names the key in logs, the vault, and admin pages.
- **Prefix:** `m4d_test_` or `m4d_live_`, so the environment is visible and a leaked key is easy to
  spot in a repo or log.
- **Stored value:** SHA-256 of the whole key. A slow hash (bcrypt, PBKDF2) only helps with
  low-entropy passwords; against a 256-bit random key it adds per-request latency for nothing.

The web app generates the key and shows it **once**. It never stores the key, and doesn't
need it again.

### Issuing and handing over keys

Keys are issued from music4dance.net, so the key never passes through email or through the
site admin:

1. An admin grants the partner developer's music4dance account a new `partnerDeveloper` role
   and links it to a client record (name, limits).
2. The partner developer signs in, opens an API keys page, generates a key with a label, and
   copies it from the one-time display.
3. Rotation: at most two active keys per client. Generate a new one, deploy it, revoke the old
   one. Admins can revoke any key.

SQL holds metadata only (keyId, client, owner, label, created, revoked), for the key list and
audit. Neither the key nor its hash goes in the database, and the function never reads SQL.

When the web app is down, keys can't be issued or revoked, but existing keys keep working.
Issuance is off the request path, so it doesn't affect the API's independence.

**As built (PR 4):**

- **Admin page** `/PartnerKeys/Clients` (`dbAdmin`, linked from the admin index): add clients
  (the client id is permanent, since it names the vault secret and the usage rows), edit name,
  limits and disabled status, link and unlink developer accounts by user name or email, and
  generate or revoke any client's keys. Linking grants `partnerDeveloper`; unlinking from the
  last client removes it. Keys a developer issued stay active after unlinking.
- **Developer page** `/PartnerKeys` (`partnerDeveloper`, linked from the account pages): the
  developer's clients and keys, a label field, Generate and Revoke. The new key appears once on
  a `no-store` response that isn't redirected, so it never sits in a URL, `TempData` or the
  database.
- **Tables** (migration `PartnerApiKeys`): `PartnerClients` (id, name, limits, disabled),
  `PartnerDevelopers` (client and account), `PartnerApiKeys` (keyId, client, label, created and
  revoked, with who).
- **Order of writes:** issuing writes the vault, then SQL; if SQL fails the vault secret is
  disabled again. Revoking disables every version of the vault secret (without reading it), then
  marks the row revoked. Saving a client writes its config secret, then SQL.
- **Code:** `m4d/Controllers/PartnerKeysController.cs`, `m4d/PublicApi/PartnerKeyService.cs`,
  `m4d/PublicApi/PartnerKeyVault.cs`, and `m4d-api/PartnerKeyFormat.cs` linked into `m4d`, so
  the issuer and the validator share one key format.
- **Configuration:** `PartnerApi:KeyVaultUri` (the environment's partner-keys vault) and
  `PartnerApi:Environment` (`live` for `m4d_live_` keys, anything else for `m4d_test_`). Set
  `https://m4d-api-keys-test.vault.azure.net/` and `test` for `m4d-test`, and
  `https://m4d-api-keys.vault.azure.net/` and `live` for `msc4dnc`. Without a vault URI the
  pages still issue keys and show the `az keyvault secret set` command for an admin (the manual
  fallback below). Locally, the site's development service principal has no role on either
  vault; run with `--AZURE_CLIENT_ID=` to fall back to your own `az login`.

### Where the hashes live: a dedicated Key Vault

Hashes and per-client limits go in a **small Key Vault used only for partner keys**, one per
environment (`m4d-api-test` and `m4d-api`), in RBAC mode. The site's existing `music4dance` vault
uses access policies (see [key-vault-rbac-migration.md](key-vault-rbac-migration.md)), which grant
permissions on the whole vault: reading it would expose every
site secret to the function, and writing to it would let the web app overwrite any of them. A
separate vault per environment also means the test site can't mint production keys.

| Identity | Role on the partner-keys vault |
| --- | --- |
| Web app managed identity (already exists) | Key Vault Secrets Officer (issue and revoke) |
| Function app managed identity | Key Vault Secrets User (read) |

| Secret | Value | Notes |
| --- | --- | --- |
| `key-{keyId}` | SHA-256 hash (hex) | Tag `client`; the secret's expiry date gives optional key expiry; disable or delete to revoke |
| `client-{clientId}` | JSON: name, rate, burst, `dailyCap`, status | Limits change without a function restart or redeploy |

The hash isn't really secret, since the key can't be recovered from it. What needs protecting is
who can *add* an entry, and the vault's RBAC and audit log cover that.

**Manual fallback.** If the web app shouldn't write to the vault, the page still generates the
key and hands it over, then shows an admin the keyId and hash to add with
`az keyvault secret set`. Function app settings are the wrong place even for manual entry: every
change restarts the app (a cold start), and a deploy can overwrite them.

### Validation in the function

Built in PR 3: `PartnerKeyFormat.cs` (format and vault layout, shared with the web app),
`PartnerKeyStore.cs` (the cache), and `PartnerGate.cs` (the per-request checks).

- `AuthorizationLevel.Anonymous` on the endpoints. `PartnerGate.Admit` runs first in each
  authenticated function, so the client id is available to the rate limiter, the usage table, and
  logs. It's a call rather than worker middleware: with one authenticated endpoint that's the
  same thing, and it can be unit tested on a plain `HttpRequest`. The platform's built-in function
  keys (used by the spike) are gone; they're stored in plaintext in the function's Storage
  account and carry no client identity.
- On startup and then every 5 minutes, the function lists the vault's secrets and reads only
  those that are new or changed (the list returns versions and dates, not values). Disabled
  secrets are dropped without being read; the vault refuses to return their values anyway. A key
  past its secret's expiry date is rejected even before the next refresh. A malformed client
  secret keeps the client's previous config rather than locking it out.
- Per request: parse the keyId, look it up in memory, SHA-256 the presented key, compare with
  `CryptographicOperations.FixedTimeEquals`. **An unknown keyId gets 401 without a vault call**,
  so junk keys can't be used to load the vault.
- A key-shaped value anywhere in the query string gets 400, even alongside a valid header.
- The keys load in the background when an instance starts. A request that wakes a cold
  instance (Flex scales to zero within 5 minutes) waits up to 10 seconds for that first load
  instead of being refused; on the test environment the first version answered the waking request
  with 503.
- After startup a vault outage doesn't matter: the cache keeps working, and only new keys and
  revocations wait. An instance that starts during an outage has no keys and returns 503 with
  `Retry-After: 30` after the 10-second wait, retrying the vault every 15 seconds.

| Status | When |
| --- | --- |
| 400 | A key in the URL |
| 401 | No `Authorization: Bearer` header, or a malformed, unknown, revoked, expired or wrong key (`WWW-Authenticate: Bearer`) |
| 403 | The key's client is `disabled` |
| 429 | Over the daily cap (`Retry-After` until UTC midnight) or the rate limit (`Retry-After` until the next token) |
| 503 | No keys loaded within 10 seconds of the instance starting |

**Manual issuing:** `m4d-api/infra/new-key.ps1 -Environment test|prod` issues (`-ClientId`,
optionally `-Name` and limits to write the client secret, `-OutFile` to keep the key off the
screen), lists (`-List`) and revokes (`-Revoke keyId`) keys with the same vault writes as the web
app's page.

### Cost

Standard Key Vault has no monthly fee; secret operations are **$0.03 per 10,000** (West US
retail price, checked 2026-11-01). The 5-minute refresh is ~9,000 operations a month per running
instance, about $0.03; issuing and revoking keys is negligible. Two vaults come to a few cents a
month. The cache is what keeps it there: a vault read on every request at 20 req/s would be
~52 million operations, about $155 a month. Premium HSM keys, Managed HSM, and certificate
renewals carry larger charges, but this design uses none of them.

---

## Proposed architecture

```txt
 Third-party app (on the user's device)
      │  (subscription checked by the partner)
      ▼
 Partner's backend  ── caches results ──┐
      │ HTTPS, Authorization: Bearer m4d_live_…
      ▼
 api.music4dance.net  (custom domain, DNS straight to the function; not via the web app)
 ┌─────────────────────────────────────────────────────────┐
 │ m4d-api  (Azure Functions, Flex Consumption, .NET 10)    │
 │  • key check → per-client limiter → resolve             │
 │  • in-memory result cache (10 min hits, 1 min misses)   │
 │  • DanceLib + bundled dances.json for names             │
 └──────┬──────────────────────┬──────────────────────┬────┘
        │ managed identity     │ managed identity     │ managed identity
        │ Search Index Data    │ Storage Table/Queue  │ Key Vault Secrets
        │ Reader only          │ Data Contributor     │ User (read)
        ▼                      ▼                      ▼
 Azure AI Search          Storage account        Partner-keys vault
  alias songs-api-prod     (the function's own)   • key-{keyId}: key hash
   → songs-prod-3          • usage table          • client-{id}: limits
                           • unmatched queue              ▲
                                  │                       │ Secrets Officer:
                                  ▼ drained when up       │ issue and revoke only
                           music4dance.net ───────────────┘
                            admin review, partner API keys page
```

The web app appears only at the bottom: as an optional consumer of the unmatched queue, and as
the place keys are issued and revoked. Nothing on the request path touches it.

### Endpoints

```txt
POST /v1/songs/resolve    resolve one recognized track
GET  /v1/health           unauthenticated liveness; checks Search reachability (cached 30s)
```

One track per call, matching live recognition. All ids the caller supplies (ISRC, Apple Music,
Spotify) go into **one** `ServiceIds/any(id: search.in(id, 'R:…,I:…,S:…'))` query, the same
pattern as the playlist ISRC fallback. Only a miss on every id falls through to title and
artist search, so most calls cost one Search query and fuzzy ones cost two.

Request: `isrc`, `appleMusicId`, `spotifyId`, `title`, `artist`, and optional
`durationSeconds` if the partner's recognition path can supply it (Apple Music catalog data
includes duration, looked up by Apple Music id).

### Exact vs fuzzy matches: return evidence, let the partner decide

An id match and a title/artist match are different kinds of answer, and the response should
make that impossible to miss. The API **doesn't hide fuzzy matches and doesn't decide for the
user**. It returns candidates with the evidence it used, plus a suggested confidence level.
The third-party app decides whether to show a result, ask "Is this the song?", or say nothing.

```json
{
  "match": "exact",            // "exact" | "ambiguous" | "fuzzy" | "none"
  "method": "isrc",            // isrc | appleMusicId | spotifyId | titleArtist; null for ambiguous and none
  "candidates": [
    {
      "songId": "3f2a…",
      "songUrl": "https://www.music4dance.net/song/details?id=3f2a…",
      "title": "Blue Bayou",
      "artist": "Linda Ronstadt",
      "lengthSeconds": 231,
      "beatsPerMinute": 178,    // beats (not bars or measures) per minute
      "meter": "4/4",
      "danceRatings": [
        { "danceId": "WCS", "danceName": "West Coast Swing", "weight": 12,
          "beatsPerMinute": 119, "styles": [] }
      ],                        // empty when the song has no confirmed dances yet
      "matchedBy": ["isrc", "appleMusicId"],   // present only for id matches
      "evidence": {             // present only for fuzzy candidates
        "titleMatch": "normalized",   // exact | normalized | partial | none
        "artistMatch": "exact",       // exact | normalized | partial | none
        "lengthDeltaSeconds": 2,      // null when either side has no length
        "confidence": "high"          // server's suggestion: high | medium | low
      }
    }
  ]
}
```

- **Exact** returns one candidate. If several songs share the id (duplicates awaiting merge),
  return the one with the most confirmed dance votes (`dance_ALL/Votes`). Songs linked through a
  shared id count as one recording: a song carrying both the ISRC and the Apple id ties an
  ISRC-only duplicate and an Apple-only duplicate together. `matchedBy` lists every supplied id
  that points at it.
- **Ambiguous** means the supplied ids point at different songs with no id in common (say, the
  ISRC at one recording and the Apple id at another). Rather than recommend dances for what
  may be the wrong version, the API returns one candidate per recording, each with its own
  `matchedBy`. The ISRC's song comes first, since the ISRC identifies the recording, then
  Apple Music, then Spotify. The partner shows it as an uncertain match.
- **An id match doesn't need confirmed dances.** A song an id identifies is returned even if it
  has no confirmed dances yet, with an empty `danceRatings`, rather than falling through to a
  title search. The partner can show "No dances yet" and link to the song page, where users
  can add them.
- **Fuzzy** returns up to 3 candidates, best first, each with `evidence`. Title search keeps the
  confirmed-content filter (below), so it only suggests songs that have dances.
- **Length is the strongest tie-breaker.** Same title and artist but a different recording
  (live, extended mix, radio edit, re-recording) usually differs in length, and often in tempo,
  which is exactly what matters for dancing. Suggested rule: within ±5 s is supporting evidence;
  off by more than ~15 s pushes confidence to `low`, whatever the text match says. Tune these
  thresholds from real data.
- Remix and dance-edit titles (see `Dances.GetAllDanceWordsUpper`, which the merge code uses
  for remix detection) should lower confidence when the recognized title lacks the qualifier,
  or vice versa. As built, a title that only contains the other ("Blue Bayou" against "Blue
  Bayou (Live)") is a `partial` match and gets `low`. An artist that doesn't match at all also
  gets `low`; a partial artist ("feat." credits) gets `medium`. Finer rules wait for real
  recognitions (open question 2).
- **Tempo units.** Every tempo in the API is in **beats per minute**: the count of beats
  (quarter notes in 4/4) per minute, not bars or measures per minute. "BPM" is sometimes used
  for *bars* per minute, especially in Europe, so the field is spelled out as `beatsPerMinute`
  and partner-facing docs should spell it out too rather than abbreviate. A 4/4 song at 120
  beats per minute is 30 bars per minute. The site shows MPM (measures per minute) for some
  dances, but the API doesn't convert: it returns the index value as stored, so the unit is the
  same whether or not the song has a `meter`, and partners divide by the meter if they want
  bars.
- **Styles.** `danceRatings[].styles` is the dance's `StyleTags` (e.g. `International`). Other
  tag categories aren't exposed yet.
- The confidence levels are guidance and part of the contract only as "high means more likely
  than medium." The `evidence` fields are stable, so the partner can apply its own rule and we
  can change ours.

When the result is `ambiguous`, `fuzzy` or `none`, enqueue the recognized identifiers as
unmatched (below); for `ambiguous`, a reviewer decides which song each id belongs to.
Record the fuzzy candidate ids alongside the unmatched identifiers. A reviewer can then
attach the ISRC or Apple id to the right song in one step. If the partner ever reports which
candidate the user accepted, that becomes a much stronger signal (see open questions).

Title search applies the confirmed-content cruft filter from `AddCruftInfo`
([SongIndex.cs:1529](../../m4dModels/SongIndex.cs#L1529)): `DanceTags/any()` and
`(not DanceTags/any() or dance_ALL/Votes ne null)`, which the function sends in its simplified
form, `DanceTags/any() and dance_ALL/Votes ne null`. That way fuzzy suggestions never include
songs with no dances or only unconfirmed ones. The id query doesn't apply it (see above).
Unconfirmed-only dance ratings are left out of `danceRatings` either way.

### Index coupling: alias plus contract test

The function must follow index version cutovers
([search-index-versioning.md](../search/search-index-versioning.md)) without redeploying and
without reading m4d's config.

- **Index alias.** Aliases are GA in the stable Search REST API and SDKs (verified 2026-09-29).
  Create `songs-api-prod → songs-prod-3` and `songs-api-test → songs-test-3`, and add a
  "repoint the API alias" step to the production migration runbook, after verification (Step
  3). Alias updates propagate within about 10 seconds, and Search refuses to delete an index
  an alias still points at, which is a useful guard against pulling the rug out.
- **Index-scoped role follows the index, not the alias.** The function's Search Index Data
  Reader role is assigned on the index itself (Search indexes aren't ARM resources, so
  `m4d-api/infra/deploy.ps1` does it, not Bicep). The cutover step is therefore: grant the
  function's identity the role on the new index, *then* repoint the alias. In the other order
  the API gets 403s until the role is granted.
- **Field-name constants.** The function doesn't reference `m4dModels`, which would drag in EF
  and most of the app. Its field names are copied into `m4d-api/IndexFields.cs`, a
  dependency-free file.
- **Schema contract test.** `m4dModels.Tests/PartnerApiIndexContractTests.cs` links
  `IndexFields.cs` and checks it against `SongIndex.BuildIndexFields()`. It checks that every
  field the API selects exists, that `Title` and `Artist` are searchable and the filtered fields
  filterable, and that every dance's `dance_{ID}` field has the subfields the API reads. It runs
  in CI-SERVER, so an index change that would break the API fails the web app's PR, not the
  partner in production.
- **Title and artist ranking.** Search's own ranking orders the candidates; the evidence rules
  are in `m4d-api/MatchEvidence.cs`. It's pure (strings in, evidence out), so it can move to
  `DanceLib` if the web app ever needs the same rules.

### Metering, settlement data, and unmatched ids

All of this goes to the function's own Storage account, never SQL:

- **Per-client rate limit** (`ClientRateLimiter.cs`). This is the main throttle. It's a token
  bucket in memory per instance, with a sustained rate and a burst size per client, read from
  the client's `client-{clientId}` secret in the partner-keys vault so they can change without a
  redeploy. With two instances a client can get up to twice its rate; the instance cap bounds
  that. See [Capacity](#capacity-and-throughput) for starting values.
- **Defaults.** A client secret is JSON like
  `{"name":"…","ratePerSecond":5,"burst":20,"dailyCap":100000,"status":"active"}`. Any limit it
  leaves out, or every limit for a key whose client has no secret, comes from the app settings
  `PARTNER_DEFAULT_RATE`, `PARTNER_DEFAULT_BURST` and `PARTNER_DEFAULT_DAILY_CAP` (20, 50 and
  100,000 when unset, per [Starting settings](#starting-settings-all-configurable)).
- **Daily cap** (`UsageMeter.cs`). Counts are kept in memory and flushed to Table Storage every
  30 seconds, which also reads back the totals other instances flushed. When the day's total
  reaches the client's `dailyCap`, return `429` with `Retry-After` until UTC midnight. Across
  instances it's accurate to within one flush interval, which is fine for a cap meant to catch
  runaway bugs.
- **Max instance count.** A backstop on cost and Search load, not the primary throttle.
- **Usage aggregates.** Table `usage`, one row per client and UTC day (PartitionKey client id,
  RowKey `yyyy-MM-dd`), with a column per result (`exact`, `ambiguous`, `fuzzy`, `none`), a
  `limited` column for requests the rate limit or cap turned away, and `total` (results only,
  which is what the cap reads). Instances update a row with ETag optimistic concurrency, so
  their counts add up. This is the self-verifying volume number for a tiered-fee settlement. It
  has no sampling problem, unlike deriving counts from Application Insights. There are no
  per-subscriber ids; settlement uses these volumes or the partner's own sales reporting.
- **Unmatched identifiers.** Enqueue misses, ambiguous results and fuzzy-only results, deduplicated per instance.
  The web app gets an admin page (later) that drains the queue into a review list. If the web
  app is down, the queue just grows. A Storage queue message lives 7 days by default, and
  that's configurable.

Storage failures **fail open**: the counts stay in memory and go out with a later flush, and
the cap keeps working from what each instance has seen, so during an outage it's per instance
rather than shared. An earlier draft had the cap fail closed after 5 minutes without Storage;
that contradicted the failure-modes table (Storage down, API up), and per-instance counting
already catches a runaway client. A Storage blip doesn't become a partner outage.

---

## Capacity and throughput

These are estimates to plan with and to share with the partner developer. The pre-PR 1 spike
should replace the latency guesses with measurements.

### Per-request cost

- **Function CPU:** key hash, parsing a ~1 KB request, projecting ~100 `dance_*` fields,
  serializing a few KB of JSON. Low single-digit milliseconds.
- **Search round trip** (same region): an id-filter query is estimated at ~20–60 ms; the
  title/artist full-text fallback at ~50–150 ms.
- **Blend:** if ~70% of lookups hit on an id, the average is ~70 ms. Plan on **~100 ms**.

### Per-instance throughput

Flex Consumption limits concurrent HTTP executions per instance by instance size. Since the
function mostly waits on Search, throughput ≈ concurrency ÷ latency:

| Instance size | Default HTTP concurrency | At 100 ms | Planning figure (÷2) |
| --- | --- | --- | --- |
| 512 MB (0.25 core) | 4 | ~40 req/s | **~20 req/s** |
| 2,048 MB (1 core) | 16 | ~160 req/s | **~80 req/s** |
| 4,096 MB (2 cores) | 32 | ~320 req/s | ~160 req/s |

The work is I/O-bound, so concurrency could be raised above the defaults. The planning figures
leave room for slow Search responses, GC pauses, and cold instances joining mid-burst.

**So 4 instances of 2 GB is roughly 300+ req/s, far more than Azure Search can serve on top of
the website's own traffic.** The function will never be the bottleneck; Search will. That's
why the per-client rate limit is the real control, and the instance cap is only a backstop.

### Expected demand from live recognition

```txt
peak lookups/s ≈ subscribers × fraction active at peak ÷ seconds between lookups × (1 − cache hit rate)
```

With 20% of subscribers active at peak, one lookup per song (~3.5 min = 210 s), and no cache:

| Third-party app subscribers | Peak lookups/s | Share of one 2 GB instance |
| --- | --- | --- |
| 1,000 | ~1 | ~1% |
| 10,000 | ~10 | ~12% |
| 100,000 | ~100 | more than one instance; Search capacity becomes the question |

Two effects push the real numbers lower:

- **The partner's cache.** Dance music repeats heavily; a popular song is looked up by many
  users. A shared cache keyed by ISRC or Apple id should absorb a large share of lookups.
- **Correlated bursts are cache-friendly.** At a dance event, many people in the same room
  recognize the *same* song within seconds, so the cache turns 50 lookups into one.

### Azure Search capacity (single replica)

Microsoft doesn't publish a queries-per-second figure for any tier. Throughput depends on
query shape, index size, and what else the replica is doing, and the official guidance is to
load test. These are rough planning ranges for **one replica on Basic or S1**, whose
per-replica throughput is broadly similar. The production tier and replica count still need to
be confirmed in the portal.

| Query shape | Rough capacity, one replica | Notes |
| --- | --- | --- |
| Id filter (`ServiceIds/any(...)`, no `search=`, no scoring) | ~50–150 QPS | Filter-only, served from the inverted index |
| Title/artist full text (`searchFields=Title,Artist`) | ~15–40 QPS | Scoring, analyzers, larger result scan |
| **Mixed, planning figure** | **~25–50 QPS** | Assumes the website's own query mix |

Each partner lookup costs **~1.3 Search queries** on average: one id query every time, plus a
full-text query for the ~30% that miss on id.

**Carving out 20% for partner traffic:**

| | Low end | High end | Planning figure |
| --- | --- | --- | --- |
| Search QPS reserved for partners (20%) | ~5 QPS | ~10 QPS | ~6 QPS |
| Partner lookups/s (÷ 1.3) | ~4 /s | ~8 /s | **~5 /s** |
| Partner lookups/min | ~240 /min | ~480 /min | **~300 /min** |
| Partner lookups/day if sustained | ~350,000 | ~690,000 | ~430,000 |

What that means against the numbers above:

- **1,000 subscribers (~1 lookup/s at peak)** uses about a fifth of the slice. Comfortable.
- **10,000 subscribers (~10 lookups/s with no cache)** is about twice the slice. It fits only
  if the partner's cache absorbs half or more of the lookups, which is plausible for dance
  music but should be checked rather than assumed.
- **The proposed per-client rate limit (20 req/s sustained, burst 50) is above the 20% slice.**
  One client running at the limit could take most of a single replica. If the 20% carve-out is
  the policy, the sustained limit should be around **5 req/s** (≈300/min), with a burst of
  ~20 to cover a room full of people recognizing the same song before the cache warms.
- The daily cap of 100,000 is well inside the slice (a sustained 5/s for a day is ~430,000).

The 20% is only meaningful relative to the website's real load. If the portal metrics show
the site peaking at, say, 5 QPS on a replica that can serve 40, the actual headroom is far more
than 20%. The spike's load test (below) should find the replica's real ceiling for this query
mix, and the limits should be reset from that.

### Starting settings (all configurable)

| Setting | Start | Why |
| --- | --- | --- |
| Instance size | 2 GB | Faster cold start than 512 MB, which matters with a person waiting. At these volumes the cost difference is small; revisit after the spike. |
| Max instances | 2 | One instance covers ~10,000 subscribers; the second covers bursts and instance recycling. Raise to 4 if limits are raised. |
| Per-client rate limit | 20 req/s sustained, burst 50 | ~20× the expected peak at 1,000 subscribers; low enough that a runaway client can't swamp Search |
| Daily cap | 100,000 calls | Far above expected use (~1,000 subscribers × a few dozen lookups on a busy day); catches bugs and loops |

**Room to grow:** before raising limits, look at the search service's own metrics in the portal
(search queries per second, search latency, throttled search query percentage) over the last
30 days. That shows how much Search headroom the website leaves. If a partner ever needs more
than that headroom, the fix is Search capacity (a second replica or a higher tier), paid for
out of the partnership. Function capacity is effectively unlimited by comparison.

---

## Client expectations

What to put in front of the partner developer, and into the partner terms:

- **Best effort, no SLA.** Same as the website.
- **Cache results** keyed by ISRC or Apple Music id: hits for up to 24 hours, misses for about
  an hour. This is within the bounded caching the terms allow, and it's the biggest lever on
  both latency and load.
- **Timeouts and retries:** time out at ~5 s. Retry once with backoff on `503` or a timeout.
  Honor `Retry-After` on `429`.
- **Degrade gracefully:** if music4dance is unavailable, show the recognition result without
  dance info rather than failing the whole feature.
- **Cold starts:** the first lookup after a quiet period may take 1–2 s longer (unless an
  always-ready instance turns out to be needed).
- **Fuzzy results are candidates, not answers.** The third-party app decides how to present
  them, using the `evidence` fields.
- **One track per call.** Library import or other bulk resolution needs a separate
  conversation first.

---

## Failure modes

| Failure | Current in-app design | Standalone function |
| --- | --- | --- |
| Web app down, restarting, deploying | API down | **API up** |
| SQL down or degraded | API down (entitlement check, `UsageLog`) | **API up** |
| Web app overloaded by crawlers/attack | API slow or down | **API up**; partner traffic doesn't compete for web app threads either |
| Azure Search down | API down | API down (503 + `Retry-After`) |
| Function Storage account down | n/a | API up; counters and queue writes dropped, see above |
| Partner-keys vault down | n/a | API up on cached keys; new keys and revocations wait. An instance that starts during the outage returns 503. |
| Entra ID down (if chosen) | n/a | New tokens fail; cached tokens work until expiry |

The remaining single point of failure is **Azure Search**, and that's acceptable under best
effort. It's a managed service with a much better track record than a single web app
instance. The other way it fails is throttling when the website overloads the shared replica:
in the 30 days before the spike that was 34 minutes, almost all during one site overload
(see [Spike results](#spike-results-2026-10-29)). The partner API returns 503s in those
minutes however it's hosted. Two notes for expectations rather than action:

- A single-replica search service is briefly unavailable during Microsoft's own maintenance.
  The partner's retry and cache behavior covers that.
- If the partnership grows enough to justify it, a second replica is the upgrade that improves
  availability for both the partner and the website. That's a business decision, not a
  prerequisite.

---

## Operations

- **Infrastructure as code.** `m4d-api/infra/main.bicep` with `test.bicepparam` and
  `prod.bicepparam`: Flex plan, function app, Storage account (identity access only, no
  account keys), partner-keys vault, Application Insights with its Log Analytics workspace, and
  role assignments (Storage data roles on its own account, Secrets User on the vault for the
  function, Secrets Officer for the matching web app). `m4d-api/infra/deploy.ps1 -Environment
  test|prod` creates the resource group, deploys the template, grants the index role, and
  deploys the code. One resource group per environment, named like the function app.
- **Environments.** `m4d-api-test` → alias `songs-api-test`, key writer web app `m4d-test`;
  `m4d-api` → `songs-api-prod`, key writer `msc4dnc`. In test, the person deploying also gets
  Secrets Officer, for adding keys by hand with `new-key.ps1`. Give the partner developer a test key
  first.
- **Deployment.** Infrastructure changes create role assignments, so they run by hand from
  `deploy.ps1` under an Owner account; they're rare. Code deploys need only rights to deploy to
  the function app: `deploy.ps1 -SkipInfrastructure` today, and later a separate pipeline stage
  so API deploys and web app deploys stay independent (that independence is the point).
  `azure-pipelines.yml` today targets the web apps only.
- **Custom domain.** `api.music4dance.net` with a Flex site-scoped managed certificate. DNS
  points straight at the function, not through Front Door or the web app, so a Front Door
  misconfiguration can't take it down either. A versioned path (`/v1`) is the contract.
- **Monitoring.** Application Insights on the function, with an availability test against
  `/v1/health` from a couple of regions and alerts on 5xx rate, on p95 latency, and on a
  client reaching its daily cap (logged as a warning: `Client {Client} reached its daily cap`).
  `Program.cs` removes the Application Insights SDK's default Warning-and-above filter; until PR
  3 it silently dropped the per-query Search timings (`Resolved {Match} by {Method} for {Client}`)
  and every other Information log.
- **Build hygiene.** The new project joins `music4dance.sln`, so the warning-clean
  `-warnaserror` CI build covers it.

---

## Effect on existing plans

- [public-api-authorization.md](public-api-authorization.md) **PR 1** (OpenIddict foundation,
  flag off) stays as is. It's the base for a later optional **account linking** phase, where
  third-party app users connect an m4d account so their votes count. That phase genuinely
  needs the web app and can accept its availability.
- **PRs 2–4** of that plan's subscriber MVP are replaced by this plan. The resolve logic moves
  into the function instead of a `/v1` controller in `m4d`. Update that doc's subscriber MVP
  section once this direction is confirmed.
- The OAuth doc's security checklist still applies where relevant: bearer only, no secrets in
  query strings, per-client limits, no unbounded result sets, and no list-all endpoint.

---

## Implementation plan

| PR | Scope |
| --- | --- |
| **1. Skeleton + infra** | `m4d-api` Functions project (isolated, .NET 10), Bicep, `/v1/health`, test environment deployed, search alias for test |
| **2. Resolve** | Single-track id resolution, title/artist fallback with candidates and `evidence` (length delta, match quality, suggested confidence), projection from index fields, dance names from `DanceLib`, the index contract test in `m4dModels.Tests`, unit tests with a faked `SearchClient` |
| **3. Auth + limits** (done) | Key validation with the vault-backed cache, per-client config from the vault, rate limit, daily cap, usage table, `new-key.ps1` for issuing keys by hand |
| **4. Key issuance** (done) | In `m4d`: `partnerDeveloper` role, client and key metadata tables (EF migration), API keys page (generate once, list, revoke), admin view of clients and keys, vault writes from the web app's identity |
| **5. Unmatched queue** | Queue writes (misses, ambiguous and fuzzy-only results) in the function; admin drain and review page in `m4d` (can trail) |
| **6. Production** | Prod alias, custom domain and certificate, alerts, runbook step added to search-index-versioning.md, production key issued to the first partner |

The spike below came before PR 1. Its `m4d-api/` project is the starting point for PRs 1 and 2.

---

## Spike results (2026-10-29)

A minimal Flex function (`m4d-api/`: resolve + health, the platform's function-key auth as a
stand-in for the API key design, DanceLib for dance names) deployed to its own resource group, `m4d-api-spike`, in West US next to Search. Its
managed identity has **Search Index Data Reader scoped to the `songs-test-3` index only**,
queried through the alias `songs-api-test`. Index-scoped roles work through an alias.

### Warm latency

300 sequential requests over a sample of 300 confirmed songs from the test index:

| | Server p50 / p95 | Plan estimate |
| --- | --- | --- |
| Id search query | 9 / 14 ms | 20–60 ms |
| Title/artist search query | 13 / 27 ms | 50–150 ms |
| Whole request, id hit | 10 / 15 ms | ~100 ms blended |
| Whole request, id miss then title/artist | 21 / 35 ms | |

From a home connection the client saw ~130 ms, almost all network round trip and a fresh TLS
handshake per request. Function latency isn't a concern.

Match quality on the sample: id lookups resolved 200 of 200 (after the ISRC case fix below).
Title + artist put the expected song first in 98 of 100, and rated 98 of them `high`.

### Cold starts

Probes after idle gaps of 5, 10, 15, 20 and 30 minutes all landed on a **new instance**, so
Flex scales to zero in under 5 minutes. A cold request took **2.2–2.5 s** at the client against
~180 ms warm. About 0.5–0.65 s of that is the first Search call (managed-identity token plus
connection setup); the rest is instance allocation and host start.

So for live recognition, nearly every first lookup after a quiet spell is cold. Options:

- Warm the credential and Search connection at startup (a hosted service that fetches a token
  and runs one query). It's cheap and removes ~0.5 s.
- One **always-ready instance**, which removes the rest at a small fixed monthly cost.
- Accept it: a 2 s first lookup with the result shown when it arrives may be fine for the
  partner's UX. Their call.

### Load

A stepped test against the test index at an ordinary hour (site Search load ~1.2 QPS),
70% id lookups and 30% id miss then title/artist:

| Step | Requests | Errors | Server p50 / p95 / max | Search p95 | Search QPS added |
| --- | --- | --- | --- | --- | --- |
| 5 req/s × 2 min | 601 | 0 | 10 / 37 / 93 ms | 23 ms | ~7 |
| 10 req/s × 2 min | 1,201 | 0 | 8 / 24 / 113 ms | 14 ms | ~14 |
| 20 req/s × 2 min | 2,401 | 0 | 8 / 22 / 203 ms | 14 ms | ~27 |

No throttling at any step, and **one 2 GB instance** served all of it. 20 req/s is about
twice the uncached peak for 10,000 subscribers. The test deliberately stopped there rather
than looking for the replica's ceiling, because test and prod indexes share the one replica.

### The remaining coupling: site overload on the shared replica

From per-minute metrics for 2026-09-29 to 2026-10-28, the `music4dance` service (Basic,
1 replica, 1 partition) averaged ~2.2 QPS and served ~4.2 million queries. It throttled in
**34 of 43,200 minutes**, an estimated **~4,100 queries (0.1%)**:

| Days (UTC) | Minutes throttled | Est. throttled queries | Share of the day's queries |
| --- | --- | --- | --- |
| Oct 3–5, 15–16, 23 | 11 | ~260 | ≤0.05% |
| Oct 24–25 | 23 | ~3,800 | 1.1–1.3% |

Oct 24–25 was a general site overload, and throttling of site traffic is being worked on
separately. Outside that event, throttling was a handful of isolated minutes.

Throttling is reported only as a percentage, so the counts are estimates: per-minute average
QPS × 60 × throttled %. Read the metric at 1-minute grain. An hourly Maximum makes a single
bad minute look like a bad hour (an earlier draft of this section read it that way), and a
portal Average over 30 days hides it almost entirely.

So partner traffic at 20 req/s is easily absorbed on a normal day, and partner availability is
tied mainly to site overload events on the shared replica. Throttling site traffic protects
both. A second replica is the option if partner revenue ever justifies it.

The per-minute `SearchQueriesPerSecond` metric doesn't line up exactly with known load windows,
so use the function's own search timings, not that metric, as the load guard.

### Bugs and corrections found

- **ISRC case.** Some ISRCs are stored lowercase in `ServiceIds` (e.g. `R:usdy41596491`), and
  `search.in` is case-sensitive, so 3 of 100 ISRC lookups missed. The spike queries both cases.
  The proper fix is normalizing to uppercase when the index (and the property log) is written.
- **Tempo units.** The index's `Tempo` is beats per minute, not measures per minute; see the
  response-field table above.
- **First-request cost.** The first Search call on an instance takes ~500 ms (token + TLS); see
  [Cold starts](#cold-starts).

### Spike resources

On 2026-11-01 the `m4d-api-test` environment, running the PR 2 resolve code, matched the
spike's quality run on the same 300-song sample. All 200 id lookups were exact. Title + artist
put the expected song first in 98 of 100 and rated all 98 `high`; the other two were rated
`low`.

So resource group `m4d-api-spike` (function app, storage, Application Insights, Log Analytics)
can be deleted in one step. Remove the spike app's index role assignment on `songs-test-3` at
the same time, or it's left pointing at a deleted identity. The alias `songs-api-test` stays;
the test environment uses it.

---

## Decisions so far

| Question | Decision |
| --- | --- |
| Service level | Best effort, no SLA, same as the website. Search replicas aren't a prerequisite. |
| Usage pattern | Live recognition, one track per call. Library import is a separate discussion. |
| Subscriber ids | Not sent. Settlement uses call volume or the partner's reporting. |
| Fuzzy matches | Returned as candidates with evidence (including length); the third-party app decides how to present them. |
| Client authentication | API keys, issued by the partner developer from a music4dance.net page, hashes in a dedicated Key Vault per environment. See [Authentication](#authentication-dont-route-token-issuance-through-the-web-app). |

## Open questions

1. **Duration.** Can the partner's recognition path supply track duration (e.g. from the Apple
   Music catalog)? Without it, length can't be used as evidence for fuzzy matches.
2. **Fuzzy thresholds.** The ±5 s / ±15 s length bands and text-match rules are starting
   guesses. Tune them against a sample of real recognitions before launch.
3. **Accepted-candidate feedback.** Would the partner report which fuzzy candidate a user
   accepted? That's the best signal for attaching ids to songs, but it's a write path, however
   narrow, and needs the same care as the voting API.
4. **Starting limits.** Do 20 req/s sustained, a burst of 50, and 100,000 calls/day suit the
   partner's launch plans? The spike showed 20 req/s is absorbed easily on a normal day, but
   the capacity section argues for ~5 req/s if the 20% carve-out is policy. Pick one number
   for both sections.
5. **Cold starts.** Startup warming only, or an always-ready instance as well? Depends on how
   the partner app handles a 2 s first lookup.

---

## Related

- [public-api-authorization.md](public-api-authorization.md): OAuth design and security checklist
- [search-index-versioning.md](../search/search-index-versioning.md): cutover runbook the alias
  step joins
- [song-search-service.md](../search/song-search-service.md): the in-app search layer this
  bypasses
- [hosting-and-identity.md](../infrastructure/hosting-and-identity.md): existing
  managed-identity RBAC pattern
- [Azure Functions Flex Consumption plan](https://learn.microsoft.com/azure/azure-functions/flex-consumption-plan)
- [Azure Functions pricing](https://azure.microsoft.com/pricing/details/functions/)
- [Azure AI Search index aliases](https://learn.microsoft.com/azure/search/search-how-to-alias)
- [APIM `rate-limit-by-key`](https://learn.microsoft.com/azure/api-management/rate-limit-by-key-policy)
  and [`quota-by-key`](https://learn.microsoft.com/azure/api-management/quota-by-key-policy):
  tier availability
