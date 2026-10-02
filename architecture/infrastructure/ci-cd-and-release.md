# CI/CD and Release

**Type:** Reference
**Status:** Current
**Last verified:** 2026-10-01
**Code:** `.github/workflows/ci-server.yaml`, `.github/workflows/ci-client.yaml`,
`.github/workflows/dco.yml`, `.github/workflows/e2e.yaml`, `azure-pipelines.yml`,
`scripts/verify-line-endings.ps1`

How a change gets from a pull request to the test site and then to production: which checks run
on GitHub, what the `main` branch rules enforce, and how the Azure DevOps pipeline deploys.
Deployment *mechanics* (deployment modes, app settings, identity, health checks) live in
[hosting-and-identity](hosting-and-identity.md), and the procedure for running a deploy lives
in [runbooks/deploy](../runbooks/deploy.md). This doc doesn't repeat them.

## The two systems

| | GitHub Actions | Azure DevOps |
| --- | --- | --- |
| Definition | `.github/workflows/*` | `azure-pipelines.yml` (the only pipeline file) |
| Job | Build, lint, test, sign-off check, nightly e2e | Build and deploy to `m4d-test` or `msc4dnc` |
| Runs | Automatically on PRs and pushes to `main`; e2e nightly | Manually, with runtime parameters |
| Touches Azure | No. No secrets, no service connections | Yes, through the `m4d-release` service connection |

The two are not linked. GitHub checks never deploy, and the Azure pipeline doesn't look at
GitHub check results or run any tests itself.

## GitHub workflows

| Workflow (`name:`) | File | Triggers | Runner | What it checks |
| --- | --- | --- | --- | --- |
| CI-SERVER | `ci-server.yaml` | PR to `main`, push to `main`, manual | `windows-latest` | `dotnet restore`; `dotnet build --configuration Release --no-restore -warnaserror`; `dotnet test` with `--filter FullyQualifiedName!~SelfCrawler` |
| CI-CLIENT | `ci-client.yaml` | PR to `main`, push to `main`, manual | `ubuntu-latest` | Line endings; `yarn lint:ci`; `yarn build` (which includes type-check); `yarn run test:ci` |
| DCO | `dco.yml` | PR to `main`, manual | `ubuntu-latest` | Every commit in the PR has a `Signed-off-by:` trailer |
| E2E | `e2e.yaml` | Nightly at 10:00 UTC, manual | `ubuntu-latest` | Playwright suite against `m4d.Sandbox` |

### CI-SERVER

- **Warning-clean build.** `-warnaserror` turns every compiler or analyzer warning into a
  failure, which is how the "server build must stay warning clean" rule in `CLAUDE.md` is
  enforced. Restore runs as a separate step so that NuGet audit advisories (NU1901 to NU1904),
  which appear when a CVE is published against an unchanged dependency, can't fail an unrelated
  PR at build time.
- **Tests.** Runs every test project in `music4dance.sln` except `SelfCrawler`, which needs a
  browser and the live site and is manual-only. Results are written as `.trx` to `./TestResults`
  but aren't uploaded or published.
- Runs on Windows. The client is not built here; the server build doesn't need it.

### CI-CLIENT

All steps after the line-ending check run in `m4d/ClientApp/`:

1. **Line endings.** `scripts/verify-line-endings.ps1 -FailOnIssues` checks git-tracked text
   files (by extension: `.cs`, `.ts`, `.vue`, `.md`, `.json`, `.yml` and so on) against the
   `.gitattributes` rules: LF for `.yml`/`.yaml` and everything under `m4d/ClientApp/`, CRLF for
   the rest,
   and no stray `0x1A` marker bytes. It enumerates tracked files (`git ls-files`) rather than
   walking the disk, so local and CI runs agree.
2. **Install.** Node 22, Yarn via Corepack.
3. **`yarn lint:ci`** = `eslint src --max-warnings 0`. Unlike `yarn lint`, it doesn't auto-fix,
   and any warning fails the job.
4. **`yarn build`** = `yarn type-check && yarn build-only` (`vue-tsc --build --force`, then
   `vite build`). There is no separate type-check step; this is it.
5. **`yarn run test:ci`** = `vitest run` with a JUnit reporter writing `reports/junit.xml`. The
   XML is uploaded as the `junit-results` artifact and published by
   `EnricoMi/publish-unit-test-result-action`, which posts a results comment on PRs (hence the
   job's `pull-requests: write` permission).

### DCO

Walks `git rev-list base..head` for the PR and fails, with one error per commit, if any commit
message lacks a line matching `^Signed-off-by: .+ <.+>$`. The failure message points at
`git commit --amend -s` and `git rebase --signoff`. The policy and the certificate text are in
`CONTRIBUTING.md`; `CLAUDE.md` requires the trailer on commits made by agents too.

### E2E

A `check-activity` job looks for commits in the last 24 hours; the `playwright` job runs only if
there were some, or if the run was started manually. It builds the client, builds
`m4d.Sandbox` in **Debug** (because Playwright's `webServer` runs `dotnet run` with no
configuration flag), installs Chromium, runs `yarn test` in `e2e/`, and uploads
`e2e/playwright-report/` for 14 days. It is deliberately not a PR check yet. See
[playwright-e2e-testing](../dev-testing/playwright-e2e-testing.md) for the suite and the
promotion plan.

### Other automation (repository settings, no workflow file)

These run as GitHub-managed dynamic workflows and have nothing in `.github/workflows/`:

- **Dependabot security updates** are enabled. There is no `.github/dependabot.yml`, so there are
  no scheduled version-update PRs; Dependabot opens PRs only for security advisories (in practice
  `npm_and_yarn` bumps in `m4d/ClientApp`). Routine upgrades are done by hand, with the
  `update-nuget` and `update-yarn` skills in `.claude/skills/` or
  `scripts/upgrade-packages.ps1`.
- **Automatic Dependency Submission (NuGet)** feeds the dependency graph on every push.
- **Secret scanning** with push protection is enabled.
- **Copilot code review** and the Copilot coding agent are enabled.

## The `main` branch rules

`main` has no classic branch protection. It's governed by one repository ruleset,
`main-default`, which:

- requires a pull request with **one approving review**;
- allows **squash merge only**, and requires linear history;
- blocks deleting the branch;
- lets organization admins bypass it.

The ruleset has **no required status checks**. CI-SERVER, CI-CLIENT and DCO run on every PR and
show their results, but GitHub doesn't stop a merge when one of them is red. Keeping them green
is a review convention, not an enforced gate.

Because merges are squashes, `main` gets one commit per PR. That commit's message is built from
the PR, so the DCO check, which only looks at the PR's own commits, says nothing about whether
the squash commit on `main` carries a sign-off.

## The deploy pipeline

`azure-pipelines.yml` builds the client (`yarn build`) before the server so that Vite's output in
`m4d/wwwroot/vclient` is included when `m4d/m4d.csproj` is published, then publishes for
`linux-x64` and deploys with `AzureWebApp@1`. Publishing is pinned to `m4d/m4d.csproj` so the
`m4d.Sandbox` stub host can never be deployed. The steps, deployment modes and the app settings
it sets are described in [hosting-and-identity § Deployment](hosting-and-identity.md#deployment).

Things worth knowing that aren't covered there:

- **Parameters, not variables.** `environment` (`test` default, or `production`) and
  `deploymentMode` (`framework-dependent` default, or `self-contained`) are runtime
  `parameters`. Compile-time `${{ if }}` blocks turn them into the `appName`,
  `aspnetEnvironment`, `searchIndex`, `searchIndexVersion` and `useSelfContained` variables, so
  defining pipeline variables with those names in the Azure DevOps UI breaks the selection.
- **No tests, no warning gate.** The pipeline runs `dotnet build --configuration Release` without
  `-warnaserror` and runs no tests. It trusts that the commit being deployed already passed
  GitHub CI.
- **No trigger section.** The file declares no `trigger:` or `pr:`. Deploys are run by hand per
  the runbook. Whether Azure DevOps also queues runs on push (its default for a YAML file with
  no `trigger:`) depends on the pipeline's trigger settings in the Azure DevOps UI, which aren't
  in the repo and weren't checked for this doc.
- **No approvals or slots.** There are no Azure DevOps environments, approval checks or
  deployment slots in the YAML. Production is deployed in place, and `test` and `production` are
  separate App Services rather than slots of one app.
- **Search index version** is hard-coded per environment (`searchIndexVersion: "3"` for both),
  so a schema bump is a pipeline edit; see
  [search-index-breaking-migration](../runbooks/search-index-breaking-migration.md).

## From PR to production

1. **Branch and PR.** Contributors fork and open a PR against `main` (see `CONTRIBUTING.md`);
   maintainers branch in the main repo. Every commit is signed off.
2. **PR checks.** CI-SERVER, CI-CLIENT and DCO run. They need no secrets, so they behave the same
   for forks.
3. **Review and merge.** One approval, squash merge.
4. **Post-merge.** CI-SERVER and CI-CLIENT run again on the push to `main`. E2E runs that night.
5. **Test.** Run the Azure DevOps pipeline with `environment: test` to deploy to `m4d-test`, and
   verify it there. A contributor who wants their PR on the test site asks in the PR; see
   [contributor-test-environments](../dev-testing/contributor-test-environments.md).
6. **Production.** Run the pipeline again with `environment: production` on the same commit.
7. **Rollback.** Re-run the pipeline on the last good commit. Details in
   [runbooks/deploy](../runbooks/deploy.md#rollback).

There's no version number, tag or release branch. A release is "the commit that was last
deployed", and the record of what's live is the Azure DevOps run history.

## Future improvements

- **Required status checks.** Add CI-SERVER, CI-CLIENT and DCO as required checks in the
  `main-default` ruleset, so a red check blocks merge instead of relying on review.
- **Explicit `trigger: none`** in `azure-pipelines.yml`, so its manual-only intent doesn't depend
  on UI settings.
- **Promote E2E to a PR check** once it has proven stable (the comment in `e2e.yaml` and
  [playwright-e2e-testing](../dev-testing/playwright-e2e-testing.md) track this).
- **Publish server test results.** CI-SERVER writes `.trx` files but doesn't upload them, so a
  failure has to be read from the raw log. CI-CLIENT already publishes its JUnit results.
- **Yarn global folder path.** The pipeline sets it to
  `$(Agent.BuildDirectory)\Yarn\Berry`, with Windows separators, on an Ubuntu agent. It works,
  but the path is not what it looks like.
- **Production approval.** An Azure DevOps environment with an approval check on `production`
  would stop an accidental production run from the default-`test` form.

## History

- 2020-03 (Azure DevOps PR 21): First Azure Pipelines CI.
- 2025-06 (#5): GitHub Actions CI-SERVER and CI-CLIENT added for PRs ("CI/Gated Checkin").
- 2025-11 (#79): Line-ending verification added to CI-CLIENT.
- 2025-12 (4c87c159): `azure-pipelines-deploy.yml` became the single `azure-pipelines.yml`, with
  runtime parameters for environment and mode. Tests were dropped from the deploy pipeline.
- 2026-01 (#97): Self-contained .NET 10 deployment with automatic app settings and startup command.
- 2026-06 (#185): CI-CLIENT runs the full `yarn build`, so type errors fail CI.
- 2026-06 (#201): Obsolete per-scenario pipeline YAML files removed.
- 2026-08 (#238, #244): Pipeline sets the App Service health check to `/health/ready`.
- 2026-08 (#250): DCO workflow and sign-off policy; pipeline publish pinned to `m4d/m4d.csproj`
  to keep `m4d.Sandbox` out of deploys.
- 2026-09 (#263, #264, #265, #269): E2E workflow, nightly and manual, skipped when nothing
  landed.
- 2026-09 (#294): `yarn lint:ci` gate in CI-CLIENT.
- 2026-09 (#295): `-warnaserror` in CI-SERVER, with restore split out.
- 2026-10-01: This doc created.

## Related

- [hosting-and-identity](hosting-and-identity.md): environments, deployment modes, app settings, identity
- [runbooks/deploy](../runbooks/deploy.md): running a deployment, verification, rollback
- [runbooks/provision-app-service](../runbooks/provision-app-service.md): the `m4d-release` service connection's permissions
- [playwright-e2e-testing](../dev-testing/playwright-e2e-testing.md): the E2E suite
- [testing-patterns](../dev-testing/testing-patterns.md): what the server and client tests look like
- [contributor-test-environments](../dev-testing/contributor-test-environments.md): fork PRs and test-site deploys on request
- [search-index-breaking-migration](../runbooks/search-index-breaking-migration.md): changing `SEARCHINDEXVERSION`
