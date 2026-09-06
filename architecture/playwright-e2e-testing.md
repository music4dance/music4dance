# Playwright End-to-End Testing

**Status:** 📋 Proposed

**Context:** [contributor-test-environments.md](contributor-test-environments.md) built
`m4d.Sandbox` — a second ASP.NET Core host that boots the real controllers/views/middleware
against in-memory stand-ins for SQL Server and Azure Search, with no external services and
nothing to install. That document flagged browser-driven e2e as newly possible but out of
scope for that PR (L1e: _"Browser-driven e2e becomes possible at all... the first point in the
whole document where Playwright-style testing of voting, tag editing, or playlist creation is
available cheaply"_) and left it as an open question (_"Is the CI end-to-end smoke test against
`m4d.Sandbox` still worth adding?"_). This document is that plan: how to set up Playwright
against `m4d.Sandbox` locally and in the cloud, and what minimal set of tests gives reasonable
coverage of the core site.

This closes that open question. It does not revisit anything already settled in
contributor-test-environments.md — `m4d.Sandbox`, the seeded users, and `SongIndexLocal`'s
search/filter/sort (L1f) are treated as given.

---

## Why this is cheap now, and wasn't before

Every earlier point in this document's prerequisite chain would have made e2e testing either
impossible or expensive:

- Against production or the shared `m4d-test` deploy: real user data, a deploy cycle per
  iteration, and no reset between runs.
- Against `m4d` run locally with a real LocalDB + Azure Search: needs a database engine
  installed, real search credentials, and third-party keys for anything touching login.
- Against `m4d.Sandbox` before L1f: the song-list/browse UI always came back empty, so nothing
  reachable through search could be exercised — only direct links to seeded songs worked.

Against `m4d.Sandbox` today: `dotnet run`, no installed database, no keys, ~400 real (already
public) seeded songs reachable through the actual search/filter UI, and three seeded users
covering the three privilege tiers that matter (admin / editor / plain). State is in-memory, so
a clean slate is a process restart, not a database reset script.

---

## Environment Setup

### What Playwright drives

`m4d.Sandbox` serves the same Razor views and the same compiled Vue islands as production —
project reference, not a reimplementation (see contributor-test-environments.md L1b). The one
gap: `wwwroot/vclient` (the Vite build output) is gitignored and doesn't exist on a fresh
checkout. `m4d.Sandbox` now creates that directory on startup so the server returns 200 instead
of 500 (see the "Create wwwroot/vclient on startup" fix), but an **empty** `vclient` means no
Vue islands actually mount — voting, tag editing, and search all live in Vue components, so a
client build is not optional for this test suite the way it is for a bare API smoke check.

**Local and CI both need `yarn build` to have run at least once** before `m4d.Sandbox` starts,
so the manifest points at real bundles. There's no need to rebuild on every test run — only
when client source changes.

### Where the Playwright project lives

A new top-level `e2e/` directory, sibling to `m4d/`, `m4dModels/`, `m4d.Sandbox/` — not nested
inside `m4d/ClientApp/`. Reasoning:

- These tests drive the whole stack (server-rendered Razor + Vue islands + in-memory EF +
  `SongIndexLocal`), not client-only units. `m4d/ClientApp`'s Vitest suite is a different
  concern (component-level, no server) and stays untouched.
- `e2e/` gets its own `package.json`/lockfile via Yarn (matching the project's yarn-not-npm
  convention), independent of `ClientApp`'s dependency set. Playwright has no reason to share a
  `node_modules` tree with Vue/Vite tooling.
- This mirrors the precedent `SelfCrawler` already sets: a project that lives in the solution
  and is real, but is deliberately outside the default build/test loop (`Test All` excludes it;
  CI excludes it) because it's a different kind of test with different infrastructure needs.
  `e2e/` is the same shape, one layer up the stack.

Proposed layout:

```
e2e/
  package.json
  playwright.config.ts
  tsconfig.json
  fixtures/
    auth.ts            # login(page, tier) helper — admin / editor / tester
    songs.ts           # findSeededSong(page, danceId) — search UI, not hardcoded GUIDs
  tests/
    smoke.spec.ts       # home, chrome, dance index/details render
    auth.spec.ts        # login as each seeded tier, role-gated UI visibility
    search-and-browse.spec.ts   # dance/tag filter, sort, paging via the real UI
    voting.spec.ts      # vote a dance rating, verify total, undo
    tagging.spec.ts     # add/remove a tag, verify TagListEditor state
    playlist.spec.ts    # create a playlist from search results
    custom-search.spec.ts      # build a filter via UI, assert the URL round-trips
  .gitignore            # test-results/, playwright-report/, blob-report/
```

Kept deliberately flat — a handful of small fixture helpers, not a full page-object framework.
Per CLAUDE.md, no abstraction beyond what seven-ish spec files actually need.

### Local setup

Prerequisites beyond what contributor-setup.md already documents: Node 22 (already required for
`ClientApp`) plus the Playwright browser binaries, installed once via `yarn playwright install
chromium` (Chromium only for v1 — see [Browser matrix](#browser-matrix-start-narrow)). Use `yarn`,
not `npx`, to run Playwright's own CLI — `e2e/.yarnrc.yml` sets `nodeLinker: pnpm`, and `npx`
can't resolve the local `playwright` binary under that linker (`sh: 1: playwright: not found`).

```bash
# one-time, or after client source changes
cd m4d/ClientApp && yarn build

# one-time
cd e2e && yarn install && yarn playwright install chromium

# run the suite — playwright.config.ts's webServer block starts m4d.Sandbox for you
cd e2e && yarn test
```

`playwright.config.ts`'s `webServer` option launches `dotnet run --project ../m4d.Sandbox`
itself (Playwright's built-in support for "start a server, wait for it to respond, then run
tests, then tear it down") — so a contributor doesn't need two terminals. Point `baseURL` at the
**HTTP** URL (`http://localhost:65085`), not HTTPS: `m4d.Sandbox/appsettings.json` already sets
`DISABLE_HTTPS_REDIRECT: true` for exactly this kind of no-friction scenario, and using HTTP
sidesteps the dev-cert-trust step entirely, locally and in CI alike. If a test specifically needs
to exercise HTTPS-only behavior later, that's a deliberate opt-in on that one test, not the
suite default.

Sample `playwright.config.ts` shape:

```ts
import { defineConfig } from "@playwright/test";

export default defineConfig({
  testDir: "./tests",
  fullyParallel: false, // shared in-memory server state — see Concurrency below
  workers: 1,
  retries: process.env.CI ? 1 : 0,
  reporter: process.env.CI ? "html" : "list",
  use: {
    baseURL: "http://localhost:65085",
    trace: "retain-on-failure",
    video: "retain-on-failure",
  },
  webServer: {
    command: "dotnet run --project ../m4d.Sandbox --no-launch-profile",
    url: "http://localhost:65085",
    reuseExistingServer: !process.env.CI,
    timeout: 60_000,
  },
  projects: [{ name: "chromium", use: { browserName: "chromium" } }],
});
```

`--no-launch-profile` avoids `launchSettings.json`'s `launchBrowser: true` popping a browser
window during automated runs; the fixed port comes from that same file
(`applicationUrl`) and needn't be repeated in config beyond the `url`/`baseURL` Playwright needs.

### Concurrency: serial by design, for now

`m4d.Sandbox`'s state — the EF InMemory database and `SongIndexLocal`'s song dictionary — is
**one shared mutable process**, not a fresh instance per test the way unit tests get a fresh
`DanceMusicTester` context each. Two tests voting on the same song, or racing to create playlists
with the same name, would be flaky by construction, not by accident. `workers: 1` /
`fullyParallel: false` sidesteps that entirely for v1: correctness over speed, and the whole
suite is small enough (single-digit spec files) that serial execution is still fast.

Each test that mutates state should also leave it clean where possible — voting tests use the
existing **"Undo My Changes"** affordance ([SongCore.vue:492](../m4d/ClientApp/src/pages/song/components/SongCore.vue#L492))
rather than relying on process restart between tests, and tests that create new entities
(playlists) give them test-run-unique names (e.g., a timestamp or random suffix) so reruns don't
collide with leftovers from a previous failed run.

If this becomes a bottleneck later, the next lever is `m4d.Sandbox`'s already-flagged, not-yet-
built `/sandbox/reset` endpoint (contributor-test-environments.md, L1e open question) — reset
between test files instead of relying on tests to self-clean. Not needed to start.

### Selector strategy

The codebase has almost no `data-testid`/`data-test` attributes today (a couple in
`CheckedList.vue`, nothing elsewhere) — but it doesn't need them. Buttons and form fields already
carry visible, stable text (`"Edit"`, `"Undo My Changes"`, `"Add Dance Style"`,
`asp-for="Input.UserName"` with a real label) because `bootstrap-vue-next` components and Razor
`asp-for` both produce accessible markup by default. **Prefer Playwright's role/label locators**
(`getByRole("button", { name: "Undo My Changes" })`, `getByLabel("Username")`) over CSS selectors
or added test IDs — they're more resistant to markup churn and they double as a light
accessibility check for free. Reach for `data-testid` only where a control is genuinely
unlabeled (an icon-only `unplugin-icons` button with no `aria-label`) — and add the missing
`aria-label` instead, where that's the real gap, rather than papering over it with a test hook.

### Browser matrix: start narrow

Chromium only for v1. This is deliberately not a cross-browser compatibility suite — it's
functional coverage of core flows. Add Firefox/WebKit projects later only if a browser-specific
bug actually shows up; running three engines from the start triples CI time for a hypothesis
with no evidence behind it yet.

### Cloud / CI setup

Per your instinct: **a separate workflow, not folded into `ci-server.yaml`/`ci-client.yaml`.**
Those two are fast, deterministic, and gate every PR; a browser-driven suite is inherently
heavier (browser install, two build steps, a running server) and — until it's proven stable —
shouldn't be able to block a merge on its own flakiness. Proposed `.github/workflows/e2e.yaml`:

```yaml
name: E2E

on:
  workflow_dispatch:
  schedule:
    - cron: "0 10 * * *" # nightly
  # Promote to `pull_request` once the suite has proven stable for a couple of weeks.

jobs:
  playwright:
    runs-on: ubuntu-latest # confirmed - see the OS note below
    steps:
      - uses: actions/checkout@v4

      - uses: actions/setup-node@v4
        with:
          node-version: "22.x"
      - run: corepack enable

      - name: Build client
        working-directory: m4d/ClientApp
        run: |
          yarn install --network-timeout=300000
          yarn build

      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: "10.x"
      # Debug, not Release: playwright.config.ts's webServer runs `dotnet run` with no
      # --configuration flag, which defaults to Debug. Building Release here would warm up an
      # output dotnet run never looks at, leaving the real Debug build to happen live inside
      # webServer.timeout.
      - run: dotnet build m4d.Sandbox/m4d.Sandbox.csproj --configuration Debug

      - name: Install Playwright
        working-directory: e2e
        run: |
          yarn install --network-timeout=300000
          yarn playwright install --with-deps chromium

      - name: Run Playwright
        working-directory: e2e
        run: yarn test
        env:
          CI: true

      - uses: actions/upload-artifact@v4
        if: (!cancelled())
        with:
          name: playwright-report
          path: e2e/playwright-report/
          retention-days: 14
```

**OS choice — confirmed via a live run.** `ci-server.yaml` runs on `windows-latest`; nothing found
in this investigation explains why (no `LocalDB` connection string is referenced outside
`m4d/appsettings.json`'s default, and `m4d.Sandbox` uses `UseInMemoryDatabase` exclusively — no
local database engine at all). `ubuntu-latest` is cheaper and faster for GitHub-hosted runners,
and a real `workflow_dispatch` run confirmed `m4d.Sandbox` boots and serves correctly there
(https://github.com/music4dance/music4dance/actions/runs/34004859307) — no Windows-only
dependency inherited through `m4d.csproj`. That run also caught a real, pre-existing bug that only
a case-sensitive filesystem could surface: `DMController.ReadJsonFile` requested
`danceGroups.json` (capital G) while the file on disk is `dancegroups.json`, silently masked on
Windows/WSL's case-insensitive filesystems (fixed in #266).

**Why nightly + on-demand, not on every PR:** matches the "separate GitHub Action outside CI"
instinct — this suite costs real minutes (client build + browser install + server boot) on every
run, and a new suite is more likely to be flaky in its first weeks than `ci-server`/`ci-client`
already are. Nightly gives fast feedback on regressions without being a merge gate from day one;
`workflow_dispatch` lets it be run on demand against a specific PR/branch while stabilizing.
Revisit `pull_request` triggering once it's been green for a couple of weeks.

---

## Coverage Plan

The goal is **reasonable coverage of core functionality with a minimal number of tests** — not
exhaustive coverage of every page listed under `m4d/ClientApp/src/pages/`. Pick one thin,
representative slice through the stack per concern, and let unit/integration tests (which are
much cheaper to write and run) continue to carry the depth.

### Tier 1 — the set to build first (~7 spec files)

| Spec                        | Exercises                                                                                                                   | Why it earns a slot                                                                                                                                                             |
| ---------------------------- | ----------------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `smoke.spec.ts`               | Home page loads; `PageFrame` chrome (nav, footer) present; dance index lists dances; a dance-details page renders           | Cheapest possible regression guard for "the app boots and serves real HTML/JS" — exactly the class of bug the `wwwroot/vclient` 500 and the missing `Vite:Server` config were    |
| `auth.spec.ts`                | Login as each of the three seeded tiers (admin/editor/tester); role-gated UI is visible/absent correctly (e.g., `dbAdmin`-only buttons in `SongCore.vue`) | Proves the seeded-user story (L1d) actually gates the UI the way the design intends, not just that the accounts exist                                                          |
| `search-and-browse.spec.ts`   | Filter the song list by dance + tempo range, sort by title, page through results                                            | Directly regression-tests L1f (`SongIndexLocal.Search`) through its real caller (`SongSearch.Search()`), the one path this whole environment previously couldn't exercise at all |
| `voting.spec.ts`              | As the plain tester, cast a dance-rating vote on a song; verify the displayed total changes; undo via "Undo My Changes"     | Core site purpose — matching music to dances is driven by these votes; also the plain-tier account's reason for existing (L1d)                                                 |
| `tagging.spec.ts`             | Add and remove a tag on a song via `TagListEditor`; verify it round-trips through `Tag.fromParts`-style tag strings          | Second core write path; per CLAUDE.md, this also indirectly checks tag-string handling stays class-library-driven rather than manually parsed                                  |
| `playlist.spec.ts`            | From search results, create a playlist and confirm the song appears in it                                                  | Exercises `PlayListController`'s `CreateTransientContext` path — the exact code that needed the InMemory fallback fix documented in contributor-test-environments.md L1b; a real regression risk if that fix ever drifts |
| `custom-search.spec.ts`       | Build a dance+tag filter through the advanced/custom search UI; assert the resulting URL/query uses the expected filter shape | Guards the CLAUDE.md "always use the class library to build/parse filter strings" rule from the UI side — a manual-construction regression would show up here as a broken query, not just a passing unit test |

Each of these should be **one or two tests per file**, not a matrix — e.g. `auth.spec.ts` is
three short tests (one per tier), not a combinatorial sweep of every role-gated button.

### Explicit non-goals for this suite

- **Anything touching live third-party services** (Google/Facebook/Spotify OAuth,
  `MusicServiceManager`'s enrichment path). Already called out as a known non-goal of the
  sandbox itself (contributor-test-environments.md L1e) — no local server work makes this
  testable, and it shouldn't be worked around with real credentials in CI.
  `AddSpotifyWithResilience`'s degrade-cleanly behavior is exactly what Playwright would hit
  instead, which is fine — that's still real coverage of the resilience path, just not of
  Spotify sign-in itself.
- **Search relevance/ranking quality.** `SongIndexLocal` is explicitly disclosed as not
  representative of real Azure Search scoring. Tests should assert *which songs come back* and
  *in what order for an explicit sort field*, never "does the best match rank first."
- **Visual regression / pixel-diff testing.** Not requested, and a maintenance cost of its own;
  revisit only if a specific visual bug recurs.
- **Cross-browser matrix** beyond Chromium (see above) until there's a concrete reason.
- **Load/performance testing.** Different tool, different question.

### Tier 2 — reasonable follow-ups, not part of the first cut

Self-registration + the `FileEmailSender` `.eml` flow (mentioned as newly testable in L1e), the
song-history viewer, `activity-log`, `admin-users`, `tempo-list`'s interactive checklist,
`song-merge`. None of these are core to "does music4dance match music to dances correctly" —
add them opportunistically when someone is already touching that area, not as a batch.

---

## Open Questions

- **Promotion to a PR-blocking check.** Start as `workflow_dispatch` + nightly; decide once it's
  been observed stable for a couple of weeks. No fixed date — a flaky gate is worse than no gate.
- **Whether stable seeded-song titles are worth adding.** Tests should locate songs through the
  real search UI (a `fixtures/songs.ts` helper, not hardcoded GUIDs copied from a console
  banner) so they survive reseeding changes — but if the ~400-song dataset's composition ever
  shifts in a way that breaks a specific filter assumption (e.g., "at least one WCS song with a
  tempo above X"), the cheap fix is a couple of dedicated fixture songs seeded specifically for
  e2e, not a hunt through the real dataset for a stable example. Not needed until it's actually a
  problem.
- **`/sandbox/reset`.** Still not built (contributor-test-environments.md's own open question).
  This plan's answer for now is "tests clean up after themselves + serial execution"; revisit if
  that proves insufficient once the suite exists.

---

## Related Documents

- [contributor-test-environments.md](contributor-test-environments.md) — `m4d.Sandbox`, seeded
  users, and `SongIndexLocal` search/filter/sort (L1f), all treated as prerequisites here
- [testing-patterns.md](testing-patterns.md) — serialized song format used by the sandbox's own
  seeding, useful background for understanding what a seeded song actually looks like
