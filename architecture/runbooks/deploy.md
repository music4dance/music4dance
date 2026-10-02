# Deploy to Test or Production

**Type:** Runbook
**Status:** Current
**Last verified:** 2026-10-01 (against `azure-pipelines.yml`)

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
3. Run. The pipeline does the rest:
   - builds the client and server
   - publishes for `linux-x64`
   - deploys
   - sets `SELF_CONTAINED_DEPLOYMENT`, `ASPNETCORE_ENVIRONMENT`, `SEARCHINDEX`, `SEARCHINDEXVERSION`
     and `WEBSITES_CONTAINER_START_TIME_LIMIT`, plus the startup command
   - points the health check at `/health/ready`

   Switching modes needs no portal changes.

Deploy to `test` first and verify before running `production`.

## Verification

- `GET https://<app>/health/ready` returns `200 {"status":"ready"}`.
- `GET https://<app>/api/health/report` shows no unexpected `Unavailable` services.
- Kudu → `/home/LogFiles/Application/`: the startup report ends `Overall Status: HEALTHY`, and
  the log shows `Production environment detected. Deployment mode: <mode>`.
- Load the home page, a dance page, and a song search.

## Rollback

Re-run the pipeline on the previous good commit or branch with the same parameters.
Deployments are full-package, so this is the whole rollback. If the release included a search
index cutover, also follow that runbook's rollback section.

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
printenv | grep -E 'PORT|HOME|SELF_CONTAINED|WEBSITE|SEARCHINDEX'
ls -la $HOME/site/keys/
tail -f /home/LogFiles/Application/*.log
```
