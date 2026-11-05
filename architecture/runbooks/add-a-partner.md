# Add a Partner API Client

**Type:** Runbook
**Status:** Current (test environment; production follows once the production API is deployed)
**Last verified:** 2026-11-05
**Code:** `m4d/Controllers/PartnerKeysController.cs`, `m4d/PublicApi/PartnerKeyService.cs`, `m4d-api/PartnerGate.cs`, `m4d-api/infra/new-key.ps1`

## When to use

A partner is going to call the partner API (`POST /v1/songs/resolve`) and needs a client record
and an API key. It also covers rotating and revoking a partner's keys, and the one-time setup
each web app needs before its pages can issue keys.

For why it works this way (key format, the partner-keys vault, limits), see
[partner-api-standalone-host](../plans/partner-api-standalone-host.md) § Authentication and
§ Metering.

| Environment | Web app (issues keys) | API | Partner-keys vault | Key prefix |
| --- | --- | --- | --- | --- |
| test | `m4d-test` (App Configuration label `Staging`) | `https://m4d-api-test.azurewebsites.net` | `m4d-api-keys-test` | `m4d_test_` |
| production | `msc4dnc` (label `Production`) | Not deployed yet (plan PR 6) | `m4d-api-keys` | `m4d_live_` |

Test and production are separate: a test key works only against the test API, and the
partner's developer needs an account on each site.

## Prerequisites / access

- `dbAdmin` on the web app the partner will use.
- For the one-time setup: write access to App Configuration store `music4dance`.
- For the manual fallback only: Key Vault Secrets Officer on the partner-keys vault.
  `m4d-api/infra/deploy.ps1` grants it to whoever deployed the environment.
- From the partner: a client id you agree on, and the music4dance user name or email of each
  developer who will manage keys.

## Steps

### 0. One-time setup per web app

Skip this if the web app's `/PartnerKeys/Clients` page doesn't show the notice that
`PartnerApi:KeyVaultUri` isn't set.

1. Set the two settings in App Configuration under the web app's label:

   ```bash
   # test
   az appconfig kv set -n music4dance --label Staging --key PartnerApi:KeyVaultUri --value https://m4d-api-keys-test.vault.azure.net/ --yes
   az appconfig kv set -n music4dance --label Staging --key PartnerApi:Environment --value test --yes
   # production
   az appconfig kv set -n music4dance --label Production --key PartnerApi:KeyVaultUri --value https://m4d-api-keys.vault.azure.net/ --yes
   az appconfig kv set -n music4dance --label Production --key PartnerApi:Environment --value live --yes
   ```

2. Restart the web app (`az webapp restart -n m4d-test -g m4d-Web`). The vault client is created
   at startup, so a sentinel refresh doesn't pick these up.
3. Check that the web app's managed identity has Key Vault Secrets Officer on the vault. The API's
   Bicep grants it, so this only fails if the identity was recreated:

   ```bash
   PID=$(az webapp identity show -n m4d-test -g m4d-Web --query principalId -o tsv)
   MSYS_NO_PATHCONV=1 az role assignment list --assignee $PID --all --query "[?contains(scope,'m4d-api-keys')].roleDefinitionName" -o tsv
   ```

### 1. Create the client

1. Sign in to the web app as a `dbAdmin` and open **Admin → Partner API Clients**
   (`/PartnerKeys/Clients`).
2. Under **Add a client**, enter:
   - **Client id**: 2 to 40 lowercase letters, digits and dashes, e.g. `acme`. It's
     permanent, because it names the vault secret and the usage rows, and it appears in logs.
   - **Name**: the partner's display name.
   - **Requests/second**, **Burst**, **Daily cap**: leave blank for the API defaults (20/s,
     burst 50, 100,000 a day). Set lower values for a trial partner.
3. Save. The client's limits are written to the vault as `client-{clientId}`.

### 2. Link the partner's developer

1. Have the developer register on the same site, if they haven't.
2. On the client, enter their user name or email under **Developers** and select
   **Add developer**. This grants the `partnerDeveloper` role.

### 3. The developer generates a key

The developer, not you, should generate the key, so it goes straight to them:

1. They sign in, open **Partner API keys** from their account pages (`/PartnerKeys`), enter a
   label (e.g. `production backend`) and select **Generate a new key**.
2. They copy the key from the page. It's shown once and isn't stored anywhere.
3. The API accepts it within 5 minutes, when the API next refreshes its keys.

An admin can also generate a key on `/PartnerKeys/Clients`, e.g. for a first test. Hand it over
through a channel you'd trust with a password, never plain email.

### 4. Send the partner what they need

- The API base URL for the environment (table above).
- How to send the key: `Authorization: Bearer <key>`. A key in the URL is refused with `400`.
- Their limits, and that `429` comes with `Retry-After`.

## Verification

1. With the key in an environment variable (so it doesn't land in shell history):

   ```bash
   curl -s -X POST https://m4d-api-test.azurewebsites.net/v1/songs/resolve \
     -H "Authorization: Bearer $PARTNER_KEY" -H "Content-Type: application/json" \
     -d '{"title":"Moon River","artist":"Andy Williams"}'
   ```

   A `200` with a `match` field means the key works. `401` within 5 minutes of generating
   it is the refresh delay; try again.
2. The vault has the key, tagged with the client:

   ```bash
   az keyvault secret list --vault-name m4d-api-keys-test --query "[?tags.client=='acme'].{name:name,enabled:attributes.enabled}" -o table
   ```

3. In the API's Application Insights, the next requests log `Resolved … for acme`.

## Rotating a key

At most two keys per client are active. The developer generates a new key, deploys it, then
revokes the old one on `/PartnerKeys`.

## Revoking a key or suspending a client

- **One key:** **Revoke** on `/PartnerKeys` (the developer) or `/PartnerKeys/Clients` (an admin).
  This disables the vault secret, and the API refuses the key within 5 minutes.
- **The whole client:** check **Disabled** on the client and save. The API answers `403` within
  5 minutes. Clear it to restore access; the keys are unchanged.
- **Removing a developer:** **remove** next to their name. Keys they issued stay active, so
  revoke them too if they're leaving.

## Rollback

Everything above is undone from the same pages: revoke the keys and disable the client. Client
ids can't be deleted from the pages, and the vault keeps disabled secrets, so pick the id with
care.

## Manual fallback

If the web app is down or isn't connected to the vault, issue and revoke keys straight in the
vault. The key is written to a file, not the terminal:

```powershell
./m4d-api/infra/new-key.ps1 -Environment test -ClientId acme -Name 'Acme' -OutFile local/acme-key.txt
./m4d-api/infra/new-key.ps1 -Environment test -List
./m4d-api/infra/new-key.ps1 -Environment test -Revoke <keyId>
```

Keys issued this way don't appear on the web app's pages, which list only what's in SQL.

## Related

- [partner-api-standalone-host](../plans/partner-api-standalone-host.md): the design, limits and failure modes
- [deploy](deploy.md): deploying the web app
