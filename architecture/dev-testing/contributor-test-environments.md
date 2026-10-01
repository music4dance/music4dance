# Contributor Test Environments

**Type:** Reference (with the unbuilt options in [plans/contributor-environments-next](../plans/contributor-environments-next.md))
**Status:** Current. L0 and L1a–e shipped in [PR #250](https://github.com/music4dance/music4dance/pull/250); L1f (search, filter and sort in `SongIndexLocal`) since. Everything past L1 is proposed.
**Last verified:** 2026-10-01 (code references checked; behavior not re-traced)
**Code:** `m4d.Sandbox/`, `m4dModels.Sandbox/` (`SandboxServiceFactory`, `SongIndexLocal`, `TestData/`), `m4d/Areas/Identity/UserManagerHelpers.cs`

How a contributor builds, runs and tests music4dance with **no** production access: no
production data, no secrets, no Azure subscription. It does this through **L0** (the real app
against an empty database) and **L1** (`m4d.Sandbox`, a host with no external services). The
hands-on guide is [contributor-setup](contributor-setup.md). This doc is the design: what's
stubbed, what's seeded, and why.

The work was prompted by an independent iOS developer (DanzQ) offering to build
[the public API](../plans/public-api-authorization.md). That effort is on hold: PR 2 is paused
after Apple denied the business plan. The environments are general contributor infrastructure,
not specific to that project. The remaining options (cloud test deploys, owner-scoped
diagnostics, samplified datasets, a contributor's own Azure deployment) and their cost and legal
analysis are in [plans/contributor-environments-next](../plans/contributor-environments-next.md).

## Decisions Settled

| Question                        | Decision                                                                                                                                                                                                                           |
| ------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Contribution licensing**      | **DCO**, not a CLA. Keep the process light — `Signed-off-by` enforced by a GitHub check                                                                                                                                            |
| **Sample data in the repo**     | **No.** No song data is committed, sanitized or otherwise. See [L1](#l1--no-external-service-local-server--recommended) for how CI coverage survives that                                                                          |
| **Sample data delivery**        | Out-of-band initially; **a dev-portal download gated on accepting testing-only terms** long term                                                                                                                                   |
| **Sanitizer form factor**       | **Admin endpoint** now, alongside `BackupDatabase`; a dev-portal endpoint later, feeding the download above                                                                                                                        |
| **Service accounts in samples** | **Not obfuscated** — `batch*`, `tempo-bot`, `dgsnure`, `@music4dance.net`. They are machine identities, not personal data. ⚠️ **Spotify proxy users are the exception** — see [the catch](../plans/contributor-environments-next.md#the-spotify-proxy-user-catch)           |
| **Realistic-scale environment** | The developer creates **their own** Azure deployment and search service, loaded with a samplified dataset                                                                                                                          |
| **Search relevance honesty**    | The local index must state plainly at startup that scoring is not representative                                                                                                                                                   |
| **Free Azure Search tier**      | Ours is consumed (site search could be moved to the paid subscription to free it, but that solves a problem we no longer have). **A new subscription carries its own free tier**, so this constraint lands on their side, not ours |

**Still open:** what the developer actually wants — you're asking. That answer may collapse
the cloud branch entirely.

---

## Hard Constraints Found in the Codebase

These shape every option below. Several rule out the obvious approach.

| Constraint                                                              | Evidence                                                                                                                                                                                                                                                                                                                                                                                                                             | Consequence                                                                                                                                                                                                     |
| ----------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **`showDiagnostics` is a full-database-export role, not a viewer role** | [AdminController.cs:1732](../../m4d/Controllers/AdminController.cs#L1732) `BackupDatabase` and [:1846](../../m4d/Controllers/AdminController.cs#L1846) `BackupTail` are gated on it, and [`SerializeUsers`](../../m4dModels/DanceMusicService.cs#L841) emits **password hashes, security stamps, emails, and external-provider keys** for every user                                                                                          | **Cannot be granted on production.** Reusing it as-is would hand over the entire user table. Needs a new narrow capability instead — see [C3](../plans/contributor-environments-next.md#c3--owner-scoped-api-diagnostics--recommended)                    |
| **Azure AI Search RBAC has no index-level scope**                       | Search is authenticated by `DefaultAzureCredential` / RBAC, not API keys ([Program.cs:308-334](../../m4d/Program.cs#L308)); data-plane roles are assignable at service scope only                                                                                                                                                                                                                                                       | There is **no way** to grant read on `songs-test-3` without also granting read on `songs-prod-3`                                                                                                                |
| **Test and production indexes live on the same search service**         | [appsettings.json](../../m4d/appsettings.json) — `SongIndexProd-*` and `SongIndexTest-*` both point at `music4dance.search.windows.net`                                                                                                                                                                                                                                                                                                 | Compounds the above. Settled by having them run their own service in their own subscription. (`PageIndex` already sits on a second service, `m4d.search.windows.net`, so multi-service is established practice) |
| **Every third-party dependency already fails soft**                     | `AddGoogleWithResilience` / `AddFacebookWithResilience` / `AddSpotifyWithResilience` ([AuthenticationBuilderExtensions.cs](../../m4d/Configuration/AuthenticationBuilderExtensions.cs)), `AddEmailSenderWithResilience` / `AddReCaptchaWithResilience` ([ServiceCollectionExtensions.cs](../../m4d/Configuration/ServiceCollectionExtensions.cs)) each catch, `MarkUnavailable`, warn, and continue; email falls back to `NullEmailSender` | **The app already boots with no third-party keys at all.** The service-resilience work (phases 1–7) accidentally solved most of the onboarding problem. No stub framework needs building                        |
| **Azure App Configuration / Key Vault are skipped in Development**      | [Program.cs:222](../../m4d/Program.cs#L222) — `if (!isDevelopment)` guards the whole `AddAzureAppConfiguration` block                                                                                                                                                                                                                                                                                                                   | Local development reads user secrets and `appsettings.Development.json`. A contributor needs **no access to our config store**, which would otherwise be a hard blocker                                         |
| **Admin bootstrap already exists**                                      | [`UserManagerHelpers.SeedData`](../../m4d/Areas/Identity/UserManagerHelpers.cs) creates `M4D_ADMIN_USER` with `EmailConfirmed = true` and grants `canTag`, `canEdit`, `showDiagnostics`, `dbAdmin`                                                                                                                                                                                                                                 | Their local admin account is **two environment variables**. No need to carry over a real password hash, and no email round-trip to confirm the account                                                          |
| **Some usernames are load-bearing in code**                             | `ChunkedSong.IsBatch` matches `batch\|P` / `batch-*`; `tempo-bot` is cap-exempt ([DanceRatingCapTests.cs](../../m4dModels.Tests/DanceRatingCapTests.cs)); `dgsnure` is hardcoded in `s_unconfirmedVoteSources` ([unconfirmed-dance-votes.md](../songs/unconfirmed-dance-votes.md)); `IsPseudo` derives from the email domain ([ApplicationUser.cs:43](../../m4dModels/ApplicationUser.cs#L43))                                                      | Preserving these verbatim is **required for correctness**, independent of the privacy argument. See [Samplification](../plans/contributor-environments-next.md#samplification-design)                                                                    |
| **SQL Server coupling is deeper than the connection string**            | `UseSqlServer` in [DanceMusicContext.cs:27](../../m4dModels/DanceMusicContext.cs#L27) and four sites in `Program.cs`; `ExecuteSqlRawAsync("TRUNCATE TABLE UsageLog")` at [AdminController.cs:1184](../../m4d/Controllers/AdminController.cs#L1184); `SqlException` caught by name in five resilience paths; 13 migrations emitting `nvarchar(max)` / `nvarchar(450)`                                                                       | **SQLite is not a drop-in.** See [the macOS question](#the-macos-database-question)                                                                                                                             |
| **The data is not MIT licensed**                                        | [README.md](../../README.md) — _"the data running on the site is not included in that license"_                                                                                                                                                                                                                                                                                                                                         | Sharing a sample is a **licensing decision**, not only a privacy one. Settled: nothing in the repo; terms-gated delivery only                                                                                   |
| **There is no `CONTRIBUTING.md` and no DCO check**                      | Repo root                                                                                                                                                                                                                                                                                                                                                                                                                            | Nothing currently establishes rights to merge an outside contribution to an auth system                                                                                                                         |

---

## The macOS Database Question

### First, a clarification: LocalDB is SQL Server, not SQLite

Worth stating plainly, because the two get conflated and the conflation changes the plan.
**SQL Server Express LocalDB is the real SQL Server engine** — `sqlservr.exe`, the same
database family as the production Azure SQL — just packaged to start on demand as a user-mode
process with file-based `.mdf` attachment and no service to administer. SQLite shares _none_
of its code: it is an embedded C library that runs in-process, with its own SQL dialect and
its own type system.

|          | LocalDB                                       | SQLite                                        |
| -------- | --------------------------------------------- | --------------------------------------------- |
| Engine   | SQL Server (`sqlservr.exe`), separate process | Embedded C library, in-process                |
| Provider | `Microsoft.Data.SqlClient`                    | `Microsoft.Data.Sqlite`                       |
| Dialect  | Full T-SQL                                    | Own dialect; type affinity, not strict typing |
| Platform | **Windows only**                              | Cross-platform, native ARM64                  |

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
  silently stops engaging — the failure mode is _invisible_, which is the worst kind
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
   and 32 GB/month) — _verify current terms_, but a new account also carries the standard
   credit and 12-month free allowances. **Lead with this.**
2. **`mssql/server` in Docker under Rosetta 2** — widely used, works, costs nothing. Needs
   Docker Desktop with Rosetta enabled. _Verify whether native ARM64 server images now exist_
   before writing the guide; if they do, this becomes option 1.
3. **SQLite** — a real option, but as a deliberate portability project with the five items
   above as its scope, not as a shortcut.

Since they will be standing up their own Azure resources anyway, option 1 costs them one
extra resource in a portal they're already in, and removes the ARM question entirely.

---

## Shipped local environments (L0, L1)

The full option ladder (L0–L4, plus the cloud options) is in
[plans/contributor-environments-next](../plans/contributor-environments-next.md). L0 and L1 have
shipped and are described here. The `L1a`–`L1f` labels are cited by code comments throughout
the sandbox code.

### L0 — Build and run with nothing ✅ (do this first)

**Cost: XS** (~half a day of documentation)

**✅ Shipped in [PR #250](https://github.com/music4dance/music4dance/pull/250)**, bundled
together with L1 into one `architecture/dev-testing/contributor-setup.md` rather than shipping separately —
it presents `m4d.Sandbox` (L1) as the fastest path first, with this empty-database path as the
second option for working against the real composition root. `CONTRIBUTING.md` and the DCO
check landed as planned; see the updated Legal & Process Prerequisites section below.

The constraints table's most useful finding: **this already works.** Empty SQL database,
migrations applied, `M4D_ADMIN_USER` / `M4D_ADMIN_PASSWORD` set, no third-party keys, no
search service, no data. Third-party services mark themselves unavailable and the app runs.

That is enough to build and unit-test API Phases 1, 2, 4, and 6 — including the entire
security-critical OAuth surface.

**Implementation:** `CONTRIBUTING.md` (with the DCO statement) plus
`architecture/dev-testing/contributor-setup.md` covering:

- Prerequisites: .NET 10 SDK, Node 22, Yarn via corepack
- **The database, leading with Azure SQL serverless** and Docker/Rosetta as the local
  alternative — see [the macOS question](#the-macos-database-question). This is the single
  most likely place a Mac-based contributor stalls, so it gets a ready-to-paste command and
  connection string, not a paragraph of prose
- `dotnet user-secrets set` for the connection string, `M4D_ADMIN_USER`, `M4D_ADMIN_PASSWORD`
- `dotnet ef database update`
- Expected startup warnings — a printed list of the "not configured" lines they _should_ see,
  so absent services read as normal rather than as breakage
- `yarn install && yarn build`, then the test targets from [CLAUDE.md](../../CLAUDE.md)
- What does **not** work without keys: social login, outbound email, captcha, song search,
  service track lookup

The "expected warnings" section is worth more than it sounds. Without it, the first five
minutes of a new contributor's experience is a wall of scary-looking startup errors.

### L1 — No-external-service local server ✅ (recommended)

**Cost: M** (~4–5 days, upper end of the band) — and it upgrades both CI and manual QA

**✅ Shipped in [PR #250](https://github.com/music4dance/music4dance/pull/250).**

**Shipped as planned**, with a handful of deltas worth recording because they change how the
next person should read the subsections below:

- **Class renames landed via alias, not in-place edits.** Rather than touching the ~15
  `m4dModels.Tests` files that reference `TestSongIndex`/`DanceMusicTester` by name, the actual
  classes moved to `m4dModels.Sandbox` under their new names (`SongIndexLocal`,
  `SandboxServiceFactory`), and a `SandboxAliases.cs` in both `m4dModels.Tests` and `m4d.Tests`
  restores the old names as `global using` aliases — `global using TestSongIndex =
m4dModels.Sandbox.SongIndexLocal;`. All ~148 existing call sites compile unchanged. The
  original concern (misleadingly-named `Test*` classes running inside a live web server) is
  still fully addressed: `m4d.Sandbox/Program.cs` references the real names directly, never the
  aliases.
- **`m4dModels.Tests/TestData/*` moved (not duplicated) to `m4dModels.Sandbox/TestData/`** —
  confirmed via `git log --follow`; the directory no longer exists under `m4dModels.Tests`.
- **`SongIndex.Create`'s late-binding assumption needed one real code change.** The plan expected
  `AttachToService` alone to keep the Azure-bound path unreached; in practice `SongIndex.Create`
  had no seam for a host to hand back a pre-built instance, so a small opt-in
  `ISongIndexFactory` interface was added (`SongIndex.cs`) — `LocalSearchServiceManager`
  implements it and `SongIndex.Create` checks for it first, before touching
  `dms.SearchService.GetInfo(id)`. Zero effect on `SearchServiceManager`, which doesn't
  implement the interface.
- **The EF backing-store question (open in the original plan) resolved to InMemory**, not
  LocalDB — see the updated **L1b** below for how the `CreateTransientContext`
  playlist-creation gap was actually closed.
- **`azure-pipelines.yml` now pins `dotnet publish` to `m4d/m4d.csproj` explicitly** (it was
  solution-wide auto-discovery before), so the compiler-enforced guarantee in L1b ("`m4d.csproj`
  has no reference to `m4dModels.Sandbox.csproj`") is backed by an explicit pipeline change too,
  not left to publish-target defaults.
- **Three bugs found and fixed while building this that are not sandbox-specific:**
  1. `SongIndexLocal.SaveSongs` (bulk save) updated `DanceStats` but never populated the
     in-memory lookup dictionary `FindSong` reads from, so every bulk-seeded song's Details page
     500'd — a sandbox-only bug, but one that would have made the seeded dataset look badly
     broken on first run.
  2. `AddReCaptchaWithResilience`'s catch block never registered a null-object fallback, unlike
     every other resilience-wrapped service — Login/Register/Payment pages 500 in **any**
     environment with no reCAPTCHA keys configured, including a plain `m4d` local dev setup with
     an empty database. Fixed with `NullReCaptchaSiteVerify`. This was a latent gap in the
     service-resilience work, found only because building the sandbox exercised the
     no-keys-at-all path end-to-end for the first time.
  3. `m4d.Sandbox`'s `appsettings.json` was missing the `Vite:Base` config block, so the Vite
     manifest lookup silently failed and every page rendered unstyled.
  4. (Found 2026-08-21, same root cause as #3.) `m4d.Sandbox`'s `Vite` config was also missing
     `Server:Port`/`Server:Https`, so the `m4d.Sandbox-vite` launch profile emitted script tags
     pointing at `Vite.AspNetCore`'s library default (`:5173`, http) instead of the actual dev
     server. The client's `vite.config.ts` hardcodes its port/https by importing
     `m4d/appsettings.Development.json` directly — that file is shared regardless of which host
     (`m4d` or `m4d.Sandbox`) is proxying to it — so the fix was adding the matching
     `Server: { Port: 7237, Https: true }` to `m4d.Sandbox/appsettings.json`'s existing `Vite`
     block. Verified end-to-end with both the client dev server and the sandbox host running
     together: the sandbox's home page now emits `https://localhost:7237/vclient/...` tags and
     that port serves real Vite module content.
- **What did not ship in PR #250:** the free-text search / dance-filter / paging capability the
  original L1a table flagged as needed once results get _rendered_ for a person.
  `SongIndexLocal` had no `Search`/`DoSearch` override, so the song-list/browse routes silently
  returned zero results (the base class's catch-all swallows the `ThrowingSearchClientFactory`
  exception rather than crashing). This has since been implemented for the filter/sort/browse
  path — see [L1f](#l1f--search-filter-and-sort-for-songindexlocal-new-fast-follow). Still not
  shipped: the CI end-to-end smoke test and the `/sandbox/reset` endpoint floated in L1e — both
  remain open, unforced choices, not regressions.

Earlier drafts of this document split this into two separate line items: a test-only stub index,
and a "sandbox configuration profile" for the server (the latter now folded in here — see the old
[L4](../plans/contributor-environments-next.md#l4--sandbox-configuration-profile-merged-into-l1)). They turn out to be the same problem.
Everything either one needs already exists, just trapped in the test project:
[`TestSongIndex`](../../m4dModels.Sandbox/SongIndexLocal.cs) (now `SongIndexLocal`) is a working in-memory `SongIndex`, and
[`DanceMusicTester.CreateService`](../../m4dModels.Sandbox/SandboxServiceFactory.cs) (now `SandboxServiceFactory`) is already the one
place that assembles the _whole_ stub graph a `SongIndex` needs to run — an in-memory
`DanceMusicContext`, a stub `IDanceStatsFileManager` (`TestDSFileManager`), and the
`DanceMusicCoreService`/`DanceMusicService` pair `SongIndex.DanceMusicService` depends on. (This
is almost certainly what "a stubbed `DanceServiceCore`" was reaching for — there is no class by
that name, but `DanceMusicCoreService` is exactly the object graph `TestSongIndex` currently gets
handed via `AttachToService`, and it's the thing that needs promoting alongside the index itself.)
The only thing missing is a home outside the test project so a running web server can use it too.

**L1a — Promote the stub layer into its own assembly.**

New class library, `m4dModels.Sandbox`, referenced by both `m4dModels.Tests` (replacing what's
built inline there today) and the sandbox host in L1b. It carries:

| Moves from `m4dModels.Tests` today                                      | Becomes                      | Notes                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                              |
| ----------------------------------------------------------------------- | ---------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `TestSongIndex`                                                         | `SongIndexLocal`             | Same overrides (`SaveSong`, `FindSong`, `GetSongFromService`, `LoadLightSongsStreamingAsync`) — the comment on `GetSongFromService` already states the point verbatim: _"Lets tests exercise service-id/ISRC lookup paths … without a real search backend."_ That's precisely the Phase 3 resolve cascade. Gains free-text title/artist search, dance-filter matching, and paging — needed once results get _rendered_ for a person instead of just asserted on in a test                                                                                                          |
| `TestDSFileManager`                                                     | `LocalDanceStatsFileManager` | Same `IDanceStatsFileManager` seam production already uses — `DanceStatsFileManager` reads `content/dances.json` with a fallback to a checked-in static copy at [DanceStatsFileManager.cs:41](../../m4dModels/DanceStatsFileManager.cs#L41); this implementation reads the checked-in test data instead. Same interface, same "fall back to what's in source control" shape                                                                                                                                                                                                           |
| _(new)_                                                                 | `LocalSearchServiceManager`  | A minimal `ISearchServiceManager`. Doesn't need to do much: `GetSongFilter` is pure string parsing, not a service call, and once a `SongIndexLocal` is pre-attached — the same late-binding `AttachToService` pattern `TestSongIndex` already uses to dodge the `SongIndex`/`DanceMusicCoreService` circular dependency — `SongIndex.Create`'s Azure-bound path in [SongIndex.cs:52](../../m4dModels/SongIndex.cs#L52) is never reached. Its job is to satisfy the constructor dependency and answer `RawEnvironment` / `CurrentIndexName` honestly as "local" for the startup banner |
| `DanceMusicTester.CreateService` / `CreatePopulatedService` / `AddUser` | `SandboxServiceFactory`      | Same shape, same parameters. `m4dModels.Tests` keeps its existing call sites working via thin same-named wrappers, so none of the ~15 test files that reference these by name need to change                                                                                                                                                                                                                                                                                                                                                                                       |
| `m4dModels.Tests/TestData/*.txt`, `*.json`                              | same files, moved            | Already public, already the small PII-cleaned dataset — this is the "existing sanitized data" this request is asking to reuse, not a new sanitization effort                                                                                                                                                                                                                                                                                                                                                                                                                       |

Renaming `Test*` classes is mechanical but real work — worth doing once rather than leaving
misleadingly-named `Test*` classes running inside a live web server. `SongIndex` is already
designed for subclassing (`SongIndexNext` and `TestSongIndex` both do it today), so this follows
an established seam rather than cutting a new one.

Per the settled decision, `SongIndexLocal` **prints a startup warning that relevance scoring is
not representative**, so nobody debugs a ranking difference that is a stub artifact.

**L1b — `m4d.Sandbox`: a second host project, not a build flag.**

The bonus ask — keep this code and data out of the artifact that reaches Azure — has two possible
mechanisms:

| Approach                                                          | How                                                                                                                                                                                                                                                                                                                                                                                                                                                                             | Verdict                                                                                                                                                                                                                   |
| ----------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Config-flag branching inside `m4d.csproj` (`SANDBOX_MODE`, `#if`) | Conditional `<ProjectReference>` + compiler constant; branches inside the shared `Program.cs`                                                                                                                                                                                                                                                                                                                                                                                   | ❌ The "one more configuration path to keep working" risk the old sandbox-profile idea already flagged against itself — except now the thing gated behind someone's discipline is _whether stub data ships to production_ |
| A second project, `m4d.Sandbox.csproj`                            | Project-references `m4d.csproj` (inherits every controller and Razor view for free — MVC's application-part discovery walks referenced assemblies, the same mechanism Razor Class Libraries rely on, and static web assets should flow the same way; **verify both during implementation**, it's the one part of this design borrowed from general ASP.NET Core behaviour rather than confirmed in this codebase) plus `m4dModels.Sandbox.csproj`. Its own minimal `Program.cs` | ✅ Recommended                                                                                                                                                                                                            |

With the second-project approach, keeping the stub assembly off production isn't a rule anyone has
to remember — `m4d.csproj` simply has no reference to `m4dModels.Sandbox.csproj`, so
`dotnet publish m4d/m4d.csproj -c Release` (what `azure-pipelines.yml` already runs) cannot pull it
in. That's a stronger guarantee than a flag: enforced by the compiler, not by a reviewer.

The cost of a second host is the usual one — two `Program.cs`-adjacent files that could drift.
Contained by factoring `m4d/Program.cs`'s composition root (currently one long imperative file)
into named extension methods — `AddM4dApplication(builder)`, `app.UseM4dPipeline()` — that both the
real `Program.cs` and `m4d.Sandbox/Program.cs` call. The sandbox host then only _overrides_ a
handful of registrations after the shared call: `ISearchServiceManager` → `LocalSearchServiceManager`
with a pre-attached `SongIndexLocal`, plus the seeding in L1d. Routing, middleware, and every
controller stay identical by construction, so there's no second copy of application logic to drift
— only the deliberately-overridden pieces can.

`m4d.Sandbox` carries its own `appsettings.json` rather than a flag layered on the production one
— this is where the old L4's specific settings land: `Commerce:Enabled: false`, captcha off, and
`AppConfig:Endpoint` left blank (closing the trap already called out in
[L3](../plans/contributor-environments-next.md#l3--they-deploy-their-own-cloud-instance--settled-direction), where a non-Development
deployment that forgets to blank it tries to reach _our_ App Configuration store).

**Resolved: InMemory, not LocalDB.** `SandboxServiceFactory` (like `DanceMusicTester` before it)
uses `UseInMemoryDatabase` for zero-install convenience. But
[`DanceMusicContext.CreateTransientContext`](../../m4dModels/DanceMusicContext.cs#L19) — used by
[`PlayListController.cs:285`](../../m4d/Controllers/PlayListController.cs#L285) for playlist creation
— threw `"Cannot create a new dbcontext from a test context"` whenever the context had no
`SqlServerOptionsExtension`, which is exactly the InMemory case. Rather than switch
`m4d.Sandbox` to a disposable LocalDB instance, `CreateTransientContext` was taught an
InMemory-aware fallback: it now also reads `InMemoryOptionsExtension.StoreName` and, when
present, builds the transient context with `UseInMemoryDatabase(storeName)` instead of throwing.
That keeps every transient context sharing the same named in-memory database the way the real
app shares one SQL Server connection string, so playlist creation works in the sandbox without
requiring a local database engine at all — the thing L1's "nothing installed" promise depended
on. **Cost not yet paid: this reintroduces the EF1001 warning it should have avoided.**
`InMemoryOptionsExtension` is exactly as internal an EF Core type as the `SqlServerOptionsExtension`
already flagged there — `dotnet build m4dModels/m4dModels.csproj` currently emits **3** EF1001
warnings (1 existing, on `ConnectionString`, plus 2 new, on `InMemoryStoreName`), not the 0 the
PR's own build checklist claimed. Rolling this into the already-tracked internal-EF-API cleanup
(constructor-inject the connection string / store name instead of recovering it from
`DbContextOptions` after the fact) would fix both call sites at once.

Add `m4d.Sandbox` and `m4dModels.Sandbox` to `music4dance.sln`, following the precedent
`SelfCrawler` already sets for a project that's part of the solution but not part of CI or the
deploy pipeline.

**L1c — Test coverage from constructed songs, not fixtures.** This is what makes the
no-data-in-repo decision cost-free, independent of L1a/L1b. Per
[testing-patterns.md](testing-patterns.md), server tests already build songs inline from the
serialized format:

```csharp
var song = await Song.Create(
    ".Create=\tUser=dwgray\tTitle=My Song\tTempo=180.0\tDanceRating=SLS+1", dms);
```

So the resolve-cascade tests construct their own songs with **known, made-up service IDs** —
`R:` / `I:` / `S:` prefixed values that exercise each rung of the cascade, plus title/artist
near-misses for the fuzzy fallback and confidence reporting. No real identifiers, no real
metadata, no licensing question, and better tests than a fixture would give because each case
is explicit about what it's probing.

**L1d — A test user, not just test data.**

Extend the existing admin-bootstrap pattern
([`UserManagerHelpers.SeedData`](../../m4d/Areas/Identity/UserManagerHelpers.cs), driven by
`M4D_ADMIN_USER` / `M4D_ADMIN_PASSWORD`) with a deliberately low-privilege second account:
`M4D_TEST_USER` / `M4D_TEST_PASSWORD`, seeded with **no roles at all**.

That's enough. Voting (`DanceRating`) and tag editing are available to any authenticated,
non-pseudo user — `[Authorize]` with no role restriction already gates the equivalent write paths
in `SongController` (e.g. `UndoUserChanges`), and on the client, `MenuContext.canEdit` only gates
the _further_ affordances (bulk tag removal, full song edit) — see
[TagListEditor.vue:126](../../m4d/ClientApp/src/components/TagListEditor.vue#L126). A roleless
account exercises exactly the code path real users hit, which is the one voting/tagging tests
actually need.

⚠️ **Don't give it an `@music4dance.net` email.** That domain is what `ApplicationUser.IsM4d` /
`IsPseudo` key off ([ApplicationUser.cs:45](../../m4dModels/ApplicationUser.cs#L45)) — the same switch
that decorates batch/service accounts with `|P` and exempts them from the rating cap. An
`@music4dance.net` test user would silently stop testing the thing it's meant to test. Use
`{name}@example.invalid` instead, matching the samplification convention already settled on for
[real users](../plans/contributor-environments-next.md#rewrite-rules-for-real-registered-users) below.

A third optional account, `M4D_EDITOR_USER`, seeded with just `canEdit`, covers the tag-removal /
full-edit surface without handing out `dbAdmin` or `showDiagnostics`. Three tiers — admin, editor,
plain — mirror the three privilege levels real users actually occupy, and the second and third are
cheap once the first exists.

**L1e — Other things worth adding, now that this is a real running server:**

- **`FileEmailSender`**, writing `.eml` files to `local/mail/` (carried over from the old L4),
  stops being a nicety here and becomes load-bearing: it's what makes self-registration and
  password reset testable for accounts a contributor creates _beyond_ the two or three seeded
  ones — relevant if "useful for manual testing" should include the sign-up flow itself, not only
  voting on seeded songs.
- **A startup banner** stating which accounts were seeded (usernames, not passwords), alongside
  the already-settled "relevance scoring is not representative" warning — so a contributor doesn't
  have to go read environment variables to find their own test login.
- **A reset path.** State lives in-process (`SongIndexLocal` is in-memory), so a manual tester who
  mangles data while poking at voting or editing needs a cheap way back to a known-good state. A
  `dotnet run` restart already gives this for free; worth deciding whether that's sufficient or a
  `/sandbox/reset` admin endpoint earns its keep — the one item in this whole section that's new
  application logic rather than wiring.
- **A real end-to-end smoke test in CI**, now cheap to add: `m4d.Sandbox` boots the full
  controller/view/middleware pipeline against nothing but in-memory stubs, so a CI job that starts
  it and hits home / dance-list / song-details / a vote is genuine HTTP-pipeline coverage that
  nothing today provides — the current suite is unit tests plus a few integration tests against
  `TestSongIndex` directly, never through `Program.cs`.
- **Browser-driven e2e becomes possible at all.** This is the first point in the whole document
  where Playwright-style testing of voting, tag editing, or playlist creation is available cheaply
  — the cloud options never unlock this as cheaply, since each iteration there costs a deploy
  cycle.
- **Known non-goal, stated explicitly:** `MusicServiceManager`'s Spotify/iTunes enrichment path
  (`UpdateSongAndServices`, used when importing a new song from a service playlist) makes live
  third-party calls and isn't stubbed by this design. Voting on and editing _existing_ seeded songs
  never touches it; creating a song from a live Spotify playlist inside the sandbox still would.
  Worth saying rather than discovering by surprise.

| Pros                                                                                                                                | Cons                                                                                                                                              |
| ----------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------- |
| One stub assembly serves CI and manual testing — no drift between "what the tests exercise" and "what a contributor clicks through" | `SongIndexLocal` still isn't Azure Search; ranking/relevance differences remain a known, disclosed gap                                            |
| The bonus (stub code/data never reaches the cloud build) falls out of project structure, not a flag someone has to remember         | Second host project is real ongoing surface area, bounded by the shared-pipeline-method design in L1b                                             |
| Three-tier seeded users (admin/editor/plain) cover the privilege levels that actually matter for voting and editing                 | Renaming `Test*` classes touches ~15 existing test files — mechanical, but a real PR                                                              |
| Unlocks browser-driven e2e for the first time in this document                                                                      | `CreateTransientContext`'s InMemory fix reintroduced 2 EF1001 warnings; `MusicServiceManager`'s live-service enrichment stays unstubbed by design |

**What this still doesn't solve, as shipped:**

- **Realistic scale and messiness.** The seed set is still the same small, already-public
  dataset — enough to walk every code path, not enough to feel like the real site. That is still
  [L2](../plans/contributor-environments-next.md#l2--samplification--terms-gated-delivery)'s job, unchanged.
- **Interactive search, filter, and sort.** `SongIndexLocal` has no `Search`/`DoSearch` override,
  so the song-list/browse UI always comes back empty against the sandbox — a contributor can only
  reach a seeded song via the direct links the startup banner prints. This was in scope for L1a
  originally and got deferred rather than dropped; see
  [L1f](#l1f--search-filter-and-sort-for-songindexlocal-new-fast-follow) below for the options.

### L1f — Search, filter, and sort for SongIndexLocal (new fast follow)

**Cost: S–M** (recommended option; see below) — not shipped in PR #250, implemented since as a
follow-up; blocked nothing else in this document

**✅ Implemented (Option A).** `SongIndexLocal.Search(SongFilter, int?, CruftFilter)` evaluates
`DanceQuery`/`TagQuery`/`UserQuery`/`KeywordQuery`/`SongSort`/tempo-length range/`CruftFilter`
directly against the in-memory song store, and `SongIndex.Search(SongFilter, ...)` was made
`virtual` so it can be overridden; `SongSearch.Search()` (`m4d/Services/SongSearch.cs`) now calls
that overload directly instead of pre-flattening to `SearchOptions` and calling the
`(string, SearchOptions, CruftFilter)` seam, so the real browse/filter UI's call path reaches the
override. The base (Azure-backed) implementation is unchanged — it still does the same
flattening inline — so production behavior is identical. Tests:
`m4dModels.Tests/SongIndexLocalSearchTests.cs` (dance threshold, tempo range, keyword substring,
tag include, sort direction, paging/total-count). Left as explicit gaps, matching the "long tail
can land incrementally" cost note below: raw/customsearch filters (`SongFilter.IsRaw` — falls
back to keyword+sort+paging only, no structured filtering), per-dance-scoped tag queries
(`DanceQueryItem.TagQuery`), Purchase/service-availability filtering, and vote-based queries
(`UserQuery.IsVoted` — `SongSearch` routes those through `VoteSearch`/`StreamAll` instead, which
`SongIndexLocal` doesn't override). The free-text search box and a few narrower flows
(`SimpleSearch`, `FindArtist`, `SongsFromTitle`, `SongsFromTitleArtist`, `List`) still call the
private `DoSearch` directly and aren't intercepted — only the filter/sort/browse path is covered.

This was flagged as needed back in the original L1a table (_"gains free-text title/artist
search, dance-filter matching, and paging — needed once results get rendered for a person
instead of just asserted on in a test"_) but got deferred out of PR #250's scope, and there was
no options analysis for it on its own. This section is that analysis.

**Where things stand today:** `SongIndexLocal` overrides `SaveSong`/`SaveSongs`/`FindSong`/
`GetSongFromService`/`LoadLightSongsStreamingAsync`, but not `Search`/`DoSearch`. Visiting the
song-list/browse pages in `m4d.Sandbox` doesn't crash — the base `SongIndex.Search`'s catch-all
(`catch (Exception e)`, `SongIndex.cs:1168`) swallows whatever
`ThrowingSearchClientFactory` throws and returns an empty `SearchResults` — but it always comes
back with zero results. A contributor can vote on, tag, and edit any of the ~400 seeded songs
(direct-link and ID-based paths all work), but can't _find_ one through the UI; the startup
banner prints direct links specifically to work around this.

**Which "in-memory database" actually holds the songs?** Worth being precise about, since the
sandbox has two unrelated in-memory stores and only one of them is where songs live:

| Store                                                              | Holds                                                        | Query mechanism today                                                                        |
| ------------------------------------------------------------------ | ------------------------------------------------------------ | -------------------------------------------------------------------------------------------- |
| EF Core `UseInMemoryDatabase` on `DanceMusicContext`               | Users, dances, tags, roles — Identity + the SQL-side tables  | Normal EF LINQ, already works, untouched by this section                                     |
| `SongIndexLocal`'s private `Dictionary<Guid, Song>` (`_songStore`) | **Songs** — the thing standing in for the Azure Search index | Point lookups only (`FindSong` by id, `GetSongFromService` by service+id) — no query surface |

Songs never touch EF at all, in the sandbox or in production — they live in Azure Search, and
`SongIndexLocal`'s dictionary is the local stand-in for that index specifically. So "use the
in-memory database" for song filtering doesn't mean teaching EF's InMemory provider anything; it
means giving `SongIndexLocal` a real `Search` implementation over its own dictionary.

**What Azure Search actually receives, worth restating because it shapes every option below:**
`SongFilter.GetOdataFilter(dms)` and `.ODataSort` already build real **OData v4 filter/orderby
expressions** — including the `any`/`all` lambda-over-collection syntax
(`OtherTags/any(t: t eq 'Halloween')`, `dance_wcs/Votes desc`), which is standard OData v4, not
an Azure-only extension — and hand them to `SongIndex.AzureParmsFromFilter` as
`SearchOptions.Filter`/`.OrderBy` (`SongIndex.cs:1465`). Free-text (`SearchString`) is separate:
that's Azure's own full-text query, unrelated to OData. This is the direct answer to "is there
an OData mechanism we could reuse" — yes, the filter half of Azure Search's own query surface
already _is_ OData, which is exactly what makes option B below possible in principle, and exactly
why option A below doesn't need it.

#### Option A — Evaluate SongFilter's parsed sub-queries directly against `Song` (recommended)

`GetOdataFilter()` doesn't parse a string — it _assembles_ one, from objects that are already
fully parsed and sitting on `SongFilter` before any stringification happens: `DanceQuery`
(`.Items`, each with `.Threshold` and an optional `.TagQuery`, plus `.IsExclusive` for and/or
semantics), `TagQuery` (`.TagList`, already split into include/exclude and classed by
Music/Style/Tempo/Other), `UserQuery` (include/exclude, like/hate/upvoted/downvoted modifiers),
`KeywordQuery`, `SongSort` (`.Id`, `.Descending`), plus the raw `TempoMin/Max`/`LengthMin/Max`/
`Purchase` fields. Override `SongIndexLocal.Search(string, SearchOptions, CruftFilter)` (the one
`virtual` seam `SongIndex` already exposes — `SongIndex.cs:1146`) to translate each of those
objects into a LINQ predicate or comparer evaluated directly against the `Song` objects in
`_songStore.Values`, skipping OData text entirely in both directions.

| Filter dimension                      | Structured source (already parsed)                                                                            | Evaluates against                                                                                                                                                                            |
| ------------------------------------- | ------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Dance selection + rating threshold    | `DanceQuery.Items` (`.Threshold`, `.IsExclusive`)                                                             | `Song.DanceRatings` (`DanceId`, vote total)                                                                                                                                                  |
| Tags (include/exclude, per class)     | `TagQuery.TagList`; ring expansion via `dms.GetTagRings` (local lookup, not Azure)                            | `Song.DanceRatings[].TagSummary.Tags` / song-level tags                                                                                                                                      |
| User activity (voted/liked/edited by) | `UserQuery` (include/exclude, modifier)                                                                       | Per-user vote/edit history on `Song` — confirm exact shape while implementing                                                                                                                |
| Tempo / length range                  | `TempoMin/Max`, `LengthMin/Max`, `SongFilter.SingleDanceId` for the per-dance tempo case                      | `Song.Tempo`/`.Length`, or the scoped dance's tempo sub-field                                                                                                                                |
| Service availability                  | `Purchase` (already split by `SplitPurchase`/`BuildServiceClause`)                                            | `Song`'s purchase/service-link list                                                                                                                                                          |
| Free text                             | `KeywordQuery`                                                                                                | Case-insensitive substring/token match on Title + Artist — **not** real relevance scoring, which is already an accepted, disclosed gap (see "Search relevance honesty" in Decisions Settled) |
| Sort                                  | `SongSort.Id`/`.Descending` (Title, Artist, Tempo, Length, Modified, Created, Comments, Dances/vote-count, …) | `OrderBy`/`OrderByDescending` on the matching `Song` property or computed vote total                                                                                                         |
| Paging                                | `SearchOptions.Skip`/`.Size`                                                                                  | `.Skip().Take()` on the ordered result                                                                                                                                                       |

This is squarely inside the CLAUDE.md rule to always use the class library rather than
hand-parsing filter strings — it just applies the rule one layer up, using the already-parsed
query objects instead of re-deriving them from OData text nobody needs to generate for this
path.

**Cost:** the common cases — dance selection, tempo/length range, substring keyword match, sort
by the obvious fields, paging — are roughly a day or two and make the sandbox usable. The long
tail (tag-ring expansion edge cases, the unconfirmed-dance cruft-filter carve-out, user
like/hate/vote modifiers, mood/beat/energy "intrinsic" sort fields) can land incrementally
without blocking the first cut; a partially-correct filter already beats today's always-empty
result. **Facets** (the per-dance/tag counts the filter sidebar shows) can return empty in a
first pass — verify the Vue side tolerates that — and are a small `GroupBy`/`Count` addition
later if wanted.

**Risk to name explicitly:** this becomes a second, hand-maintained implementation of "what a
filter means," alongside `GetOdataFilter()`'s. Nothing forces them to agree if someone edits one
without the other. Mitigate with a small set of shared tests — construct a `SongFilter`, assert
the same expected songs come back from `SongIndexLocal` — not full equivalence testing, just
enough to catch drift.

#### Option B — A real OData library, parsing the actual `GetOdataFilter()` string

The literal version of "is there an OData mechanism we could bolt on": `Microsoft.AspNetCore.OData`
(the official ASP.NET Core package) parses `$filter`/`$orderby` and applies them to an
`IQueryable<T>` — and since the `any`/`all` collection-lambda syntax `SongFilter` emits is
standard OData v4, not an Azure-only dialect, a real OData parser should understand the shapes
this codebase actually generates.

The blocker is the Entity Data Model (EDM) the library needs to translate `$filter` into LINQ.
Flat fields (Title, Artist, Tempo, Length, tag-list collections) map cleanly onto a CLR type via
`ODataConventionModelBuilder`. The dynamic per-dance fields (`dance_{danceId}/Votes`,
`dance_{danceId}/{tempo field}`) don't — one dance-shaped sub-object per dance, and the set of
dances is only known from `DanceLibrary` at runtime, not from a fixed C# type at compile time.
OData's "open type" (dynamic property bag) feature can model this in principle, but routing a
path expression like `dance_wcs/Votes` through the library's `IQueryable` translation via an open
type is not the package's mainstream, well-documented use case — real spike needed to know the
true cost before committing to it.

**Verdict: not the first move.** The payoff — exercising the exact string `GetOdataFilter()`/
`ODataSort` produce, which regression-tests that generation code in a way Option A never
would — is real, but it isn't needed to unblock "a contributor can browse the seeded songs," and
the dynamic-field modeling risk is exactly the kind of open-ended integration cost this effort
has otherwise avoided (see the constraints table at the top of this document). Worth a half-day
spike later if OData-generation regression coverage becomes a goal in its own right — not gated
on shipping search.

#### Option C — Flatten to `SongDocument` shape, hand-roll a parser for the closed grammar actually emitted

A middle ground: `SongIndex.DocumentFromSong` already exists and is exposed for tests
(`SongIndexLocal.CallDocumentFromSong`), so each seeded song could be flattened once into the
same document shape Azure would index. Rather than a general OData parser, write a small
recursive-descent parser for only the bounded grammar `SongFilter`/`TagQuery`/`DanceQuery`/
`UserQuery` actually produce — `and`/`or`/`not`, `eq`/`ne`/`ge`/`le`/`gt`/`lt`,
`field/any(t: t eq 'x')`, `field/all(t: t ne 'x')`, `field ne null`. Because the generator is our
own code, the grammar is fully known and closed — this isn't "implement OData," it's "implement
the six shapes we ever produce."

Gets Option B's fidelity benefit (tests the real generated string) without Option B's EDM/open-type
risk. Costs more than Option A to build (a parser, even a small one, is real work with its own
tests) for a benefit — string fidelity — that mostly matters if Option A's hand-written
predicates are found to drift from `GetOdataFilter()` in practice. Reasonable second step if that
happens; not obviously worth building up front.

#### Recommendation

Build **Option A** first. It's the cheapest path to a sandbox where browsing/filtering actually
works, it doesn't require deciding anything about EDM modeling or open types up front, and
"doesn't have to be efficient" (a few hundred songs, evaluated with plain LINQ over a
`Dictionary.Values`) means there's no performance case for the heavier options either. Treat B
and C as later options gated on wanting OData-generation fidelity testing for its own sake, not
as prerequisites to closing the loop opened by L1's "no search yet" gap. This doesn't block
anything else in the document — L0, L1's other pieces, and L3 all stand on their own without it —
so the natural time to schedule it is whenever contributor interactive testing starts actually
getting used, which is also roughly when L2's more realistic dataset would make browsing feel
worthwhile in the first place.

## Related Documents

- [public-api-authorization.md](../plans/public-api-authorization.md) — the contribution being scoped
- [index-backup-streaming.md](../search/index-backup-streaming.md) — the extraction half of samplification
- [testing-patterns.md](testing-patterns.md) — serialized song format for constructed test songs
- [user-name-visibility.md](../users-admin/user-name-visibility.md) — pseudo/batch user semantics
- [unconfirmed-dance-votes.md](../songs/unconfirmed-dance-votes.md) — `dgsnure`, the ±1 cap
- [search-index-versioning.md](../search/search-index-versioning.md) — index naming and versioning
- [SELF_CONTAINED_DEPLOYMENT.md](../infrastructure/deployment.md) — deployment modes
- [admin-pages.md](../users-admin/admin-pages.md) — admin surface and role gating
- [playwright-e2e-testing.md](playwright-e2e-testing.md) — browser-driven e2e against
  `m4d.Sandbox`, unlocked by L1/L1f above
