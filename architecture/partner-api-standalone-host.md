# Partner API: Standalone Host Independent of the Web App

**Status:** Proposed — planning only, nothing built

**Context:** DanzQ is tentatively moving to option 2 in the collaboration analysis. DanzQ sells
its own subscription, and Thorsten's backend calls music4dance server to server with a single
confidential credential. The per-user OAuth design in
[public-api-authorization.md](public-api-authorization.md) is no longer on the critical path.
This document asks whether that thinner API can run **outside the ASP.NET app**, so a
music4dance.net outage (deploy, memory pressure, attack mitigation, SQL trouble) doesn't take
DanzQ down with it.

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

- DanzQ lookups keep working when `msc4dnc` (the production App Service) is down, restarting,
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
- Per-subscriber identifiers. DanzQ won't send them; settlement uses call volume or DanzQ's
  own reporting.
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
| Song lookup by ISRC / Apple / Spotify id | `SongIndex.Search` with `ServiceIds/any(id: search.in(...))`, same as [SongController.cs:229](../m4d/Controllers/SongController.cs#L229) | Same OData filter sent from the function | ✅ |
| Fuzzy title + artist | `SongIndex.SongsFromTitleArtist`, [SongIndex.cs:1339](../m4dModels/SongIndex.cs#L1339) | Same query (`searchFields=Title,Artist`); ranking logic must be shared or ported | ✅ |
| Song fields in the response | `CreateSongs` decompresses `Properties` and replays the property log into `Song` | **Read the denormalized index fields directly** (below) | ✅ |
| Dance names | `Dances.Instance`, loaded by `DanceStatsManager` via `FileManager` | Bundle `dances.json` into the function and load it with `DanceLib`'s `Dances.Load` | ✅ |
| Index name | `SEARCHINDEX` / `SEARCHINDEXVERSION` + `appsettings.json` sections | A Search **index alias** (`songs-api-prod`) | ✅ |
| Client authentication | OpenIddict bearer (per-user tokens) | API key or Entra ID client credentials, **not** m4d-issued tokens | ✅ if designed that way |
| Metering / global cap | `RateLimitingMiddleware` (per IP, in memory) | In-function limiter + Table Storage counters | ✅ |
| Usage attribution | `UsageLog` in SQL | Table Storage aggregates | ✅ |
| Unmatched-id review queue | (proposed) SQL table | Storage queue or table, consumed by the web app when it's up | ✅ |

### The index already carries the response

`DocumentFromSong` ([SongIndex.cs:2135](../m4dModels/SongIndex.cs#L2135)) writes these top-level
fields: `SongId`, `Title`, `Artist`, `Length`, `Tempo`, `ServiceIds`, `TempoTags`, and a complex
field per dance, `dance_{ID}`, with `Votes`, `Tempo`, `TempoTags`, `StyleTags`, and `OtherTags`.
That covers the whole MVP response in
[public-api-authorization.md § API Surface](public-api-authorization.md#api-surface):

| Response field | Index source |
| --- | --- |
| `title`, `artist`, `length`, `tempo` (MPM) | Top-level fields |
| `meter` | Derived from `TempoTags` containing `4/4`, `3/4`, or `2/4`. This mirrors `Song.Meter` ([Song.cs:4682](../m4dModels/Song.cs#L4682)). |
| `danceRatings[].weight` | `dance_{ID}/Votes`. `-1` is the unconfirmed-only sentinel ([SongIndex.cs:2246](../m4dModels/SongIndex.cs#L2246)); exclude it. |
| `danceRatings[].tempo` | `dance_{ID}/Tempo` (already falls back to song tempo at index time) |
| `danceRatings[].tags` | `dance_{ID}/StyleTags` etc. |
| `match.method` | Which requested id appears in the returned `ServiceIds` |
| Fuzzy-match signals (`lengthSeconds`, etc.) | `Length` (seconds, already filterable for the site's length range), `Title`, `Artist`, search `@search.score` |
| `songUrl` | Built from `SongId`: the "View on music4dance" deep link from the collaboration analysis |

So the function never touches `Properties`, the compressed property log. That matters for
three reasons:

1. It removes the heaviest dependency: `m4dModels`, `DanceMusicService`, `DanceStats`, and EF.
2. `Properties`, `Users`, and `Comments` contain usernames and user text. A strict `$select`
   whitelist means they never leave the search service, which is a real privacy improvement
   over materializing `Song`.
3. The function is a few hundred lines, not a second copy of the domain model.

**Constraint this accepts:** the API contract is limited to what the index holds. A future
response field that exists only in the property log needs an index field first (additive, via
`AddMissingIndexFields`, per [individual-artists.md](individual-artists.md) §5).

---

## Hosting options

| Option | Fixed cost | Independence from app | Fit | Verdict |
| --- | --- | --- | --- | --- |
| **A. Azure Functions, Flex Consumption** (.NET 10 isolated) | ~$0 on demand; optional always-ready instance | Full: own plan, own identity, own hostname | C# and `DanceLib` reuse, managed identity to Search, scale to zero | ✅ **Recommended** |
| B. Azure Container Apps (consumption) | ~$0, scale to zero | Full | Same code as a minimal ASP.NET Core API in a container. More moving parts (registry, image builds). | Good fallback if Functions chafes |
| C. APIM Consumption, policy-only proxy to Search | ~$0 + per-call | Full | No code at all, but response shaping in policy XML is awkward. Consumption **lacks `rate-limit-by-key` and `quota-by-key`** (they need Basic v2+ at a real monthly cost). | ❌ Wrong tool at this scale |
| D. Give Thorsten a Search **query key** (optionally to a slim dedicated index) | $0 | Full | Zero compute. But his app couples to our Azure schema and OData; no metering, no response shaping; a full-index key exposes `Properties`/`Users`/`Comments`. | ❌ No contract, no control |
| E. Second App Service / slot running m4d | Plan cost | Partial: same code, same failure modes | Shields against platform problems but not app bugs or SQL outages | ❌ Doesn't solve the actual worry |
| F. Keep it in the web app (the current plan) | $0 | None | Simplest | Baseline for comparison |

### Why Flex Consumption

Checked against the current docs on 2026-09-29:

- It's Microsoft's recommended serverless plan. The older Linux Consumption plan gets no new
  features, and migrating to Flex means a new app, so start on Flex.
- It supports .NET 8, 9, and **10** in the isolated worker model, which matches the solution's
  `net10.0`.
- On-demand billing has a monthly free grant of GB-seconds and executions. Check the
  [pricing page](https://azure.microsoft.com/pricing/details/functions/) for the current
  numbers. At DanzQ MVP volumes (thousands to low hundreds of thousands of calls a month) it
  should cost close to nothing.
- **Cold starts** are the tradeoff. A scaled-to-zero .NET isolated app takes on the order of a
  second or two for its first request. Live recognition makes that visible, since a person is
  waiting on the result. Traffic will be bursty (evenings, weekends, dance events), so the
  first lookup after a quiet spell will be slow. If the spike shows it's noticeable, set **one
  always-ready instance** for the HTTP group. That's a small fixed monthly charge (always-ready has no free
  grant), still well under a Basic App Service plan.
- Its limits (one app per plan, no deployment slots, 30-second host start timeout) don't bite
  here. Rolling-update zero-downtime deploys are in preview; a partner API can tolerate a few
  seconds of deploy blip, and Thorsten's retry logic should expect 503s anyway.

---

## Authentication: don't route token issuance through the web app

The options doc suggested OpenIddict `client_credentials` for the server-to-server client. That
works when the API lives in the web app. **For a standalone host it defeats the purpose:**
the token endpoint is `/connect/token` on music4dance.net. With one-hour access tokens, a web
app outage longer than an hour (or any outage that hits a token refresh) takes the API down
anyway.

Two credential schemes that don't depend on the app:

| | **API key** (recommended to start) | **Entra ID client credentials** |
| --- | --- | --- |
| What Thorsten holds | A random 256-bit key, e.g. `m4d_live_…` | A client secret or certificate for an app registration in our tenant |
| Validation | Function hashes the presented key (SHA-256) and compares against stored hashes in constant time | Function validates Entra JWTs: issuer, audience, `roles` claim `Songs.Read`. Built-in App Service auth (Easy Auth) can do this before code runs. |
| Issuer availability | No issuer | Microsoft's identity platform (independent of m4d) |
| Rotation | Two active keys per client; add new, Thorsten switches, delete old | Standard secret/cert rollover |
| Revocation | Delete the hash; takes effect on next config refresh | Disable the app registration or remove the role assignment |
| Build cost | ~50 lines | Configuration, plus Thorsten learns MSAL |
| Standards story | Common for server-to-server APIs (Stripe-style); fine when the key never ships to devices | Full OAuth 2.0 client credentials |

**Recommendation:** start with API keys, stored as hashes in the function's app settings (with a
Key Vault reference if you want them out of plain config). Per-client records hold `clientId`,
`keyHashes[]`, `dailyCap`, and `status`. Move to Entra if a second partner arrives who prefers
it, or if you want the "no bespoke auth" argument from the OAuth doc to hold here too. Either
way the credential never goes in the iOS app; the options doc already covers why.

Header: `Authorization: Bearer m4d_live_…`, not a query string, so keys stay out of access
logs. Reject anything that looks like a key in the URL.

---

## Proposed architecture

```txt
 DanzQ iOS app
      │  (Apple subscription checked by DanzQ)
      ▼
 Thorsten's backend  ── caches results ──┐
      │ HTTPS, Authorization: Bearer m4d_live_…
      ▼
 api.music4dance.net  (custom domain, DNS straight to the function; not via the web app)
 ┌─────────────────────────────────────────────────────────┐
 │ m4d-api  (Azure Functions, Flex Consumption, .NET 10)    │
 │  • key check → per-client limiter → resolve             │
 │  • in-memory result cache (10 min hits, 1 min misses)   │
 │  • DanceLib + bundled dances.json for names             │
 └──────┬──────────────────────┬───────────────────────────┘
        │ managed identity      │ managed identity
        │ Search Index Data     │ Storage Table/Queue Data
        │ Reader only           │ Contributor
        ▼                       ▼
 Azure AI Search           Storage account (the function's own)
  alias songs-api-prod      • usage table: client/day/endpoint/method counts
   → songs-prod-3           • unmatched queue: ids we didn't find
                                   │
                                   ▼  drained when the web app is up
                            music4dance.net admin review
```

The web app appears only at the bottom right, as an optional consumer of the unmatched queue.
Nothing on the request path touches it.

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
`durationSeconds` if DanzQ's recognition path can supply it (Apple Music catalog data includes
duration, looked up by Apple Music id).

### Exact vs fuzzy matches: return evidence, let DanzQ decide

An id match and a title/artist match are different kinds of answer, and the response should
make that impossible to miss. The API **doesn't hide fuzzy matches and doesn't decide for the
user**. It returns candidates with the evidence it used, plus a suggested confidence level.
DanzQ decides whether to show a result, ask "Is this the song?", or say nothing.

```json
{
  "match": "exact",            // "exact" | "fuzzy" | "none"
  "method": "isrc",            // isrc | appleMusicId | spotifyId | titleArtist
  "candidates": [
    {
      "songId": "3f2a…",
      "songUrl": "https://www.music4dance.net/song/details?id=3f2a…",
      "title": "Blue Bayou",
      "artist": "Linda Ronstadt",
      "lengthSeconds": 231,
      "tempo": 44.5,
      "meter": "4/4",
      "danceRatings": [ … ],
      "evidence": {             // present only for fuzzy candidates
        "titleMatch": "normalized",   // exact | normalized | partial
        "artistMatch": "exact",       // exact | normalized | partial | none
        "lengthDeltaSeconds": 2,      // null when either side has no length
        "confidence": "high"          // server's suggestion: high | medium | low
      }
    }
  ]
}
```

- **Exact** returns one candidate. If several songs share the id (duplicates awaiting merge),
  return the one with the most confirmed dance votes (`dance_ALL/Votes`).
- **Fuzzy** returns up to 3 candidates, best first, each with `evidence`.
- **Length is the strongest tie-breaker.** Same title and artist but a different recording
  (live, extended mix, radio edit, re-recording) usually differs in length, and often in tempo,
  which is exactly what matters for dancing. Suggested rule: within ±5 s is supporting evidence;
  off by more than ~15 s pushes confidence to `low`, whatever the text match says. Tune these
  thresholds from real data.
- Remix and dance-edit titles (see `Dances.GetAllDanceWordsUpper`, which the merge code uses
  for remix detection) should lower confidence when the recognized title lacks the qualifier,
  or vice versa.
- The confidence levels are guidance and part of the contract only as "high means more likely
  than medium." The `evidence` fields are stable, so DanzQ can apply its own rule and we can
  change ours.

When the result is `fuzzy` or `none`, enqueue the recognized identifiers as unmatched (below).
Record the fuzzy candidate ids alongside the unmatched identifiers. A reviewer can then
attach the ISRC or Apple id to the right song in one step. If DanzQ ever reports which
candidate the user accepted, that becomes a much stronger signal (see open questions).

Always apply the confirmed-content cruft filter from `AddCruftInfo`
([SongIndex.cs:1529](../m4dModels/SongIndex.cs#L1529)): `DanceTags/any()` and
`(not DanceTags/any() or dance_ALL/Votes ne null)`. That way DanzQ never sees songs with no
dances or only unconfirmed ones.

### Index coupling: alias plus contract test

The function must follow index version cutovers
([search-index-versioning.md](search-index-versioning.md)) without redeploying and without
reading m4d's config.

- **Index alias.** Aliases are GA in the stable Search REST API and SDKs (verified 2026-09-29).
  Create `songs-api-prod → songs-prod-3` and `songs-api-test → songs-test-3`, and add a
  "repoint the API alias" step to the production migration runbook, after verification (Step
  3). Alias updates propagate within about 10 seconds, and Search refuses to delete an index
  an alias still points at, which is a useful guard against pulling the rug out.
- **Schema contract test.** The function selects a fixed field list. Add a test in
  `m4dModels.Tests` that calls `SongIndex.BuildIndexFields()` and asserts every field and
  subfield the API selects exists. It runs in CI-SERVER, so an index change that would break
  the API fails the web app's PR, not DanzQ in production.
- **Field-name constants.** Don't make the function reference `m4dModels`; that drags in EF and
  most of the app. Either copy the handful of names into the function with the contract test
  as the guard, or move them to a tiny shared file linked into both projects. Prefer the
  copy-plus-test; it's the smaller change.
- **Title and artist ranking.** Whatever normalization PR 4 would have used for fuzzy
  matching needs to live somewhere both can reach, most likely `DanceLib` or a new
  dependency-free library. Keep it pure (strings in, score out) so it moves easily.

### Metering, settlement data, and unmatched ids

All of this goes to the function's own Storage account, never SQL:

- **Per-client rate limit.** This is the main throttle. It's a token bucket in memory per
  instance, with a sustained rate and a burst size per client, read from configuration so
  they can change without a redeploy. See [Capacity](#capacity-and-throughput) for starting
  values.
- **Daily cap.** A daily counter per client in Table Storage, flushed every ~30 seconds. When
  the day's total passes the client's `dailyCap`, return `429` with `Retry-After`. Across
  instances it's accurate to within one flush interval, which is fine for a cap meant to catch
  runaway bugs.
- **Max instance count.** A backstop on cost and Search load, not the primary throttle.
- **Usage aggregates.** One row per client, day, and match method (`exact` / `fuzzy` / `none`),
  holding counts. This is the self-verifying volume number for a tiered-fee settlement. It has
  no sampling problem, unlike deriving counts from Application Insights. There are no
  per-subscriber ids; settlement uses these volumes or DanzQ's own sales reporting.
- **Unmatched identifiers.** Enqueue misses and fuzzy-only results, deduplicated per instance.
  The web app gets an admin page (later) that drains the queue into a review list. If the web
  app is down, the queue just grows. A Storage queue message lives 7 days by default, and
  that's configurable.

Storage failures must **fail open** on logging (serve the result, drop the counter increment)
and **fail closed** only on the cap check when the counter has been unreadable for long
enough to matter (say 5 minutes). A Storage blip shouldn't become a DanzQ outage.

---

## Capacity and throughput

These are estimates to plan with and to share with Thorsten. The pre-PR 1 spike should
replace the latency guesses with measurements.

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

| DanzQ subscribers | Peak lookups/s | Share of one 2 GB instance |
| --- | --- | --- |
| 1,000 | ~1 | ~1% |
| 10,000 | ~10 | ~12% |
| 100,000 | ~100 | more than one instance; Search capacity becomes the question |

Two effects push the real numbers lower:

- **Thorsten's cache.** Dance music repeats heavily; a popular song is looked up by many users.
  A shared cache keyed by ISRC or Apple id should absorb a large share of lookups.
- **Correlated bursts are cache-friendly.** At a dance event, many people in the same room
  recognize the *same* song within seconds, so the cache turns 50 lookups into one.

### Starting settings (all configurable)

| Setting | Start | Why |
| --- | --- | --- |
| Instance size | 2 GB | Faster cold start than 512 MB, which matters with a person waiting. At these volumes the cost difference is small; revisit after the spike. |
| Max instances | 2 | One instance covers ~10,000 subscribers; the second covers bursts and instance recycling. Raise to 4 if limits are raised. |
| Per-client rate limit | 20 req/s sustained, burst 50 | ~20× the expected peak at 1,000 subscribers; low enough that a runaway client can't swamp Search |
| Daily cap | 100,000 calls | Far above expected use (~1,000 subscribers × a few dozen lookups on a busy day); catches bugs and loops |

**Room to grow:** before raising limits, look at the search service's own metrics in the portal
(search queries per second, search latency, throttled search query percentage) over the last
30 days. That shows how much Search headroom the website leaves. If DanzQ ever needs more
than that headroom, the fix is Search capacity (a second replica or a higher tier), paid for
out of the partnership. Function capacity is effectively unlimited by comparison.

---

## Client expectations

What to put in front of Thorsten, and into the partner terms:

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
- **Fuzzy results are candidates, not answers.** DanzQ decides how to present them, using the
  `evidence` fields.
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
| Entra ID down (if chosen) | n/a | New tokens fail; cached tokens work until expiry |

The remaining single point of failure is **Azure Search**, and that's acceptable under best
effort. It's a managed service with a much better track record than a single web app
instance. Two notes for expectations rather than action:

- A single-replica search service is briefly unavailable during Microsoft's own maintenance.
  DanzQ's retry and cache behavior covers that.
- If the partnership grows enough to justify it, a second replica is the upgrade that improves
  availability for both DanzQ and the website. That's a business decision, not a prerequisite.

---

## Operations

- **Infrastructure as code.** Bicep for the Flex plan, function app, Storage account, and role
  assignments (Search Index Data Reader on `music4dance`, Storage data roles on its own
  account), under `m4d-api/infra/` or similar. One resource group per environment.
- **Environments.** `m4d-api-test` → alias `songs-api-test`, `m4d-api` → `songs-api-prod`. Give
  Thorsten a test key first.
- **Deployment.** Flex deploys a zip package to blob storage. Add a separate stage or pipeline
  so API deploys and web app deploys are independent; that independence is the point.
  `azure-pipelines.yml` today targets the web apps only.
- **Custom domain.** `api.music4dance.net` with a Flex site-scoped managed certificate. DNS
  points straight at the function, not through Front Door or the web app, so a Front Door
  misconfiguration can't take it down either. A versioned path (`/v1`) is the contract.
- **Monitoring.** Application Insights on the function, with an availability test against
  `/v1/health` from a couple of regions and alerts on 5xx rate, on p95 latency, and on a
  client reaching its daily cap.
- **Build hygiene.** The new project joins `music4dance.sln`, so the warning-clean
  `-warnaserror` CI build covers it.

---

## Effect on existing plans

- [public-api-authorization.md](public-api-authorization.md) **PR 1** (OpenIddict foundation,
  flag off) stays as is. It's the base for a later optional **account linking** phase, where
  DanzQ users connect an m4d account so their votes count. That phase genuinely needs the
  web app and can accept its availability.
- **PRs 2–4** for the DanzQ MVP are replaced by this plan. The resolve logic moves into the
  function instead of a `/v1` controller in `m4d`. Update that doc's "Current DanzQ Subscriber
  MVP" section once this direction is confirmed.
- The OAuth doc's security checklist still applies where relevant: bearer only, no secrets in
  query strings, per-client limits, no unbounded result sets, and no list-all endpoint.

---

## Implementation plan

| PR | Scope |
| --- | --- |
| **1. Skeleton + infra** | `m4d-api` Functions project (isolated, .NET 10), Bicep, `/v1/health`, test environment deployed, search alias for test |
| **2. Resolve** | Single-track id resolution, title/artist fallback with candidates and `evidence` (length delta, match quality, suggested confidence), projection from index fields, dance names from `DanceLib`, the index contract test in `m4dModels.Tests`, unit tests with a faked `SearchClient` |
| **3. Auth + limits** | API key validation, per-client config, rate limit, daily cap, usage table |
| **4. Unmatched queue** | Queue writes (misses and fuzzy-only results) in the function; admin drain and review page in `m4d` (can trail) |
| **5. Production** | Prod alias, custom domain and certificate, alerts, runbook step added to search-index-versioning.md, key handed to Thorsten |

Before PR 1, a short spike is worth doing: deploy a minimal Flex function with managed identity
against the test search service, then measure:

- Cold-start latency on a real resolve query, which decides whether an always-ready instance
  is needed.
- Warm latency for id and title/artist queries, which replaces the estimates in
  [Capacity](#capacity-and-throughput).
- A short load test (e.g. 50 req/s for a few minutes) against the **test** index, watching
  Search latency and throttling.

---

## Decisions so far

| Question | Decision |
| --- | --- |
| Service level | Best effort, no SLA, same as the website. Search replicas aren't a prerequisite. |
| Usage pattern | Live recognition, one track per call. Library import is a separate discussion. |
| Subscriber ids | Not sent. Settlement uses call volume or DanzQ's reporting. |
| Fuzzy matches | Returned as candidates with evidence (including length); DanzQ decides how to present them. |

## Open questions

1. **API key vs Entra** for Thorsten. Does he have a preference, or existing Azure/Entra
   tooling?
2. **Duration.** Can DanzQ's recognition path supply track duration (e.g. from the Apple Music
   catalog)? Without it, length can't be used as evidence for fuzzy matches.
3. **Fuzzy thresholds.** The ±5 s / ±15 s length bands and text-match rules are starting
   guesses. Tune them against a sample of real recognitions before launch.
4. **Accepted-candidate feedback.** Would DanzQ report which fuzzy candidate a user accepted?
   That's the best signal for attaching ids to songs, but it's a write path, however narrow,
   and needs the same care as the voting API.
5. **Starting limits.** Do 20 req/s sustained, a burst of 50, and 100,000 calls/day suit
   Thorsten's launch plans?

---

## References

- [Azure Functions Flex Consumption plan](https://learn.microsoft.com/azure/azure-functions/flex-consumption-plan)
- [Azure Functions pricing](https://azure.microsoft.com/pricing/details/functions/)
- [Azure AI Search index aliases](https://learn.microsoft.com/azure/search/search-how-to-alias)
- [APIM `rate-limit-by-key`](https://learn.microsoft.com/azure/api-management/rate-limit-by-key-policy)
  and [`quota-by-key`](https://learn.microsoft.com/azure/api-management/quota-by-key-policy):
  tier availability
- [public-api-authorization.md](public-api-authorization.md): OAuth design and security checklist
- [search-index-versioning.md](search-index-versioning.md): cutover runbook the alias step joins
- [song-search-service.md](song-search-service.md): the in-app search layer this bypasses
- [azure-app-service-setup-managed-identity.md](azure-app-service-setup-managed-identity.md):
  existing managed-identity RBAC pattern for Search
