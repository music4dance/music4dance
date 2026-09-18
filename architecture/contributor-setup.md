# Contributor Setup

Two ways to get a running server, from least to most setup:

| Path | Setup needed | What works |
| --- | --- | --- |
| **[`m4d.Sandbox`](#the-fastest-path-m4dsandbox)** | .NET 10 SDK only (Node 22 too, for a styled UI) | Everything except real search relevance and Spotify/iTunes import |
| **[The real `m4d` app, empty database](#running-the-real-app-against-an-empty-database)** | .NET 10 SDK, Node 22, a SQL database | Everything `m4d.Sandbox` does, against the real composition root |

Both need **zero third-party API keys, zero Azure access, and zero production data**. This is
the practical result of [contributor-test-environments.md](contributor-test-environments.md):
every third-party dependency in this codebase already fails soft, so the app runs with nothing
configured at all.

## Installing Prerequisites

Skip anything you already have from previous work; this only covers first-time setup.

### Windows

- **.NET 10 SDK** — `winget install Microsoft.DotNet.SDK.10`, or the installer from
  [dotnet.microsoft.com/download](https://dotnet.microsoft.com/download). Visual Studio's
  installer only offers SDKs that existed when your current VS *installer build* shipped, so if
  you installed .NET via the VS Installer's "Individual Components" tab and don't see a 10.0 SDK
  listed, don't wait on a VS update — install the SDK standalone; `dotnet build`/`dotnet run`
  from the command line don't need VS to know about it. Full `net10.0` IDE support (IntelliSense,
  project system) needs Visual Studio 2026, or a VS2022 installer build released after .NET 10 GA.
- **Node.js 22** — `winget install OpenJS.NodeJS.LTS`, then enable Corepack:
  `corepack enable`. CI pins `22.x`; later LTS versions (e.g. 24) have been observed to work for
  local `yarn install`/`yarn build`, but 22 is the safe default if you hit anything version-shaped.
- **SQL Server Express LocalDB** — only needed for
  [the real app path](#running-the-real-app-against-an-empty-database); it ships with Visual
  Studio's "Data storage and processing" workload, or install it standalone via the
  [SQL Server Express installer](https://www.microsoft.com/en-us/sql-server/sql-server-downloads)
  (LocalDB option).
- **Windows 11: check Smart App Control isn't blocking local builds.** On a clean Windows 11
  install it can block `dotnet.exe` from loading freshly-built, unsigned project DLLs (you'd see
  `Could not load file or assembly '...': An Application Control policy has blocked this file.`,
  confirmable via `Get-WinEvent -LogName "Microsoft-Windows-CodeIntegrity/Operational"`, event ID
  3077/3033). It has no per-app/folder exception mechanism, so the only fix is turning it off
  entirely: Settings → Privacy & security → Windows Security → App & browser control → Smart App
  Control → Off. This is one-way — re-enabling it later requires a clean Windows reinstall — so
  it's a call only you should make, not something to toggle automatically.

### macOS

- **.NET 10 SDK** — `brew install --cask dotnet-sdk`, or the `.pkg` installer from
  [dotnet.microsoft.com/download](https://dotnet.microsoft.com/download).
- **Node.js 22** — `brew install node@22`, then link it (it's keg-only, so it isn't put on `PATH`
  automatically and won't be what `node`/`corepack` resolve to otherwise):
  `brew link --force --overwrite node@22`. Then `corepack enable`.
- No local SQL Server engine is available on macOS — not needed for the `m4d.Sandbox` path; see
  [the macOS database question](contributor-test-environments.md#the-macos-database-question) for
  the real-app path's Azure SQL serverless free-tier and Docker options.

Verify either platform with:

```sh
dotnet --list-sdks   # expect a 10.x entry
node --version        # expect v22.x
```

---

## The fastest path: `m4d.Sandbox`

```sh
dotnet run --project m4d.Sandbox
```

No connection string, no `user-secrets`, no database engine to install. It runs the real `m4d`
controllers, views, and routes (`m4d.Sandbox` references `m4d.csproj` directly, and points its
`WebRootPath` at `m4d/wwwroot`) against an in-memory database and an in-memory `SongIndexLocal`
instead of Azure Search, seeded with the real, already-public, PII-cleaned song/dance/tag history
embedded from [`m4dModels.Sandbox/TestData/`](../m4dModels.Sandbox/TestData/) — a few hundred
songs with their full edit/tag/rating history, not synthetic placeholders.

On startup it prints a banner with:

- The seeded accounts (usernames only) — an admin (`canTag`/`canEdit`/`showDiagnostics`/`dbAdmin`),
  an editor (`canEdit` only), and a plain roleless account for exercising the ordinary
  voting/tagging path a real user hits
- A handful of direct links into the seeded songs (there's no free-text search yet — see
  "Known gaps" below)
- A warning that search relevance is not representative (it's not backed by real Azure Search)
- A reminder that state is in-memory — `Ctrl+C` and re-run for a clean slate

**Use the HTTP link from the banner, not HTTPS.** The first `dotnet run` on a machine generates
an ASP.NET Core dev certificate but doesn't trust it, so hitting the HTTPS URL shows a browser
cert warning until you run `dotnet dev-certs https --trust`. `m4d.Sandbox/appsettings.json`
already sets `DISABLE_HTTPS_REDIRECT: true` for exactly this reason — same rationale as
[the Playwright e2e setup](playwright-e2e-testing.md#local-setup), which points at
`http://localhost:65085` for the same reason. There's no need to trust the dev cert for the
sandbox path at all; only do so if you specifically need to exercise HTTPS-only behavior.

The banner only prints usernames, not passwords. The default passwords for the three seeded
accounts above are in [`m4d.Sandbox/appsettings.json`](../m4d.Sandbox/appsettings.json) — not a
secret, just public sandbox defaults:

| Account | Username | Password |
| --- | --- | --- |
| admin | `admin` | `Sandbox!Admin1` |
| tester | `tester` | `Sandbox!Test1` |
| editor | `editor` | `Sandbox!Editor1` |

Set `M4D_ADMIN_USER`/`M4D_ADMIN_PASSWORD`, `M4D_TEST_USER`/`M4D_TEST_PASSWORD`, and/or
`M4D_EDITOR_USER`/`M4D_EDITOR_PASSWORD` as environment variables before running if you want to
override any of these defaults.

**For a styled, hydrated UI** (Bootstrap CSS, the Vue widgets), build the client once — it's a
one-time step, not a per-run one, and it's shared: `m4d.Sandbox` serves the exact same
`m4d/wwwroot` that the real app does, so there's no separate client build to maintain per host.

```sh
cd m4d/ClientApp && yarn install && yarn build
```

Without it the sandbox process itself still starts fine, but every page is **blank** — every
page app (`m4d/ClientApp/src/pages/*/App.vue`) mounts into `<div id="app">` with no
server-rendered fallback, so with no built manifest there's no bundle `<script>` to inject and
Vue never mounts. `Vite:IgnoreMissingAssets` just means the app skips injecting that (nonexistent)
tag instead of throwing, not that a working unstyled page is served. `dotnet build`/`dotnet run`
do **not** trigger this client build themselves (only CI's separate client job does); it's always
a deliberate `yarn build` step, for both hosts — and required, not just cosmetic.

**If you already have the sandbox running and then run `yarn build`, restart it.** The running
process resolved "no manifest" at startup and won't notice new files appearing under
`wwwroot/vclient/` — `Ctrl+C` and re-run `dotnet run --project m4d.Sandbox` after the build
completes.

### Editing the Vue client (hot reload)

`m4d` and `m4d.Sandbox` share one `ClientApp` — there's no separate client build to target per
host. To iterate on Vue/TypeScript with hot reload instead of rebuilding via `yarn build` on
every change, run the Vite dev server alongside whichever host you're using:

```sh
cd m4d/ClientApp && yarn dev
```

Then launch the server with the `ASPNETCORE_VITE` flag set, which proxies asset requests to that
dev server instead of reading the static manifest. Both hosts already have a launch profile for
this (`m4d-vite` for the real app, `m4d.Sandbox-vite` for the sandbox):

```sh
dotnet run --project m4d --launch-profile m4d-vite
dotnet run --project m4d.Sandbox --launch-profile m4d.Sandbox-vite
```

**Known gaps**, so they read as expected rather than as bugs:

- Free-text song search and browse/paging aren't implemented in `SongIndexLocal` yet — direct
  lookups (voting, tagging, editing an already-seeded song) work; searching for one by title
  doesn't return results yet
- `MusicServiceManager`'s live Spotify/iTunes enrichment (importing a *new* song from a service
  playlist) isn't stubbed and will fail — editing/voting on the seeded songs never touches this
  path

## Running the real app against an empty database

Prerequisites:

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 22](https://nodejs.org/) with [Corepack](https://nodejs.org/api/corepack.html)
  enabled (`corepack enable`) — this repo uses Yarn, not npm
- A SQL Server database. `appsettings.json`'s default connection string already points at
  **SQL Server Express LocalDB** (`(localdb)\mssqllocaldb`), which ships with Visual Studio on
  Windows and needs no setup beyond having it installed. On macOS/Linux, or if you'd rather not
  install anything locally, see
  [the macOS database question](contributor-test-environments.md#the-macos-database-question)
  for the Azure SQL serverless free-tier option (real SQL Server engine, no local install, no
  ARM/Docker questions) and the Docker alternative.

### Configure

```sh
dotnet user-secrets set "ConnectionStrings:DanceMusicContextConnection" "<your connection string>" --project m4d
dotnet user-secrets set "M4D_ADMIN_USER" "admin" --project m4d
dotnet user-secrets set "M4D_ADMIN_PASSWORD" "<a password meeting the site's password policy>" --project m4d
```

Skip the connection-string line if you're using the LocalDB default — it's already in
`appsettings.json`.

Optional, same low-privilege test accounts `m4d.Sandbox` seeds automatically:

```sh
dotnet user-secrets set "M4D_TEST_USER" "tester" --project m4d
dotnet user-secrets set "M4D_TEST_PASSWORD" "<password>" --project m4d
```

### Create the database

The `dotnet-ef` CLI tool is pinned as a local tool in `m4d/.config/dotnet-tools.json`, not
installed globally — restore it first, and run `dotnet ef` from inside `m4d/` (local tools are
resolved from the nearest `.config/dotnet-tools.json` walking up from the current directory, and
there's no manifest at the repo root):

```sh
cd m4d
dotnet tool restore
dotnet ef database update --project ../m4dModels --startup-project .
```

The command's own console output ends with a scary-looking `HostAbortedException` dump
("Application failed to build") — that's expected. `dotnet ef` intentionally aborts the app's
normal startup host after extracting just enough to build the `DbContext`; the app's own
top-level exception handler logs that abort verbosely, but it isn't a failure. Check the exit
code or query the database directly if you want to confirm success rather than trust the log
output.

### Run

```sh
dotnet run --project m4d
```

This uses the `m4d-build` launch profile (the first `commandName: Project` entry in
`m4d/Properties/launchSettings.json`), which — unlike `m4d.Sandbox` — does **not** set
`DISABLE_HTTPS_REDIRECT`. The HTTP URL (`http://localhost:5000`) 307-redirects to HTTPS
(`https://localhost:5001`), so for this path you do need to trust the dev cert:

```sh
dotnet dev-certs https --trust
```

or use the `m4d-spotify` profile instead (`dotnet run --project m4d --launch-profile m4d-spotify`),
which binds HTTP-only on `http://127.0.0.1:5000` with `DISABLE_HTTPS_REDIRECT` already set, the
same workaround `m4d.Sandbox` uses.

### Expected startup warnings

You should see a `WARNING:` line for each of these — they mean the resilience layer is working
as designed, not that something is broken:

```text
WARNING: Google OAuth not configured: ...
WARNING: Facebook OAuth not configured: ...
WARNING: Spotify OAuth not configured: ...
WARNING: Email service not configured: ...
WARNING: reCAPTCHA not configured: ...
```

Azure Search clients register successfully at startup (connections are lazy-loaded and only
health-checked on first use), so they won't show up in the startup health summary — the failure
shows up later, on the first search/lookup that actually hits Azure Search.

### What doesn't work without keys

- Social login (Google / Facebook / Spotify) — sign up with a local username/password instead
- Outbound email (password reset, confirmation) — falls back to logging a warning instead of
  sending; nothing is delivered
- Song search and service (Spotify/iTunes) track lookup — no Azure Search service configured, and
  (unlike `m4d.Sandbox`) there's no local stand-in for it here, so there's nothing to browse or
  search — see the optional step below if you want browsable content on this path
- **Known bug, not a setup issue**: a *failed* local-account login attempt 500s instead of
  failing soft, because `Login.cshtml` conditionally renders `<recaptcha-script-v2 />` after a
  failed attempt, and `Owl.reCAPTCHA.IreCAPTCHALanguageCodeProvider` is never registered in DI
  when reCAPTCHA is unconfigured. A *successful* login (right credentials, first try) is
  unaffected. Worth a real fix independent of this doc.

### Build and test

```sh
yarn install && yarn build   # client (from m4d/ClientApp)
dotnet build                 # server
```

See [CLAUDE.md](../CLAUDE.md) for the full test-target table (`Server: Test`, `Test All`, etc.)
and testing conventions.

### Optional: seed the same browsable content into the real LocalDB

The plain real-app path above starts with a genuinely empty site — no dances, no songs, nothing
to click through — because songs are never SQL-backed in this codebase; they live entirely in the
search index (Azure Search in production, an in-memory stand-in in `m4d.Sandbox`). Seeding the SQL
database alone can't fix that, since the real app's own DI always wires up the real (here,
unconfigured) Azure-Search-backed service.

`m4d.Sandbox` has a `SANDBOX_USE_LOCALDB` opt-in for exactly this: it backs itself with the same
real, persistent SQL LocalDB (`m4d`) this section just created and migrated, instead of its
default in-memory database, while still using its in-memory `LocalSearchServiceManager` in place
of Azure Search. Run it via the `m4d.Sandbox-localdb` launch profile, **after** completing
"Create the database" above:

```sh
dotnet run --project m4d.Sandbox --launch-profile m4d.Sandbox-localdb
```

This gets you the same seeded, browsable songs/dances/tags `m4d.Sandbox` always provides, but
backed by the real SQL Server engine and persistent storage instead of in-memory — useful for
exercising real migrations/persistence behavior with content to actually click through. Two
things carry over from `m4d.Sandbox`, not the real-app path:

- Songs are still search-index-backed, not SQL-backed, so they reseed fresh every run regardless
  of this flag — only SQL-backed state (user accounts, activity log, playlists, saved searches)
  persists across restarts.
- It's still the sandbox's own DI (`ConfigureSearch: false`), so this is **not** the same as
  making `dotnet run --project m4d` itself show songs — that's not achievable without real Azure
  Search access.

---

## Related documents

- [contributor-test-environments.md](contributor-test-environments.md) — the options analysis
  this setup is drawn from, including the cloud-deploy path for end-to-end iOS validation
- [testing-patterns.md](testing-patterns.md) — the serialized song format used to construct
  test songs inline, for writing new tests against either path above
- [CLAUDE.md](../CLAUDE.md) — stack conventions and coding standards
