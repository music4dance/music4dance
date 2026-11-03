# Deploy to Test or Production

**Type:** Runbook
**Status:** Current
**Last verified:** 2026-11-03 (against `azure-pipelines.yml`)

## When to use

Shipping a build to `m4d-test` or `msc4dnc`. For how deployment modes, identity and health
checks work, see [hosting-and-identity](../infrastructure/hosting-and-identity.md); for where
deploys fit after PR checks and merge, see [ci-cd-and-release](../infrastructure/ci-cd-and-release.md).

## Prerequisites / access

- Run access on the Azure DevOps pipeline built from `azure-pipelines.yml`.
- The pipeline's `m4d-release` service connection needs Website Contributor (or equivalent) on
  the target app, so it can set app settings and the health check path. See
  [provision-app-service § 4.3](provision-app-service.md#43-grant-pipeline-service-principal-permissions-required-for-automated-configuration).
- If the release changes the search index schema, follow
  [search-index-breaking-migration](search-index-breaking-migration.md)
  **first**. `SEARCHINDEXVERSION` is fixed in the pipeline variables.

## Steps

1. Azure DevOps → Pipelines → the music4dance deploy pipeline → **Run pipeline**.
2. Choose the runtime parameters. Don't define pipeline *variables* with these names, because
   they won't be picked up.
   - **Target Environment**: `test` deploys to `m4d-test`; `production` deploys to `msc4dnc`.
   - **Deployment Mode**: `framework-dependent` (the default) or `self-contained`.
   - **Run From Package**: on (the default) mounts the package read-only instead of extracting
     it. Leave it on; turning it off is the rollback below.
3. Run. The pipeline does the rest:
   - builds the client and server
   - publishes for `linux-x64`
   - sets `M4D_DATA_ROOT` and `WEBSITE_RUN_FROM_PACKAGE`, if they've changed
   - deploys
   - stops and starts the app, only if Run From Package was just switched on or off
   - sets `SELF_CONTAINED_DEPLOYMENT`, `ASPNETCORE_ENVIRONMENT`, `SEARCHINDEX`, `SEARCHINDEXVERSION`
     and `WEBSITES_CONTAINER_START_TIME_LIMIT`, plus the startup command
   - points the health check at `/health/ready`, if it isn't already

   Switching modes needs no portal changes.

Deploy to `test` first and verify before running `production`.

An ordinary deploy has no downtime: the old container serves until the new one passes its
startup probe. A run that switches Run From Package on or off takes the site down once, for
between about 45 seconds and 4 minutes (see
[hosting-and-identity § Run From Package](../infrastructure/hosting-and-identity.md#run-from-package)).

## Verification

- `GET https://<app>/health/ready` returns `200 {"status":"ready"}`.
- `GET https://<app>/api/health/report` shows no unexpected `Unavailable` services.
- Kudu → `/home/LogFiles/Application/`: the startup report ends `Overall Status: HEALTHY`, and
  the log shows `Production environment detected. Deployment mode: <mode>` and
  `App data root: /home/data/m4d`.
- With Run From Package on, `/home/data/SitePackages/packagename.txt` names the zip this run
  uploaded.
- Load the home page, a dance page, and a song search.

## Rollback

Re-run the pipeline on the previous good commit or branch with the same parameters.
Deployments are full-package, so this is the whole rollback. If the release included a search
index cutover, also follow that runbook's rollback section.

To go back to extracting into `wwwroot`, run with **Run From Package** off. Runtime data stays
in `/home/data/m4d`. A commit from before 2026-11-02 ignores `M4D_DATA_ROOT` and uses
`wwwroot/AppData` again.

## Troubleshooting

- **App won't start / startup timeout.** Check Kudu logs for credential errors. Verify the
  managed identity is on and its grants exist (see
  [provision-app-service § Phase 6](provision-app-service.md#phase-6-troubleshooting)).
  `WEBSITES_INCLUDE_CLOUD_CERTS=true` can add 2–3 minutes to startup.
- **`KeyVaultReferenceException ... Forbidden`.** The app's identity lacks a Get/List
  **access policy** on Key Vault `music4dance`. RBAC roles on that vault do nothing. After
  fixing, restart the app; there's no need to redeploy.
- **Wrong mode or target app.** The parameters were overridden by variables, or not selected at
  run time.
- **Self-contained only:** the startup command must be `/home/site/wwwroot/m4d`, which the
  pipeline sets. Data protection keys live in `$HOME/site/keys`. If users are logged out after
  every restart, check that `HOME` is set and the directory is writable.

Useful console commands (Kudu / SSH):

```bash
printenv | grep -E 'PORT|HOME|SELF_CONTAINED|WEBSITE|SEARCHINDEX|M4D_DATA_ROOT'
ls -la $HOME/site/keys/
tail -f /home/LogFiles/Application/*.log
```
