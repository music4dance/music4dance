# Troubleshooting: Managed Identity Setup for Phase 1

## Issue Encountered

After deploying Phase 1 changes with self-contained mode, the application crashed with:

```
System.InvalidOperationException: Unable to find the required services. Please add all the required services by calling 'IServiceCollection.AddAzureAppConfiguration()'
```

**Root Cause:** Managed Identity was not properly enabled in Azure, AND there was a code bug that didn't handle the failure case properly.

## Problems Identified

### Problem 1: Managed Identity Not Enabled (Primary Issue)

The logs showed:

```
ManagedIdentityCredential authentication unavailable. No response received from the managed identity endpoint.
```

This means **System-assigned Managed Identity was not enabled** in the Azure Web App.

### Problem 2: Code Bug (Fixed)

When App Configuration failed to connect, the code didn't register the Azure App Configuration service, but later tried to use it via `app.UseAzureAppConfiguration()`, causing a crash.

**Fix Applied:** Now checks if the service is available before calling the middleware:

```csharp
if (serviceHealth.IsServiceAvailable("AppConfiguration"))
{
    _ = app.UseAzureAppConfiguration();
}
```

## Resolution Steps

### Step 1: Enable Managed Identity in Azure Portal

**For Test Environment (m4d-linux):**

1. Go to Azure Portal → App Services → `m4d-linux`
2. Settings → **Identity**
3. **System assigned** tab
4. Status: Set to **On** (currently it's OFF or not responding)
5. Click **Save**
6. **Copy the Object (principal) ID** that appears

**For Production (msc4dnc):**

- Follow same steps if deploying Phase 1 to production

### Step 2: Grant RBAC Permissions

After enabling managed identity, grant permissions:

#### Azure App Configuration Permissions

1. Go to Azure Portal → **Azure App Configuration** resource (`music4dance`)
2. Access control (IAM) → Role assignments
3. Click "+ Add" → "Add role assignment"
4. **Role**: Select `App Configuration Data Reader`
5. **Members**:
   - Click "Select members"
   - Change filter to "Managed identity"
   - Select your web app (`m4d-linux` or `msc4dnc`)
6. Review + assign

#### Azure Cognitive Search Permissions

1. Go to Azure Portal → **Azure Cognitive Search** service
2. Access control (IAM) → Role assignments
3. Click "+ Add" → "Add role assignment"
4. **Role**: Select `Search Index Data Reader`
5. **Members**:
   - Click "Select members"
   - Change filter to "Managed identity"
   - Select your web app (`m4d-linux` or `msc4dnc`)
6. Review + assign

### Step 3: Verify Settings

In Azure Portal → Web App → Configuration → Application Settings, verify:

✅ **Required:**

- `SELF_CONTAINED_DEPLOYMENT` = `true`
- `ASPNETCORE_ENVIRONMENT` = `Staging` or `Production` (not Development)
- `AppConfig:Endpoint` = `https://music4dance.azconfig.io`

❌ **Remove if present (no longer needed):**

- `AppConfig__ConnectionString`
- `AzureSearch__ApiKey`

### Step 4: Verify Startup Command

Configuration → General settings:

- **Startup Command**: `/home/site/wwwroot/m4d`

### Step 5: Redeploy

After making Azure configuration changes, redeploy the application.

## Expected Startup Logs (Success)

When properly configured, you should see:

```
Environment: Staging (or Production)
SELF_CONTAINED_DEPLOYMENT flag: True
Running in self-contained mode
Production environment detected. Deployment mode: self-contained
Configuring Azure App Configuration with managed identity
AppConfig:Endpoint = https://music4dance.azconfig.io
DefaultAzureCredential created successfully for App Configuration
Attempting to connect to App Configuration...
Azure App Configuration configured successfully with managed identity
Configuring Azure Search with managed identity
Creating DefaultAzureCredential for Azure Search clients
DefaultAzureCredential created successfully for Azure Search
Azure Search clients configured successfully with managed identity
Azure App Configuration middleware enabled
```

## Common Mistakes to Avoid

### ❌ Mistake 1: Leaving Managed Identity Disabled

**Old documentation said to disable it for self-contained mode - this is now WRONG!**

Both deployment modes need managed identity **ENABLED**.

### ❌ Mistake 2: Not Waiting for Identity Propagation

After enabling managed identity, wait 1-2 minutes for Azure AD to propagate the identity before granting RBAC permissions.

### ❌ Mistake 3: Granting Permissions to Wrong Resource

Make sure you're granting permissions on the App Configuration and Search resources, not the web app itself.

### ❌ Mistake 4: Using Wrong Role

- App Configuration needs `App Configuration Data Reader` (not Owner, not Contributor)
- Search needs `Search Index Data Reader` (not Search Service Contributor)

## Verification Checklist

Before deploying, verify:

- [ ] System-assigned Managed Identity is **Enabled** (On)
- [ ] Managed Identity has `App Configuration Data Reader` role on App Configuration
- [ ] Managed Identity has `Search Index Data Reader` role on Search service
- [ ] `AppConfig__ConnectionString` removed from Application Settings
- [ ] `AzureSearch__ApiKey` removed from Application Settings
- [ ] `SELF_CONTAINED_DEPLOYMENT` set to `true`
- [ ] Startup command set to `/home/site/wwwroot/m4d`
- [ ] Latest code deployed (with the bug fix)

## Testing Managed Identity

To test if managed identity is working, you can use Azure CLI from the web app's SSH/Kudu console:

```bash
# Get access token using managed identity
curl 'http://169.254.169.254/metadata/identity/oauth2/token?api-version=2018-02-01&resource=https://management.azure.com/' -H Metadata:true
```

If managed identity is working, you'll get a JSON response with an access token. If not, you'll get a connection error.

## About .NET 10 References

**Note:** The documentation mentions .NET 10, but your project actually targets `net9.0` (.NET 9). The references to .NET 10 in documentation are outdated and should be updated to .NET 9.

Self-contained deployment bundles the .NET 9 runtime, so the host OS doesn't need .NET installed.

## Advanced Troubleshooting: Diagnostic Endpoints

### Created Diagnostic Controller

A diagnostic controller has been added at `m4d/APIControllers/DiagnosticsController.cs` to test managed identity and Key Vault access directly from the running application.

**⚠️ IMPORTANT:** Remove this controller after troubleshooting - it exposes diagnostic information.

### Available Endpoints

#### 1. Test Credential Chain

**URL:** `https://m4d-linux.azurewebsites.net/api/diagnostics/test-credential`

**Purpose:** Shows what environment variables are available and which credential method DefaultAzureCredential will use.

**What it shows:**

- All methods DefaultAzureCredential tries (Environment, Workload Identity, Managed Identity, etc.)
- Relevant environment variables (MSI_ENDPOINT, IDENTITY_ENDPOINT, etc.)
- Whether managed identity environment variables are present

**Expected output (if managed identity is working):**

```
3. ManagedIdentityCredential ⭐ (This should work for Azure App Service)
   MSI_ENDPOINT: (not set)
   MSI_SECRET: ***SET***
   IDENTITY_ENDPOINT: http://...
   IDENTITY_HEADER: ***SET***
```

#### 2. Test Key Vault Access

**URL:** `https://m4d-linux.azurewebsites.net/api/diagnostics/test-keyvault`

**Purpose:** Actually attempts to read a Key Vault secret using the managed identity, showing exactly where the failure occurs.

**Success response:**

```
=== Key Vault Managed Identity Test ===

Key Vault URI: https://music4dance.vault.azure.net/
Secret Name: Authentication--Amazon--ClientId

✓ DefaultAzureCredential created
✓ Access token obtained successfully
  Token expires: 2025-12-27 16:30:00Z

✓ SecretClient created

Attempting to read secret 'Authentication--Amazon--ClientId'...
✓ SECRET READ SUCCESSFULLY!
  Secret Name: Authentication--Amazon--ClientId
  Secret Value: amzn1.app... (truncated)
  Content Type: (empty)
  Enabled: True
  Created: 2023-05-15 10:23:45Z
  Updated: 2024-11-20 14:32:10Z

=== TEST PASSED ===
Managed identity has correct permissions to read Key Vault secrets.
```

**Failure response (403 - No permission):**

```
✗ PERMISSION DENIED (403 Forbidden)
  Error Code: Forbidden
  Message: The user, group or application ... does not have secrets get permission

=== TEST FAILED ===
The managed identity does NOT have permission to read Key Vault secrets.

Required actions:
1. Verify managed identity is enabled for this app
2. Grant 'Key Vault Secrets User' role to the managed identity on the Key Vault
3. Wait 5-10 minutes for Azure AD propagation
4. Check if Key Vault uses Access Policies instead of RBAC
```

**Failure response (Managed identity not available):**

```
✗ MANAGED IDENTITY NOT AVAILABLE
  Message: ManagedIdentityCredential authentication unavailable

=== TEST FAILED ===
Managed identity is not enabled or not working.

Required actions:
1. Enable System-assigned managed identity in App Service settings
2. Verify SELF_CONTAINED_DEPLOYMENT environment variable is set correctly
```

### How to Use

1. **Deploy the application** with the diagnostic controller included
2. **Navigate to the test-credential endpoint** first to verify managed identity environment is available
3. **Navigate to the test-keyvault endpoint** to test actual Key Vault access
4. **Read the detailed output** - it will tell you exactly what's failing
5. **Follow the "Required actions"** shown in the error message

### If RBAC Doesn't Work: Switch to Access Policies

If the diagnostic endpoint shows "PERMISSION DENIED" even after granting RBAC roles and waiting for propagation, your Key Vault may be using the legacy **Access Policies** permission model instead of RBAC.

**To check the permission model:**

1. Azure Portal → Key Vault `music4dance` → **Settings** → **Access configuration**
2. Look at "Permission model" - is it "Azure role-based access control" or "Vault access policy"?

**To switch to Access Policies (if RBAC isn't working):**

1. Azure Portal → Key Vault `music4dance` → **Access policies** (left menu)
2. Click **"+ Create"**
3. **Permissions** tab:
   - Secret permissions: Select **Get** and **List**
   - Certificate permissions: None
   - Key permissions: None
   - Click **Next**
4. **Principal** tab:
   - Search for and select **m4d-linux** (or **msc4dnc** for production)
   - Click **Next**
5. **Application (optional)** tab:
   - Skip this
   - Click **Next**
6. **Review + create** → Click **Create**

**Note:** Access Policies provide the same functionality as RBAC roles but use a different permission model. If your production environment (`msc4dnc`) only has "Key Vault Reader" RBAC role and works, it likely has an Access Policy configured instead.

### Production Comparison

**To check how production is configured:**

1. Azure Portal → Key Vault `music4dance` → **Access policies**
2. Look for `msc4dnc` in the list
3. If present with "Get" permission under Secret permissions, that's why production works without the RBAC role

You can use the same Access Policy approach for `m4d-linux` if RBAC continues to cause issues.

### Cleanup After Testing

**Once troubleshooting is complete:**

1. Delete `m4d/APIControllers/DiagnosticsController.cs`
2. Rebuild and redeploy without the diagnostic endpoints
3. These endpoints expose sensitive diagnostic information and should not remain in production

## Next Steps

1. **Fix the Azure configuration** (enable managed identity + grant permissions)
2. **Redeploy** the updated code
3. **Monitor startup logs** to verify success
4. If successful, document the working configuration
5. Consider deploying to production once stable in test

## Rollback Option

If you need to revert to the old method (connection strings/API keys):

1. Revert code changes: `git revert <commit-hash>`
2. Add back to Application Settings:
   - `AppConfig__ConnectionString`
   - `AzureSearch__ApiKey`
3. Disable managed identity (optional)
4. Redeploy

But **this is not recommended** - fixing managed identity is the better path forward.

---

**Date:** December 27, 2025
**Issue:** Phase 1 deployment failure - multiple issues identified
**Problems:**

1. Managed identity not enabled (fixed)
2. Code crash when App Configuration unavailable (fixed - added conditional check)
3. Key Vault permissions missing (RBAC roles granted but still failing - investigating)
4. Static assets manifest missing (fixed - use UseStaticFiles() for self-contained mode)

**Status:** Key Vault access investigation paused - diagnostic endpoints created for detailed testing
**Created:** DiagnosticsController with test-credential and test-keyvault endpoints
**Next:** Deploy and test diagnostic endpoints to determine if RBAC vs Access Policies issue
