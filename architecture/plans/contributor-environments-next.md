# Contributor Environments: Next Steps

**Type:** Plan
**Status:** Proposed. Rungs 2–6 (and L2/L3) aren't started. Rungs 0–1 shipped; see
[contributor-test-environments](../dev-testing/contributor-test-environments.md).
**Last verified:** 2026-10-01

The options analysis for giving outside contributors more than the local sandbox: test deploys,
self-service diagnostics, realistic data, and their own cloud instance. It was originally written
for the DanzQ public-API collaboration, which is on hold.

## Executive Summary

**The central finding: the contribution needs far less access than it first appears.**

Roughly two-thirds of the API work — OpenIddict wiring, `/oauth/authorize`, `/oauth/token`,
`/oauth/revoke`, PKCE verification, consent UI, OIDC `id_token`, refresh rotation, tier
policy, metering — touches **SQL and ASP.NET Core Identity only**. It needs zero song data,
zero Azure Search, zero third-party keys, and zero deployed instance. That is also the
security-critical part where review burden is highest. **The work that needs the least access
is the work that matters most.** That alignment is lucky and should be exploited.

**Recommended sequence:**

| Rung  | What                                                                                          | Unblocks                        | Cost         | Status                                                                                                                                                                                           |
| ----- | --------------------------------------------------------------------------------------------- | ------------------------------- | ------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| **0** | Contributor setup guide + DCO; empty local DB, no keys, no data                               | API Phases 1–2, 4, 6            | **XS**       | ✅ Shipped ([PR #250](https://github.com/music4dance/music4dance/pull/250))                                                                                                                      |
| **1** | No-external-service local server: shared stub assembly, `m4d.Sandbox` host, seeded test users | API Phases 3, 7; manual QA; e2e | **M**        | ✅ Shipped ([PR #250](https://github.com/music4dance/music4dance/pull/250)); interactive search/filter/sort followed, see [L1f](../dev-testing/contributor-test-environments.md#l1f--search-filter-and-sort-for-songindexlocal-new-fast-follow) |
| **2** | You deploy their PR branch to the existing test site on request                               | End-to-end iOS validation       | **XS**       | 📋 Proposed                                                                                                                                                                                      |
| **3** | Owner-scoped API diagnostics (their `client_id` only)                                         | Self-service debugging          | **S**        | 📋 Proposed                                                                                                                                                                                      |
| **4** | Samplification endpoint + terms-gated delivery                                                | Browseable realistic dataset    | **L**        | 📋 Proposed                                                                                                                                                                                      |
| **5** | GitHub Actions environment-gated deploy                                                       | Self-service test deploys       | **M**        | 📋 Proposed                                                                                                                                                                                      |
| **6** | Their own Azure deployment (guide only)                                                       | Full independence               | **M** (docs) | 📋 Proposed                                                                                                                                                                                      |

Rungs 0–3 total a bit over a week — rung 1 grew once it started carrying manual QA and e2e as
well as CI coverage — and still cover the realistic need. **Rung 4 is now the only source of
browseable data**, since nothing ships in the repo — but it is needed for _interactive_
development, not for correctness testing, so it still isn't a prerequisite for starting.

**Since this was written:** L0 and L1 shipped in
[PR #250](https://github.com/music4dance/music4dance/pull/250) largely as planned, plus a few
real bugs the sandbox build surfaced along the way (see the "Shipped" notes inside L0/L1 below).
One piece of L1's original scope was deliberately deferred rather than shipped in that PR:
`SongIndexLocal` had no free-text search, dance/tag filtering, or sort, so the sandbox could
only be browsed via direct links to seeded songs. That gap has since been closed for the
filter/sort/browse path — see [L1f](../dev-testing/contributor-test-environments.md#l1f--search-filter-and-sort-for-songindexlocal-new-fast-follow).

**Two assumptions worth killing early**, because both inflate the plan:

1. _"They need a cloud instance to test the iOS app."_ No. `ASWebAuthenticationSession`
   against a local server works fine — a dev cert on the LAN, or a Cloudflare/ngrok/Tailscale
   tunnel for a real HTTPS hostname. The `danzq://` redirect never touches our infrastructure.
2. _"They need production-like data to build the API."_ No. The resolve cascade needs the
   _shape_ of the data — songs carrying ISRC / iTunes / Spotify service IDs — and tests can
   construct that inline. Realistic data is for exploring the site, not for proving the code.

---

## The Reframe: what does each phase actually require?

Mapping the phases from [public-api-authorization.md](../plans/public-api-authorization.md#implementation-phases)
onto environment needs:

| Phase                                            | SQL | Song data   | Azure Search | 3rd-party keys | Deployed instance |
| ------------------------------------------------ | --- | ----------- | ------------ | -------------- | ----------------- |
| 1. Foundation (OpenIddict, schema, `/v1/dances`) | ✅  | —           | —            | —              | —                 |
| 2. Authorization flow (**security-critical**)    | ✅  | —           | —            | —              | —                 |
| 3. Read API (`resolve` cascade)                  | ✅  | constructed | stub OK      | —              | —                 |
| 4. Metering / tier policy                        | ✅  | —           | —            | —              | —                 |
| 5. Trial tier (DeviceCheck)                      | ✅  | —           | —            | Apple only¹    | helpful           |
| 6. Developer self-serve `/developers`            | ✅  | —           | —            | —              | —                 |
| 7. Voting write API                              | ✅  | constructed | stub OK      | —              | —                 |

¹ Apple DeviceCheck / App Attest keys belong to **their** Apple developer team, not ours —
nothing to provision on our side.

`/v1/dances` deserves a callout: the dance catalog comes from the `Dance` table and
`DanceStatsManager`, and [`m4dModels.Tests/TestData/`](../../m4dModels.Sandbox/TestData/) (since moved to `m4dModels.Sandbox/TestData/`) already
ships `test-dances.json`, `test-dances.txt`, `test-tags.txt`, and `dancestatistics.txt`. The
entire Phase 1 deliverable is testable against data **already committed to the public repo** —
and the dance/tag catalog fields (`dances`/`groups`/`tagGroups`) aren't song data, so the
no-commit decision doesn't touch them. (`dancestatistics.txt` also carries a `cachedSongs` array
that _is_ real, PII-cleaned song data used to seed the `m4d.Sandbox` host's database — see
[song-internal-format.md §12.1](../songs/song-internal-format.md#121-contributor-test-fixture-m4dmodelssandboxtestdatadancestatisticstxt)
for its format and the two different consumers that read it.)

---

## Path 1 — Cloud Options

### C1 — They submit a PR, you review and deploy to the existing test site ✅

**Cost: XS** (zero build; ~30 min per deploy cycle of your time)

The existing `azure-pipelines.yml` already parameterizes `environment: test` → the `m4d-test`
app plus `SongIndexTest-3`. Nothing to build.

| Pros                                                                                    | Cons                                                                                                   |
| --------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------ |
| Zero engineering cost — works today                                                     | Serializes on your availability; a 4-hour loop feels slow to a volunteer                               |
| You review every line before it runs anywhere                                           | Every trivial fix costs you an interrupt                                                               |
| Fork-based PRs get no secrets and no OIDC token — the strongest isolation GitHub offers | Doesn't scale past one contributor                                                                     |
| No trust grant required at all                                                          | They cannot see server-side failure detail without [C3](#c3--owner-scoped-api-diagnostics--recommended) |

**Verdict: start here.** At one contributor and a phased PR plan (7 phases, likely 2–4 PRs
each), this is maybe a dozen deploy cycles total. Build automation only if that proves painful
— and pair it with C3 so each cycle yields real diagnostic information instead of a shrug.

**Implementation:** none. Document the request protocol (comment on the PR, you deploy, you
post back the test URL and a diagnostics link) in `CONTRIBUTING.md`.

### C2 — Grant a diagnostics role on production ❌

**Cost: XS to grant, unbounded to regret**

This is the "reuse `showDiagnostics`" idea, and the codebase says no: that role gates
`BackupDatabase`, which dumps **every user's password hash, email, security stamp, and
external-provider keys**. It also gates `UsageLog` (real user behavioural data), index backup,
and the bulk-modify surface.

| Pros             | Cons                                                                                        |
| ---------------- | ------------------------------------------------------------------------------------------- |
| Nothing to build | Grants full PII export to someone with no contractual relationship                          |
|                  | Provider keys are stable Google/Facebook/Spotify user identifiers — irrevocable once leaked |
|                  | Almost certainly a privacy-law problem for any EU users                                     |

**Verdict: reject as stated.** The _want_ behind it is legitimate, though — see C3.

### C3 — Owner-scoped API diagnostics ✅ (recommended)

**Cost: S** (~1 day)

The correct version of C2. Instead of a role, use **ownership**: the design doc's
`ApiClientProfile` already carries `DeveloperUserId`
([public-api-authorization.md](../plans/public-api-authorization.md#data-model)). So the scope is
naturally self-limiting — _"you can see diagnostics for API clients you own."_ No role grant,
no PII, no admin surface, and it generalizes to every future third-party developer for free.

Surface, all filtered to `client_id` values the caller owns:

- Recent `/v1/*` requests: timestamp, route, status, latency, matched tier, allowance remaining
- Token events: issued / refreshed / revoked / rejected, **with the rejection reason** —
  `invalid_code_verifier`, `redirect_uri_mismatch`, `code_replayed`, `token_revoked`,
  `allowance_exhausted`. This is the single highest-value item; OAuth failures are opaque
  from the client side and this is where a contributor otherwise burns days
- Rate-limit counters for their client
- Never: usernames, emails, tokens, other clients' traffic, site-wide logs

| Pros                                                                                                      | Cons                                                                             |
| --------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------- |
| Turns C1's slow loop into a fast one — they diagnose their own failures                                   | New endpoint to build and secure                                                 |
| Zero PII exposure by construction                                                                         | Only useful once Phase 1's schema exists (chicken-and-egg for the earliest work) |
| Ships as part of the API anyway — it is `/developers` self-serve infrastructure (Phase 6), pulled forward | Needs care that "own client" checks can't be spoofed                             |

**Implementation:** extend `UsageLog` with the nullable `ClientId` that Phase 4 already
specifies; add an `ApiClientEvent` table for token lifecycle events; add
`/developers/{clientId}/diagnostics` authorized by `profile.DeveloperUserId == currentUserId`.
Reuse the existing rate-limit counters rather than adding new ones.

**Pull this into Phase 1**, not Phase 6. It pays for itself immediately by making C1 viable —
and the same `/developers` surface later hosts the samplified-data download (L2).

### C4 — GitHub Actions deploy to test, gated away from production ✅

**Cost: M** (2–4 days, partly work worth doing anyway)

The question was: _can we let them deploy to test without letting them deploy to prod?_
**Yes, and the enforcement is real** — GitHub Environments are enforced by GitHub, not by the
workflow file, so a contributor who edits the workflow to target production still cannot
deploy.

Mechanism:

1. Port `azure-pipelines.yml` to `.github/workflows/deploy.yaml` with a `workflow_dispatch`
   `environment` input (it is already cleanly parameterized, so this is mostly mechanical).
2. Two GitHub Environments: `test` and `production`.
3. Two Entra app registrations with **federated credentials scoped per environment** —
   subject `repo:music4dance/music4dance:environment:test` for one,
   `…:environment:production` for the other. The test identity gets Contributor on the test
   resource group only. No long-lived secrets anywhere (`azure/login` with OIDC).
4. `production` environment: **required reviewers = you**, deployment branch policy = `main`
   only. Both are GitHub-side gates.
5. `CODEOWNERS` on `.github/workflows/**` requiring your review.

| Pros                                                        | Cons                                                                                                      |
| ----------------------------------------------------------- | --------------------------------------------------------------------------------------------------------- |
| They self-serve test deploys; you stop being the bottleneck | Requires giving repo **write** access — a real trust grant, and they could push to non-protected branches |
| Prod gate is enforced by the platform, not by convention    | Loses the fork-PR isolation of C1 (fork PRs get no OIDC token, which is a _feature_)                      |
| No stored secrets — OIDC federation only                    | Two pipelines to maintain unless you fully migrate off Azure DevOps                                       |
| You get a better prod pipeline out of it regardless         | Test deploys touch the shared search service, so a bad migration can disturb `songs-test-3`               |

**Verdict: build it if and only if C1's loop becomes the bottleneck.** It is the right
long-term answer and it is genuinely secure; it is just premature at one contributor — and
largely moot if they run their own deployment (L3).

**Caveat worth internalizing:** even with perfect gating, a test deploy runs _their_ code
against a database and search index that are yours. Test-environment compromise is not
production compromise, but it is not nothing either.

### C5 — Dedicated API test instance ❌

**Cost: M** + **$/month**

A third environment (`m4d-api-test`) so their deploys never disturb your own test site.

**Verdict: skip.** Superseded by the settled decision that they run their own deployment.
Standing up a third environment on your bill, with a third search service to satisfy the RBAC
constraint, buys nothing that L3 doesn't buy for free.

---

## Remaining local options

### L2 — Samplification + terms-gated delivery

**Cost: L** (1–2 weeks) — see [Samplification](#samplification-design)

Now the **only** source of realistic data, given nothing ships in the repo. Delivered
out-of-band at first, and through a dev-portal download later.

| Pros                                                                            | Cons                                                                      |
| ------------------------------------------------------------------------------- | ------------------------------------------------------------------------- |
| Realistic scale and messiness; real Azure Search semantics on their own service | The largest build on this list                                            |
| Reusable for your own testing and for every future contributor                  | Requires the data-use terms to exist first                                |
| Lets them work on _any_ part of the codebase, not just the API                  | Sanitizer correctness is security-relevant and needs its own tests        |
| The dev-portal download is a natural Phase 6 feature, not a one-off             | Free-tier search limits constrain sample size — now their limit, not ours |

**Verdict: build the sanitizer when they ask for browseable data**, not before. The delivery
channel can start as "you send them a file after they accept the terms" and graduate to the
dev portal once `/developers` exists.

### L3 — They deploy their own cloud instance ✅ (settled direction)

**Cost: M** for documentation; their cloud bill

The chosen route for realistic-scale work. A new Azure subscription carries **its own
unconsumed free search tier**, which dissolves the constraint that made this awkward on our
side. Their resource list: App Service (or Container Apps), Azure SQL serverless, and a
free-tier Azure AI Search service.

This also answers the external-dependency question concretely:

| Dependency                   | Config key                                                   | Do they need it?                                                                                                                                                 |
| ---------------------------- | ------------------------------------------------------------ | ---------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Google OAuth                 | `Authentication:Google:*`                                    | No — degrades cleanly. Own app if wanted (free)                                                                                                                  |
| Facebook OAuth               | `Authentication:Facebook:*`                                  | No — and Facebook requires app review; recommend leaving off                                                                                                     |
| Spotify OAuth                | `Authentication:Spotify:*`                                   | Only if testing Spotify sign-in. Own app, free, minutes. [Program.cs:739](../../m4d/Program.cs#L739) already documents the `localhost` HTTPS-redirect accommodation |
| Email (Azure Comm. Services) | `Authentication:AzureCommunicationServices:ConnectionString` | No — `NullEmailSender` fallback, and seeded/sanitized users are pre-confirmed                                                                                    |
| reCAPTCHA                    | `Authentication:reCAPTCHA:*`                                 | No. Google publishes always-pass test keys — include them in the guide (_verify still current_)                                                                  |
| Azure Search                 | RBAC, `SongIndex*` sections                                  | Theirs, never ours                                                                                                                                               |
| App Config + Key Vault       | `AppConfig:Endpoint`                                         | **No** — Development skips it entirely. For their _deployed_ instance, plain app settings work                                                                   |
| Commerce                     | `Configuration:Commerce:Enabled`                             | No — set `false`                                                                                                                                                 |
| GTM / Google Tags            | feature flags                                                | Already `false` in `appsettings.Development.json`                                                                                                                |

**Never share:** Google, Facebook, Spotify, or Azure Communication Services credentials. Those
are _our_ identity with those providers; sharing them is both a security problem and likely a
terms violation. **They create their own or go without** — and going without works.

One caveat for their deployed instance: `AppConfig:Endpoint` is set in the base
`appsettings.json`, so a non-Development deployment will try to reach _our_ App Configuration
store and fail. The setup guide must tell them to blank it — a one-line trap that would
otherwise cost them an afternoon.

### L4 — Sandbox configuration profile (merged into L1)

Everything this used to propose — a declared no-external-dependencies mode,
`Commerce:Enabled: false`, captcha off, clearing `AppConfig:Endpoint`, `FileEmailSender`, the
startup banner — is now part of
[L1](../dev-testing/contributor-test-environments.md#l1--no-external-service-local-server--recommended), specifically L1b and L1e. Building the
sandbox as its own host project (`m4d.Sandbox`) makes "no external dependencies" a separately
buildable target with its own `appsettings.json`, rather than a flag layered onto the production
one — which was this option's own stated risk (_"one more configuration path to keep working"_).
Kept as a heading only so this stays discoverable by its old name.

---

## Samplification Design

Needed for L2. **Good news: the extraction half already exists.**

### Extraction — already built

- `Admin/BackupDatabase` ([AdminController.cs:1732](../../m4d/Controllers/AdminController.cs#L1732))
  emits users, dances, tags, playlists, and searches sections. The songs section is
  commented out (`DBKILL`) because songs now live in the index.
- `Admin/IndexBackup` ([AdminController.cs:1665](../../m4d/Controllers/AdminController.cs#L1665))
  streams the songs section from the index via `BackupIndexStreamingAsync` — **and it already
  takes a `SongFilter`.** Sample _selection_ is therefore free: express the subset as a filter.
- `Admin/ReloadDatabase` ([AdminController.cs:928](../../m4d/Controllers/AdminController.cs#L928))
  is the load path, already sectioned and already able to wipe-and-reload.
- Precedent exists: [`test-users-clean.txt`](../../m4dModels.Sandbox/TestData/test-users-clean.txt)
  is a hand-sanitized user set using `UserA` / `UserB` / `batch-a`, placeholder hashes
  (`XXXXXXXXXXX`), and zero-GUID security stamps. **The convention is already established** —
  this work automates what was done once by hand.

### Form factor — settled

An **admin endpoint** (`Admin/Samplify`) alongside `BackupDatabase`, reusing the existing
`StartAdminTask` / `AdminMonitor` progress plumbing. Later, a dev-portal endpoint produces the
same artifact as a **download gated on accepting testing-only terms**, which fits naturally
into the Phase 6 `/developers` surface.

Not a `scripts/*.ps1` text transform: usernames live _inside_ the property log, which has a
real parser (`ModifiedRecord`, `SongPropertyBlockParser`), and [CLAUDE.md](../../CLAUDE.md) is
explicit that these formats must be built and parsed through the class library. A regex over
property logs is exactly the silent breakage that rule exists to prevent.

### Transform — order matters

This is what answers "how do songs and users stay consistent?":

1. **Select songs** via `SongFilter` (`Admin/IndexBackup`). For API work, filter toward songs
   carrying service IDs.
2. **Derive the user set from the songs**, not the other way round. Scan the sampled property
   logs for every distinct `User=` value; that set — plus preserved service accounts — is
   exactly what the users section must contain. This guarantees consistency in the direction
   that matters (every user a song references exists). The reverse case, a user with no songs,
   is harmless.
3. **Emit** the pseudonymized users section, then the sanitized songs section.

### Service accounts stay in the clear ✅

Agreed, and the reasoning is now doubly strong — these are machine identities owned by
music4dance, so they are **not personal data**, _and_ preserving them is **required for
correctness** because code keys off the names:

| Preserved verbatim             | Not PII because            | Correctness reason                                              |
| ------------------------------ | -------------------------- | --------------------------------------------------------------- |
| `batch`, `batch-*`             | m4d import automation      | `ChunkedSong.IsBatch`; exempt from `TryGetCappedDelta`'s ±1 cap |
| `tempo-bot`                    | m4d tempo automation       | Cap-exempt; asserted by name in `DanceRatingCapTests`           |
| `dgsnure`                      | Non-personal data source   | Hardcoded in `s_unconfirmedVoteSources`                         |
| Any `@music4dance.net` account | Our own service identities | `IsPseudo` / `IsM4d` derives from the email domain              |
| The `\|P` pseudo suffix        | Not an identifier          | `ModifiedRecord` splits on it; stripping it changes attribution |

This is a real simplification: the preserve-list stops being a grudging exception and becomes
the intended behaviour.

### The Spotify proxy-user catch

⚠️ **Spotify proxy users do not belong in that list.** From
[ApplicationUser.cs:48](../../m4dModels/ApplicationUser.cs#L48):

```csharp
public bool IsSpotify => Email?.EndsWith("@spotify.com", StringComparison.OrdinalIgnoreCase) ?? false;
public string SpotifyId => IsSpotify ? EmailAlias : null;
```

The email is `{spotifyUserId}@spotify.com`, and `SpotifyId` is **the local part of that
email** — a real Spotify account identifier belonging to a real person whose public playlist
was imported. That is the same category as the `Providers` column, which must be dropped: a
stable third-party identifier for a natural person, not a machine account. It only _looks_
like a service account because `IsPseudo` returns true for it.

**Rule: keep the `@spotify.com` domain, rewrite the local part.** Mapping
`realuser123@spotify.com` → `sp-000173@spotify.com` preserves `IsSpotify`, `IsPseudo`, the
`|P` decoration, and every display and vote-attribution path, while removing the real
identifier. Same treatment for the `UserName` if it embeds the Spotify ID.

Worth a denylist test of its own, since this is the one case where the privacy rule and the
"it's a service account" intuition point in opposite directions.

### Rewrite rules for real registered users

| Field                      | Treatment                                                                                                                                                                     |
| -------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `UserName`                 | Deterministic pseudonym — `Firstname L.` from a name table, via a salted keyed hash so successive samples are stable and diffable. Resolve collisions explicitly              |
| `Email`                    | `{pseudonym}@example.invalid` — RFC 2606 reserved TLD, so no accidental delivery                                                                                              |
| `PasswordHash`             | Fixed placeholder (`LoadUsers` treats blank as null; `test-users-clean.txt` uses `XXXXXXXXXXX`)                                                                               |
| `SecurityStamp`            | Zero GUID, matching existing test data                                                                                                                                        |
| `Providers`                | **Empty.** Holds Google/Facebook/Spotify provider keys — stable third-party user identifiers. Highest-severity field in the file                                              |
| `Region`                   | Drop, or coarsen to country                                                                                                                                                   |
| Subscription fields        | Synthesize rather than copy — real purchase history is commercially sensitive. Keep the _shape_: one premium, one trial, one lapsed, several free, so tier logic is exercised |
| `LastActive` / `StartDate` | Keep, or jitter by days                                                                                                                                                       |

**Property log:** map every `User=` occurrence (including `|P` forms) through the same table.
Consistency with the users section is what makes vote replay produce the same answer.

**Drop by default:** the searches and playlists sections. Saved-search filters can embed
usernames — `test-searches.txt` contains `\-me|H`, a user filter — and playlists carry user
FKs and Spotify playlist IDs. Low value, non-trivial to sanitize correctly. Substitute a
handful of synthetic searches if needed.

**Their admin account:** don't transplant it. `M4D_ADMIN_USER` / `M4D_ADMIN_PASSWORD` seeding
already creates a confirmed account with `canTag`, `canEdit`, `showDiagnostics`, and `dbAdmin`.
Two environment variables, zero new code, and no password hash ever leaves our systems.

### Verification — the part that makes this trustworthy

A sanitizer without tests is a leak with a schedule. Four tests, all cheap:

1. **PII denylist scan.** No real username, email, password hash, security stamp, or provider
   key from the source appears anywhere in the output. Run it as a gate, not a review step.
2. **Spotify local-part scan.** No source `SpotifyId` survives. Called out separately because
   it is the case most likely to be waved through as "just a service account."
3. **Referential integrity.** Every `User=` in the songs section resolves to a user in the
   users section.
4. **Vote-math invariance.** Sum dance-rating weights per dance before and after
   sanitization; they must match. The sharpest test on the list — it proves the username
   rewrite did not disturb `TryGetCappedDelta` or the batch exemptions, which is the exact
   failure mode the preserve-list exists to prevent. Cheap to write, catches the subtlest bug.

Plus a **size check**: report output size against the target search tier's storage limit,
since the free tier bounds sample size. Note that the `SongPropertyCompression` feature flag
(Brotli above 10k chars) affects stored size.

Sanitized output goes to `local/` (gitignored per [CLAUDE.md](../../CLAUDE.md)). Nothing is
committed.

---

## Legal & Process Prerequisites

Deliberately light, per the settled decision.

1. **DCO, not a CLA. ✅ Shipped in [PR #250](https://github.com/music4dance/music4dance/pull/250).**
   `CONTRIBUTING.md` carries the
   [Developer Certificate of Origin](https://developercertificate.org/) text,
   `.github/workflows/dco.yml` enforces `Signed-off-by` on every PR commit, and the VS Code
   workspace sets `git.alwaysSignOff` so it's on by default from the Source Control panel.
   `git commit -s` is the whole contributor burden. No paperwork, no signature collection, no
   lawyer.
2. **Data-use terms**, required before any sample data changes hands: development and testing
   only, no redistribution, no re-identification attempts, delete on request. One page.
   Necessary because the README already states the data sits outside the MIT grant. This is
   also the text the dev-portal download gates on, so writing it once serves both channels.
3. **Privacy posture.** Pseudonymized is not anonymous. The defensible position — and it is
   genuinely defensible — is _real song data, synthetic human identities, real machine
   identities_: all real usernames and emails rewritten, Spotify local parts rewritten,
   provider keys dropped, hashes discarded, service accounts untouched. Say that explicitly in
   the terms so both sides know what was done.
4. **Secrets hygiene**, stated plainly to them: they will never receive our third-party
   credentials, and they don't need them. That is a design property of the resilience layer,
   not a limitation to work around.
5. **Repo access decision.** C1 (fork PRs) requires no grant. C4 requires write access.
   Fork-based is the correct default given no contractual relationship.

---

## Cost Summary

| Option                                                                                    | Build cost    | Ongoing $ | Unblocks                                 | Verdict                             |
| ----------------------------------------------------------------------------------------- | ------------- | --------- | ---------------------------------------- | ----------------------------------- |
| **L0** Setup guide + DCO, run with nothing                                                | XS            | —         | Phases 1, 2, 4, 6                        | ✅ **Shipped** (PR #250)            |
| **L1** No-external-service local server (stub assembly + `m4d.Sandbox` host + test users) | M (~4–5 days) | —         | Phases 3, 7; CI coverage; manual QA; e2e | ✅ **Shipped** (PR #250)            |
| **C1** You deploy their PR to test                                                        | XS            | —         | End-to-end validation                    | ✅ **Default** — proposed           |
| **C3** Owner-scoped API diagnostics                                                       | S             | —         | Self-service debugging                   | ✅ **Pull into Phase 1** — proposed |
| **L1f** Search/filter/sort for `SongIndexLocal`                                           | S–M           | —         | Interactive browsing of the sandbox      | ✅ **Shipped** (Option A)           |
| **L3** Their own cloud instance (guide)                                                   | M (docs)      | Theirs    | Full independence                        | ✅ Settled direction — proposed     |
| **L2** Samplification + terms-gated delivery                                              | L             | —         | Browseable realistic data                | ⏸ When they ask                     |
| **C4** GitHub Actions environment-gated deploy                                            | M             | —         | Self-service deploys                     | ⏸ Only if C1 stalls                 |
| **C5** Dedicated API test instance                                                        | M             | $$        | Isolation                                | ❌ Superseded by L3                 |
| **C2** `showDiagnostics` on production                                                    | XS            | —         | —                                        | ❌ **Reject**                       |

Scale: XS < ½ day · S ≈ 1 day · M ≈ 2–4 days · L ≈ 1–2 weeks. Estimates are of _your_ time and
are rough. L1 sat at the top of the M band once it started carrying the sandbox host and
stub-layer promotion, not just the old `SongIndexLocal`-only scope — and shipped there.

**The path:** L0 → L1 → L1f are done ([PR #250](https://github.com/music4dance/music4dance/pull/250)
plus the [L1f](../dev-testing/contributor-test-environments.md#l1f--search-filter-and-sort-for-songindexlocal-new-fast-follow) follow-up).
Next up is C1/C3 whenever a contributor is actually in the loop, with L3's setup guide written
when they ask for a deployment of their own. L2 and C4 stay on the shelf until something
specific demands them.

---

## Open Questions

- **What does the developer actually want?** You're asking — and the answer may collapse
  whole branches of this document. If they're content with L0 + L1 and a tunnel to localhost,
  the cloud options are all moot and L2 waits indefinitely.
- **What is the current SQL-Server-on-Apple-Silicon story?** Specifically whether native ARM64
  server container images now exist. Determines whether L0's guide leads with Docker or with
  Azure SQL serverless. Verify before writing.
- **Is the free Azure SQL Database tier still on offer, and on what terms?** It would make
  their database cost genuinely zero and is the cleanest answer to the ARM question. Verify
  before promising.
- **How large a sample is useful?** Bounded by their free search tier. Recommend starting at
  ~2,000–5,000 songs, measuring the resulting index size, and tuning — rather than guessing
  and rebuilding.
- **Does the dev-portal download belong in Phase 6, or earlier?** It shares the
  `/developers` surface with C3, so building both at once is cheaper than building them apart
  — but only if L2's sanitizer exists by then.
- ~~Does `m4d.Sandbox` referencing `m4d.csproj` actually pick up controllers, views, and static
  web assets for free?~~ **Resolved: yes.** `m4d.Sandbox/Program.cs` project-references
  `m4d.csproj` and points `WebRootPath` explicitly at `m4d/wwwroot` (the one piece that needed
  doing manually — controllers/views came for free via project reference, static assets did
  not until `WebRootPath` was set).
- ~~InMemory or LocalDB for `m4d.Sandbox`'s EF store?~~ **Resolved: InMemory**, with
  `CreateTransientContext` taught to build a matching `UseInMemoryDatabase` context instead of
  throwing (see L1b above) — at the cost of 2 more EF1001 warnings, not yet folded into the
  tracked internal-EF-API cleanup.
- ~~Which of L1f's options should the search/filter/sort follow-up actually build?~~
  **Resolved: Option A**, implemented. `SongIndexLocal.Search(SongFilter, ...)` now evaluates
  `DanceQuery`/`TagQuery`/`UserQuery`/`KeywordQuery`/`SongSort`/tempo-length range/cruft directly
  against `_songStore`, and `SongSearch.Search()` (`m4d/Services/SongSearch.cs`) was changed to
  call that overload (now `virtual` on `SongIndex`) instead of pre-flattening to `SearchOptions`,
  so the browse/filter UI's actual call path reaches it — Azure-backed behavior is unchanged
  since the base implementation still just does the same flattening inline. Covered by
  `m4dModels.Tests/SongIndexLocalSearchTests.cs` (dance threshold, tempo range, keyword, tag,
  sort, paging). Remaining gaps, deferred as originally scoped: raw/customsearch filters
  (falls back to keyword+sort+paging only), per-dance-scoped tag queries
  (`DanceQueryItem.TagQuery`), Purchase/service-availability filtering, and vote-based queries
  (`UserQuery.IsVoted` — still routes through `VoteSearch`/`StreamAll`, which this class doesn't
  override) — and `SimpleSearch`/`FindArtist`/`SongsFromTitle`/`SongsFromTitleArtist`/`List`
  (the free-text search box and a few narrower flows) still call the private `DoSearch` directly
  and aren't intercepted.
- **Is there any day/window-boundary time dependency in the rating-cap logic** (`TryGetCappedDelta`
  and friends) that would make manual voting tests flaky in the sandbox around midnight or a
  period rollover? Flagging as a question rather than asserting either way — worth a quick check
  before promising contributors a frictionless voting demo.
- **Is a `/sandbox/reset` endpoint worth building**, or does a `dotnet run` restart cover the
  "get back to a known-good state" need well enough? Not built in PR #250. The former is real
  new code; the latter is free. Decide once someone's actually hit the friction, not
  preemptively.
- ~~Is the CI end-to-end smoke test against `m4d.Sandbox` (L1e) still worth adding?~~
  **Planned:** see [playwright-e2e-testing.md](../dev-testing/playwright-e2e-testing.md) for the environment
  setup and minimal coverage plan, proposed as a separate GitHub Actions workflow rather than
  folded into `ci-server.yaml`/`ci-client.yaml`.

---

