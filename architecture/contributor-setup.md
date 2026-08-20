# Contributor Setup

Two ways to get a running server, from least to most setup:

| Path | Setup needed | What works |
| --- | --- | --- |
| **[`m4d.Sandbox`](#the-fastest-path-m4dsandbox)** | .NET 10 SDK only | Everything except real search relevance and Spotify/iTunes import |
| **[The real `m4d` app, empty database](#running-the-real-app-against-an-empty-database)** | .NET 10 SDK, Node 22, a SQL database | Everything `m4d.Sandbox` does, against the real composition root |

Both need **zero third-party API keys, zero Azure access, and zero production data**. This is
the practical result of [contributor-test-environments.md](contributor-test-environments.md):
every third-party dependency in this codebase already fails soft, so the app runs with nothing
configured at all.

---

## The fastest path: `m4d.Sandbox`

```sh
dotnet run --project m4d.Sandbox
```

That's it. No connection string, no `user-secrets`, no database engine to install. It runs the
real `m4d` controllers, views, and routes (`m4d.Sandbox` references `m4d.csproj` directly) against
an in-memory database and an in-memory `SongIndexLocal` instead of Azure Search, seeded with the
small, already-public, PII-cleaned dataset in
[`m4dModels.Sandbox/TestData/`](../m4dModels.Sandbox/TestData/).

On startup it prints a banner with:

- The seeded accounts (usernames only) — an admin (`canTag`/`canEdit`/`showDiagnostics`/`dbAdmin`),
  an editor (`canEdit` only), and a plain roleless account for exercising the ordinary
  voting/tagging path a real user hits
- A warning that search relevance is not representative (it's not backed by real Azure Search)
- A reminder that state is in-memory — `Ctrl+C` and re-run for a clean slate

Set `M4D_TEST_USER` / `M4D_TEST_PASSWORD` (and optionally `M4D_EDITOR_USER` /
`M4D_EDITOR_PASSWORD`) as environment variables before running if you want to choose your own
credentials instead of the defaults printed in the banner.

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

```sh
dotnet ef database update --project m4dModels --startup-project m4d
```

### Run

```sh
dotnet run --project m4d
```

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

Azure Search will also report unavailable in the startup health summary — expected with no
Azure Search service configured.

### What doesn't work without keys

- Social login (Google / Facebook / Spotify) — sign up with a local username/password instead
- Outbound email (password reset, confirmation) — falls back to logging a warning instead of
  sending; nothing is delivered
- Captcha on forms that use it
- Song search and service (Spotify/iTunes) track lookup — no Azure Search service configured

### Build and test

```sh
yarn install && yarn build   # client (from m4d/ClientApp)
dotnet build                 # server
```

See [CLAUDE.md](../CLAUDE.md) for the full test-target table (`Server: Test`, `Test All`, etc.)
and testing conventions.

---

## Related documents

- [contributor-test-environments.md](contributor-test-environments.md) — the options analysis
  this setup is drawn from, including the cloud-deploy path for end-to-end iOS validation
- [testing-patterns.md](testing-patterns.md) — the serialized song format used to construct
  test songs inline, for writing new tests against either path above
- [CLAUDE.md](../CLAUDE.md) — stack conventions and coding standards
