# Public API & Third-Party Authorization

**Status:** Foundation implemented behind a disabled feature flag; later slices remain proposed

**Context:** An independent iOS developer (DanzQ) has offered to contribute a token
mechanism so their app can look up songs and show matching dances, without asking users
to type their music4dance password into a third-party app. This document generalizes that
request into a design that can onboard any number of third-party developers.

---

## Current DanzQ Subscriber MVP

Issue [#253](https://github.com/music4dance/music4dance/issues/253) narrows the first
delivery to read-only access for existing subscribers. This section records the current
contract and takes precedence over the broader trial and write-API ideas later in this
document.

- OpenIddict uses Authorization Code with PKCE for the public native client. DanzQ has no
  client secret and uses the exact redirect URI `com.domke.danzq:/oauth/callback`.
- The client requests `account:read`, `songs:read`, and `offline_access`. It treats access
  tokens as opaque. The MVP does not request `openid` or use an `id_token`.
- OpenIddict's conventional `/connect/*` paths are used for the authorization flow.
- The API uses a dedicated bearer scheme. Site cookies must not authorize `/v1/*`.
- The first API consists of `/v1/me` and `POST /v1/songs/resolve`. There is no
  `/v1/dances`, trial grant, voting, or general song-search surface in this MVP.
- Subscriber entitlement is evaluated from current database state. The exact treatment of
  manual roles and expiry remains open and is isolated behind one authorization policy.
- Client attribution in `UsageLog` belongs with subscriber enforcement. Property-log
  attribution is required only before the first API write and does not block this read-only
  slice.

### Foundation implementation

The foundation adds the four standard OpenIddict EF Core tables, a DanzQ client descriptor,
the validation bearer scheme, read-scope constants, and a fail-closed subscriber policy
requirement. `FeatureManagement:PublicApi` is false in every checked-in configuration.
It is a global startup switch: filter-based definitions and variants are not supported, and
changes take effect only after a restart. When enabled in Development or Staging, the DanzQ
registration is created or updated at startup and temporary signing and encryption keys are
used. The ASP.NET Core server integration exposes discovery metadata and public signing
keys over HTTPS, and validates requests at `/connect/authorize`, `/connect/token`, and
`/connect/revocation`. Invalid requests receive OAuth errors. A valid authorization request
returns `temporarily_unavailable` to the validated callback without issuing a code. PR 2
must replace that temporary rejection with sign-in and consent handling, and implement and
test token issuance, refresh, and revocation before the flow is usable by a client.
Enabling the feature in Production fails until durable keys are configured. It is also
rejected whenever `PROD_DB` is set. No `/v1/*` endpoint is mapped by the foundation.

---

## Executive Summary

Music4dance should expose a **versioned public API** protected by **OAuth 2.0 Authorization
Code flow with PKCE**, issuing **revocable tokens**, with metering designed to convert
trial users into subscribers quickly.

Two metering models are specified below. **Model A** (the contributor's proposal — a small
*lifetime* free allowance, then a paid account) is the recommended default. **Model B** adds
an intermediate free-registered tier. The design deliberately makes the choice **a
configuration decision rather than an architectural one**, so it can be changed — or A/B
tested — without a schema or code change.

Three properties drive the whole design:

1. **The password never leaves music4dance.** The user authenticates on our site in a
   system browser; the app only ever sees a token.
2. **The token identifies the user, not their entitlement.** Subscription state is looked
   up on every request, so a lapsed subscription downgrades immediately with no token
   reissue. This matches the existing nightly job in
   [RecomputeController.cs:87](../m4d/APIControllers/RecomputeController.cs#L87) that strips
   `PremiumRole` when `SubscriptionEnd` passes.
3. **Every request is attributable and revocable** — by user, by app, or by both.

---

## What Exists Today (and why it isn't enough)

| Asset | Location | Verdict |
| --- | --- | --- |
| `TokenAuthorization` policy | [TokenRequirement.cs](../m4d/Utilities/TokenRequirement.cs) | **Not reusable.** A single shared secret from config compared against a base64 header, held in a `static` field. Fine for an internal cron job; no concept of a user, no per-client revocation, no expiry. |
| `/api/*` controllers | [m4d/APIControllers/](../m4d/APIControllers/) | **Not reusable as-is.** Every one is `[ValidateAntiForgeryToken]` — first-party endpoints for our Vue frontend, authenticated by session cookie. A third-party app cannot supply an antiforgery token. |
| `POST /api/song/` | [SongController.cs:173](../m4d/APIControllers/SongController.cs#L173) | **Must not be exposed.** Accepts an arbitrary `SongHistory` of property deltas into `CreateOrMergeSong` — full edit power. The voting API below is deliberately far narrower. |
| Subscription model | `ApplicationUser.SubscriptionLevel` / `SubscriptionEnd`, `PremiumRole` | **Reusable directly.** Paid tier is `IsInRole(PremiumRole) \|\| IsInRole(TrialRole)`, the same test as [ContentController.cs:55](../m4d/Controllers/ContentController.cs#L55). |
| ISRC + iTunes service IDs | [ISRCService.cs](../m4dModels/ISRCService.cs), [ITunesService.cs](../m4dModels/ITunesService.cs) | **Reusable and important** — see [Song Resolution](#song-resolution-isrc-and-apple-music-ids). |
| `TryGetCappedDelta` | [Song.cs:4358](../m4dModels/Song.cs#L4358) | **Reusable — and it is what makes a write API safe.** Enforces ±1 per user per dance. |
| `RateLimitingMiddleware` | [RateLimitingMiddleware.cs](../m4d/Middleware/RateLimitingMiddleware.cs) | **Reusable with changes.** Keys on IP; API traffic must key on token/client. |
| `UsageLog` | [UsageLog.cs](../m4dModels/UsageLog.cs) | **Extend** with client attribution. |

**Key decision:** the public API gets its own route prefix (`/v1/…`), its own authentication
scheme, and its own controllers. It does not share the `/api/*` surface.

---

## Protocol Choice

### OAuth 2.0 Authorization Code + PKCE

The settled answer for "native app, user authorizes once, revocable credential, no password
sharing." What Apple, Google, and Spotify all use, and what App Store reviewers expect.

**Do not** accept a bespoke token scheme. Custom auth is where security bugs live, and this
is the one part of the system where a bug is a breach.

### PKCE has no relationship to deployment topology

Correcting the earlier draft, which muddied this: **PKCE is entirely orthogonal to how many
instances we run.** It solves a problem that lives on the *device* — on mobile, the redirect
back to the app (`com.domke.danzq:/oauth/callback`) can in principle be intercepted by another app
that registered the same URI scheme. PKCE means an intercepted authorization code is
useless without the `code_verifier`, which never left the legitimate app.

That is a client-side threat model. It would be exactly as necessary with a hundred
instances behind a load balancer as with one. **No dependency created.**

### Token format: opaque vs. JWT

The earlier draft listed single-instance deployment as a reason to prefer opaque reference
tokens. That was a weak argument and worth retracting: **reference tokens do not depend on
single-instance either.** They need a *shared token store*, which is the SQL database we
already share. Multi-instance reference tokens are what every large OAuth provider runs.

The reasons that actually stand, independent of topology:

- **Revocation is the contributor's own stated goal.** A self-contained JWT stays valid
  until it expires regardless of what we do; a reference token dies the moment the row is
  marked revoked.
- **We query the database anyway** to check `PremiumRole` per request, so the JWT saves no
  round trip. Its main selling point doesn't apply here.

If the site ever scales out, the mitigation is a short-TTL per-instance cache with bounded
revocation lag — which is precisely the tradeoff JWTs force on you anyway, except with a
JWT you can't opt out of it.

### Client-visible account state

Access tokens are opaque to the client by contract, regardless of their server-side format.
A client that needs signed identity claims would normally use OpenID Connect and an
`id_token`; OpenIddict supports that option.

DanzQ's subscriber MVP does not need an identity token. It requests no `openid` scope and
obtains current account and entitlement state from `/v1/me`, avoiding stale subscription
claims. OpenID Connect remains available if a future client has a genuine identity-token
requirement.

### Reference documentation

| Spec | What it gives you |
| --- | --- |
| [RFC 6749](https://datatracker.ietf.org/doc/html/rfc6749) | OAuth 2.0 core — Authorization Code grant |
| [RFC 7636](https://datatracker.ietf.org/doc/html/rfc7636) | **PKCE** — mandatory here |
| [RFC 8252](https://datatracker.ietf.org/doc/html/rfc8252) | **OAuth for Native Apps** (BCP 212) — requires an external user-agent, never an embedded webview |
| [OpenID Connect Core](https://openid.net/specs/openid-connect-core-1_0.html) | `id_token` for a future client that needs identity claims |
| [RFC 6750](https://datatracker.ietf.org/doc/html/rfc6750) | Bearer token usage |
| [RFC 7009](https://datatracker.ietf.org/doc/html/rfc7009) | Token revocation endpoint |
| [RFC 8414](https://datatracker.ietf.org/doc/html/rfc8414) | AS metadata — the `/.well-known` doc that lets a new developer self-configure |
| [RFC 9700](https://datatracker.ietf.org/doc/html/rfc9700) | **OAuth Security BCP** (BCP 240, Jan 2025) — the review checklist |
| [RFC 9068](https://datatracker.ietf.org/doc/html/rfc9068) | JWT profile for access tokens, if we ever go that way |

Apple-side: [`ASWebAuthenticationSession`](https://developer.apple.com/documentation/authenticationservices/aswebauthenticationsession)
for the browser surface, and [DeviceCheck / App Attest](https://developer.apple.com/documentation/devicecheck)
for the trial metering discussed next.

### Implementation: OpenIddict

| Option | Assessment |
| --- | --- |
| **OpenIddict** ✅ | MIT licensed, ASP.NET Core native, EF Core storage, layers onto the `AddDefaultIdentity<ApplicationUser>()` already in [Program.cs:435](../m4d/Program.cs#L435). Supports OIDC out of the box. **Recommended.** |
| Duende IdentityServer | Commercial licensing — the cost concern raised is well founded, and it's the reason OpenIddict is the recommendation. |
| Hand-rolled | We'd own PKCE verification, redirect-URI matching, code replay prevention, and rotation correctness forever. **Not recommended.** |

---

## Access Tiers and Metering

### The conversion goal drives the design

The stated goal is to move people into a paid account quickly if they find the app useful.
That argues for allowances small enough that an engaged user hits the wall in the first
session or two — while they still remember why they wanted it.

### Model A — Lifetime trial, then paid (the contributor's proposal, recommended)

| Tier | Who | Allowance | Data |
| --- | --- | --- | --- |
| **Trial** | Anyone, no account | **~15 lookups, lifetime** | Reduced: top 3 dance matches |
| **Subscriber** | Paid m4d account | Unmetered (fair-use ceiling) | Full |

Simple to explain, simple to build, and the strongest conversion pressure. An engaged user
exhausts 15 lookups in one sitting and gets a single clear ask.

**The risk to watch:** a hard paywall at lookup 15, for someone who installed a free app
ten minutes ago, is a steep first ask. Model A's conversion rate is worth measuring rather
than assuming.

### Model B — Lifetime trial, free account, then paid

| Tier | Who | Allowance | Data |
| --- | --- | --- | --- |
| **Trial** | Anyone, no account | **~15 lookups, lifetime** | Reduced: top 3 dance matches |
| **Free account** | Registered m4d user | **~10 lookups/day** | Standard, no premium fields |
| **Subscriber** | Paid m4d account | Unmetered (fair-use ceiling) | Full |

The middle tier is a conversion *leak* against the stated goal — but it buys something real:
an email address and a user record, which drops the person into the engagement funnel
already built in [visitor-engagement-monetization.md](visitor-engagement-monetization.md).
Registration is itself a conversion step; Model A skips it entirely.

A middle option worth considering: keep the free-registered tier but make it **time-boxed**
(a 14-day trial allowance rather than a perpetual daily one), so it captures the email
without becoming a permanent free ride.

### Recommendation: build for both, ship Model A

Do not hardcode the tier count. Express metering as a policy table:

```csharp
public class ApiTierPolicy
{
    public string Name { get; set; }            // "trial", "free", "subscriber"
    public int Allowance { get; set; }          // count
    public AllowancePeriod Period { get; set; } // Lifetime, Daily, Monthly
    public PayloadShape Payload { get; set; }   // Reduced, Standard, Full
    public bool RequiresAccount { get; set; }
    public bool RequiresSubscription { get; set; }
}
```

Seed it from configuration. Switching Model A → Model B, or retuning 15 → 8, becomes a
config change. Given that the right numbers here are genuinely unknown until real users hit
them, **the ability to retune without a deploy is worth more than picking correctly now.**

### Enforcing a *lifetime* cap is much harder than a daily one

This deserves emphasis because it's the one place Model A is materially harder to build.

**A daily cap is self-healing.** Someone who defeats it gains one extra day's allowance;
tomorrow the bucket resets for everyone anyway. Evasion is barely worth the effort.

**A lifetime cap is not.** If the cap is 15-lookups-forever and it's keyed on something the
user can reset, a reinstall loop yields *unlimited* free access. Device persistence stops
being a nice-to-have and becomes the load-bearing element.

Mechanisms, weakest to strongest:

1. **`UserDefaults` / app storage — useless here.** Wiped on app deletion. Reinstall resets
   the counter.
2. **iOS Keychain — the correct local store.** Keychain items *survive app deletion* by
   default, unlike `UserDefaults`. With iCloud Keychain sync they follow the user across
   devices. Defeated by a full device wipe or a deliberate Keychain reset, but it handles
   the accidental and casual cases correctly.
3. **Apple DeviceCheck — the right answer, and purpose-built for this.** DeviceCheck gives
   two bits of per-device storage held *on Apple's servers*, scoped to the developer's team,
   which **persist across app deletion and device reset**. Apple documents this for exactly
   our use case: identifying devices that have already taken a promotional offer. One bit
   is "trial consumed." This is the mechanism to specify.
4. **App Attest** — proves the request came from a genuine, unmodified build on real
   hardware. Complements DeviceCheck (which answers *"has this device already tried?"*)
   by answering *"is this a real client at all?"*. Add if abuse appears.

**Design note:** DeviceCheck is per-Apple-developer-team, so the *client app* owns those
bits, not us. That means the trial-consumed check is partly enforced on the developer's
side — which is fine for honest clients but not a security boundary. Our backstop stays a
**hard per-client daily ceiling**: regardless of how many device identities appear,
`danzq-ios` gets N anonymous calls per day total. Worst case is bounded and known, and the
ceiling doubles as the abuse alarm.

Combined with the reduced Tier-0 payload (top 3 dances, no tags or tempo detail), the value
of defeating the cap stays low even when it's possible.

---

## User Experience

### Flow A — Trial (no registration)

```plaintext
User installs DanzQ
  │
  ├─> First launch: device identity established
  │     ├─> Keychain: persistent install UUID (survives reinstall)
  │     └─> DeviceCheck: query "trial consumed" bit
  │   └─> POST /connect/token
  │         grant_type=urn:m4d:params:oauth:grant-type:anonymous
  │         client_id=danzq-ios & device_id=<uuid>
  │       → { access_token, scope: "songs:read", tier: "trial",
  │           allowance_remaining: 15 }
  │
  ├─> User Shazams a track → POST /v1/songs/resolve
  │   └─> Top 3 dance matches. No tags, no per-dance tempo detail.
  │       X-M4D-Allowance-Remaining: 14
  │
  └─> Allowance exhausted
      └─> "You've used your 15 free lookups. Subscribe to music4dance to
           keep identifying dances." → Flow B
```

### Flow B — Connecting an m4d account

```plaintext
User taps "Connect music4dance account"
  │
  ├─> App generates code_verifier + code_challenge (PKCE)
  ├─> App opens ASWebAuthenticationSession →
  │     https://www.music4dance.net/connect/authorize
  │       ?client_id=danzq-ios&redirect_uri=com.domke.danzq:/oauth/callback
  │       &response_type=code&scope=account:read+songs:read+offline_access
  │       &code_challenge=<S256>&code_challenge_method=S256&state=<nonce>
  │
  ├─> [music4dance.net — real browser, real URL bar]
  │     ├─> Not signed in? → m4d login, incl. "Create an account"
  │     │     └─> sign-up and subscription both happen in our funnel
  │     └─> Consent screen:
  │           "DanzQ wants to:
  │              • Look up songs and dance matches
  │              • See your username and subscription status
  │            DanzQ cannot see your password or change your data.
  │            Disconnect any time from Account → Connected Apps."
  │
  ├─> Allow → 302 com.domke.danzq:/oauth/callback?code=<one-time>&state=<nonce>
  │
  ├─> App: POST /connect/token (code + code_verifier)
  │       → { access_token (opaque), refresh_token }
  │
  └─> App shows "Connected as dwgray · Premium until 2027-03-14"
```

The user types their password only on our domain, in a browser they can inspect — exactly
the property the developer asked for, and the one Apple looks for.

### Flow C — Ongoing use and revocation

- Access tokens expire hourly. PR 2 must define and protocol-test refresh-token rotation and
  replay handling before token issuance is enabled.
- User revokes at **Account → Connected Apps**: app name, connection date, last used,
  scopes, `[Disconnect]`.
- App revokes on sign-out via `POST /connect/revocation`.
- We revoke a whole client from the admin area — every token for that `client_id` dies.

### Upgrade prompt

```json
{
  "error": "subscription_required",
  "message": "You've used all 15 free lookups.",
  "upgrade_url": "https://www.music4dance.net/commerce/subscribe?ref=danzq-ios"
}
```

The `ref` attributes the conversion, which is what makes supporting third-party apps
worthwhile. **Raise Apple's IAP rules with the developer early** — they constrain how an app
may present an external subscription link, and it's better to learn the constraints before
the flow is built than after.

---

## Song Resolution: ISRC and Apple Music IDs

Confirmed in the codebase, and the answer to whether we can look up by ISRC is **yes**:

- **ISRC is already a first-class pseudo-service.** `ServiceType.ISRC`, prefix `R:`, stored
  in the Azure Search `ServiceIds` field as `R:USRC17607839`
  ([ISRCService.cs](../m4dModels/ISRCService.cs)).
- **Apple/iTunes track IDs are stored too.** `ServiceType.ITunes`, prefix `I:`
  ([ITunesService.cs](../m4dModels/ITunesService.cs)).
- **There is working precedent for exactly this lookup.**
  [SongController.cs:202-247](../m4d/Controllers/SongController.cs#L202) already falls back
  to an ISRC search via `ServiceIds/any(id: search.in(id, 'R:…'))` when a direct Spotify-id
  match misses. The API can reuse that pattern rather than inventing one.

### The coverage caveat that shapes the design

Per the comments in `ISRCService.cs`, **ISRCs are populated only from Spotify track
metadata** — there is no standalone ISRC search API. So ISRC coverage is *partial and skewed
toward Spotify-matched songs*. An ISRC-only lookup will miss songs we genuinely have.

This makes the fallback chain essential rather than optional:

```txt
POST /v1/songs/resolve

  1. isrc          → ServiceIds "R:{isrc}"     exact   ─┐
  2. appleMusicId  → ServiceIds "I:{id}"       exact    ├─ high confidence
  3. spotifyId     → ServiceIds "S:{id}"       exact   ─┘
  4. title+artist  → SongsFromTitleArtist      fuzzy   ─── lower confidence
```

Return which method matched, so the client can render confidence honestly:

```json
{
  "match": { "method": "isrc", "confidence": "exact" },
  "song": { "songId": "3f2a…", "title": "Blue Bayou", … }
}
```

### The Shazam angle

There are **no Shazam references anywhere in the codebase**, so this is about the app's
recognition path, not an existing import. ShazamKit's `SHMediaItem` returns an Apple Music
ID and generally an ISRC — worth confirming with the developer exactly which identifiers his
recognition path yields, since that determines how much of the cascade above actually gets
used in practice.

**A genuine value exchange worth proposing:** when the app resolves a track via Shazam and
we *miss*, that's information we don't have — an ISRC or Apple Music ID for a recording
absent from our database, or present but unlinked. Logging unmatched identifiers (with no
write access required) would improve the database as a side effect of the integration. That
reframes the API from pure cost to a two-way trade, and it's a good thing to raise while
goodwill is high.

---

## API Surface

```txt
GET   /v1/me                 Current account and subscriber entitlement
POST  /v1/songs/resolve      Read-only resolution from recognition metadata
```

Generic search, song details, and a dance catalog are possible later additions, not part of
the DanzQ subscriber MVP.

Example subscriber response, using real field names from [Song.cs](../m4dModels/Song.cs)
and [DanceRating.cs](../m4dModels/DanceRating.cs):

```json
{
  "songId": "3f2a…",
  "title": "Blue Bayou",
  "artist": "Linda Ronstadt",
  "tempo": 44.5,
  "meter": "4/4",
  "length": 231,
  "danceRatings": [
    { "danceId": "WCS", "danceName": "West Coast Swing",
      "weight": 12, "tempo": 44.5, "tags": ["Smooth:Style"] },
    { "danceId": "RMB", "danceName": "Rumba", "weight": 3 }
  ]
}
```

Tempo is MPM, consistent with the domain. Trial tier omits `tags` and per-rating `tempo`,
and truncates `danceRatings` to 3.

### The API must not accept cookies

```csharp
[Authorize(Policy = PublicApiDefaults.SubscriberPolicy)]
```

If `/v1/*` also accepted the site session cookie, every CSRF concern that
`[ValidateAntiForgeryToken]` currently handles on `/api/*` would reappear on endpoints with
no antiforgery protection. Bearer only. Each endpoint must additionally enforce its required
scope.

---

## Dance Voting Write API (fast follow)

Appealing, and **safer than it first sounds** — because the abuse ceiling already exists in
the domain model.

### The existing cap is the security argument

[`TryGetCappedDelta`](../m4dModels/Song.cs#L4358) enforces **±1 per user, per dance**, applied
during property-log replay. A fully malicious client holding a legitimate user token still
cannot move a dance rating by more than ±1 for that account. The blast radius is
structurally bounded by logic that already exists and is already tested — we are not
inventing a trust model, we're exposing one.

### Endpoint shape: narrow and idempotent

**Do not expose `POST /api/song/`.** It takes an arbitrary `SongHistory` of property deltas
into `CreateOrMergeSong` — full edit power over any field. A third-party client must never
be able to express that.

Instead, a single-purpose endpoint where the server constructs the `DanceRatingDelta`:

```txt
PUT    /v1/songs/{songId}/votes/{danceId}     body: { "vote": 1 }   // 1 | -1
DELETE /v1/songs/{songId}/votes/{danceId}                           // retract
GET    /v1/songs/{songId}/votes                                     // this user's votes
```

`PUT` with a target value rather than `POST` with a delta, deliberately: it is **naturally
idempotent**. Mobile networks retry, and a retried `PUT` of "my vote is +1" is harmless,
where a retried `POST` of "+1" is a double vote. The ±1 cap would absorb it anyway, but
designing the retry problem away is better than relying on a backstop.

The client can express exactly two things: a dance and a direction. Nothing else.

### Scope and tier

- New scope **`dances:vote`**, requested separately, listed separately on the consent screen,
  **not** granted by default. A read-only client never holds write capability.
- **Any authenticated m4d account may vote** — not subscriber-gated. Votes are contributions;
  restricting them to paying users reduces the data we get, which is backwards. (Flagging
  this as a choice rather than assuming it, since it differs slightly from "tied to a paid
  account.")
- Never available to the trial tier — an anonymous device token cannot vote.

### The design question to settle *before* shipping

`s_unconfirmedVoteSources` ([unconfirmed-dance-votes.md](unconfirmed-dance-votes.md)) is
keyed by **username**. Votes arriving through the API come from real user accounts, so
there is currently **no way to distinguish an API-sourced vote from a website vote** without
marking that user's votes wholesale.

If we want API votes distinguishable — for trust weighting, for bulk rollback if a client
misbehaves, or simply for analytics — the property log needs a marker recording the
originating client.

**This decision is effectively irreversible**, because the property log is append-only
history: votes recorded without client attribution can never have it added retroactively.
Decide before the first API vote is written, not after. Recommendation: **record the client
ID from the start**, even if nothing reads it initially. It is cheap now and impossible later.

### Why this is a separate phase

Agreed on reviewing it separately. Beyond the review burden, it's the first time a
third-party client can *mutate* community data, and it should ship only after the read API
has demonstrated the client behaves well. The phasing is also a natural trust ladder for
onboarding future developers: read access first, write access on request.

---

## Identity Provider Strategy

Broader than this project, and worth a straight answer: **don't couple an identity migration
to the third-party API.** But the interest is well-founded, and this project moves toward it
rather than away.

### What is actually load-bearing about `ApplicationUser`

The coupling is deeper than a typical Identity install:

- **Domain fields on the user record** — `SubscriptionLevel`, `SubscriptionEnd`, `Region`,
  `Privacy`, `ServicePreference`, `ColumnDefaults`, `HitCount`, `LifetimePurchased`.
- **Foreign keys** from `Searches`, `ActivityLog`, `UsageLog`, `PlayList`.
- **Usernames embedded in song data.** The song property log stores `User=dwgray` inline
  ([ModifiedRecord.cs](../m4dModels/ModifiedRecord.cs)), inside compressed Azure Search
  documents, replayed on every song materialization to compute vote attribution and the ±1
  cap. These are immutable historical records, not a table you can rewrite.
- **Roles** (`premium`, `trial`, `canEdit`, `canTag`, `dbAdmin`, …) drive authorization
  throughout.
- **Legacy password hashes** — `PasswordHasherCompatibilityMode.IdentityV2`
  ([Program.cs:510](../m4d/Program.cs#L510)).
- **Three external providers already federated** — Google, Facebook, Spotify.

The conclusion that follows: **the local user table cannot be removed.** Whatever provider
handles credentials, `ApplicationUser` survives as the profile and attribution record.

So what's genuinely on offer is moving *credential handling* — passwords, MFA, recovery,
social federation — out of the app. That's real value, but it's a fraction of the surface,
and the social-federation part is already done.

### Options, if evaluated separately

| Option | Notes |
| --- | --- |
| **Microsoft Entra External ID** | Natural first look given Azure hosting; generous free MAU tier (verify current terms). Consolidates billing and identity in one cloud. |
| **Auth0 / Okta** | Excellent DX, most mature. Pricing escalates with MAU — verify against your user count before falling for the free tier. |
| **Clerk / Stytch / WorkOS** | Modern DX, strong prebuilt UI. Less .NET-native; more work to reconcile with ASP.NET Core Identity. |
| **Keycloak / FusionAuth / Logto** | Open source, self-hostable, no per-MAU cost. Trades licensing cost for operational cost — you now run an identity server. |
| **Status quo + OpenIddict** ✅ | Keep Identity, add an OAuth/OIDC server on top. **Recommended for now.** |

### Why this project helps rather than hinders

The useful insight: **becoming an OAuth/OIDC authorization server creates exactly the seam a
future migration needs.** Once third-party clients authenticate against *our* `/connect/*`
endpoints, the credential backend behind those endpoints is an implementation detail. Swap
it later and no client notices — that is the entire point of the abstraction.

Building on OpenIddict is a step *toward* offloading identity, not a commitment against it.

### Recommendation

Keep them separate. Two hard things at once, one of which is a security-critical migration
of every existing user's credentials, is how outages happen. Ship the API on OpenIddict;
evaluate Entra External ID as its own project with its own risk budget, once the OAuth seam
exists and has proven itself.

---

## Data Model

Let OpenIddict own the OAuth tables (`OpenIddictApplications`, `OpenIddictAuthorizations`,
`OpenIddictTokens`, `OpenIddictScopes`). Add m4d-owned tables for what it has no opinion on:

```csharp
public class ApiClientProfile
{
    public int Id { get; set; }
    public string ClientId { get; set; }        // matches OpenIddict application
    public string DisplayName { get; set; }     // "DanzQ" — shown on consent screen
    public string DeveloperUserId { get; set; } // FK to ApplicationUser
    public string DeveloperEmail { get; set; }
    public string HomepageUrl { get; set; }
    public ApiClientStatus Status { get; set; } // Pending, Approved, Suspended, Revoked
    public string TierPolicySet { get; set; }   // which ApiTierPolicy set applies
    public bool AllowVoting { get; set; }       // dances:vote grantable per client
    public DateTime Created { get; set; }
    public DateTime? ApprovedDate { get; set; }
    public string Notes { get; set; }           // admin-only
}

public class ApiDeviceAllowance                 // lifetime trial tracking
{
    public long Id { get; set; }
    public string ClientId { get; set; }
    public string DeviceId { get; set; }        // hashed
    public int Consumed { get; set; }
    public DateTime FirstSeen { get; set; }
    public DateTime LastSeen { get; set; }
}
```

Per project convention: no nullable reference type annotations — check null explicitly.

Extend `UsageLog` with a nullable `ClientId` so API traffic is separable from site traffic in
the existing analytics ([usage-log-analysis-plan.md](usage-log-analysis-plan.md)).

---

## Onboarding Other Developers

**Registration** — a logged-in user registers an app at `/developers` (name, homepage,
redirect URIs, contact) and receives a `client_id` in `Pending` state. Public clients get no
secret; correct and expected for mobile under RFC 8252.

**Approval** — manual from the admin area while volume is low. The gate is where you verify
the app is real, redirect URIs are sane, and terms are accepted.

**Terms of use** — publish before the first external client ships:

- Attribution: "Powered by music4dance," visible in the app, linked
- No bulk extraction, redistribution, or resale of the song database
- Caching permitted for reasonable periods; no permanent local mirrors
- Quota limits and our revocation rights, stated plainly
- Contact obligation so we can reach the developer about breaking changes

The database is community-contributed. Terms are how the API doesn't quietly become an
export pipe.

**Versioning** — once an app ships, `/v1` is a contract. Add fields freely; never remove or
retype one. Breaking changes get `/v2` plus a deprecation window announced by email.

**Trust ladder** — read access on approval; `dances:vote` granted per client
(`AllowVoting`) after the client has demonstrated good behavior.

---

## Security Requirements

From RFC 9700 / RFC 8252:

- [ ] PKCE `S256` required on every authorization request; `plain` rejected
- [ ] Exact redirect-URI string matching — no prefix or wildcard matching
- [ ] Authorization codes single-use, ≤60s TTL, bound to `client_id` + `code_verifier`
- [ ] `state` required and verified
- [ ] Implicit and password grants **not** enabled
- [ ] Refresh token rotation with replay detection (reuse revokes the chain)
- [ ] Access tokens 1h TTL, opaque, hashed at rest
- [ ] Consent screen names the app and its scopes in plain language
- [ ] `dances:vote` never granted implicitly with read scopes
- [ ] Bearer scheme only on `/v1/*`; cookies not accepted
- [ ] HTTPS everywhere; no token in query strings or logs
- [ ] Per-token and per-client rate limits, separate from the site's per-IP limits
- [ ] Token endpoint added to `ShouldRateLimit` paths
- [ ] Hard per-client daily ceiling on anonymous grants, with alerting
- [ ] Revocation reflected within one request — no cached authorization decisions

**Scraping defense:** page size caps, no unbounded result sets, no "list all songs"
endpoint, per-client volume alerting. The API is a new front door to the entire dataset.

---

## Implementation Phases

### Core: subscriber MVP, in order

| PR | Scope | Excluded |
| --- | --- | --- |
| **1. Foundation and schema** | OpenIddict stores and migration, disabled feature flag, scopes, DanzQ client registration, bearer scheme, subscriber-policy seam, protocol hosting and discovery | Authorization UI, token issuance, and `/v1/*` endpoints |
| **2. Authorization flow** | Sign-in and consent at `/connect/*`, code exchange, PKCE, refresh and revocation behavior, protocol tests | Song and account APIs |
| **3. Account and subscriber enforcement** | `/v1/me`, current entitlement evaluation, API errors, rate-limit keying, `UsageLog.ClientId` | Song resolution |
| **4. Read-only resolution** | `POST /v1/songs/resolve`, ISRC then Apple Music then title/artist matching, minimal dance projection | Trial access and writes |

These four PRs complete the server-side authenticated-subscriber MVP. DanzQ's client
integration and an end-to-end test are also required before the app itself can ship.

### Required before real Production traffic

| Gate | Scope | Notes |
| --- | --- | --- |
| **Production key management** | Replace `AddEphemeralSigningKey` and `AddEphemeralEncryptionKey` with persisted signing and encryption keys, then replace the Production startup block with a durable-key configuration check | Production stays blocked until this lands. Staging may use ephemeral keys for PR smoke tests. |

The four Core PRs can be deployed to `m4d-test` in Staging without durable keys. Production
traffic additionally requires Production key management, regardless of which later additions
have shipped.

### Later: independent, no required order

Each addition builds on the Core, but none depends on the other later additions.

| Addition | Scope | Notes |
| --- | --- | --- |
| **Trial tier** | Anonymous grant, Keychain + DeviceCheck binding, per-client ceiling, reduced payload | Ships the no-registration experience. |
| **Developer self-serve** | `/developers` registration, admin approve/revoke UI, published terms | Needed before developer #2. |
| **Voting write API** | `PUT/DELETE /v1/songs/{id}/votes/{danceId}`, `dances:vote` scope, client attribution in property log | Requires a separate security review. Property-log attribution must be settled before the first Production API write. |

The single `PublicApi` flag covers the Core dependency chain. Each later addition should use
its own flag so it can ship independently: `PublicApiTrial`, `PublicApiDeveloperPortal`, and
`PublicApiVoting`.

---

## Accepting the Contribution

The offer is genuine and the developer has correctly identified a real gap. Guardrails,
given this touches authentication:

1. **We choose the library.** Specify OpenIddict up front rather than reviewing a
   hand-rolled implementation and asking for a rewrite. Cheaper for both sides.
2. **Keep the access token opaque to DanzQ.** The MVP obtains current account and
   subscription state from `/v1/me` and does not request an OIDC `id_token`.
3. **Split the PR by phase.** Schema + OAuth endpoints + API surface + rate limiting in one
   PR is not reviewably safe.
4. **Feature-flag everything**, default off, so work can merge before it's exposed.
5. **Review against RFC 9700** as an explicit checklist.
6. **Server tests required** ([testing-patterns.md](testing-patterns.md)) including negative
   cases: wrong `code_verifier`, replayed code, mismatched redirect URI, revoked token,
   exhausted lifetime allowance, lapsed subscription, vote beyond the ±1 cap.
7. **The consent screen is ours** — our branding, our copy about what we permit.
8. **Terms of use before the first external client ships.**
9. **Ask what his Shazam path returns** (ISRC? Apple Music ID? both?) — it determines how
   much of the resolution cascade matters in practice.

---

## Open Questions

- **Model A or B?** Recommendation is A, built so B is a config change. Worth measuring
  rather than deciding by argument.
- **Is 15 the right lifetime allowance?** Unknowable until real users hit it — hence the
  policy table.
- **Should API votes be distinguishable from site votes in the property log?** Recommendation
  is yes. Decide and implement this before the first production API write.
- **Should the trial tier be grantable per client**, rather than automatic on approval?
  Leaning yes — it's the highest-abuse surface.
- **Playlist access as a subscriber differentiator?**
  See [playlist-management.md](playlist-management.md).
- **Do we want unmatched-ISRC logging** as a database-enrichment side effect? Low cost, real
  value, good goodwill.

---

## Related Documents

- [unconfirmed-dance-votes.md](unconfirmed-dance-votes.md) — vote trust model and the ±1 cap
- [account-management.md](account-management.md) — where "Connected Apps" belongs
- [identity-endpoint-protection.md](identity-endpoint-protection.md) — existing identity hardening
- [distributed-attack-mitigation.md](distributed-attack-mitigation.md) — rate-limiting architecture
- [visitor-engagement-monetization.md](visitor-engagement-monetization.md) — subscription funnel
- [music-service-model.md](music-service-model.md) — service IDs, prefixes, `ServiceIds` field
- [song-search-service.md](song-search-service.md) — the search layer the API sits on
