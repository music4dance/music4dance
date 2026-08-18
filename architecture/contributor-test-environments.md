# Contributor Test Environments

**Status:** 📋 Proposed — options analysis, key decisions settled

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

## Decisions Settled

| Question | Decision |
| --- | --- |
| **Contribution licensing** | **DCO**, not a CLA. Keep the process light — `Signed-off-by` enforced by a GitHub check |
| **Sample data in the repo** | **No.** No song data is committed, sanitized or otherwise. See [L1](#l1--local-stub-index--inline-test-coverage--recommended) for how CI coverage survives that |
| **Sample data delivery** | Out-of-band initially; **a dev-portal download gated on accepting testing-only terms** long term |
| **Sanitizer form factor** | **Admin endpoint** now, alongside `BackupDatabase`; a dev-portal endpoint later, feeding the download above |
| **Service accounts in samples** | **Not obfuscated** — `batch*`, `tempo-bot`, `dgsnure`, `@music4dance.net`. They are machine identities, not personal data. ⚠️ **Spotify proxy users are the exception** — see [the catch](#the-spotify-proxy-user-catch) |
| **Realistic-scale environment** | The developer creates **their own** Azure deployment and search service, loaded with a samplified dataset |
| **Search relevance honesty** | The local index must state plainly at startup that scoring is not representative |
| **Free Azure Search tier** | Ours is consumed (site search could be moved to the paid subscription to free it, but that solves a problem we no longer have). **A new subscription carries its own free tier**, so this constraint lands on their side, not ours |

**Still open:** what the developer actually wants — you're asking. That answer may collapse
the cloud branch entirely.

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
| **0** | Contributor setup guide + DCO; empty local DB, no keys, no data | API Phases 1–2, 4, 6 | **XS** |
| **1** | `SongIndexLocal` + inline-constructed test coverage | API Phases 3, 7 | **M** |
| **2** | You deploy their PR branch to the existing test site on request | End-to-end iOS validation | **XS** |
| **3** | Owner-scoped API diagnostics (their `client_id` only) | Self-service debugging | **S** |
| **4** | Samplification endpoint + terms-gated delivery | Browseable realistic dataset | **L** |
| **5** | GitHub Actions environment-gated deploy | Self-service test deploys | **M** |
| **6** | Their own Azure deployment (guide only) | Full independence | **M** (docs) |

Rungs 0–3 total under a week and cover the realistic need. **Rung 4 is now the only source of
browseable data**, since nothing ships in the repo — but it is needed for *interactive*
development, not for correctness testing, so it still isn't a prerequisite for starting.

**Two assumptions worth killing early**, because both inflate the plan:

1. *"They need a cloud instance to test the iOS app."* No. `ASWebAuthenticationSession`
   against a local server works fine — a dev cert on the LAN, or a Cloudflare/ngrok/Tailscale
   tunnel for a real HTTPS hostname. The `danzq://` redirect never touches our infrastructure.
2. *"They need production-like data to build the API."* No. The resolve cascade needs the
   *shape* of the data — songs carrying ISRC / iTunes / Spotify service IDs — and tests can
   construct that inline. Realistic data is for exploring the site, not for proving the code.

---

## The Reframe: what does each phase actually require?

Mapping the phases from [public-api-authorization.md](public-api-authorization.md#implementation-phases)
onto environment needs:

| Phase | SQL | Song data | Azure Search | 3rd-party keys | Deployed instance |
| --- | --- | --- | --- | --- | --- |
| 1. Foundation (OpenIddict, schema, `/v1/dances`) | ✅ | — | — | — | — |
| 2. Authorization flow (**security-critical**) | ✅ | — | — | — | — |
| 3. Read API (`resolve` cascade) | ✅ | constructed | stub OK | — | — |
| 4. Metering / tier policy | ✅ | — | — | — | — |
| 5. Trial tier (DeviceCheck) | ✅ | — | — | Apple only¹ | helpful |
| 6. Developer self-serve `/developers` | ✅ | — | — | — | — |
| 7. Voting write API | ✅ | constructed | stub OK | — | — |

¹ Apple DeviceCheck / App Attest keys belong to **their** Apple developer team, not ours —
nothing to provision on our side.

`/v1/dances` deserves a callout: the dance catalog comes from the `Dance` table and
`DanceStatsManager`, and [`m4dModels.Tests/TestData/`](../m4dModels.Tests/TestData/) already
ships `test-dances.json`, `test-dances.txt`, `test-tags.txt`, and `dancestatistics.txt`. The
entire Phase 1 deliverable is testable against data **already committed to the public repo** —
and none of it is song data, so the no-commit decision doesn't touch it.

---

## Hard Constraints Found in the Codebase

These shape every option below. Several rule out the obvious approach.

| Constraint | Evidence | Consequence |
| --- | --- | --- |
| **`showDiagnostics` is a full-database-export role, not a viewer role** | [AdminController.cs:1732](../m4d/Controllers/AdminController.cs#L1732) `BackupDatabase` and [:1846](../m4d/Controllers/AdminController.cs#L1846) `BackupTail` are gated on it, and [`SerializeUsers`](../m4dModels/DanceMusicService.cs#L841) emits **password hashes, security stamps, emails, and external-provider keys** for every user | **Cannot be granted on production.** Reusing it as-is would hand over the entire user table. Needs a new narrow capability instead — see [C3](#c3--owner-scoped-api-diagnostics-recommended) |
| **Azure AI Search RBAC has no index-level scope** | Search is authenticated by `DefaultAzureCredential` / RBAC, not API keys ([Program.cs:308-334](../m4d/Program.cs#L308)); data-plane roles are assignable at service scope only | There is **no way** to grant read on `songs-test-3` without also granting read on `songs-prod-3` |
| **Test and production indexes live on the same search service** | [appsettings.json](../m4d/appsettings.json) — `SongIndexProd-*` and `SongIndexTest-*` both point at `music4dance.search.windows.net` | Compounds the above. Settled by having them run their own service in their own subscription. (`PageIndex` already sits on a second service, `m4d.search.windows.net`, so multi-service is established practice) |
| **Every third-party dependency already fails soft** | `AddGoogleWithResilience` / `AddFacebookWithResilience` / `AddSpotifyWithResilience` ([AuthenticationBuilderExtensions.cs](../m4d/Configuration/AuthenticationBuilderExtensions.cs)), `AddEmailSenderWithResilience` / `AddReCaptchaWithResilience` ([ServiceCollectionExtensions.cs](../m4d/Configuration/ServiceCollectionExtensions.cs)) each catch, `MarkUnavailable`, warn, and continue; email falls back to `NullEmailSender` | **The app already boots with no third-party keys at all.** The service-resilience work (phases 1–7) accidentally solved most of the onboarding problem. No stub framework needs building |
| **Azure App Configuration / Key Vault are skipped in Development** | [Program.cs:222](../m4d/Program.cs#L222) — `if (!isDevelopment)` guards the whole `AddAzureAppConfiguration` block | Local development reads user secrets and `appsettings.Development.json`. A contributor needs **no access to our config store**, which would otherwise be a hard blocker |
| **Admin bootstrap already exists** | [`UserManagerHelpers.SeedUsers`](../m4d/Areas/Identity/UserManagerHelpers.cs#L13) creates `M4D_ADMIN_USER` with `EmailConfirmed = true` and grants `canTag`, `canEdit`, `showDiagnostics`, `dbAdmin` | Their local admin account is **two environment variables**. No need to carry over a real password hash, and no email round-trip to confirm the account |
| **Some usernames are load-bearing in code** | `ChunkedSong.IsBatch` matches `batch\|P` / `batch-*`; `tempo-bot` is cap-exempt ([DanceRatingCapTests.cs](../m4dModels.Tests/DanceRatingCapTests.cs)); `dgsnure` is hardcoded in `s_unconfirmedVoteSources` ([unconfirmed-dance-votes.md](unconfirmed-dance-votes.md)); `IsPseudo` derives from the email domain ([ApplicationUser.cs:43](../m4dModels/ApplicationUser.cs#L43)) | Preserving these verbatim is **required for correctness**, independent of the privacy argument. See [Samplification](#samplification-design) |
| **SQL Server coupling is deeper than the connection string** | `UseSqlServer` in [DanceMusicContext.cs:27](../m4dModels/DanceMusicContext.cs#L27) and four sites in `Program.cs`; `ExecuteSqlRawAsync("TRUNCATE TABLE UsageLog")` at [AdminController.cs:1184](../m4d/Controllers/AdminController.cs#L1184); `SqlException` caught by name in five resilience paths; 13 migrations emitting `nvarchar(max)` / `nvarchar(450)` | **SQLite is not a drop-in.** See [the macOS question](#the-macos-database-question) |
| **The data is not MIT licensed** | [README.md](../README.md) — *"the data running on the site is not included in that license"* | Sharing a sample is a **licensing decision**, not only a privacy one. Settled: nothing in the repo; terms-gated delivery only |
| **There is no `CONTRIBUTING.md` and no DCO check** | Repo root | Nothing currently establishes rights to merge an outside contribution to an auth system |

---

## The macOS Database Question

### First, a clarification: LocalDB is SQL Server, not SQLite

Worth stating plainly, because the two get conflated and the conflation changes the plan.
**SQL Server Express LocalDB is the real SQL Server engine** — `sqlservr.exe`, the same
database family as the production Azure SQL — just packaged to start on demand as a user-mode
process with file-based `.mdf` attachment and no service to administer. SQLite shares *none*
of its code: it is an embedded C library that runs in-process, with its own SQL dialect and
its own type system.

| | LocalDB | SQLite |
| --- | --- | --- |
| Engine | SQL Server (`sqlservr.exe`), separate process | Embedded C library, in-process |
| Provider | `Microsoft.Data.SqlClient` | `Microsoft.Data.Sqlite` |
| Dialect | Full T-SQL | Own dialect; type affinity, not strict typing |
| Platform | **Windows only** | Cross-platform, native ARM64 |

The confusion is understandable — both are "lightweight, file-based, zero-admin" — but they
have no common ancestry.

Two consequences:

- **Good news:** the current dev setup is high fidelity. LocalDB and Azure SQL are the same
  engine, so collation, T-SQL, and provider behaviour match production. That is a large part
  of why the app moves between dev and prod without surprises, and it is worth protecting.
- **The constraint stands.** The default connection string
  (`Server=(localdb)\mssqllocaldb;…;Trusted_Connection=True;MultipleActiveResultSets=true`)
  is Windows-only three times over: `(localdb)` is a Windows-only host,
  `Trusted_Connection=True` is Windows integrated authentication, and
  `MultipleActiveResultSets=true` (MARS) is a SQL Server feature with **no SQLite equivalent
  at all**.

So moving a contributor to SQLite is not "use the lightweight version of what you already
have" — it is adopting a second database engine, and it trades away the dev/prod fidelity the
current setup gives you for free.

### Why SQLite isn't a drop-in

Your point about SQLite is correct on its own terms — it is a native ARM64 binary and needs no
emulation. But **the conclusion doesn't follow cheaply**, because the coupling to SQL Server is
not just the connection string:

- `UseSqlServer` is hardcoded in `DanceMusicContext` and four places in `Program.cs`
- **Migrations are provider-specific.** All 13 emit `nvarchar(max)` / `nvarchar(450)`; SQLite
  would need a parallel migration set or `EnsureCreated()` with no migration story at all
- `ExecuteSqlRawAsync("TRUNCATE TABLE UsageLog")` — SQLite has no `TRUNCATE`
- **Five resilience paths catch `Microsoft.Data.SqlClient.SqlException` by type**
  (`Program.cs:487`, `DMController.cs:46`, `UserMapper.cs:238`, `DanceStatsInstance.cs:219`
  and `:290`). On SQLite these become `SqliteException` and the degradation machinery
  silently stops engaging — the failure mode is *invisible*, which is the worst kind
- **Collation semantics differ.** SQL Server's default collation is case-insensitive; SQLite's
  `=` is case-sensitive and its `LIKE` is case-insensitive for ASCII only. Username lookup and
  title/artist matching depend on case-insensitive comparison, so this produces bugs that
  reproduce on their machine and nowhere else — exactly the divergence a contributor
  environment must avoid

That's a provider-portability project, not a setup step. Worth doing someday for test speed;
not worth doing to unblock one contributor.

**Better answers, in order:**

1. **Azure SQL serverless in their own subscription** ✅ — coherent with the settled decision
   that they run their own deployment. Auto-pause makes idle cost trivial, it's the real
   engine so no divergence, and there is nothing to install locally. Microsoft has published a
   perpetually-free Azure SQL Database tier (serverless, on the order of 100k vCore-seconds
   and 32 GB/month) — *verify current terms*, but a new account also carries the standard
   credit and 12-month free allowances. **Lead with this.**
2. **`mssql/server` in Docker under Rosetta 2** — widely used, works, costs nothing. Needs
   Docker Desktop with Rosetta enabled. *Verify whether native ARM64 server images now exist*
   before writing the guide; if they do, this becomes option 1.
3. **SQLite** — a real option, but as a deliberate portability project with the five items
   above as its scope, not as a shortcut.

Since they will be standing up their own Azure resources anyway, option 1 costs them one
extra resource in a portal they're already in, and removes the ARM question entirely.

---

## Path 1 — Cloud Options

### C1 — They submit a PR, you review and deploy to the existing test site ✅

**Cost: XS** (zero build; ~30 min per deploy cycle of your time)

The existing `azure-pipelines.yml` already parameterizes `environment: test` → the `m4d-test`
app plus `SongIndexTest-3`. Nothing to build.

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

**Pull this into Phase 1**, not Phase 6. It pays for itself immediately by making C1 viable —
and the same `/developers` surface later hosts the samplified-data download (L2).

### C4 — GitHub Actions deploy to test, gated away from production ✅

**Cost: M** (2–4 days, partly work worth doing anyway)

The question was: *can we let them deploy to test without letting them deploy to prod?*
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

**Verdict: build it if and only if C1's loop becomes the bottleneck.** It is the right
long-term answer and it is genuinely secure; it is just premature at one contributor — and
largely moot if they run their own deployment (L3).

**Caveat worth internalizing:** even with perfect gating, a test deploy runs *their* code
against a database and search index that are yours. Test-environment compromise is not
production compromise, but it is not nothing either.

### C5 — Dedicated API test instance ❌

**Cost: M** + **$/month**

A third environment (`m4d-api-test`) so their deploys never disturb your own test site.

**Verdict: skip.** Superseded by the settled decision that they run their own deployment.
Standing up a third environment on your bill, with a third search service to satisfy the RBAC
constraint, buys nothing that L3 doesn't buy for free.

---

## Path 2 — Local Options

### L0 — Build and run with nothing ✅ (do this first)

**Cost: XS** (~half a day of documentation)

The constraints table's most useful finding: **this already works.** Empty SQL database,
migrations applied, `M4D_ADMIN_USER` / `M4D_ADMIN_PASSWORD` set, no third-party keys, no
search service, no data. Third-party services mark themselves unavailable and the app runs.

That is enough to build and unit-test API Phases 1, 2, 4, and 6 — including the entire
security-critical OAuth surface.

**Implementation:** `CONTRIBUTING.md` (with the DCO statement) plus
`architecture/contributor-setup.md` covering:

- Prerequisites: .NET 10 SDK, Node 22, Yarn via corepack
- **The database, leading with Azure SQL serverless** and Docker/Rosetta as the local
  alternative — see [the macOS question](#the-macos-database-question). This is the single
  most likely place a Mac-based contributor stalls, so it gets a ready-to-paste command and
  connection string, not a paragraph of prose
- `dotnet user-secrets set` for the connection string, `M4D_ADMIN_USER`, `M4D_ADMIN_PASSWORD`
- `dotnet ef database update`
- Expected startup warnings — a printed list of the "not configured" lines they *should* see,
  so absent services read as normal rather than as breakage
- `yarn install && yarn build`, then the test targets from [CLAUDE.md](../CLAUDE.md)
- What does **not** work without keys: social login, outbound email, captcha, song search,
  service track lookup

The "expected warnings" section is worth more than it sounds. Without it, the first five
minutes of a new contributor's experience is a wall of scary-looking startup errors.

### L1 — Local stub index + inline test coverage ✅ (recommended)

**Cost: M** (2–4 days) — and it improves your own test suite

This is the option not on the original list, and with no data shipping in the repo it splits
cleanly into a code half and a testing half. **Neither half needs a data file.**

**L1a — `SongIndexLocal`.** [`TestSongIndex`](../m4dModels.Tests/TestSongIndex.cs) is already
an in-memory `SongIndex` subclass — and it already overrides `GetSongFromService`, with a
comment stating its purpose verbatim: *"Lets tests exercise service-id/ISRC lookup paths …
without a real search backend."* **That is precisely the Phase 3 resolve cascade.** The
capability exists; it is just trapped in the test project.

Promote it to a `SongIndexLocal` in `m4dModels`, selected by configuration (e.g.
`SearchBackend: Local`), holding songs in memory. `SongIndex` is already designed for
subclassing — `SongIndexNext` and `TestSongIndex` both do it — so this follows an established
seam rather than cutting a new one. Gaps to fill beyond `TestSongIndex`: free-text search over
title/artist, dance filters, and paging good enough for the site to render.

Per the settled decision, it **prints a startup warning that relevance scoring is not
representative**, so nobody debugs a ranking difference that is a stub artifact.

**L1b — test coverage from constructed songs, not fixtures.** This is what makes the
no-data-in-repo decision cost-free. Per [testing-patterns.md](testing-patterns.md), server
tests already build songs inline from the serialized format:

```csharp
var song = await Song.Create(
    ".Create=\tUser=dwgray\tTitle=My Song\tTempo=180.0\tDanceRating=SLS+1", dms);
```

So the resolve-cascade tests construct their own songs with **known, made-up service IDs** —
`R:` / `I:` / `S:` prefixed values that exercise each rung of the cascade, plus title/artist
near-misses for the fuzzy fallback and confidence reporting. No real identifiers, no real
metadata, no licensing question, and better tests than a fixture would give because each case
is explicit about what it's probing.

| Pros | Cons |
| --- | --- |
| No Azure, no keys, no cost, **no data agreement at all** | Stub is not Azure Search — scoring and analyzer behaviour differ, so "works locally" ≠ "works in prod" |
| Runs in CI, so the API gets real regression coverage | `SongIndexLocal` is a new component you own and must keep in step with `SongIndex` |
| Directly benefits your own testing — Phases 3 and 7 become unit-testable | Gives them nothing to *browse* — the site is empty until L2 |
| Turns "here is a setup guide" into "clone, build, run" | |
| Reusable by every future contributor forever | |

**What this deliberately does not solve:** having a populated site to click around in. That is
L2's job, and it is a comfort-and-exploration need rather than a correctness need — which is
exactly why L2 can wait for the developer to ask for it.

### L2 — Samplification + terms-gated delivery

**Cost: L** (1–2 weeks) — see [Samplification](#samplification-design)

Now the **only** source of realistic data, given nothing ships in the repo. Delivered
out-of-band at first, and through a dev-portal download later.

| Pros | Cons |
| --- | --- |
| Realistic scale and messiness; real Azure Search semantics on their own service | The largest build on this list |
| Reusable for your own testing and for every future contributor | Requires the data-use terms to exist first |
| Lets them work on *any* part of the codebase, not just the API | Sanitizer correctness is security-relevant and needs its own tests |
| The dev-portal download is a natural Phase 6 feature, not a one-off | Free-tier search limits constrain sample size — now their limit, not ours |

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

| Dependency | Config key | Do they need it? |
| --- | --- | --- |
| Google OAuth | `Authentication:Google:*` | No — degrades cleanly. Own app if wanted (free) |
| Facebook OAuth | `Authentication:Facebook:*` | No — and Facebook requires app review; recommend leaving off |
| Spotify OAuth | `Authentication:Spotify:*` | Only if testing Spotify sign-in. Own app, free, minutes. [Program.cs:739](../m4d/Program.cs#L739) already documents the `localhost` HTTPS-redirect accommodation |
| Email (Azure Comm. Services) | `Authentication:AzureCommunicationServices:ConnectionString` | No — `NullEmailSender` fallback, and seeded/sanitized users are pre-confirmed |
| reCAPTCHA | `Authentication:reCAPTCHA:*` | No. Google publishes always-pass test keys — include them in the guide (*verify still current*) |
| Azure Search | RBAC, `SongIndex*` sections | Theirs, never ours |
| App Config + Key Vault | `AppConfig:Endpoint` | **No** — Development skips it entirely. For their *deployed* instance, plain app settings work |
| Commerce | `Configuration:Commerce:Enabled` | No — set `false` |
| GTM / Google Tags | feature flags | Already `false` in `appsettings.Development.json` |

**Never share:** Google, Facebook, Spotify, or Azure Communication Services credentials. Those
are *our* identity with those providers; sharing them is both a security problem and likely a
terms violation. **They create their own or go without** — and going without works.

One caveat for their deployed instance: `AppConfig:Endpoint` is set in the base
`appsettings.json`, so a non-Development deployment will try to reach *our* App Configuration
store and fail. The setup guide must tell them to blank it — a one-line trap that would
otherwise cost them an afternoon.

### L4 — Sandbox configuration profile ✅

**Cost: S** (~1 day)

Rather than building stubs, make "no external dependencies" a **declared, tested mode** instead
of an accident that happens to work:

- `appsettings.Sandbox.json` + a `SANDBOX_MODE` flag
- Forces `SearchBackend: Local` (L1a), `Commerce:Enabled: false`, captcha off, and **clears
  `AppConfig:Endpoint`** — closing the trap noted above
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

Needed for L2. **Good news: the extraction half already exists.**

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

### Form factor — settled

An **admin endpoint** (`Admin/Samplify`) alongside `BackupDatabase`, reusing the existing
`StartAdminTask` / `AdminMonitor` progress plumbing. Later, a dev-portal endpoint produces the
same artifact as a **download gated on accepting testing-only terms**, which fits naturally
into the Phase 6 `/developers` surface.

Not a `scripts/*.ps1` text transform: usernames live *inside* the property log, which has a
real parser (`ModifiedRecord`, `SongPropertyBlockParser`), and [CLAUDE.md](../CLAUDE.md) is
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
music4dance, so they are **not personal data**, *and* preserving them is **required for
correctness** because code keys off the names:

| Preserved verbatim | Not PII because | Correctness reason |
| --- | --- | --- |
| `batch`, `batch-*` | m4d import automation | `ChunkedSong.IsBatch`; exempt from `TryGetCappedDelta`'s ±1 cap |
| `tempo-bot` | m4d tempo automation | Cap-exempt; asserted by name in `DanceRatingCapTests` |
| `dgsnure` | Non-personal data source | Hardcoded in `s_unconfirmedVoteSources` |
| Any `@music4dance.net` account | Our own service identities | `IsPseudo` / `IsM4d` derives from the email domain |
| The `\|P` pseudo suffix | Not an identifier | `ModifiedRecord` splits on it; stripping it changes attribution |

This is a real simplification: the preserve-list stops being a grudging exception and becomes
the intended behaviour.

### The Spotify proxy-user catch

⚠️ **Spotify proxy users do not belong in that list.** From
[ApplicationUser.cs:48](../m4dModels/ApplicationUser.cs#L48):

```csharp
public bool IsSpotify => Email?.EndsWith("@spotify.com", StringComparison.OrdinalIgnoreCase) ?? false;
public string SpotifyId => IsSpotify ? EmailAlias : null;
```

The email is `{spotifyUserId}@spotify.com`, and `SpotifyId` is **the local part of that
email** — a real Spotify account identifier belonging to a real person whose public playlist
was imported. That is the same category as the `Providers` column, which must be dropped: a
stable third-party identifier for a natural person, not a machine account. It only *looks*
like a service account because `IsPseudo` returns true for it.

**Rule: keep the `@spotify.com` domain, rewrite the local part.** Mapping
`realuser123@spotify.com` → `sp-000173@spotify.com` preserves `IsSpotify`, `IsPseudo`, the
`|P` decoration, and every display and vote-attribution path, while removing the real
identifier. Same treatment for the `UserName` if it embeds the Spotify ID.

Worth a denylist test of its own, since this is the one case where the privacy rule and the
"it's a service account" intuition point in opposite directions.

### Rewrite rules for real registered users

| Field | Treatment |
| --- | --- |
| `UserName` | Deterministic pseudonym — `Firstname L.` from a name table, via a salted keyed hash so successive samples are stable and diffable. Resolve collisions explicitly |
| `Email` | `{pseudonym}@example.invalid` — RFC 2606 reserved TLD, so no accidental delivery |
| `PasswordHash` | Fixed placeholder (`LoadUsers` treats blank as null; `test-users-clean.txt` uses `XXXXXXXXXXX`) |
| `SecurityStamp` | Zero GUID, matching existing test data |
| `Providers` | **Empty.** Holds Google/Facebook/Spotify provider keys — stable third-party user identifiers. Highest-severity field in the file |
| `Region` | Drop, or coarsen to country |
| Subscription fields | Synthesize rather than copy — real purchase history is commercially sensitive. Keep the *shape*: one premium, one trial, one lapsed, several free, so tier logic is exercised |
| `LastActive` / `StartDate` | Keep, or jitter by days |

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

Sanitized output goes to `local/` (gitignored per [CLAUDE.md](../CLAUDE.md)). Nothing is
committed.

---

## Legal & Process Prerequisites

Deliberately light, per the settled decision.

1. **DCO, not a CLA.** Add `CONTRIBUTING.md` with the
   [Developer Certificate of Origin](https://developercertificate.org/) text, require
   `Signed-off-by` in commits, and enforce it with a GitHub check. `git commit -s` is the
   whole contributor burden. No paperwork, no signature collection, no lawyer.
2. **Data-use terms**, required before any sample data changes hands: development and testing
   only, no redistribution, no re-identification attempts, delete on request. One page.
   Necessary because the README already states the data sits outside the MIT grant. This is
   also the text the dev-portal download gates on, so writing it once serves both channels.
3. **Privacy posture.** Pseudonymized is not anonymous. The defensible position — and it is
   genuinely defensible — is *real song data, synthetic human identities, real machine
   identities*: all real usernames and emails rewritten, Spotify local parts rewritten,
   provider keys dropped, hashes discarded, service accounts untouched. Say that explicitly in
   the terms so both sides know what was done.
4. **Secrets hygiene**, stated plainly to them: they will never receive our third-party
   credentials, and they don't need them. That is a design property of the resilience layer,
   not a limitation to work around.
5. **Repo access decision.** C1 (fork PRs) requires no grant. C4 requires write access.
   Fork-based is the correct default given no contractual relationship.

---

## Cost Summary

| Option | Build cost | Ongoing $ | Unblocks | Verdict |
| --- | --- | --- | --- | --- |
| **L0** Setup guide + DCO, run with nothing | XS | — | Phases 1, 2, 4, 6 | ✅ **Do first** |
| **C1** You deploy their PR to test | XS | — | End-to-end validation | ✅ **Default** |
| **C3** Owner-scoped API diagnostics | S | — | Self-service debugging | ✅ **Pull into Phase 1** |
| **L4** Sandbox config profile + `FileEmailSender` | S | — | Reproducible environment | ✅ Cheap, useful to you |
| **L1** `SongIndexLocal` + inline test coverage | M | — | Phases 3, 7; CI coverage | ✅ **Highest leverage** |
| **L3** Their own cloud instance (guide) | M (docs) | Theirs | Full independence | ✅ Settled direction |
| **L2** Samplification + terms-gated delivery | L | — | Browseable realistic data | ⏸ When they ask |
| **C4** GitHub Actions environment-gated deploy | M | — | Self-service deploys | ⏸ Only if C1 stalls |
| **C5** Dedicated API test instance | M | $$ | Isolation | ❌ Superseded by L3 |
| **C2** `showDiagnostics` on production | XS | — | — | ❌ **Reject** |

Scale: XS < ½ day · S ≈ 1 day · M ≈ 2–4 days · L ≈ 1–2 weeks. Estimates are of *your* time and
are rough.

**The path:** L0 → C1 → C3 → L1, adding L4 alongside, with L3's setup guide written when they
ask for a deployment of their own. That is roughly one week of your effort, covers all seven
API phases, ships **no data to anyone**, requires no Azure access for them, and leaves behind
reusable contributor infrastructure. L2 and C4 stay on the shelf until something specific
demands them.

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
- **Should `SongIndexLocal` eventually replace `TestSongIndex`?** Two in-memory index
  implementations will drift. Worth deciding at build time rather than discovering later.

---

## Related Documents

- [public-api-authorization.md](public-api-authorization.md) — the contribution being scoped
- [index-backup-streaming.md](index-backup-streaming.md) — the extraction half of samplification
- [testing-patterns.md](testing-patterns.md) — serialized song format for constructed test songs
- [user-name-visibility.md](user-name-visibility.md) — pseudo/batch user semantics
- [unconfirmed-dance-votes.md](unconfirmed-dance-votes.md) — `dgsnure`, the ±1 cap
- [search-index-versioning.md](search-index-versioning.md) — index naming and versioning
- [SELF_CONTAINED_DEPLOYMENT.md](SELF_CONTAINED_DEPLOYMENT.md) — deployment modes
- [admin-pages.md](admin-pages.md) — admin surface and role gating
