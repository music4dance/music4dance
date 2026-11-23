# Partner API Deploy Pipeline

**Type:** Plan
**Status:** Proposed (deferred: `deploy.ps1` works, other priorities first)
**Last verified:** 2026-11-21
**Code:** `m4d-api/infra/deploy.ps1`, `azure-pipelines.yml`

Move routine `m4d-api` code deploys from `m4d-api/infra/deploy.ps1` into an Azure DevOps
pipeline, the same way the web app deploys. Infrastructure deploys stay in the script.

## Why

Today every API release is a local `deploy.ps1` run. That works, but:

- There's no shared record of what's live. The web app's record is the Azure DevOps run history;
  the API's is whatever the last person to run the script deployed from their working tree.
- A local run deploys whatever is checked out, including uncommitted changes. A pipeline
  deploys a commit.
- Releasing the API needs Azure CLI, a signed-in account with rights on the function app, and
  PowerShell 7. A pipeline needs only an Azure DevOps login.

## What stays in the script

`deploy.ps1` does two jobs, and only one belongs in a pipeline.

| Job | Steps | Rights needed | How often | Where it runs |
| --- | --- | --- | --- | --- |
| Infrastructure | Resource group, `main.bicep`, Search Index Data Reader on the index behind the alias | Owner (or Contributor plus User Access Administrator): it creates role assignments | Rare: infrastructure changes, index version cutovers | `deploy.ps1` by hand, unchanged |
| Code | `dotnet publish`, zip deploy, `/v1/health` check | Deploy rights on the function app only | Every release | The new pipeline |

Putting the infrastructure job in a pipeline would mean giving a service connection
Owner-level rights on the subscription, for something run a few times a year. That's not
worth it. The script's header already anticipates this split: `-SkipInfrastructure` "only needs
rights to deploy to the function app, which is what a pipeline would use."

## Design

A second pipeline file, `azure-pipelines-api.yml`, set up like `azure-pipelines.yml`:

- **Manual only**, with `trigger: none` and `pr: none` stated explicitly, so it doesn't rely on
  the trigger settings in the Azure DevOps UI.
- **One runtime parameter**, `environment` (`test` default, or `production`). Compile-time
  `${{ if }}` blocks map it to the app and resource group names: `m4d-api-test` or `m4d-api`.
  Each environment's app and resource group share one name.
- **Steps:** install the .NET 10 SDK; `dotnet publish m4d-api/m4d-api.csproj --configuration
  Release`; deploy with `AzureFunctionApp@2`, which supports Flex Consumption; then curl
  `https://<defaultHostName>/v1/health` and fail the run on anything but 200. Get the host name
  with `az resource show`, as `deploy.ps1` does, because `az functionapp show` leaves it empty
  for Flex apps.
- **Service connection:** the existing `m4d-release`.
- **Separate from the web app pipeline.** Being able to deploy the API without touching the web
  app (and the other way round) is a goal of the standalone host (see
  [partner-api-standalone-host § Operations](partner-api-standalone-host.md#operations)). A
  combined "deploy both" option can come later if changes that need both turn out to be common:
  the shared key format (`PartnerKeyFormat.cs`) or index field changes.

## Prerequisites

1. **Grant deploy rights.** The `m4d-release` identity is scoped to the web apps today. Give it
   Website Contributor on the `m4d-api-test` function app, or its resource group. Do test first,
   and do production only after a test run succeeds.
2. **Check whether Flex needs more.** A Flex Consumption deploy goes through the app's deployment
   storage container. Confirm on test whether Website Contributor is enough, or whether the
   deploying identity also needs a Storage Blob data role on the app's storage account. If it
   does, add that role assignment to `main.bicep` (as a parameter, the way `keyWriterUserIds` is
   one), not by hand.
3. **Register the pipeline** in Azure DevOps, pointing at `azure-pipelines-api.yml`.

## Docs to update when it ships

- [ci-cd-and-release](../infrastructure/ci-cd-and-release.md): the second pipeline, and the API's
  place in "From PR to production."
- [runbooks/deploy](../runbooks/deploy.md): the API release steps.
- [partner-api-standalone-host § Operations](partner-api-standalone-host.md#operations): the
  Deployment bullet, or the reference doc it becomes.
- [search-index-breaking-migration](../runbooks/search-index-breaking-migration.md) stays
  pointed at `deploy.ps1`, since the index role grant is part of the infrastructure job.
- `deploy.ps1`'s header: say that code-only releases go through the pipeline, and that
  `-SkipInfrastructure` is the fallback when the pipeline isn't available.

## Alternatives

- **GitHub Actions with OIDC** (federated credentials, no stored secret). It works, but it would
  split deploys across two systems, with the web app in Azure DevOps and the API in GitHub. That's
  only worth it as part of moving the web app's deploy too.
- **Deploy from VS Code** with the Azure Functions extension. It was rejected in #424: it skips
  the health check and deploys the local working tree.

## Related

- [partner-api-standalone-host](partner-api-standalone-host.md)
- [ci-cd-and-release](../infrastructure/ci-cd-and-release.md)
- [runbooks/deploy](../runbooks/deploy.md)
