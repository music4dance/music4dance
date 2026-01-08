# Managed Identity Support for Self-Contained Deployments

## Overview

This document outlines the implementation of managed identity authentication for Azure services in self-contained deployments, eliminating the need for connection strings and API keys while improving security posture.

**Status:** ✅ **Phase 1 Complete** - Managed identity unified for both deployment modes. Deployment pipeline automatically configures deployment mode settings.

## Current State

### Authentication by Deployment Mode

**Framework-Dependent Mode:**

- Azure App Configuration: Managed Identity (DefaultAzureCredential)
- Azure Cognitive Search: Managed Identity (DefaultAzureCredential)
- SQL Server: Connection string with SQL authentication

**Self-Contained Mode:**

- Azure App Configuration: Connection string (access key)
- Azure Cognitive Search: API key authentication
- SQL Server: Connection string with SQL authentication

### The Problem

The current implementation explicitly branches on `isSelfContained` flag and switches to key-based authentication for App Configuration and Search in self-contained mode. This was implemented as a workaround based on the assumption that managed identity doesn't work in self-contained deployments.

**Reality:** Managed identity DOES work in self-contained deployments when:

1. The Azure.Identity library is bundled (already included via self-contained publish)
2. System-assigned Managed Identity is enabled in Azure Web App
3. Appropriate RBAC permissions are granted

The current approach requires:

- Storing secrets in configuration (AppConfig**ConnectionString, AzureSearch**ApiKey)
- Manual secret rotation
- More complex deployment configuration
- Disabling managed identity in Azure Portal (per current docs)

## Proposed Solution: Two-Phase Migration to Managed Identity

### Phase 1: App Configuration & Search (Simple)

Unify authentication logic to use `DefaultAzureCredential` for both deployment modes.

**Services in Scope:**

- Azure App Configuration
- Azure Cognitive Search (6 search indexes)

**Benefits:**

- ✅ No secrets in configuration
- ✅ Automatic credential rotation
- ✅ Simpler code (remove branching logic)
- ✅ Better security posture
- ✅ Same authentication flow for both deployment modes

**Requirements:**

- System-assigned Managed Identity enabled in Azure Web App
- RBAC role assignments for managed identity:
  - App Configuration Data Reader
  - Search Index Data Reader
- Remove AppConfig**ConnectionString and AzureSearch**ApiKey from configuration

### Phase 2: SQL Server (Complex)

Migrate SQL Server authentication from SQL authentication to Azure AD authentication with managed identity.

**Why Separate Phase:**

- More complex implementation
- Requires SQL Server configuration changes
- Different error handling patterns
- Entity Framework access token provider needed
- Database permission management required

**Benefits:**

- ✅ Complete elimination of passwords
- ✅ Centralized access management via Azure AD
- ✅ Audit trail of database access
- ✅ No connection string secrets

## Pipeline Automation (December 2025)

### Automatic Deployment Mode Configuration

The deployment pipeline (`azure-pipelines.yml`) now automatically configures all deployment mode settings, eliminating the need for manual Azure Portal configuration.

**What the Pipeline Automates:**

1. **SELF_CONTAINED_DEPLOYMENT Environment Variable**

   - Automatically set to `true` for self-contained deployments
   - Automatically set to `false` for framework-dependent deployments
   - Ensures environment variable always matches the deployment mode

2. **Startup Command Configuration**
   - Sets `/home/site/wwwroot/m4d` for self-contained deployments
   - Clears startup command for framework-dependent deployments

**Benefits:**

- ✅ No manual Azure Portal configuration required
- ✅ Deployment mode and configuration always in sync
- ✅ Eliminates configuration drift between environments
- ✅ Single source of truth (pipeline parameter)
- ✅ Prevents errors from mismatched settings

**How It Works:**

The pipeline uses an Azure CLI task after deployment:

```yaml
- task: AzureCLI@2
  displayName: "Configure app settings for deployment mode"
  inputs:
    inlineScript: |
      # Set SELF_CONTAINED_DEPLOYMENT environment variable
      az webapp config appsettings set \
        --name $(appName) \
        --settings SELF_CONTAINED_DEPLOYMENT=$(useSelfContained)

      # Configure startup command based on deployment mode
      if [ "$(useSelfContained)" = "true" ]; then
        az webapp config set --startup-file "/home/site/wwwroot/m4d"
      else
        az webapp config set --startup-file ""
      fi
```

**User Experience:**

Before:

1. Set pipeline parameter: `deploymentMode: self-contained`
2. Run pipeline
3. Go to Azure Portal
4. Set `SELF_CONTAINED_DEPLOYMENT=true` in Application Settings
5. Set startup command in General Settings

After:

1. Set pipeline parameter: `deploymentMode: self-contained`
2. Run pipeline ✅ (everything else is automatic)

## Phase 1 Implementation Details

### Code Changes Required

#### 1. Program.cs - Remove Self-Contained Branching

**Lines 130-176: App Configuration Section**

Current logic:

```csharp
if (isSelfContained)
{
    // Use connection string
    var appConfigConnectionString = configuration["AppConfig:ConnectionString"];
    options.Connect(appConfigConnectionString)
}
else
{
    // Use managed identity
    var credentials = new DefaultAzureCredential();
    options.Connect(new Uri(appConfigEndpoint), credentials)
}
```

New logic (unified):

```csharp
// Use managed identity for both modes
var appConfigEndpoint = configuration["AppConfig:Endpoint"];
if (!string.IsNullOrEmpty(appConfigEndpoint))
{
    var credentials = new DefaultAzureCredential();
    options.Connect(new Uri(appConfigEndpoint), credentials)
        .ConfigureKeyVault(kv => kv.SetCredential(credentials))
        // ... rest of configuration
}
```

**Lines 255-386: Azure Search Section**

Current logic:

```csharp
if (isSelfContained)
{
    // Use API key
    var searchApiKey = configuration["AzureSearch:ApiKey"];
    var keyCredential = new Azure.AzureKeyCredential(searchApiKey);
    // Register clients with key credential
}
else
{
    // Use managed identity
    services.AddAzureClients(clientBuilder =>
    {
        var credentials = new DefaultAzureCredential();
        clientBuilder.UseCredential(credentials);
        // Register clients
    });
}
```

New logic (unified):

```csharp
// Use managed identity for both modes
services.AddAzureClients(clientBuilder =>
{
    var credentials = new DefaultAzureCredential();
    clientBuilder.UseCredential(credentials);

    foreach (var section in indexSections)
    {
        clientBuilder.AddSearchClient(section).WithName(section.Key);
    }
    // ... rest of client registration
});
```

#### 2. Update Error Messages

Update console logging to remove references to "self-contained mode" and "framework-dependent mode" for these services.

### Azure Configuration Changes

#### Enable Managed Identity (if disabled)

1. Azure Portal → Web App (msc4dnc or m4d-linux)
2. Settings → Identity
3. System assigned → Status: **On**
4. Save and copy Object (principal) ID

#### Grant RBAC Permissions

**Azure App Configuration:**

1. Azure Portal → Azure App Configuration resource
2. Access control (IAM) → Add role assignment
3. Role: **App Configuration Data Reader**
4. Members: Select managed identity → Select web app
5. Review + assign

**Azure Cognitive Search:**

1. Azure Portal → Azure Cognitive Search resource
2. Access control (IAM) → Add role assignment
3. Role: **Search Index Data Reader**
4. Members: Select managed identity → Select web app
5. Review + assign

#### Grant Key Vault Permissions

**Current State (December 2025):** Key Vault "music4dance" uses **Vault access policy** permission model (legacy). Production (msc4dnc) and test (m4d-linux) instances both share this Key Vault.

**Immediate Solution - Add Access Policy (Works Now):**

1. Azure Portal → Key Vault "music4dance"
2. Settings → Access policies → Create
3. Permissions tab:
   - Secret permissions: **Get**, **List**
4. Principal tab:
   - Search for and select: **m4d-linux** (or **msc4dnc** for production)
5. Review + create → Create

Repeat for each web app managed identity that needs access.

**Long-term Solution - Migrate to RBAC:** See "Key Vault RBAC Migration Plan" section below.

#### Remove Secrets from Configuration

**Azure Web App → Configuration → Application Settings:**

Remove these settings (no longer needed):

- `AppConfig__ConnectionString`
- `AzureSearch__ApiKey`

Keep these settings:

- `SELF_CONTAINED_DEPLOYMENT` (still controls Kestrel/port configuration)
- All other configuration values

### Deployment Documentation Updates

Update [SELF_CONTAINED_DEPLOYMENT.md](../SELF_CONTAINED_DEPLOYMENT.md):

**Section: "For Self-Contained Deployments"**

Remove from required settings:

- ~~AppConfig\_\_ConnectionString~~
- ~~AzureSearch\_\_ApiKey~~

Remove instruction to disable managed identity:

- ~~**Disable Managed Identity** (required for self-contained)~~

Add note about unified authentication:

- Both deployment modes now use managed identity
- Ensure System-assigned Managed Identity is **enabled**
- Grant RBAC permissions as documented

### Testing Strategy

#### Local Development Testing

Local development continues to work with:

- Azure CLI authentication (az login)
- Visual Studio authentication
- Development Service Principal (if configured)

DefaultAzureCredential automatically falls back to these methods.

#### Azure Testing - Self-Contained Mode

1. Deploy to test environment (m4d-linux) with self-contained mode
2. Verify startup logs show:
   - "Using managed identity (DefaultAzureCredential) for App Configuration"
   - "DefaultAzureCredential created successfully"
   - "Azure App Configuration added successfully with managed identity"
   - "Azure Search clients configured successfully with managed identity"
3. Verify App Configuration features work (feature flags, configuration refresh)
4. Verify Search functionality works (song search, page search)
5. Monitor for authentication errors

#### Azure Testing - Framework-Dependent Mode

1. Deploy to test or production with framework-dependent mode
2. Verify identical startup logs (no mode-specific differences)
3. Verify all services continue to function

### Rollback Plan

If Phase 1 encounters issues:

1. Revert code changes in Program.cs (git revert)
2. Re-add application settings:
   - `AppConfig__ConnectionString`
   - `AzureSearch__ApiKey`
3. Disable managed identity (if causing issues)
4. Redeploy previous working version

Settings can be kept in Azure App Configuration or Key Vault for quick restoration.

## Phase 2 Implementation Details (SQL Server)

### Overview

Phase 2 migrates SQL Server authentication from SQL authentication (username/password) to Azure AD authentication with managed identity.

### Prerequisites

- Phase 1 completed successfully
- Azure SQL Server configured for Azure AD authentication
- Admin access to SQL Server to grant permissions

### Technical Challenges

1. **Entity Framework Integration**: Need to configure access token provider
2. **Connection String Format**: Different format for managed identity
3. **SQL Permissions**: Granular permission management
4. **Migration Testing**: Ensure no data access issues
5. **Local Development**: Different auth for local vs. cloud

### Code Changes Required

#### 1. Add Azure.Identity Support for SQL

Install additional package if needed:

```xml
<PackageReference Include="Microsoft.Data.SqlClient" />
```

#### 2. Configure SQL Connection with Access Token

```csharp
services.AddDbContext<DanceMusicContext>(options =>
{
    var connectionString = configuration.GetConnectionString("DanceMusicContextConnection");

    if (environment.IsProduction() && !useLocalDatabase)
    {
        // Production: Use managed identity
        options.UseSqlServer(connectionString, sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure();

            // Configure access token provider
            var credential = new DefaultAzureCredential();
            sqlOptions.AccessToken(async () =>
            {
                var token = await credential.GetTokenAsync(
                    new Azure.Core.TokenRequestContext(
                        new[] { "https://database.windows.net/.default" }));
                return token.Token;
            });
        });
    }
    else
    {
        // Development: Use connection string authentication
        options.UseSqlServer(connectionString);
    }
});
```

#### 3. Update Connection String Format

**Current (SQL Auth):**

```
Server=tcp:server.database.windows.net,1433;Database=dbname;User ID=username;Password=password;
```

**New (Managed Identity):**

```
Server=tcp:server.database.windows.net,1433;Database=dbname;
```

Store both in configuration:

- `ConnectionStrings:DanceMusicContextConnection` (local/development)
- `ConnectionStrings:DanceMusicContextConnectionProduction` (managed identity)

### Azure SQL Configuration

#### 1. Enable Azure AD Authentication

1. Azure Portal → SQL Server
2. Settings → Azure Active Directory admin
3. Set Azure AD admin
4. Save

#### 2. Grant Managed Identity Access

Connect to SQL database with Azure AD admin credentials:

```sql
-- Create user from managed identity
CREATE USER [your-web-app-name] FROM EXTERNAL PROVIDER;

-- Grant necessary permissions
ALTER ROLE db_datareader ADD MEMBER [your-web-app-name];
ALTER ROLE db_datawriter ADD MEMBER [your-web-app-name];
ALTER ROLE db_ddladmin ADD MEMBER [your-web-app-name]; -- For migrations

-- Grant specific permissions if needed
GRANT EXECUTE TO [your-web-app-name];
GRANT VIEW DEFINITION TO [your-web-app-name];
```

#### 3. Verify Connection

Test with Azure CLI:

```bash
az sql db show-connection-string --client ado.net --name dbname --server servername
```

#### 4. Troubleshooting: Object ID Mismatch Issue (December 2025)

**Problem Encountered with m4d-linux (Test Instance):**

When attempting to grant SQL database access to the m4d-linux managed identity, the standard `CREATE USER [m4d-linux] FROM EXTERNAL PROVIDER` command creates a user, but with an **incorrect Object ID** that doesn't match the web app's managed identity.

**Connection String Format (Working for msc4dnc, Failing for m4d-linux):**

```
Data Source=n8a541qjnq.database.windows.net,1433;Initial Catalog=music4dance_test;Authentication=ActiveDirectoryManagedIdentity
```

**Steps Attempted:**

1. ✅ Verified Azure AD admin is set on SQL Server (Microsoft Entra admin)
2. ✅ Created user with `CREATE USER [m4d-linux] FROM EXTERNAL PROVIDER`
3. ✅ Granted roles: db_datareader, db_datawriter, db_ddladmin
4. ❌ Verified Object ID - **SQL user has different Object ID than web app's managed identity**
5. ❌ Dropped and recreated user - **still creates with wrong Object ID**
6. ✅ Confirmed managed identity is enabled on m4d-linux web app
7. ✅ Confirmed Key Vault access policy was successfully added and working
8. ❌ Connection still fails: `Login failed for user '<token-identified principal>'`

**Verification Queries Used:**

```sql
-- Check Object ID in SQL
SELECT
    name,
    type_desc,
    CAST(sid AS uniqueidentifier) AS azure_object_id
FROM sys.database_principals
WHERE name = 'm4d-linux'
AND type_desc = 'EXTERNAL_USER';

-- Compare roles between working (msc4dnc) and non-working (m4d-linux)
SELECT
    dp.name AS principal_name,
    dp.type_desc,
    drole.name AS role_name
FROM sys.database_principals dp
LEFT JOIN sys.database_role_members drm ON dp.principal_id = drm.member_principal_id
LEFT JOIN sys.database_principals drole ON drm.role_principal_id = drole.principal_id
WHERE dp.name IN ('msc4dnc', 'm4d-linux')
ORDER BY dp.name;
```

**Root Cause Hypothesis:**

When SQL Server executes `CREATE USER [m4d-linux] FROM EXTERNAL PROVIDER`, it queries Azure AD by name and may be finding a **different principal** with the same name (old service principal, deleted identity, or name collision). The `FROM EXTERNAL PROVIDER` clause doesn't allow specifying Object ID explicitly.

**Potential Solutions to Try:**

1. **Use Application ID instead of name** (if m4d-linux has one):

   ```sql
   CREATE USER [m4d-linux] FROM EXTERNAL PROVIDER WITH OBJECT_ID = 'actual-object-id-guid';
   ```

   _(Note: This syntax may not be supported in all SQL versions)_

2. **Check for name conflicts in Azure AD:**

   - Azure Portal → Azure Active Directory → Enterprise applications
   - Search for "m4d-linux" - are there multiple results?
   - Check for deleted/orphaned managed identities

3. **Create user with SID directly** (bypassing name lookup):

   ```sql
   -- This is complex and may not work for managed identities
   DECLARE @objectId UNIQUEIDENTIFIER = 'YOUR-OBJECT-ID-HERE';
   DECLARE @sid VARBINARY(85) = CAST(@objectId AS VARBINARY(16));
   EXEC sp_addrolemember 'db_datareader', [m4d-linux];
   ```

4. **Temporarily use SQL authentication** until managed identity issue resolved:

   - Change connection string to include `User ID=...;Password=...`
   - Store credentials in Key Vault
   - Revisit managed identity setup later

5. **Check SQL Server networking**:
   - Azure Portal → SQL Server → Networking
   - Verify "Allow Azure services and resources to access this server" = **ON**
   - Check if any firewall rules are blocking managed identity connections

**Current Status (December 31, 2025):**

- ✅ Production (msc4dnc): SQL managed identity authentication **WORKING**
- ❌ Test (m4d-linux): SQL managed identity authentication **FAILING** due to Object ID mismatch
- ✅ All other services (App Config, Key Vault, Search): Working with managed identity

**Service Connector Attempt (December 31, 2025):**

Attempted to use Azure Service Connector (`az webapp connection create sql`) to automatically create SQL user with correct Object ID. **Result: FAILED**

- Service Connector creation succeeded via Azure CLI
- App service became completely unresponsive after Service Connector creation
- Kudu deployment service hung during ZIP extraction
- Suspected cause: Service Connector added environment variables (`AZURE_SQL_CONNECTIONSTRING`, `SQLCONNSTR_*`) that conflicted with existing connection string configuration
- App would not recover even after redeployment attempts

**Resolution Strategy:**

1. **Short-term**: Delete Service Connector, revert to SQL authentication with username/password stored in Key Vault
2. **Long-term**: Create new staging environment (m4d-staging) with fresh configuration to test managed identity setup from scratch

**Next Steps:**

1. Delete Service Connector on m4d-linux
2. Restore SQL authentication connection string
3. Verify m4d-linux recovers and deployments work
4. Create fresh m4d-staging instance following setup guide: [Azure App Service Setup with Managed Identity](azure-app-service-setup-managed-identity.md)

### Testing Strategy

#### Development Testing

Keep SQL authentication for local development:

- LocalDB or SQL Server with SQL auth
- Entity Framework migrations work as before
- No Azure AD required locally

#### Azure Testing

1. Deploy with managed identity SQL connection
2. Verify database migrations run successfully
3. Test CRUD operations:
   - User login/registration
   - Song search and display
   - Playlist operations
   - Admin functions
4. Monitor for authentication failures
5. Check SQL logs for managed identity connections

### Rollback Plan

1. Revert connection string to include SQL credentials
2. Redeploy application
3. Remove managed identity user from SQL if needed

Keep SQL authentication credentials in Key Vault for emergency rollback.

## Implementation Timeline

### Phase 1: App Configuration & Search

- **Effort**: 2-4 hours
- **Risk**: Low
- **Dependencies**: None
- **Testing**: 1-2 hours
- **Rollback**: Simple (revert code + config)

### Phase 2: SQL Server

- **Effort**: 4-8 hours
- **Risk**: Medium
- **Dependencies**: Phase 1 complete, SQL admin access
- **Testing**: 2-4 hours (thorough DB testing needed)
- **Rollback**: Moderate (connection string change + redeploy)

## Success Criteria

### Phase 1 Complete When:

- ✅ App Configuration works with managed identity in both modes
- ✅ Search works with managed identity in both modes
- ✅ No secrets stored in application settings
- ✅ Both test and production deployments successful
- ✅ Documentation updated

### Phase 2 Complete When:

- ✅ SQL Database works with managed identity authentication
- ✅ All database operations function correctly
- ✅ Migrations can be run in production
- ✅ No SQL authentication credentials in configuration
- ✅ Local development still works with SQL auth

## Security Benefits

After both phases complete:

1. **No Secrets in Configuration**: All Azure service authentication uses managed identity
2. **Automatic Credential Rotation**: Azure handles credential lifecycle
3. **Centralized Access Management**: All permissions managed via Azure RBAC
4. **Audit Trail**: Azure AD logs all authentication attempts
5. **Principle of Least Privilege**: Granular permissions per service
6. **Reduced Attack Surface**: No credentials to leak or steal

## Key Vault RBAC Migration Plan

### Current State (December 2025)

**Key Vault:** music4dance (westus)
**Permission Model:** Vault access policy (legacy)
**Shared By:**

- Production: msc4dnc (Windows, framework-dependent)
- Test/Staging: m4d-linux (Linux, framework-dependent)

**Why Migrate to RBAC:**

- Modern Azure security model
- Consistent with App Configuration and Search (already using RBAC)
- Better audit trail and access reviews
- Easier to manage at scale
- Required for some advanced Key Vault features

### Migration Strategy: Zero-Downtime Approach

The challenge: Both production and test use the same Key Vault. We can't switch the permission model without affecting both environments simultaneously.

**Solution:** Dual-permission approach during migration

Key Vault supports **both access policies AND RBAC simultaneously** when set to RBAC mode. This allows gradual migration.

### Migration Steps

#### Phase 1: Document Current Access Policies (Preparation)

Before making changes, document all existing access policies:

1. Azure Portal → Key Vault "music4dance" → Access policies
2. Document each policy:
   - Principal name (user, app, managed identity)
   - Permissions (Get/List/Set/Delete for Keys, Secrets, Certificates)
   - Purpose/owner

Keep this documentation for audit and rollback purposes.

#### Phase 2: Add RBAC Permissions (Non-Breaking)

Add RBAC role assignments WITHOUT changing the permission model yet:

1. Azure Portal → Key Vault "music4dance" → Access control (IAM)
2. Add role assignments for each principal:

**For Web App Managed Identities (msc4dnc, m4d-linux):**

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

#### Phase 3: Switch Permission Model (Breaking Change Window)

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

#### Phase 4: Clean Up Access Policies (Post-Migration)

After successful switch, old access policies are ignored but still visible:

1. Azure Portal → Key Vault → Access policies
2. Document that these are legacy (no longer active)
3. Optionally delete them (they have no effect in RBAC mode)

#### Phase 5: Verify and Monitor

**Verification Checklist:**

- ✅ Production (msc4dnc) starts without errors
- ✅ Test (m4d-linux) starts without errors
- ✅ All OAuth providers work (Google, Facebook, Amazon, Spotify)
- ✅ App Configuration loads secrets from Key Vault
- ✅ No "Forbidden" errors in logs
- ✅ Admin users can still manage Key Vault secrets

**Monitor for 24-48 hours:**

- Application Insights for errors
- Key Vault diagnostic logs
- User-reported authentication issues

### Rollback Plan

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

### Testing Plan Before Production Migration

**Recommended:** Test in a separate Key Vault first

1. Create test Key Vault: "music4dance-test"
2. Copy a few test secrets
3. Configure m4d-linux to use test Key Vault temporarily
4. Practice migration steps:
   - Add RBAC roles
   - Switch to RBAC mode
   - Verify access works
   - Switch back to access policies
   - Verify access still works
5. Document any issues
6. Schedule production migration

### Migration Timeline

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

### Post-Migration: Fully RBAC-Based Security

After migration complete, all Azure services use RBAC:

| Service                 | Authentication Method | Permission Model |
| ----------------------- | --------------------- | ---------------- |
| Azure App Configuration | Managed Identity      | RBAC             |
| Azure Cognitive Search  | Managed Identity      | RBAC             |
| Azure Key Vault         | Managed Identity      | RBAC             |
| Azure SQL Database      | Managed Identity      | Azure AD         |
| Azure Communication Svc | Connection String\*   | Access Key       |

\*Future consideration: Azure Communication Services also supports managed identity authentication

### Additional Notes

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

## Setting Up New Instances

For detailed instructions on creating new App Service instances with managed identity authentication, see:

**[Azure App Service Setup with Managed Identity Authentication](azure-app-service-setup-managed-identity.md)**

This standalone guide covers:

- Complete step-by-step setup instructions
- Prerequisites and configuration details
- Troubleshooting common issues
- Verification and testing procedures
- Success criteria checklist

Use this guide when:

- Creating new staging/test environments
- Setting up production instances
- Replacing problematic instances
- Auditing existing configurations

## References

- [Azure Identity Client Library](https://learn.microsoft.com/en-us/dotnet/api/overview/azure/identity-readme)
- [DefaultAzureCredential](https://learn.microsoft.com/en-us/dotnet/api/azure.identity.defaultazurecredential)
- [Azure SQL with Managed Identity](https://learn.microsoft.com/en-us/azure/azure-sql/database/authentication-azure-ad-user-assigned-managed-identity)
- [App Configuration with Managed Identity](https://learn.microsoft.com/en-us/azure/azure-app-configuration/howto-integrate-azure-managed-service-identity)
- [Azure Search with Managed Identity](https://learn.microsoft.com/en-us/azure/search/search-howto-managed-identities-data-sources)
- [Key Vault Access Policies vs RBAC](https://learn.microsoft.com/en-us/azure/key-vault/general/rbac-migration)
- [Key Vault RBAC Roles](https://learn.microsoft.com/en-us/azure/key-vault/general/rbac-guide)

## Revision History

| Date       | Version | Author  | Changes                                                     |
| ---------- | ------- | ------- | ----------------------------------------------------------- |
| 2025-12-26 | 1.0     | Initial | Created migration plan document                             |
| 2025-12-29 | 1.1     | Copilot | Added Key Vault RBAC migration plan and access policy notes |
