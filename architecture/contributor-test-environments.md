# Contributor Test Environments

**Status:** 📋 Proposed — options analysis, nothing implemented

**Context:** An independent iOS developer has offered to implement
[public-api-authorization.md](public-api-authorization.md). There is no business or
contractual relationship, so they must be able to build, run, and test without access to
production code paths, production data, production secrets, or the Azure subscription.

This document lays out the options, what each costs, and a recommended sequence.

It also closes a promise already in the [README](../README.md):

> *"there are some additional hurdles to getting sandboxed development environments set up.
> If you are interested in contributing code, please create a feature with your idea …
> That will increase the priority of figuring out ways to get past the blocking issues."*

This is that priority arriving. Whatever gets built here is reusable for every future
contributor, which changes the cost calculus — it is infrastructure, not a favor.

---

## Executive Summary

**The central finding: the contribution needs far less access than it first appears.**

Roughly two-thirds of the API work — OpenIddict wiring, `/oauth/authorize`, `/oauth/token`,
`/oauth/revoke`, PKCE verification, consent UI, OIDC `id_token`, refresh rotation, tier
policy, metering — touches **SQL and ASP.NET Core Identity only**. It needs zero song data,
zero Azure Search, zero third-party keys, and zero deployed instance. That is also the
security-critical part where review burden is highest. **The work that needs the least access
is the work that matters most.** That alignment is lucky and should be exploited.

**Recommended sequence:**

| Rung | What | Unblocks | Cost |
| --- | --- | --- | --- |
| **0** | Contributor setup guide + DCO; empty local DB, no keys, no data | API Phases 1–2, 4 | **XS** |
| **1** | Committed song fixture + local stub index (`SongIndexLocal`) | API Phases 3, 7 | **M** |
| **2** | You deploy their PR branch to the existing test site on request | End-to-end iOS validation | **XS** |
| **3** | Owner-scoped API diagnostics (their `client_id` only) | Self-service debugging | **S** |
| **4** | Samplification (sanitized data export) | Realistic-scale local testing | **L** |
| **5** | GitHub Actions environment-gated deploy | Self-service test deploys | **M** |
| **6** | Dedicated Azure sandbox resource group | Their own cloud instance | **M** + $/mo |

Rungs 0–3 total under a week and cover the realistic need. Rungs 4–6 are the expensive ones
and **none of them is a prerequisite for starting**. Do not build them speculatively.

**Two assumptions worth killing early**, because both inflate the plan:

1. *"They need a cloud instance to test the iOS app."* No. `ASWebAuthenticationSession`
   against a local server works fine — a dev cert on the LAN, or a Cloudflare/ngrok/Tailscale
   tunnel for a real HTTPS hostname. The `danzq://` redirect never touches our infrastructure.
2. *"They need production-like data."* No. The resolve cascade needs the *shape* of the data
   (songs carrying ISRC / iTunes / Spotify service IDs), not its volume. A few hundred
   hand-picked songs exercise every branch. Scale testing is our job, on our index.

---

## The Reframe: what does each phase actually require?

Mapping the phases from [public-api-authorization.md](public-api-authorization.md#implementation-phases)
onto environment needs:

| Phase | SQL | Song data | Azure Search | 3rd-party keys | Deployed instance |
| --- | --- | --- | --- | --- | --- |
| 1. Foundation (OpenIddict, schema, `/v1/dances`) | ✅ | — | — | — | — |
| 2. Authorization flow (**security-critical**) | ✅ | — | — | — | — |
| 3. Read API (`resolve` cascade) | ✅ | small fixture | stub OK | — | — |
| 4. Metering / tier policy | ✅ | — | — | — | — |
| 5. Trial tier (DeviceCheck) | ✅ | — | — | Apple only¹ | helpful |
| 6. Developer self-serve `/developers` | ✅ | — | — | — | — |
| 7. Voting write API | ✅ | small fixture | stub OK | — | — |

¹ Apple DeviceCheck / App Attest keys belong to **their** Apple developer team, not ours —
nothing to provision on our side.

`/v1/dances` deserves a callout: the dance catalog comes from the `Dance` table and
`DanceStatsManager`, and [`m4dModels.Tests/TestData/`](../m4dModels.Tests/TestData/) already
ships `test-dances.json`, `test-dances.txt`, `test-tags.txt`, and `dancestatistics.txt`. The
entire Phase 1 deliverable is testable against data **already committed to the public repo**.

---

## Hard Constraints Found in the Codebase

These shape every option below. Several rule out the obvious approach.

| Constraint | Evidence | Consequence |
| --- | --- | --- |
| **`showDiagnostics` is a full-database-export role, not a viewer role** | [AdminController.cs:1732](../m4d/Controllers/AdminController.cs#L1732) `BackupDatabase` and [:1846](../m4d/Controllers/AdminController.cs#L1846) `BackupTail` are gated on it, and [`SerializeUsers`](../m4dModels/DanceMusicService.cs#L841) emits **password hashes, security stamps, emails, and external-provider keys** for every user | **Cannot be granted on production.** Reusing it as-is would hand over the entire user table. Needs a new narrow capability instead — see [C3](#c3--owner-scoped-api-diagnostics-recommended) |
| **Azure AI Search RBAC has no index-level scope** | Search is authenticated by `DefaultAzureCredential` / RBAC, not API keys ([Program.cs:308-334](../m4d/Program.cs#L308)); data-plane roles are assignable at service scope only | There is **no way** to grant read on `songs-test-3` without also granting read on `songs-prod-3` |
| **Test and production indexes live on the same search service** | [appsettings.json](../m4d/appsettings.json) — `SongIndexProd-*` and `SongIndexTest-*` both point at `music4dance.search.windows.net` | Compounds the above. Any search access for a contributor means a **separate service**, theirs or a new sandbox one. (`PageIndex` already sits on a second service, `m4d.search.windows.net`, so multi-service is established practice) |
| **Every third-party dependency already fails soft** | `AddGoogleWithResilience` / `AddFacebookWithResilience` / `AddSpotifyWithResilience` ([AuthenticationBuilderExtensions.cs](../m4d/Configuration/AuthenticationBuilderExtensions.cs)), `AddEmailSenderWithResilience` / `AddReCaptchaWithResilience` ([ServiceCollectionExtensions.cs](../m4d/Configuration/ServiceCollectionExtensions.cs)) each catch, `MarkUnavailable`, warn, and continue; email falls back to `NullEmailSender` | **The app already boots with no third-party keys at all.** The service-resilience work (phases 1–7) accidentally solved most of the onboarding problem. No stub framework needs building |
| **Azure App Configuration / Key Vault are skipped in Development** | [Program.cs:222](../m4d/Program.cs#L222) — `if (!isDevelopment)` guards the whole `AddAzureAppConfiguration` block | Local development reads user secrets and `appsettings.Development.json`. A contributor needs **no access to our config store**, which would otherwise be a hard blocker |
| **Admin bootstrap already exists** | [`UserManagerHelpers.SeedUsers`](../m4d/Areas/Identity/UserManagerHelpers.cs#L13) creates `M4D_ADMIN_USER` with `EmailConfirmed = true` and grants `canTag`, `canEdit`, `showDiagnostics`, `dbAdmin` | Their local admin account is **two environment variables**. No need to carry over a real password hash, and no email round-trip to confirm the account |
| **Some usernames are load-bearing in code** | `ChunkedSong.IsBatch` matches `batch\|P` / `batch-*`; `tempo-bot` is cap-exempt ([DanceRatingCapTests.cs](../m4dModels.Tests/DanceRatingCapTests.cs)); `dgsnure` is hardcoded in `s_unconfirmedVoteSources` ([unconfirmed-dance-votes.md](unconfirmed-dance-votes.md)); `IsPseudo` derives from an `@music4dance.net` email ([user-name-visibility.md](user-name-visibility.md)) | A naive "randomize all usernames" sanitizer **silently changes vote arithmetic and display logic**, producing sample data that behaves differently from production. See [Samplification](#samplification-design) |
| **The data is not MIT licensed** | [README.md](../README.md) — *"the data running on the site is not included in that license"* | Sharing a sample is a **licensing decision**, not only a privacy one. See [Legal Prerequisites](#legal--process-prerequisites) |
| **`(localdb)\mssqllocaldb` is Windows-only** | [appsettings.json](../m4d/appsettings.json) default connection string | An iOS developer is almost certainly on macOS. SQL Server in Docker is the answer, but Apple Silicon needs verification — the setup guide must cover this or they stall on step one |
| **There is no `CONTRIBUTING.md` and no CLA/DCO** | Repo root | Nothing currently establishes rights to merge an outside contribution to an auth system |

---

## Path 1 — Cloud Options

### C1 — They submit a PR, you review and deploy to the existing test site ✅

**Cost: XS** (zero build; ~30 min per deploy cycle of your time)

The existing `azure-pipelines.yml` already parameterizes `environment: test` → `m4d-test` app
+ `SongIndexTest-3`. Nothing to build.

| Pros | Cons |
| --- | --- |
| Zero engineering cost — works today | Serializes on your availability; a 4-hour loop feels slow to a volunteer |
| You review every line before it runs anywhere | Every trivial fix costs you an interrupt |
| Fork-based PRs get no secrets and no OIDC token — the strongest isolation GitHub offers | Doesn't scale past one contributor |
| No trust grant required at all | They cannot see server-side failure detail without [C3](#c3--owner-scoped-api-diagnostics-recommended) |

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

| Pros | Cons |
| --- | --- |
| Nothing to build | Grants full PII export to someone with no contractual relationship |
| | Provider keys are stable Google/Facebook/Spotify user identifiers — irrevocable once leaked |
| | Almost certainly a privacy-law problem for any EU users |

**Verdict: reject as stated.** The *want* behind it is legitimate, though — see C3.

### C3 — Owner-scoped API diagnostics ✅ (recommended)

**Cost: S** (~1 day)

The correct version of C2. Instead of a role, use **ownership**: the design doc's
`ApiClientProfile` already carries `DeveloperUserId`
([public-api-authorization.md](public-api-authorization.md#data-model)). So the scope is
naturally self-limiting — *"you can see diagnostics for API clients you own."* No role grant,
no PII, no admin surface, and it generalizes to every future third-party developer for free.

Surface, all filtered to `client_id` values the caller owns:

- Recent `/v1/*` requests: timestamp, route, status, latency, matched tier, allowance remaining
- Token events: issued / refreshed / revoked / rejected, **with the rejection reason** —
  `invalid_code_verifier`, `redirect_uri_mismatch`, `code_replayed`, `token_revoked`,
  `allowance_exhausted`. This is the single highest-value item; OAuth failures are opaque
  from the client side and this is where a contributor otherwise burns days
- Rate-limit counters for their client
- Never: usernames, emails, tokens, other clients' traffic, site-wide logs

| Pros | Cons |
| --- | --- |
| Turns C1's slow loop into a fast one — they diagnose their own failures | New endpoint to build and secure |
| Zero PII exposure by construction | Only useful once Phase 1's schema exists (chicken-and-egg for the earliest work) |
| Ships as part of the API anyway — it is `/developers` self-serve infrastructure (Phase 6), pulled forward | Needs care that "own client" checks can't be spoofed |

**Implementation:** extend `UsageLog` with the nullable `ClientId` that Phase 4 already
specifies; add an `ApiClientEvent` table for token lifecycle events; add
`/developers/{clientId}/diagnostics` authorized by `profile.DeveloperUserId == currentUserId`.
Reuse the existing rate-limit counters rather than adding new ones.

**Pull this into Phase 1**, not Phase 6. It pays for itself immediately by making C1 viable.

### C4 — GitHub Actions deploy to test, gated away from production ✅

**Cost: M** (2–4 days, partly work worth doing anyway)

The user's question was: *can we let them deploy to test without letting them deploy to prod?*
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

| Pros | Cons |
| --- | --- |
| They self-serve test deploys; you stop being the bottleneck | Requires giving repo **write** access — a real trust grant, and they could push to non-protected branches |
| Prod gate is enforced by the platform, not by convention | Loses the fork-PR isolation of C1 (fork PRs get no OIDC token, which is a *feature*) |
| No stored secrets — OIDC federation only | Two pipelines to maintain unless you fully migrate off Azure DevOps |
| You get a better prod pipeline out of it regardless | Test deploys touch the shared search service, so a bad migration can disturb `songs-test-3` |
| Reusable for every future contributor | |

**Verdict: build it if and only if C1's loop becomes the bottleneck.** It is the right
long-term answer and it is genuinely secure; it is just premature at one contributor.

**Caveat worth internalizing:** even with perfect gating, a test deploy runs *their* code
against a database and search index that are yours. Test-environment compromise is not
production compromise, but it is not nothing either.

### C5 — Dedicated API test instance

**Cost: M** + **$/month**

A third environment (`m4d-api-test`) so their deploys never disturb your own test site.

| Pros | Cons |
| --- | --- |
| Full isolation from your test workflow | Another app service, another SQL DB, and — because of the RBAC constraint — another **search service** |
| Free to destroy and rebuild | Ongoing cost against a project explicitly run without an SLA to keep costs down |
| | Third environment to keep in sync with migrations and config |

**Verdict: skip.** The pipeline already supports `test`; collisions at one contributor are
rare, and coordination is cheaper than a standing environment. Revisit only if two
contributors ever need it simultaneously.

### C6 — They stand up their own instance

**Cost: M** for the guide; **$/month is theirs**

Covered under [L3](#l3--they-deploy-their-own-cloud-instance), since the hard part is the
setup documentation and the data, not the cloud.

---

## Path 2 — Local Options

### L0 — Build and run with nothing ✅ (do this first)

**Cost: XS** (~half a day of documentation)

The constraints table's most useful finding: **this already works.** Empty SQL database,
migrations applied, `M4D_ADMIN_USER` / `M4D_ADMIN_PASSWORD` set, no third-party keys, no
search service, no data. Third-party services mark themselves unavailable and the app runs.

That is enough to build and unit-test API Phases 1, 2, 4, and 6 — including the entire
security-critical OAuth surface.

**Implementation:** `CONTRIBUTING.md` + `architecture/contributor-setup.md` covering:

- Prerequisites: .NET 10 SDK, Node 22, Yarn via corepack, SQL Server
- **The macOS SQL story explicitly** — `mssql/server` in Docker; *verify the current Apple
  Silicon situation before writing this section*, since it is the single most likely place a
  Mac-based contributor stalls. Include a ready-to-paste `docker run` and connection string
- `dotnet user-secrets set` for the connection string, `M4D_ADMIN_USER`, `M4D_ADMIN_PASSWORD`
- `dotnet ef database update`
- Expected startup warnings — a printed list of the "not configured" lines they *should* see,
  so absent services read as normal rather than as breakage
- `yarn install && yarn build`, then the test targets from [CLAUDE.md](../CLAUDE.md)
- What does **not** work without keys: social login, outbound email, captcha, song search,
  service track lookup

The "expected warnings" section is worth more than it sounds. Without it, the first
five minutes of a new contributor's experience is a wall of scary-looking startup errors.

### L1 — Song fixture + local stub index ✅ (recommended, highest leverage)

**Cost: M** (2–4 days) — and it improves your own test suite

This is the option not on the original list, and I think it is the best value on the board.

[`TestSongIndex`](../m4dModels.Tests/TestSongIndex.cs) is already an in-memory `SongIndex`
subclass — and it already overrides `GetSongFromService`, with a comment stating its purpose
verbatim: *"Lets tests exercise service-id/ISRC lookup paths … without a real search
backend."* **That is precisely the Phase 3 resolve cascade.** The capability exists; it is
just trapped in the test project.

Two pieces to build:

1. **Promote it.** A `SongIndexLocal` in `m4dModels`, selected by configuration (e.g.
   `SearchBackend: Local`), that keeps songs in memory and hydrates from a fixture file at
   startup. `SongIndex` is already designed for subclassing — `SongIndexNext` and
   `TestSongIndex` both do it — so this follows an established seam rather than cutting a new
   one. Gaps to fill beyond `TestSongIndex`: free-text search over title/artist, dance
   filters, and paging good enough for the site to render. It does **not** need to match Azure
   Search semantics — it needs to be good enough to develop against, and honest about the
   difference.
2. **A committed song fixture.** A few hundred songs in the existing serialized format,
   generated by `Admin/IndexBackup` with a `SongFilter` and passed through the sanitizer from
   [Samplification](#samplification-design). Chosen for *coverage*, not size:
   - songs with ISRC (`R:`), iTunes (`I:`), and Spotify (`S:`) service IDs — the exact
     high-confidence rungs of the cascade
   - songs with **only** title/artist, to exercise the fuzzy fallback
   - deliberate near-miss title/artist pairs, so match confidence can be evaluated
   - multi-dance songs, tempo edge cases, songs with tags, songs with `batch`-sourced votes,
     songs with a `dgsnure` unconfirmed vote

| Pros | Cons |
| --- | --- |
| No Azure, no keys, no cost, no data agreement (if the fixture ships in the repo) | Fixture is not Azure Search — scoring and analyzer behaviour differ, so "works locally" ≠ "works in prod" |
| Runs in CI, so the API gets real regression coverage | `SongIndexLocal` is a new component you own and must keep in step with `SongIndex` |
| Directly benefits your own testing — Phase 3 and 7 become unit-testable | Committing song data to a public MIT repo needs a licensing note (see below) |
| Turns "here is a setup guide" into "clone, build, run" | Fixture goes stale as the format evolves; needs a regeneration path |
| Reusable by every future contributor forever | |

**Licensing note:** `test-dances.txt`, `test-tags.txt`, and a 2 MB `dancestatistics.txt` are
**already committed** to the public repo, so derived-data precedent exists. A song fixture
goes slightly further (titles/artists are third-party factual metadata; votes and tags are the
community-contributed part). With synthetic usernames throughout, this is defensible — but
it is your call, and the repo should carry an explicit note that data files are under separate
terms from the MIT code. The fallback is distributing the fixture out-of-band, at the cost of
losing the CI benefit.

### L2 — Samplified realistic dataset

**Cost: L** (1–2 weeks) — see [Samplification](#samplification-design)

A sanitized subset of real data, loaded through the existing `Admin/ReloadDatabase` path, plus
their own free-tier Azure Search service.

| Pros | Cons |
| --- | --- |
| Realistic scale and messiness; real Azure Search semantics | The largest build on this list |
| Reusable for your own testing and for future contributors | Requires a data-use agreement |
| Exercises the real index, so scoring behaviour is genuine | They must create an Azure account and a search service |
| Would let them work on *any* part of the codebase, not just the API | Free-tier limits constrain sample size — *verify current limits*, historically ~50 MB / 3 indexes |
| | Sanitizer correctness is a security-relevant property that needs its own tests |

**Verdict: valuable, but not for this contribution.** Build it when the need is broader than
one API project — or build the *sanitizer* now as part of L1 (the fixture needs it anyway) and
defer the full-dataset pipeline.

**Note on the free search tier:** Azure historically allows **one free search service per
subscription**. Check whether yours is already consumed — if it is, this option quietly
becomes "they pay for Basic (~$75/mo)," which likely kills it. Worth verifying before
promising anything.

### L3 — They deploy their own cloud instance

**Cost: M** for documentation; their cloud bill

L2 plus their own App Service and SQL. Reasonable for a serious long-term contributor, and it
answers the user's question about external dependencies concretely:

| Dependency | Config key | Do they need it? |
| --- | --- | --- |
| Google OAuth | `Authentication:Google:*` | No — degrades cleanly. Own app if wanted (free) |
| Facebook OAuth | `Authentication:Facebook:*` | No — and Facebook requires app review; recommend leaving off |
| Spotify OAuth | `Authentication:Spotify:*` | Only if testing Spotify sign-in. Own app, free, minutes. [Program.cs:739](../m4d/Program.cs#L739) already documents the `localhost` HTTPS-redirect accommodation |
| Email (Azure Comm. Services) | `Authentication:AzureCommunicationServices:ConnectionString` | No — `NullEmailSender` fallback, and seeded/sanitized users are pre-confirmed |
| reCAPTCHA | `Authentication:reCAPTCHA:*` | No. Google publishes always-pass test keys — include them in the guide (*verify still current*) |
| Azure Search | RBAC, `SongIndex*` sections | Only for L2/L3; theirs, never ours |
| App Config + Key Vault | `AppConfig:Endpoint` | **No** — Development skips it entirely |
| Commerce | `Configuration:Commerce:Enabled` | No — set `false` |
| GTM / Google Tags | feature flags | Already `false` in `appsettings.Development.json` |

**Never share:** Google, Facebook, Spotify, or Azure Communication Services credentials. Those
are *our* identity with those providers; sharing them is both a security problem and likely a
terms violation. **They create their own or go without** — and going without works.

### L4 — Sandbox configuration profile ✅

**Cost: S** (~1 day)

Rather than building stubs, make "no external dependencies" a **declared, tested mode** instead
of an accident that happens to work:

- `appsettings.Sandbox.json` + a `SANDBOX_MODE` flag
- Forces `SearchBackend: Local` (L1), `Commerce:Enabled: false`, captcha off
- A Development-only `FileEmailSender` writing `.eml` files to `local/mail/` — genuinely
  useful for **your** work too (password reset and confirmation flows become inspectable
  locally), and it removes the last reason a contributor would ask for an email key
- A startup banner stating plainly which services are stubbed

| Pros | Cons |
| --- | --- |
| Turns an emergent property into a supported, CI-tested configuration | One more configuration path to keep working |
| `FileEmailSender` is independently useful to you | Risk of sandbox-only bugs if it drifts from real config |
| Makes the contributor's environment reproducible and describable | |

---

## Samplification Design

The largest single build, needed for L2 and (in miniature) for L1's fixture. **Good news: the
extraction half already exists.**

### Extraction — already built

- `Admin/BackupDatabase` ([AdminController.cs:1732](../m4d/Controllers/AdminController.cs#L1732))
  emits users, dances, tags, playlists, and searches sections. The songs section is
  commented out (`DBKILL`) because songs now live in the index.
- `Admin/IndexBackup` ([AdminController.cs:1665](../m4d/Controllers/AdminController.cs#L1665))
  streams the songs section from the index via `BackupIndexStreamingAsync` — **and it already
  takes a `SongFilter`.** Sample *selection* is therefore free: express the subset as a filter.
- `Admin/ReloadDatabase` ([AdminController.cs:928](../m4d/Controllers/AdminController.cs#L928))
  is the load path, already sectioned and already able to wipe-and-reload.
- Precedent exists: [`test-users-clean.txt`](../m4dModels.Tests/TestData/test-users-clean.txt)
  is a hand-sanitized user set using `UserA` / `UserB` / `batch-a`, placeholder hashes
  (`XXXXXXXXXXX`), and zero-GUID security stamps. **The convention is already established** —
  this work automates what was done once by hand.

### Transform — the new build

Order matters, and it answers the "how do songs and users stay consistent?" question:

1. **Select songs** via `SongFilter` (`Admin/IndexBackup`). For API work, filter toward songs
   carrying service IDs.
2. **Derive the user set from the songs**, not the other way round. Scan the sampled property
   logs for every distinct `User=` value; that set — plus preserved system accounts — is
   exactly what the users section must contain. This guarantees consistency in the direction
   that matters (every user a song references exists). The reverse case, a user with no songs,
   is harmless.
3. **Emit** the pseudonymized users section, then the sanitized songs section.

**Where to implement it:** as an admin action (`Admin/Samplify`) or a CLI tool inside
`m4dModels` — **not** a `scripts/*.ps1` text transform. Usernames live *inside* the property
log, which has a real parser (`ModifiedRecord`, `SongPropertyBlockParser`), and
[CLAUDE.md](../CLAUDE.md) is explicit that these formats must be built and parsed through the
class library. A regex over property logs is exactly the silent-breakage this rule exists to
prevent.

### Transform rules

**Preserve verbatim** — these names are load-bearing in code, and rewriting them changes
behaviour:

| Preserve | Why |
| --- | --- |
| Any `@music4dance.net` account | `IsPseudo` / `IsM4d` derives from the email domain |
| `batch`, `batch-*` | `ChunkedSong.IsBatch`; exempt from `TryGetCappedDelta`'s ±1 cap |
| `tempo-bot` | Cap-exempt; asserted by name in `DanceRatingCapTests` |
| `dgsnure` | Hardcoded in `s_unconfirmedVoteSources` |
| Spotify proxy users | `IsPseudo` via `IsSpotify` |
| The `\|P` pseudo suffix | `ModifiedRecord` splits on it; stripping it changes attribution |

**Rewrite** for real registered users:

| Field | Treatment |
| --- | --- |
| `UserName` | Deterministic pseudonym — `Firstname L.` from a name table, via a salted keyed hash so successive samples are stable and diffable. Resolve collisions explicitly |
| `Email` | `{pseudonym}@example.invalid` — RFC 2606 reserved TLD, so no accidental delivery |
| `PasswordHash` | Fixed placeholder (`LoadUsers` treats blank as null; `test-users-clean.txt` uses `XXXXXXXXXXX`) |
| `SecurityStamp` | Zero GUID, matching existing test data |
| `Providers` | **Empty.** This column holds Google/Facebook/Spotify provider keys — stable third-party user identifiers. Highest-severity field in the file |
| `Region` | Drop, or coarsen to country |
| Subscription fields | Synthesize rather than copy — real purchase history is commercially sensitive. Keep the *shape*: one premium, one trial, one lapsed, several free, so tier logic is exercised |
| `LastActive` / `StartDate` | Keep, or jitter by days |

**Property log:** map every `User=` occurrence (including `|P` forms) through the same table.
Consistency with the users section is what makes vote replay produce the same answer.

**Drop by default:** the searches and playlists sections. Saved-search filters can embed
usernames — `test-searches.txt` contains `\-me|H`, a user filter — and playlists carry user
FKs and Spotify playlist IDs. Low value for API work, non-trivial to sanitize correctly.
Substitute a handful of synthetic searches if needed.

**Their admin account:** don't transplant it. `M4D_ADMIN_USER` / `M4D_ADMIN_PASSWORD` seeding
already creates a confirmed account with `canTag`, `canEdit`, `showDiagnostics`, and `dbAdmin`.
Two environment variables, zero new code, and no password hash ever leaves our systems.

### Verification — the part that makes this trustworthy

A sanitizer without tests is a leak with a schedule. Three tests, all cheap:

1. **PII denylist scan.** No real username, email, password hash, security stamp, or provider
   key from the source appears anywhere in the output. Run it as a gate, not a review step.
2. **Referential integrity.** Every `User=` in the songs section resolves to a user in the
   users section.
3. **Vote-math invariance.** Sum dance-rating weights per dance before and after
   sanitization; they must match. This is the sharpest test on the list — it proves the
   username rewrite did not disturb `TryGetCappedDelta` or the batch exemptions, which is the
   exact failure mode the preserve-list exists to prevent. Cheap to write, catches the
   subtlest bug.

Plus a **size check**: report the output size against the target search tier's storage limit,
since the free tier is the binding constraint on sample size. Note that the
`SongPropertyCompression` feature flag (Brotli above 10k chars) affects stored size.

Sanitized output goes to `local/` (gitignored per [CLAUDE.md](../CLAUDE.md)), except the small
committed fixture from L1.

---

## Legal & Process Prerequisites

Cheap, and genuinely blocking. Do these before sharing anything.

1. **Contribution rights.** No `CONTRIBUTING.md` and no CLA/DCO exist today. For a
   contribution to the **authentication system**, establish this in writing first. A
   [DCO](https://developercertificate.org/) (`Signed-off-by` in commits, enforced by a GitHub
   check) is the near-zero-cost option and standard practice; a full CLA is heavier and
   probably unnecessary here.
2. **Data-use terms**, if any sample data changes hands: development and testing only, no
   redistribution, no re-identification attempts, delete on request. One page. Necessary
   because the README already states the data is outside the MIT grant.
3. **Privacy posture.** Pseudonymized is not anonymous. The defensible position — and it is
   genuinely defensible — is *real song data, fully synthetic identities*: all real usernames
   and emails rewritten, all provider keys dropped, all hashes discarded. Say that explicitly
   in the terms so both sides know what was done.
4. **Secrets hygiene**, stated plainly to them: they will never receive our third-party
   credentials, and they don't need them. That is a design property of the resilience layer,
   not a limitation to work around.
5. **Repo access decision.** C1 (fork PRs) requires no grant. C4 requires write access.
   Fork-based is the correct default given no contractual relationship.

---

## Cost Summary

| Option | Build cost | Ongoing $ | Unblocks | Verdict |
| --- | --- | --- | --- | --- |
| **L0** Setup guide, run with nothing | XS | — | Phases 1, 2, 4, 6 | ✅ **Do first** |
| **C1** You deploy their PR to test | XS | — | End-to-end validation | ✅ **Default** |
| **C3** Owner-scoped API diagnostics | S | — | Self-service debugging | ✅ **Pull into Phase 1** |
| **L4** Sandbox config profile + `FileEmailSender` | S | — | Reproducible environment | ✅ Cheap, useful to you |
| **L1** Song fixture + `SongIndexLocal` | M | — | Phases 3, 7; CI coverage | ✅ **Highest leverage** |
| **C4** GitHub Actions environment-gated deploy | M | — | Self-service deploys | ⏸ Only if C1 stalls |
| **L2** Samplification pipeline | L | — | Realistic-scale local | ⏸ Build sanitizer now, pipeline later |
| **L3** Their own cloud instance | M (docs) | Theirs | Full independence | ⏸ If they ask |
| **C5** Dedicated API test instance | M | $$ | Isolation | ❌ Premature |
| **C2** `showDiagnostics` on production | XS | — | — | ❌ **Reject** |

Scale: XS < ½ day · S ≈ 1 day · M ≈ 2–4 days · L ≈ 1–2 weeks. Estimates are of *your* time and
are rough.

**The path:** L0 → C1 → C3 → L1, adding L4 alongside. That is roughly one week of your effort,
covers all seven API phases, requires no data agreement if the L1 fixture ships in-repo,
requires no Azure access for them, and leaves behind reusable contributor infrastructure. C4,
L2, and L3 are real options — just not yet.

---

## Open Questions

- **Is the free Azure Search tier still available on your subscription?** Historically one per
  subscription. If already consumed, L2/L3 quietly become a ~$75/mo Basic-tier proposition,
  which probably rules them out. **Check before promising anything.**
- **Will you commit a song fixture to the public repo?** L1's CI benefit depends on it.
  Derived-data precedent exists (`dancestatistics.txt`, `test-tags.txt`), but songs go a step
  further. A repo note that data files sit under separate terms from the MIT code would cover
  it.
- **What is the current SQL-Server-on-Apple-Silicon story?** Verify before writing L0's setup
  guide; it is the most likely place a Mac contributor stalls on step one.
- **Which environment does the contributor actually want?** Ask before building. They may be
  perfectly happy with L0 + L1 and a tunnel to localhost, in which case the entire cloud
  branch is moot. This question is free and should come first.
- **Does `SongIndexLocal` need to be honest about scoring differences?** Recommend yes,
  loudly — a startup warning that search relevance is not representative, so nobody debugs a
  ranking difference that is a fixture artifact.
- **DCO or full CLA?** Recommend DCO for cost, but this touches authentication, so it's worth
  a deliberate decision rather than a default.
- **Should the sanitizer be an admin endpoint or a CLI tool?** Endpoint reuses the existing
  admin task/monitor plumbing; CLI is easier to run repeatedly over a large export. Endpoint
  is probably right given `BackupDatabase` already lives there.

---

## Related Documents

- [public-api-authorization.md](public-api-authorization.md) — the contribution being scoped
- [index-backup-streaming.md](index-backup-streaming.md) — the extraction half of samplification
- [testing-patterns.md](testing-patterns.md) — serialized song format for fixtures
- [user-name-visibility.md](user-name-visibility.md) — pseudo/batch user semantics
- [unconfirmed-dance-votes.md](unconfirmed-dance-votes.md) — `dgsnure`, the ±1 cap
- [search-index-versioning.md](search-index-versioning.md) — index naming and versioning
- [SELF_CONTAINED_DEPLOYMENT.md](SELF_CONTAINED_DEPLOYMENT.md) — deployment modes
- [admin-pages.md](admin-pages.md) — admin surface and role gating
