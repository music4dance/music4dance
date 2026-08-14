# Public API & Third-Party Authorization

**Status:** 📋 Proposed — design only, nothing implemented

**Context:** An independent iOS developer (DanzQ) has offered to contribute a token
mechanism so their app can look up songs and show matching dances, without asking users
to type their music4dance password into a third-party app. This document generalizes that
request into a design that can onboard any number of third-party developers.

---

## Executive Summary

Music4dance should expose a **versioned, read-only public API** protected by
**OAuth 2.0 Authorization Code flow with PKCE**, issuing **opaque, revocable reference
tokens**. Access is metered in three tiers:

| Tier | Who | Auth | Data | Quota (starting point) |
| --- | --- | --- | --- | --- |
| **0 — Trial** | Anyone, no m4d account | Device-bound anonymous token | Reduced (top dance matches only) | ~25 lookups/device/day, hard per-app ceiling |
| **1 — Free account** | Registered m4d user | User token, no premium role | Standard | ~200 lookups/day |
| **2 — Subscriber** | Premium / Trial role | Same token, role checked per request | Full (tags, tempo detail, ratings) | ~2000 lookups/day |

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
| `TokenAuthorization` policy | [TokenRequirement.cs](../m4d/Utilities/TokenRequirement.cs) | **Not reusable.** A single shared secret from config (`Authentication:RecomputeJob:Key`) compared against a base64 header, held in a `static` field. Fine for an internal cron job; it has no concept of a user, cannot be revoked per-client, and has no expiry. |
| `/api/*` controllers | [m4d/APIControllers/](../m4d/APIControllers/) | **Not reusable as-is.** Every one is decorated `[ValidateAntiForgeryToken]` — they are first-party endpoints for our own Vue frontend, authenticated by session cookie. A third-party app cannot supply an antiforgery token, and loosening these endpoints would weaken the site. |
| Subscription model | `ApplicationUser.SubscriptionLevel` / `SubscriptionEnd`, `PremiumRole` | **Reusable directly.** Tier 2 is exactly `User.IsInRole(PremiumRole) \|\| User.IsInRole(TrialRole)`, the same test used in [ContentController.cs:55](../m4d/Controllers/ContentController.cs#L55). |
| `RateLimitingMiddleware` | [RateLimitingMiddleware.cs](../m4d/Middleware/RateLimitingMiddleware.cs) | **Reusable with changes.** Keys on IP via `GetClientIdentifier`. API traffic must key on token/client instead — a whole office behind one NAT would otherwise share a bucket. |
| `UsageLog` | [UsageLog.cs](../m4dModels/UsageLog.cs) | **Extend.** Add client attribution so API traffic is separable from site traffic in analytics. |

**Key decision that follows:** the public API gets its own route prefix (`/v1/…`), its own
authentication scheme, and its own controllers. It does not share the `/api/*` surface.

---

## Protocol Choice

### Recommendation: OAuth 2.0 Authorization Code + PKCE

This is the settled, boring answer for "native app, user authorizes once, revocable
credential, no password sharing." It is what Apple, Google, Spotify, and every other
service the app already talks to use, and it is what App Store reviewers expect.

**Do not** accept a bespoke token scheme, even a simple one. Custom auth is where security
bugs live, and this is the one part of the system where a bug is a breach.

### Reference documentation

| Spec | What it gives you |
| --- | --- |
| [RFC 6749](https://datatracker.ietf.org/doc/html/rfc6749) | OAuth 2.0 core — the Authorization Code grant |
| [RFC 7636](https://datatracker.ietf.org/doc/html/rfc7636) | **PKCE** — mandatory here; it is what makes a secret-less mobile client safe |
| [RFC 8252](https://datatracker.ietf.org/doc/html/rfc8252) | **OAuth 2.0 for Native Apps** (BCP 212) — the doc the iOS developer is implicitly citing. Requires an *external* user-agent (`ASWebAuthenticationSession`), never an embedded webview |
| [RFC 6750](https://datatracker.ietf.org/doc/html/rfc6750) | Bearer token usage — `Authorization: Bearer <token>` |
| [RFC 7009](https://datatracker.ietf.org/doc/html/rfc7009) | Token revocation endpoint |
| [RFC 8414](https://datatracker.ietf.org/doc/html/rfc8414) | Authorization Server Metadata — the `/.well-known` discovery doc that lets a new developer self-configure |
| [RFC 9700](https://datatracker.ietf.org/doc/html/rfc9700) | **OAuth 2.0 Security Best Current Practice** (BCP 240, Jan 2025) — the current checklist; read this one before reviewing the PR |
| [OAuth 2.1 draft](https://datatracker.ietf.org/doc/html/draft-ietf-oauth-v2-1) | Consolidates the above and drops the unsafe grants. Still a draft, but following it is a good filter for design choices |
| [RFC 8628](https://datatracker.ietf.org/doc/html/rfc8628) | Device Authorization Grant — not needed now; relevant if anyone builds for Apple TV / watchOS |

Apple-side: [`ASWebAuthenticationSession`](https://developer.apple.com/documentation/authenticationservices/aswebauthenticationsession)
is the required browser surface, and [App Attest](https://developer.apple.com/documentation/devicecheck)
is the mechanism for the anonymous tier hardening described below.

### Implementation: OpenIddict, not hand-rolled

| Option | Assessment |
| --- | --- |
| **OpenIddict** ✅ | MIT licensed (matches the contributor's offer), ASP.NET Core native, EF Core storage, designed to layer on top of `AddDefaultIdentity<ApplicationUser>()` which we already call in [Program.cs:435](../m4d/Program.cs#L435). **Recommended.** |
| Duende IdentityServer | Commercial license above a revenue threshold. Overkill for our scale and an ongoing licensing question — verify current terms before considering. |
| Hand-rolled endpoints | What the contributor proposed by default. Smaller diff, but we would own the correctness of PKCE verification, redirect-URI matching, code replay prevention, and token rotation forever. **Not recommended.** |

The tradeoff is honest: OpenIddict is a real dependency with real ceremony for what starts
as one endpoint serving one app. It earns its place the moment there is a second developer,
and it means the security-critical paths are maintained by someone else.

### Token format: opaque reference tokens, not JWTs

Store tokens server-side and look them up per request. Rationale specific to us:

- **Revocation is the point.** The contributor's own framing is "revocable by you." A
  self-contained JWT is valid until it expires no matter what we do; a reference token dies
  the instant the row is marked revoked.
- **We hit the database anyway** to check `PremiumRole`, so there is no lookup to save.
- **We run a single instance** (see [SELF_CONTAINED_DEPLOYMENT.md](SELF_CONTAINED_DEPLOYMENT.md)),
  so there is no distributed-validation problem that JWTs would solve.

If the API ever fans out to multiple services, revisit with [RFC 9068](https://datatracker.ietf.org/doc/html/rfc9068).

---

## User Experience

### Flow A — Anonymous trial (no registration)

```plaintext
User installs DanzQ
  │
  ├─> First launch: app generates a random device ID, stores it in the iOS Keychain
  │   └─> POST /oauth/token
  │         grant_type=urn:m4d:params:oauth:grant-type:anonymous
  │         client_id=danzq-ios
  │         device_id=<uuid>
  │       → { access_token, expires_in: 86400, scope: "songs:read", tier: "trial" }
  │
  ├─> User searches "Blue Bayou" → GET /v1/songs?title=…&artist=…
  │   └─> Returns top 3 dance matches. No tags, no per-dance tempo detail.
  │       Response header: X-M4D-Quota-Remaining: 24
  │
  └─> Quota exhausted (or user taps a locked field)
      └─> App shows: "Connect your free music4dance account for more lookups"
          → Flow B
```

No registration, no friction, works on first launch. The reduced payload is deliberate: it
caps the scraping value of the unauthenticated tier *and* creates the upgrade prompt.

### Flow B — Connecting an m4d account

```plaintext
User taps "Connect music4dance account"
  │
  ├─> App generates code_verifier + code_challenge (PKCE)
  ├─> App opens ASWebAuthenticationSession →
  │     https://www.music4dance.net/oauth/authorize
  │       ?client_id=danzq-ios
  │       &redirect_uri=danzq://auth/callback
  │       &response_type=code
  │       &scope=songs:read+dances:read+profile:read
  │       &code_challenge=<S256>&code_challenge_method=S256&state=<nonce>
  │
  ├─> [music4dance.net, real browser, real URL bar]
  │     ├─> Not signed in? → normal m4d login page
  │     │     └─> incl. "Create an account" — sign-up happens here, in our funnel
  │     └─> Consent screen:
  │           "DanzQ wants to:
  │              • Look up songs and dance matches
  │              • See your username and subscription status
  │            DanzQ will not be able to see your password or change your data.
  │            You can disconnect it any time from Account → Connected Apps."
  │           [ Allow ]  [ Cancel ]
  │
  ├─> Allow → 302 danzq://auth/callback?code=<one-time>&state=<nonce>
  │     └─> iOS hands control back to the app; browser sheet closes
  │
  ├─> App: POST /oauth/token
  │         grant_type=authorization_code, code, code_verifier, client_id, redirect_uri
  │       → { access_token, refresh_token, expires_in: 3600, … }
  │
  └─> App shows "Connected as dwgray · Premium until 2027-03-14"
```

The user types their password only on our domain, in a browser they can inspect. That is
exactly the property the developer asked for, and the one Apple looks for.

### Flow C — Ongoing use and revocation

- Access token expires hourly; the app silently refreshes. **Refresh tokens rotate** on
  every use (a replayed refresh token revokes the whole chain — RFC 9700 §4.14).
- User revokes from **Account → Connected Apps** on the site: name, connection date, last
  used, scopes, `[Disconnect]`. New page under the existing account-management area.
- App revokes on sign-out: `POST /oauth/revoke`.
- We revoke an entire client from the admin area if a developer misbehaves — every token
  issued to that `client_id` dies at once.

### Upgrade path to subscription

When a Tier 1 user requests premium data, return `403` with a machine-readable body:

```json
{
  "error": "subscription_required",
  "message": "Tempo detail and dance tags require a music4dance subscription.",
  "upgrade_url": "https://www.music4dance.net/commerce/subscribe?ref=danzq-ios"
}
```

The `ref` parameter attributes the conversion, which is what makes third-party apps worth
supporting at all. Note Apple's in-app-purchase rules constrain how an app may *present*
an external subscription link — that is the developer's problem to solve, but worth raising
early so it doesn't derail the integration late.

---

## Access Tiers in Detail

### Tier 0 — Anonymous, unregistered

The hard question: how do you meter someone who hasn't identified themselves?

**Phase 1 — device-bound tokens.** The app generates a UUID at first launch and keeps it in
the Keychain. Tokens are bound to `(client_id, device_id)`. A determined attacker rotates
the UUID and defeats this — which is fine, because the real protection is the layer below:

**A hard per-client daily ceiling.** Regardless of how many device IDs appear, `danzq-ios`
gets N anonymous calls per day in total. Worst case is bounded and known, and the ceiling
is the alarm: if it trips, either the app got popular (good, talk to the developer) or it's
being abused (revoke, investigate).

Layer on the existing per-IP-subnet limits as a secondary control.

**Phase 2 — App Attest, if abuse appears.** iOS App Attest lets Apple cryptographically
vouch that a request came from a genuine, unmodified build of DanzQ on real hardware. The
server verifies the attestation before issuing an anonymous token. This is the strong
answer, but it's meaningful work on both sides and Android would need Play Integrity
separately. Don't build it until the data says you need it.

**Reduced payload** (top 3 dance matches, no tags, no tempo detail) keeps this tier from
becoming a free bulk-data endpoint even if the metering is beaten.

### Tier 1 — Registered, free

Token maps to a real `ApplicationUser`. Standard payload. Higher quota. This tier exists
because *registration is the conversion step we actually want* — it gets the user into the
funnel described in [visitor-engagement-monetization.md](visitor-engagement-monetization.md).

### Tier 2 — Subscriber

Checked per request:

```csharp
// Same test as ContentController.IsPremium() — deliberately, so the API and site never diverge
var isPremium = User.IsInRole(DanceMusicCoreService.PremiumRole) ||
                User.IsInRole(DanceMusicCoreService.TrialRole);
```

Full payload, highest quota. **Subscription state is never encoded in the token** — a
cancellation takes effect on the next request.

---

## API Surface

Separate prefix, separate scheme, explicit versioning:

```txt
GET  /v1/songs?title=&artist=          Look up by title/artist
GET  /v1/songs?search=                 Free-text search
GET  /v1/songs/{id}                    Song detail
GET  /v1/dances                        Dance catalog + tempo ranges (cacheable, static)
GET  /v1/me                            Username, tier, quota remaining
```

Example Tier 2 response, using the real field names from
[Song.cs](../m4dModels/Song.cs) and [DanceRating.cs](../m4dModels/DanceRating.cs):

```json
{
  "songId": "3f2a…",
  "title": "Blue Bayou",
  "artist": "Linda Ronstadt",
  "tempo": 44.5,
  "meter": "4/4",
  "length": 231,
  "danceRatings": [
    { "danceId": "west-coast-swing", "danceName": "West Coast Swing",
      "weight": 12, "tempo": 44.5, "tags": ["Smooth:Style"] },
    { "danceId": "rumba", "danceName": "Rumba", "weight": 3 }
  ]
}
```

Tempo is MPM, consistent with the rest of the domain. Tier 0 omits `tags`, per-rating
`tempo`, and truncates `danceRatings` to 3.

### Critical: the API must not accept cookies

```csharp
[Authorize(AuthenticationSchemes = M4dApiDefaults.BearerScheme)]
```

If the public API also accepted the site session cookie, every CSRF concern that
`[ValidateAntiForgeryToken]` currently handles on `/api/*` would reappear on an endpoint
that has no antiforgery protection. Bearer only, and no `[ValidateAntiForgeryToken]`
(it would be meaningless and would break legitimate clients).

---

## Data Model

Let OpenIddict own the OAuth tables (`OpenIddictApplications`, `OpenIddictAuthorizations`,
`OpenIddictTokens`, `OpenIddictScopes`). Add one m4d-owned table for the things OpenIddict
has no opinion about:

```csharp
public class ApiClientProfile
{
    public int Id { get; set; }
    public string ClientId { get; set; }        // matches OpenIddict application
    public string DisplayName { get; set; }     // "DanzQ" — shown on the consent screen
    public string DeveloperUserId { get; set; } // FK to ApplicationUser
    public string DeveloperEmail { get; set; }
    public string HomepageUrl { get; set; }
    public ApiClientStatus Status { get; set; } // Pending, Approved, Suspended, Revoked
    public int QuotaTier { get; set; }
    public DateTime Created { get; set; }
    public DateTime? ApprovedDate { get; set; }
    public string Notes { get; set; }           // admin-only
}
```

Per project convention, no nullable reference type annotations — check null explicitly.

Extend `UsageLog` with a nullable `ClientId` so API traffic is separable from site traffic
in the existing analytics (see [usage-log-analysis-plan.md](usage-log-analysis-plan.md)).

---

## Onboarding Other Developers

This is what makes the design worth building rather than special-casing DanzQ.

**Registration** — a logged-in user visits `/developers`, registers an app (name, homepage,
redirect URIs, contact), and receives a `client_id` in `Pending` state. Public clients get
no secret; that is correct and expected for mobile apps under RFC 8252.

**Approval** — manual, from the admin area, while volume is low. The gate is where you check
the app is real, the redirect URIs are sane, and the developer has agreed to the terms.

**Terms of use** — publish these before the first external client ships. Minimum:

- Attribution: "Powered by music4dance" with a link, visible in the app
- No bulk extraction, redistribution, or resale of the song database
- Caching allowed for reasonable periods; no permanent local mirrors
- Quota limits and our right to revoke, stated plainly
- Contact obligation so we can reach the developer about breaking changes

The database is community-contributed. Terms are how you keep the API from quietly becoming
an export pipe.

**Versioning** — once an app ships, `/v1` is a contract. Add fields freely; never remove or
retype one. Breaking changes get `/v2` and a deprecation window announced by email to
registered developers.

**Admin visibility** — a page listing clients with request volume, error rate, quota
consumption, and a revoke button, alongside the existing rate-limit dashboards in
[AdminController.cs:447](../m4d/Controllers/AdminController.cs#L447).

---

## Security Requirements

Non-negotiable, all from RFC 9700 / RFC 8252:

- [ ] PKCE `S256` required on every authorization request; plain rejected
- [ ] Exact redirect-URI string matching — no prefix or wildcard matching
- [ ] Authorization codes: single-use, ≤60s TTL, bound to `client_id` + `code_verifier`
- [ ] `state` parameter required and verified
- [ ] Implicit and password grants **not** enabled
- [ ] Refresh token rotation with replay detection (reuse revokes the chain)
- [ ] Access tokens: 1h TTL, opaque, hashed at rest
- [ ] Consent screen names the app and its scopes in plain language
- [ ] Bearer scheme only on `/v1/*`; cookies not accepted
- [ ] HTTPS everywhere; no token in query strings or logs
- [ ] Per-token and per-client rate limits, separate from the site's per-IP limits
- [ ] Token endpoint added to `ShouldRateLimit` paths with brute-force protection
- [ ] Revocation reflected within one request — no cached authorization decisions

**Scraping defense**, which is easy to overlook: page size caps, no unbounded result sets,
no "list all songs" endpoint, and per-client volume alerting. The API is a new front door to
the entire dataset.

---

## Implementation Phases

| Phase | Scope | Notes |
| --- | --- | --- |
| **1. Foundation** | OpenIddict + EF stores, `ApiClientProfile`, migrations, bearer scheme, `/v1/dances` as a trivial first endpoint | No UI yet. Proves the plumbing. |
| **2. Authorization flow** | `/oauth/authorize`, `/oauth/token`, `/oauth/revoke`, `.well-known` metadata, consent page | The security-critical phase. Review hardest here. |
| **3. Read API** | `/v1/songs`, `/v1/songs/{id}`, `/v1/me`, tiered payload shaping | Reuses `SongIndex`; little new domain logic. |
| **4. Metering** | Per-token/client quotas, `X-M4D-Quota-*` headers, `UsageLog.ClientId` | Extends existing rate-limit middleware. |
| **5. Anonymous tier** | Anonymous grant, device binding, per-client ceiling, reduced payload | Can ship after DanzQ's authenticated path works. |
| **6. Developer self-serve** | `/developers` registration, admin approval + revoke UI, published terms | Needed before developer #2. |

Phases 1–3 are enough for DanzQ to ship against Tier 1/2. Phase 5 delivers the
try-before-registering experience.

---

## Accepting the Contribution

The offer is genuine and useful, and the developer has correctly identified a real gap. A
few guardrails, given this code touches authentication:

1. **We choose the library.** Specify OpenIddict up front rather than reviewing a
   hand-rolled implementation and asking for a rewrite. Cheaper for both sides.
2. **Split the PR by phase.** One PR containing schema, OAuth endpoints, API surface, and
   rate limiting is not reviewably safe. Phases 1, 2, and 3 as separate PRs.
3. **Feature-flag it.** Everything behind a config switch, default off, so it can merge
   before it's ready to be exposed.
4. **Review against RFC 9700** as an explicit checklist, not by reading for style.
5. **Server tests required**, following [testing-patterns.md](testing-patterns.md) —
   including negative cases: wrong `code_verifier`, replayed code, mismatched redirect URI,
   revoked token, exceeded quota, lapsed subscription.
6. **The consent screen is ours.** It carries our branding and sets user expectations about
   what we permit; we should write that copy.
7. **Terms of use before the first external client ships**, not after.

One thing to decide before starting: **is API access included in the existing subscription
or priced separately?** Recommendation: include it. A third-party app that drives
subscriptions is worth more than a separate small revenue line, and a second price point
complicates a subscription model that currently works. Revisit only if a client's load
becomes a real cost.

---

## Open Questions

- Should Tier 0 be available to *every* approved client, or only to clients we explicitly
  grant it to? (Leaning: explicit grant — it's the highest-abuse surface.)
- Do we want write scopes eventually — letting an app submit tempo data or dance votes back?
  That is a much larger trust decision and should stay out of v1.
- Does the DanzQ developer want playlist access? [playlist-management.md](playlist-management.md)
  describes premium functionality that could be a natural Tier 2 differentiator.

---

## Related Documents

- [account-management.md](account-management.md) — where "Connected Apps" belongs
- [identity-endpoint-protection.md](identity-endpoint-protection.md) — existing identity hardening
- [distributed-attack-mitigation.md](distributed-attack-mitigation.md) — rate-limiting architecture
- [visitor-engagement-monetization.md](visitor-engagement-monetization.md) — subscription funnel
- [song-search-service.md](song-search-service.md) — the search layer the API sits on
