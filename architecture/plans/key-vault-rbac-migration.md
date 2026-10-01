# Key Vault RBAC Migration

**Type:** Plan
**Status:** Not started. Verified 2026-10-01: `az keyvault show --name music4dance --query properties.enableRbacAuthorization` returned `false`.
**Last verified:** 2026-10-01

Move Key Vault `music4dance` from the legacy access-policy permission model to Azure RBAC.
Today every app identity reads secrets through an **access policy** (Secrets Get/List), and RBAC
role assignments on the vault have no effect. See
[hosting-and-identity § Identity and secrets](../infrastructure/hosting-and-identity.md#identity-and-secrets).

**Re-check status:**

```bash
az keyvault show --name music4dance --query "properties.enableRbacAuthorization"   # false = not migrated
az keyvault show --name music4dance --query "properties.accessPolicies[].objectId"  # remaining policies
az role assignment list --scope $(az keyvault show --name music4dance --query id -o tsv) \
  --query "[].{who:principalName, role:roleDefinitionName}" -o table
```

| `enableRbacAuthorization` | Access policies | "Key Vault Secrets User" assignments | Meaning |
| --- | --- | --- | --- |
| false | present | none | Not started |
| false | present | present | Roles added, not yet switched |
| true | present | — | Switched, old policies not cleaned up |
| true | none | present | Done: fold this into hosting-and-identity and delete this plan |


## Current State

**Key Vault:** music4dance (westus)
**Permission Model:** Vault access policy (legacy), verified 2026-10-01
**Shared By:**

- Production: `msc4dnc` (Linux App Service)
- Test/Staging: `m4d-test` (Linux App Service)

**Why Migrate to RBAC:**

- Modern Azure security model
- Consistent with App Configuration and Search (already using RBAC)
- Better audit trail and access reviews
- Easier to manage at scale
- Required for some advanced Key Vault features

## Migration Strategy: Zero-Downtime Approach

The challenge: Both production and test use the same Key Vault. We can't switch the permission model without affecting both environments simultaneously.

**Solution:** Dual-permission approach during migration

Key Vault supports **both access policies AND RBAC simultaneously** when set to RBAC mode. This allows gradual migration.

## Migration Steps

### Phase 1: Document Current Access Policies (Preparation)

Before making changes, document all existing access policies:

1. Azure Portal → Key Vault "music4dance" → Access policies
2. Document each policy:
   - Principal name (user, app, managed identity)
   - Permissions (Get/List/Set/Delete for Keys, Secrets, Certificates)
   - Purpose/owner

Keep this documentation for audit and rollback purposes.

### Phase 2: Add RBAC Permissions (Non-Breaking)

Add RBAC role assignments WITHOUT changing the permission model yet:

1. Azure Portal → Key Vault "music4dance" → Access control (IAM)
2. Add role assignments for each principal:

**For Web App Managed Identities (msc4dnc, m4d-test):**

- Role: **Key Vault Secrets User** (read-only)
- Scope: This Key Vault

**For Admins/DevOps:**

- Role: **Key Vault Administrator** (full access)
- Scope: This Key Vault

**For CI/CD Service Principals (if any):**

- Role: **Key Vault Secrets Officer** (read/write secrets)
- Scope: This Key Vault

**Common Roles:**

- `Key Vault Reader`: Read metadata only (not secret values)
- `Key Vault Secrets User`: Read secret values
- `Key Vault Secrets Officer`: Read/write secrets
- `Key Vault Administrator`: Full Key Vault management

At this point: Access policies still work, RBAC roles assigned but not active.

### Phase 3: Switch Permission Model (Breaking Change Window)

**Prerequisites:**

- All access policies documented
- Equivalent RBAC roles assigned
- Both production and test environments tested with RBAC in lower environment
- Rollback plan ready
- Maintenance window scheduled (low-traffic time)

**Steps:**

1. **Announce maintenance window** to stakeholders
2. Azure Portal → Key Vault "music4dance" → Settings → **Access configuration**
3. Change Permission model from **"Vault access policy"** to **"Azure role-based access control"**
4. Save/Apply
5. **Immediately test both production and test environments:**
   - Verify app starts successfully
   - Check startup logs for Key Vault access
   - Test OAuth flows (Google, Facebook, Spotify)
   - Verify no authentication errors

**Expected Downtime:** < 5 minutes (time to switch and verify)

### Phase 4: Clean Up Access Policies (Post-Migration)

After successful switch, old access policies are ignored but still visible:

1. Azure Portal → Key Vault → Access policies
2. Document that these are legacy (no longer active)
3. Optionally delete them (they have no effect in RBAC mode)

### Phase 5: Verify and Monitor

**Verification Checklist:**

- ✅ Production (msc4dnc) starts without errors
- ✅ Test (m4d-test) starts without errors
- ✅ All OAuth providers work (Google, Facebook, Amazon, Spotify)
- ✅ App Configuration loads secrets from Key Vault
- ✅ No "Forbidden" errors in logs
- ✅ Admin users can still manage Key Vault secrets

**Monitor for 24-48 hours:**

- Application Insights for errors
- Key Vault diagnostic logs
- User-reported authentication issues

## Rollback Plan

If issues occur after switching to RBAC:

**Quick Rollback (Immediate):**

1. Azure Portal → Key Vault "music4dance" → Access configuration
2. Switch back to **"Vault access policy"**
3. Access policies automatically reactivate
4. Apps work as before

**Time to rollback:** < 2 minutes

**Why This Works:**

- Access policies are not deleted when switching to RBAC
- They're preserved and reactivate when switching back
- Zero data loss

## Testing Plan Before Production Migration

**Recommended:** Test in a separate Key Vault first

1. Create test Key Vault: "music4dance-test"
2. Copy a few test secrets
3. Configure m4d-test to use test Key Vault temporarily
4. Practice migration steps:
   - Add RBAC roles
   - Switch to RBAC mode
   - Verify access works
   - Switch back to access policies
   - Verify access still works
5. Document any issues
6. Schedule production migration

## Migration Timeline

**Estimated Effort:**

- Phase 1 (Document): 30 minutes
- Phase 2 (Add RBAC): 1 hour
- Phase 3 (Switch): 30 minutes + testing
- Phase 4 (Cleanup): 15 minutes
- Phase 5 (Monitor): Ongoing

**Total Active Work:** ~2-3 hours
**Recommended Maintenance Window:** 30 minutes (during Phase 3)

**Suggested Schedule:**

1. **Week 1:** Document current state, add RBAC roles (non-breaking)
2. **Week 2:** Test with test Key Vault if desired
3. **Week 3:** Schedule maintenance window, perform migration
4. **Week 4:** Monitor and verify, clean up access policies

## Post-Migration: Fully RBAC-Based Security

After migration complete, all Azure services use RBAC:

| Service                 | Authentication Method | Permission Model |
| ----------------------- | --------------------- | ---------------- |
| Azure App Configuration | Managed Identity      | RBAC             |
| Azure Cognitive Search  | Managed Identity      | RBAC             |
| Azure Key Vault         | Managed Identity      | RBAC             |
| Azure SQL Database      | Managed Identity      | Azure AD         |
| Azure Communication Svc | Connection String\*   | Access Key       |

\*Future consideration: Azure Communication Services also supports managed identity authentication

## Additional Notes

**App Configuration References:**

- App Configuration stores Key Vault references (not actual secrets)
- Format: `{"uri":"https://music4dance.vault.azure.net/secrets/SecretName"}`
- When app requests config, App Configuration fetches from Key Vault using managed identity
- Requires: App must have permission on BOTH App Configuration AND Key Vault

**Current Implementation:**

- ✅ App has RBAC on App Configuration
- ✅ App has Access Policy on Key Vault (working)
- 🔄 Future: App will have RBAC on Key Vault (after migration)

**No Code Changes Required:**

- The code already uses `DefaultAzureCredential`
- Switching Key Vault to RBAC is purely Azure configuration
- App behavior unchanged
