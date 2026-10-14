# Renew the AutoMapper License Key

**Type:** Runbook
**Status:** Current
**Last verified:** 2026-10-14
**Code:** `m4d/Services/LicenseKeyCredential.cs`, `m4d/Configuration/M4dApplicationExtensions.cs` (`AddAutoMapper`)

## When to use

AutoMapper 15+ needs a commercial license key from Lucky Penny Software, and the key lasts a
year. Starting 30 days before it expires, `LicenseKeyCredential` (checked on every songstats
recompute) sends an **Action Needed: Renew the AutoMapper license key** admin email once a day.
An expired key shows up in the production logs as `LuckyPennySoftware.AutoMapper.License`
errors at each startup. AutoMapper keeps working, but it's out of license.

The current key expires **2027-10-14**.

## Prerequisites / access

- The Lucky Penny Software account that holds the license.
- Write access to the Key Vault secret that App Configuration's `Authentication:AutoMapper:Key`
  references, and permission to restart the App Services.

## Steps

1. Renew at Lucky Penny Software and copy the new key. It's a JWT whose `exp` claim is the new
   expiry date.
2. **Production and staging:** in the `music4dance` App Configuration store, open
   Configuration explorer → `Authentication:AutoMapper:Key` (unlabeled). It's a Key Vault
   reference, so follow it to the secret and add a **new version** with the new key. Don't
   edit the App Configuration entry itself.
3. Restart each App Service. AutoMapper reads the key only at startup, so neither the
   App Configuration sentinel nor Key Vault refresh will apply it.
4. **Local development:** `dotnet user-secrets set "Authentication:AutoMapper:Key" "<key>" --project m4d`.

## Verification

- `/Admin/Diagnostics` → **Expiring Credentials** shows the new date for the AutoMapper license
  key.
- No `LuckyPennySoftware.AutoMapper.License` errors in the next log pull (see
  [read-production-logs](read-production-logs.md)).
- The daily Action Needed email stops after the next songstats recompute. The diagnostics page
  and the email read the key live from configuration, so they can show the new date before
  the restart.

## Rollback

Add another secret version holding the previous key (it's still in the secret's version
history), then restart.

## Related

- [configuration-and-feature-flags](../infrastructure/configuration-and-feature-flags.md#secrets-and-third-party-credentials)
- [service-resilience § Admin notifications](../infrastructure/service-resilience.md#admin-notifications)
