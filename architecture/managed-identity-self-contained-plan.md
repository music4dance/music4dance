# Managed Identity Support for Self-Contained Deployments

## Overview

This document outlines the plan to enable managed identity authentication for Azure services in self-contained deployments, eliminating the need for connection strings and API keys while improving security posture.

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

## References

- [Azure Identity Client Library](https://learn.microsoft.com/en-us/dotnet/api/overview/azure/identity-readme)
- [DefaultAzureCredential](https://learn.microsoft.com/en-us/dotnet/api/azure.identity.defaultazurecredential)
- [Azure SQL with Managed Identity](https://learn.microsoft.com/en-us/azure/azure-sql/database/authentication-azure-ad-user-assigned-managed-identity)
- [App Configuration with Managed Identity](https://learn.microsoft.com/en-us/azure/azure-app-configuration/howto-integrate-azure-managed-service-identity)
- [Azure Search with Managed Identity](https://learn.microsoft.com/en-us/azure/search/search-howto-managed-identities-data-sources)

## Revision History

| Date       | Version | Author  | Changes                         |
| ---------- | ------- | ------- | ------------------------------- |
| 2025-12-26 | 1.0     | Initial | Created migration plan document |
