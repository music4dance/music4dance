import { defineConfig } from "@playwright/test";

// Playwright drives m4d.Sandbox - the no-external-service local server described in
// architecture/contributor-test-environments.md (L1) - not production or the shared m4d-test
// deploy. See architecture/playwright-e2e-testing.md for the full environment write-up.
//
// Before running: `cd ../m4d/ClientApp && yarn build` at least once, so wwwroot/vclient has
// real bundles (m4d.Sandbox creates the directory on startup either way, but an empty one means
// no Vue islands mount - voting/tagging/search all live client-side).
const port = 65085;

// Set to run against a sandbox you've already started yourself (a separate terminal, or a
// deployed instance) - this also disables the webServer block below, since starting our own
// dotnet run on the hard-coded port would be pointless (and could conflict with the one
// M4D_SANDBOX_URL points at) once a server is already reachable elsewhere.
// Trimmed and treated as unset when blank, so an accidentally-empty env var (e.g.
// M4D_SANDBOX_URL= with no value) doesn't silently produce an invalid empty baseURL while still
// skipping the webServer that would have caught the misconfiguration.
const externalSandboxUrl = process.env.M4D_SANDBOX_URL?.trim() || undefined;

export default defineConfig({
  testDir: "./tests",

  // m4d.Sandbox is one shared in-memory process (EF InMemory + SongIndexLocal's in-process
  // song dictionary) - not a fresh instance per test the way unit tests get a fresh
  // DanceMusicTester context. Two tests mutating the same song/playlist concurrently would be
  // flaky by construction. Keep this serial until/unless that's revisited (see the
  // "Concurrency" section of playwright-e2e-testing.md).
  fullyParallel: false,
  workers: 1,

  retries: process.env.CI ? 1 : 0,
  reporter: process.env.CI ? "html" : "list",

  use: {
    // Plain HTTP, not HTTPS: m4d.Sandbox/appsettings.json already sets
    // DISABLE_HTTPS_REDIRECT so this is a supported, no-friction path - it also sidesteps dev
    // cert trust setup entirely, locally and in CI alike.
    baseURL: externalSandboxUrl ?? `http://localhost:${port}`,
    trace: "retain-on-failure",
    video: "retain-on-failure",
    screenshot: "only-on-failure",
  },

  // Starts m4d.Sandbox for the run and tears it down afterward. Skipped entirely when
  // M4D_SANDBOX_URL is set (see above) - reuseExistingServer only covers "already running on
  // this same hard-coded port", not "running somewhere else instead".
  //
  // --no-launch-profile is required to stop launchSettings.json's launchBrowser:true from
  // popping a browser window during automated runs - but that flag also throws away
  // launchSettings.json's applicationUrl and ASPNETCORE_ENVIRONMENT=Development along with it,
  // so both must be set explicitly here or the server silently comes up on the ASP.NET Core
  // default (http://localhost:5000, Production) instead - confirmed by hand, since that's
  // exactly the failure mode that produced a webServer boot timeout on port 65085.
  webServer: externalSandboxUrl
    ? undefined
    : {
        command: "dotnet run --project ../m4d.Sandbox --no-launch-profile",
        url: `http://localhost:${port}`,
        reuseExistingServer: !process.env.CI,
        // Seeding ~400 songs and building SongIndexLocal at startup, plus dotnet run's own
        // build-check overhead, comfortably fits under 60s locally but ran past it on a cold
        // GitHub-hosted runner (confirmed by a live workflow_dispatch run:
        // https://github.com/music4dance/music4dance/actions/runs/34002543313). Give CI more room.
        timeout: process.env.CI ? 120_000 : 60_000,
        // Defaults are "ignore" for stdout and "pipe" for stderr - pipe both explicitly so a
        // webServer timeout always shows real startup progress in CI logs, not just half of it.
        stdout: "pipe",
        stderr: "pipe",
        env: {
          ASPNETCORE_ENVIRONMENT: "Development",
          ASPNETCORE_URLS: `http://localhost:${port}`,
        },
      },

  projects: [
    {
      name: "chromium",
      use: { browserName: "chromium" },
    },
    // Deliberately Chromium-only for now - this is a functional-coverage suite, not a
    // cross-browser compatibility matrix. Add Firefox/WebKit projects only if a browser-specific
    // bug actually shows up (see playwright-e2e-testing.md, "Browser matrix").
  ],
});
