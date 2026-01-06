# Azure App Service Setup with Managed Identity Authentication

**Last Updated**: January 2, 2026
**Applies to**: music4dance.net ASP.NET Core application on Azure Linux App Service

## Overview

This guide provides step-by-step instructions for deploying a new Azure App Service instance of the music4dance.net application using managed identity authentication for all Azure services (App Configuration, Azure Search, Key Vault, SQL Database).

**Goal**: Create a fully configured App Service instance where all Azure service authentication uses system-assigned managed identity, eliminating the need for connection strings, API keys, or stored credentials.

**Use Cases**:

- Setting up new staging environments
- Creating production instances
- Replacing problematic instances with clean configuration
- Reference for troubleshooting existing deployments

## Prerequisites

Before starting, gather the following information from an existing working instance (e.g., msc4dnc production):

- **Azure subscription ID** and resource group names
- **Azure App Configuration** endpoint: `https://music4dance.azconfig.io`
- **Azure Cognitive Search** endpoints and index names
  - Songs indexes: `https://music4dance.search.windows.net`
  - Page index: `https://m4d.search.windows.net` (if different)
- **Azure SQL Server** name and database name
  - Server: `n8a541qjnq.database.windows.net`
  - Database: `music4dance` (production) or `music4dance_test` (staging/test)
- **Azure Key Vault** name: `music4dance`
- **OAuth provider credentials** (stored in Key Vault/App Config)
  - Google, Facebook, Spotify ClientId/ClientSecret
- **Azure Communication Services** connection string (for email)
- **reCAPTCHA** site keys (v2 and v3)
- **Azure DevOps** organization, project, and service connection name

## Architecture Overview

The application uses the following Azure services:

| Service                 | Purpose                              | Authentication Method | Permission Model          |
| ----------------------- | ------------------------------------ | --------------------- | ------------------------- |
| Azure App Configuration | Feature flags, configuration         | Managed Identity      | RBAC (Data Reader)        |
| Azure Cognitive Search  | Song search, page search (6 indexes) | Managed Identity      | RBAC (Index Data Reader)  |
| Azure Key Vault         | Secrets (OAuth, reCAPTCHA, email)    | Managed Identity      | Access Policy (Get, List) |
| Azure SQL Database      | User data, songs, playlists          | Managed Identity      | SQL User with roles       |
| Application Insights    | Monitoring, logging                  | Automatic             | N/A                       |

**Code Configuration**: ASP.NET Core with `DefaultAzureCredential` throughout `Program.cs` for all Azure service connections.

## Phase 1: Create App Service and Enable Managed Identity

### 1.1 Create App Service (Azure Portal)

1. Azure Portal → **Create a resource** → **Web App**
2. **Basics** tab:
   - **Subscription**: (same as existing instances)
   - **Resource Group**: `m4d-Web` (or create new)
   - **Name**: `m4d-<environment>` (e.g., `m4d-staging`, `m4d-test2`)
   - **Publish**: Code
   - **Runtime stack**: .NET 10
   - **Operating System**: **Linux**
   - **Region**: West US (same as other resources)
   - **Linux Plan**: Select existing or create new
   - **Pricing plan**: Match production (e.g., B1, S1) or use lower tier for non-production
3. **Deployment** tab:
   - **Continuous deployment**: Disable (configure later via Deployment Center)
4. **Networking** tab:
   - **Enable public access**: On
   - **Enable network injection**: Off (unless required)
5. **Monitoring** tab:
   - **Enable Application Insights**: **Yes**
   - **Application Insights**: Create new: `<app-name>-insights`
6. **Review + create** → **Create**
7. Wait for deployment to complete

### 1.2 Enable System-Assigned Managed Identity

1. Go to the newly created App Service
2. **Settings** → **Identity**
3. **System assigned** tab:
   - **Status**: **On**
   - Click **Save**
4. **Important**: **Copy the Object (principal) ID** displayed after saving
5. Record this ID - you'll need it for SQL user creation:
   ```
   App Name: m4d-<environment>
   Object ID: <paste-guid-here>
   ```

### 1.3 Configure Basic App Settings

**Note**: All required settings are configured automatically by the deployment pipeline:

- **App Configuration endpoint** and **Search service endpoints**: In `appsettings.json` (source controlled)
- **SELF_CONTAINED_DEPLOYMENT**: Automatically set by pipeline (true for self-contained, false for framework-dependent)
- **ASPNETCORE_ENVIRONMENT**: Automatically set by pipeline based on environment parameter (Production/Staging only)
  - **IMPORTANT**: Cloud instances (Azure App Service) must use Staging or Production only
  - Development environment is **only** for local developer machines
  - Development environment enables detailed error pages visible to all users (security risk in cloud)
- **SEARCHINDEX** and **SEARCHINDEXVERSION**: Automatically set by pipeline (staging/test → SongIndexTest-2, production → SongIndexProd-2)
- **WEBSITES_INCLUDE_CLOUD_CERTS**: Automatically set to true (optimizes certificate loading during startup)

**No manual configuration needed at this step**. The pipeline's `AzureWebApp@1` task uses the `appSettings` parameter to configure all necessary environment variables during deployment.

If you need to override settings manually (e.g., for testing or when deploying outside the pipeline):

1. App Service → **Settings** → **Configuration**
2. **Application settings** tab → **+ New application setting**
3. Add any overrides (e.g., to use different search index)

**Do NOT add** at this stage:

- Connection strings (will add in Phase 3)
- OAuth credentials (come from App Config/Key Vault)
- Search API keys (using managed identity instead)

## Phase 2: Grant Managed Identity Access to Azure Services

### 2.1 Azure App Configuration Access (RBAC)

1. Azure Portal → **Azure App Configuration** resource (`music4dance`)
2. **Access control (IAM)** → **Add** → **Add role assignment**
3. **Role** tab:
   - Select: **App Configuration Data Reader**
   - Click **Next**
4. **Members** tab:
   - Assign access to: **Managed identity**
   - Click **+ Select members**
   - Subscription: (your subscription)
   - Managed identity: **App Service**
   - Select: **m4d-<environment>**
   - Click **Select**
5. **Review + assign** → **Review + assign**

### 2.2 Azure Cognitive Search Access (RBAC)

The application needs two roles on each Search service:

- **Search Index Data Reader** - Required for searching/reading documents
- **Search Index Data Contributor** - Required for adding/editing songs (admin uploads, user edits)

**Optional**: **Search Service Contributor** - Only needed for creating/deleting indexes (rare operation, can skip for non-production)

Repeat for **each role** on **each Search service** (typically 2 services × 2 roles = 4 role assignments):

**Songs Index Search Service (`music4dance`):**

1. Azure Portal → **Azure Cognitive Search** resource (`music4dance`)
2. **Access control (IAM)** → **Add role assignment**
3. **Role** tab:
   - Select: **Search Index Data Reader**
   - Click **Next**
4. **Members** tab:
   - Assign access to: **Managed identity**
   - Click **+ Select members**
   - Managed identity: **App Service** → `m4d-<environment>`
   - Click **Select**
5. **Review + assign** → **Review + assign**
6. **Repeat steps 2-5** for **Search Index Data Contributor** role

**Page Index Search Service (`m4d`):**

1. Azure Portal → **Azure Cognitive Search** resource (`m4d`)
2. Repeat steps above for both roles:
   - **Search Index Data Reader**
   - **Search Index Data Contributor**

**Why two roles?** The app both searches indexes (read) and updates them when users add/edit songs or admins upload song lists (write). SelfCrawler also writes to the page index for site search.

### 2.3 Azure Key Vault Access Policy

**Note**: Key Vault uses Access Policy model (not RBAC). Migration to RBAC planned for future.

1. Azure Portal → **Key Vault** (`music4dance`)
2. **Settings** → **Access policies**
3. Click **Create**
4. **Permissions** tab:
   - **Secret permissions**: Check **Get** and **List** (minimum required)
   - **Key permissions**: None
   - **Certificate permissions**: None
   - Click **Next**
5. **Principal** tab:
   - Search for: `m4d-<environment>`
   - Select the managed identity
   - Click **Next**
6. **Application** (optional) tab:
   - Leave blank
   - Click **Next**
7. **Review + create** → **Create**

### 2.4 Azure SQL Server - Verify Azure AD Admin (One-time)

**Skip if already configured on SQL Server**

1. Azure Portal → **SQL Server** (`n8a541qjnq`)
2. **Settings** → **Microsoft Entra ID** (formerly "Azure Active Directory")
3. If "No Microsoft Entra admin configured":
   - Click **Set admin**
   - Select your Azure AD user or admin group
   - Click **Select** → **Save**

### 2.5 Azure SQL Server - Verify Networking

1. SQL Server → **Security** → **Networking**
2. **Firewall rules** tab:
   - **Allow Azure services and resources to access this server**: **Yes** (checked)
   - This is required for managed identity authentication
3. Click **Save** if changed

### 2.6 Azure SQL Database - Configure Service Connector (Recommended)

**This is the recommended approach** - Service Connector automatically handles all the complexity of creating SQL users, granting permissions, and configuring connection strings.

1. Azure Portal → App Service (`m4d-staging` or your app)
2. **Settings** → **Service Connector**
3. Click **+ Create**
4. **Basics** tab:
   - **Service type**: **SQL Database**
   - **Connection name**: Leave default or use `sql_connection`
   - **Subscription**: (your subscription)
   - **SQL server**: `n8a541qjnq`
   - **SQL database**: `music4dance_test` (staging/test) or `music4dance` (production)
   - **Client type**: **.NET**
   - Click **Next**
5. **Authentication** tab:
   - **Authentication type**: **System assigned managed identity**
   - **Username**: (leave blank - managed identity doesn't use username)
   - Click **Next**
6. **Networking** tab:
   - **Network configuration**: Leave default settings
   - Click **Next**
7. **Review + create**:
   - Review settings
   - Click **Create**
8. Wait for deployment to complete (1-2 minutes)

**What Service Connector Does**:

- ✅ Creates SQL user automatically with correct Object ID
- ✅ Grants necessary permissions (db_datareader, db_datawriter, db_ddladmin)
- ✅ Configures connection string as `AZURE_SQL_CONNECTIONSTRING` environment variable
- ✅ Updates automatically if managed identity changes

**Important Note**: The Service Connector creates the environment variable `AZURE_SQL_CONNECTIONSTRING`, which the application code automatically prioritizes over the `DanceMusicContextConnection` connection string when present.

**Verify Service Connector**:

After creation:

1. Service Connector blade → Click the connection
2. **Validate** → Click **Validate**
3. ✅ Should show "Validation passed"
4. **Hidden value** section → Click **Show** to see connection string
   - Should contain: `Authentication=Active Directory Managed Identity`
   - Should NOT contain username/password

**Troubleshooting Service Connector**:

If validation fails:

- Ensure firewall allows Azure services (Step 2.5)
- Ensure Azure AD admin is configured (Step 2.4)
- Delete and recreate the Service Connector
- Check that managed identity is enabled and Object ID is correct

---

**Alternative: Manual SQL User Creation** (Skip if using Service Connector)

<details>
<summary>Click to expand manual instructions (only use if Service Connector fails)</summary>

This is the fallback method that requires explicit SQL user creation. Only use if Service Connector doesn't work for your scenario.

**Option 1: Azure Portal Query Editor**

1. Azure Portal → SQL Database (`music4dance_test` or `music4dance`)
2. **Query editor (preview)** → Sign in with Azure AD admin
3. Run the following SQL (replace `<object-id-guid>` and `<app-name>`):

```sql
-- Replace placeholders with actual values
-- <app-name>: m4d-staging, m4d-test2, etc.
-- <object-id-guid>: GUID from step 1.2
DECLARE @appName NVARCHAR(128) = '<app-name>';
DECLARE @objectId NVARCHAR(36) = '<object-id-guid>';

DECLARE @sql NVARCHAR(MAX);

-- Create user with explicit Object ID
SET @sql = '
    CREATE USER [' + @appName + '] FROM EXTERNAL PROVIDER WITH OBJECT_ID = ''' + @objectId + '''
';
EXEC sp_executesql @sql;

-- Grant standard permissions
SET @sql = 'ALTER ROLE db_datareader ADD MEMBER [' + @appName + ']';
EXEC sp_executesql @sql;

SET @sql = 'ALTER ROLE db_datawriter ADD MEMBER [' + @appName + ']';
EXEC sp_executesql @sql;

SET @sql = 'ALTER ROLE db_ddladmin ADD MEMBER [' + @appName + ']';  -- For EF migrations
EXEC sp_executesql @sql;

SET @sql = 'GRANT EXECUTE TO [' + @appName + ']';
EXEC sp_executesql @sql;

SET @sql = 'GRANT VIEW DEFINITION TO [' + @appName + ']';
EXEC sp_executesql @sql;

PRINT 'User created and permissions granted successfully';
GO

-- VERIFY: Check that Object ID matches
SELECT
    name AS UserName,
    type_desc AS UserType,
    CAST(sid AS uniqueidentifier) AS ObjectId_In_SQL
FROM sys.database_principals
WHERE type_desc = 'EXTERNAL_USER'
ORDER BY name;
```

**Option 2: Azure Data Studio / SSMS**

1. Connect to SQL Server using **Azure Active Directory - Universal with MFA**
2. Select database in dropdown
3. Run SQL above

**Then add connection string manually** (Phase 3.1)

</details>

## Phase 3: Configure Application Settings

### 3.1 Verify SQL Connection String

**If using Service Connector** (recommended - Step 2.6):

- ✅ Connection string is automatically configured as `AZURE_SQL_CONNECTIONSTRING` environment variable
- ✅ No manual configuration needed
- Skip to [Step 3.2](#32-verify-application-settings)

**If using manual SQL user creation** (fallback):

1. App Service → **Settings** → **Configuration**
2. **Connection strings** tab → **+ New connection string**
3. Enter:
   - **Name**: `DanceMusicContextConnection`
   - **Value**:
     ```
     Data Source=n8a541qjnq.database.windows.net,1433;Initial Catalog=<database-name>;Authentication=ActiveDirectoryManagedIdentity
     ```
     Replace `<database-name>` with:
     - `music4dance` (production)
     - `music4dance_test` (staging/test)
   - **Type**: **SQLAzure**
4. Click **OK**
5. Click **Save** → **Continue**

**Important**: Connection string has **no username or password** - authentication happens via managed identity token.

### 3.2 Verify Application Settings

After the first pipeline deployment, your Application Settings will be automatically configured:

**Application settings:**

```
AppConfig__Endpoint = https://music4dance.azconfig.io
ASPNETCORE_ENVIRONMENT = Staging (or Production)
SELF_CONTAINED_DEPLOYMENT = false (or true for self-contained deployments)
SEARCHINDEX = SongIndexTest (staging/test) or SongIndexProd (production)
SEARCHINDEXVERSION = 2
WEBSITES_INCLUDE_CLOUD_CERTS = true
```

**Note**: These settings are automatically configured by the deployment pipeline using the `appSettings` parameter in the `AzureWebApp@1` task. No manual configuration is required.

**Connection strings:**

If using Service Connector (recommended):

```
AZURE_SQL_CONNECTIONSTRING = Data Source=n8a541qjnq.database.windows.net,1433;Initial Catalog=music4dance_test;Authentication=Active Directory Managed Identity
```

If using manual configuration:

```
DanceMusicContextConnection = Data Source=n8a541qjnq.database.windows.net,1433;Initial Catalog=music4dance_test;Authentication=ActiveDirectoryManagedIdentity
```

**Note**: The application automatically uses `AZURE_SQL_CONNECTIONSTRING` when present, falling back to `DanceMusicContextConnection` for local development.

**DO NOT ADD**:

- `AppConfig__ConnectionString` (removed - using managed identity)
- `AzureSearch__ApiKey` (removed - using managed identity)
- OAuth credentials (these come from App Config/Key Vault)
- Email service connection string (comes from App Config/Key Vault)

### 3.3 Verify Search Index Configuration (Code)

These settings are in `appsettings.json` (checked into source) and do **not** need to be in Azure settings:

```json
{
  "SongIndexProd-2": {
    "endpoint": "https://music4dance.search.windows.net",
    "indexname": "songs-prod-2"
  },
  "SongIndexTest-3": {
    "endpoint": "https://music4dance.search.windows.net",
    "indexname": "songs-test-3"
  },
  "PageIndex": {
    "endpoint": "https://m4d.search.windows.net",
    "indexname": "pages"
  }
}
```

Verify these match your current `m4d/appsettings.json` file.

## Phase 4: Deployment Configuration

### 4.1 Configure Deployment Center (Azure DevOps)

1. App Service → **Deployment** → **Deployment Center**
2. **Settings** tab → Click **Add** (if not configured)
3. **Source**: **Azure Repos** (or GitHub if using)
4. Configure:
   - **Organization**: (your Azure DevOps organization)
   - **Project**: music4dance
   - **Repository**: music4dance
   - **Branch**: `self-contained-dotnet10` (or current deployment branch)
5. **Build Provider**: **Azure Pipelines**
6. Click **Save**

### 4.2 Create or Update Azure Pipeline

**Option A: Use Existing Pipeline** (Recommended)

The unified `azure-pipelines.yml` supports all environments and deployment modes:

1. Azure DevOps → Pipelines → **New pipeline** (or edit existing)
2. Select repository: music4dance
3. Configure pipeline → **Existing Azure Pipelines YAML file**
4. Select: `azure-pipelines.yml`
5. The pipeline includes automatic application settings configuration:
   ```yaml
   - task: AzureWebApp@1
     displayName: "Deploy to Azure Web App ($(appName)) - framework-dependent"
     condition: eq(variables['useSelfContained'], false)
     inputs:
       azureSubscription: "m4d-release"
       appType: "webAppLinux"
       appName: "$(appName)"
       package: "$(System.DefaultWorkingDirectory)/**/*.zip"
       startUpCommand: "dotnet m4d.dll"
       appSettings: -SELF_CONTAINED_DEPLOYMENT false -ASPNETCORE_ENVIRONMENT $(aspnetEnvironment) -SEARCHINDEX $(searchIndex) -SEARCHINDEXVERSION $(searchIndexVersion) -WEBSITES_INCLUDE_CLOUD_CERTS true
   ```
6. **Save and run**

**Key Features**:

- **Automatic environment configuration**: The `appSettings` parameter sets all required application settings during deployment
- **No manual Portal configuration needed**: Settings are applied atomically with the deployment
- **Environment-specific values**: Pipeline variables automatically configure the correct search index and environment name based on the `environment` parameter

**Option B: Create New Pipeline**

Create `azure-pipelines-<environment>.yml`:

```yaml
# azure-pipelines-staging.yml
trigger:
  branches:
    include:
      - self-contained-dotnet10
  paths:
    exclude:
      - "*.md"
      - "architecture/**"

pool:
  vmImage: "ubuntu-latest"

variables:
  buildConfiguration: "Release"
  dotnetVersion: "10.x"

steps:
  - task: UseDotNet@2
    displayName: "Install .NET SDK"
    inputs:
      version: $(dotnetVersion)

  - script: dotnet restore
    displayName: "Restore NuGet packages"

  - script: dotnet build --configuration $(buildConfiguration) --no-restore
    displayName: "Build solution"

  - script: dotnet test --configuration $(buildConfiguration) --no-build --filter "FullyQualifiedName!~SelfCrawler"
    displayName: "Run tests (exclude SelfCrawler)"

  - script: dotnet publish m4d/m4d.csproj --configuration $(buildConfiguration) --output $(Build.ArtifactStagingDirectory) --self-contained true --runtime linux-x64
    displayName: "Publish self-contained Linux"

  - task: AzureWebApp@1
    displayName: "Deploy to Azure Web App"
    inputs:
      azureSubscription: "<your-service-connection-name>"
      appType: "webAppLinux"
      appName: "m4d-<environment>"
      package: "$(Build.ArtifactStagingDirectory)"
      runtimeStack: "DOTNETCORE|9.0"
```

**Find service connection name**:

- Azure DevOps → Project Settings → Service connections
- Look for Azure Resource Manager connection
- Copy the name

### 4.3 Grant Pipeline Service Principal Permissions (Required for Automated Configuration)

The deployment pipeline automatically configures app settings and startup commands based on deployment mode. For this automation to work, the Azure DevOps service principal needs permissions on the App Service.

**Required Permission**: **Website Contributor** role on the App Service

**When to Do This**: Before running the pipeline, or if you see authorization errors during the "Configure app settings for deployment mode" step.

#### Method 1: Azure Portal (Recommended for Beginners)

Repeat for each web app (m4d-linux, msc4dnc, etc.):

1. **Azure Portal** → Your **App Service** (e.g., m4d-staging)
2. **Access control (IAM)** → **Add** → **Add role assignment**
3. **Role** tab:
   - Search for and select: **Website Contributor**
   - Click **Next**
4. **Members** tab:
   - **Assign access to**: Select **User, group, or service principal**
   - Click **+ Select members**
   - Search for your service connection name (e.g., "m4d-release") OR paste the Object ID from error logs
   - Select it from results
   - Click **Select**
   - Click **Next**
5. **Review + assign** → **Review + assign**
6. Wait 1-2 minutes for permissions to propagate

**How to find the service principal**:

- If you see authorization errors in pipeline logs, look for the Object ID in the error message
- Example: `The client '***' with object id 'ef3d2c24-a8d6-47fd-8f26-7c25cd161d35'`
- Copy that Object ID and paste it in the member search

#### Method 2: Azure CLI (Faster for Multiple Apps)

If you have Azure CLI installed and authenticated:

```bash
# Grant Website Contributor role to the service principal on m4d-linux
az role assignment create --assignee <service-principal-object-id> --role "Website Contributor" --scope "/subscriptions/<subscription-id>/resourceGroups/music4dance/providers/Microsoft.Web/sites/m4d-linux"

# Grant Website Contributor role to the service principal on msc4dnc
az role assignment create --assignee <service-principal-object-id> --role "Website Contributor" --scope "/subscriptions/<subscription-id>/resourceGroups/music4dance/providers/Microsoft.Web/sites/msc4dnc"
```

**Replace placeholders**:

- `<service-principal-object-id>`: Object ID from pipeline error logs (e.g., `ef3d2c24-a8d6-47fd-8f26-7c25cd161d35`)
- `<subscription-id>`: Your Azure subscription ID (e.g., `35a37095-adba-4229-a691-e55bf38ecf36`)

**Example (actual values)**:

```bash
az role assignment create --assignee ef3d2c24-a8d6-47fd-8f26-7c25cd161d35 --role "Website Contributor" --scope "/subscriptions/35a37095-adba-4229-a691-e55bf38ecf36/resourceGroups/music4dance/providers/Microsoft.Web/sites/m4d-linux"
```

**Note**: Azure CLI commands may occasionally encounter timeout issues. If this happens, use the Azure Portal method instead or retry after a few minutes.

#### Verify Permissions

After granting permissions:

1. Wait 1-2 minutes for propagation
2. Run the deployment pipeline again
3. Look for success messages in the "Configure app settings for deployment mode" step:
   - `✓ SELF_CONTAINED_DEPLOYMENT set successfully`
   - `✓ Startup command set successfully`

If you still see authorization errors, verify:

- ✅ Correct service principal Object ID used
- ✅ Role is "Website Contributor" (not "Contributor" or "Reader")
- ✅ Scope is the specific App Service resource (not resource group or subscription)

## Phase 5: Deploy and Test

### 5.1 Initial Deployment

1. Azure DevOps → Pipelines → Select your pipeline
2. Click **Run pipeline**
3. Select branch: `self-contained-dotnet10`
4. Click **Run**
5. Monitor build progress
6. Verify deployment succeeds

**Expected**: "Deploy to Azure Web App" task completes successfully with:

```
Package deployment using ZIP Deploy initiated.
Successfully updated deployment History
App Service Application URL: https://m4d-<environment>.azurewebsites.net
```

### 5.2 Verify Startup Logs

1. Azure Portal → App Service → **Monitoring** → **Log stream**
2. Wait for app to start (may take 30-60 seconds)
3. Look for these **success messages**:

```
Environment: Staging (or Production)
SELF_CONTAINED_DEPLOYMENT flag: true
Running in self-contained mode
Binding to port 8080
Configuring Azure App Configuration with managed identity
DefaultAzureCredential created successfully for App Configuration
Azure App Configuration configured successfully with managed identity
Found 6 search index configuration sections
Configuring Azure Search with managed identity
DefaultAzureCredential created successfully for Azure Search
Azure Search clients configured successfully with managed identity
Configuring SQL Server database context
Database context configured successfully
```

**Red flags** (should NOT appear):

- ❌ `ERROR connecting to App Configuration`
- ❌ `ERROR configuring Azure Search`
- ❌ `ERROR configuring database`
- ❌ `Login failed for user '<token-identified principal>'`
- ❌ `AuthorizationFailed` or `Forbidden` errors

If you see errors, proceed to Phase 6 (Troubleshooting).

### 5.3 Test Application Functionality

1. **Browse to**: `https://m4d-<environment>.azurewebsites.net`

2. **Verify Home Page**:

   - ✅ Page loads without errors
   - ✅ Navigation menu appears
   - ✅ No error messages
   - Tests: Azure Search, App Configuration

3. **Verify Search**:

   - Click "Songs" or search for a dance/song
   - ✅ Search results appear
   - Tests: Azure Search indexes

4. **Verify User Registration/Login**:

   - Click "Register" or "Log In"
   - ✅ Form appears
   - ✅ Can create account or sign in
   - Tests: SQL Database connection

5. **Verify OAuth Providers** (if enabled):

   - Click "Log in with Google" (or Facebook, Spotify)
   - ✅ Redirect to provider
   - ✅ Can authenticate
   - Tests: Key Vault access for OAuth secrets

6. **Check Application Insights**:
   - Azure Portal → Application Insights → **Failures**
   - ✅ No exceptions in last hour
   - ✅ No dependency failures (SQL, App Config, Search)

### 5.4 Verify Service Health Status

If your application has a service health endpoint (e.g., `/admin/health` or internal status page):

1. Navigate to health status page
2. Verify all services show **Healthy**:
   - ✅ AppConfiguration: Healthy
   - ✅ SearchService: Healthy
   - ✅ Database: Healthy
   - ✅ EmailService: Healthy (or Unavailable if not configured)
   - ✅ GoogleOAuth: Healthy
   - ✅ FacebookOAuth: Healthy
   - ✅ SpotifyOAuth: Healthy

**If services show Unavailable**: Check corresponding Phase 2 steps for that service.

## Phase 6: Troubleshooting

### Issue: App Configuration Connection Fails

**Symptoms**:

- Log stream shows: `ERROR connecting to App Configuration`
- Exception type: `Azure.RequestFailedException`, `AuthorizationFailed`, or `Forbidden`

**Checks**:

1. ✅ Managed identity enabled? → [Step 1.2](#12-enable-system-assigned-managed-identity)
2. ✅ RBAC role assigned? → [Step 2.1](#21-azure-app-configuration-access-rbac)
   - Must be **App Configuration Data Reader** role
   - Must be assigned to **this specific app's managed identity**
3. ✅ Endpoint correct? → [Step 1.3](#13-configure-basic-app-settings)
   - `AppConfig__Endpoint = https://music4dance.azconfig.io` (no trailing slash)
4. ✅ No connection string? → Should NOT have `AppConfig__ConnectionString` setting

**Fix**: If role assignment is missing, add it and **restart the app service**.

### Issue: Azure Search Connection Fails

**Symptoms**:

- Log stream shows: `ERROR configuring Azure Search`
- Search functionality returns no results or errors

**Checks**:

1. ✅ RBAC role assigned on BOTH search services? → [Step 2.2](#22-azure-cognitive-search-access-rbac)
   - `music4dance` search service (songs indexes)
   - `m4d` search service (page index, if different)
2. ✅ Role is **Search Index Data Reader**?
3. ✅ No API key? → Should NOT have `AzureSearch__ApiKey` setting

**Fix**: Add missing role assignment and restart app.

### Issue: SQL Connection Fails

**Symptoms**:

- Log stream shows: `Login failed for user '<token-identified principal>'`
- Database operations fail with authentication errors
- User registration/login broken

**Checks**:

1. ✅ SQL user created with correct Object ID? → [Step 2.6](#26-azure-sql-database---create-sql-user-for-managed-identity)
   - Run verification query:
     ```sql
     SELECT name, CAST(sid AS uniqueidentifier) AS ObjectId
     FROM sys.database_principals
     WHERE name = 'm4d-<environment>' AND type_desc = 'EXTERNAL_USER';
     ```
   - ObjectId must match the Object ID from step 1.2
2. ✅ Connection string format correct? → [Step 3.1](#31-add-sql-connection-string-managed-identity)
   - Must include `Authentication=ActiveDirectoryManagedIdentity`
   - Must NOT include `User ID` or `Password`
3. ✅ Firewall allows Azure services? → [Step 2.5](#25-azure-sql-server---verify-networking)
4. ✅ Azure AD admin set on SQL Server? → [Step 2.4](#24-azure-sql-server---verify-azure-ad-admin-one-time)

**Fix (Object ID mismatch)**:

1. Drop user: `DROP USER [m4d-<environment>];`
2. Recreate with **explicit Object ID**: Re-run SQL from step 2.6 with correct GUID

### Issue: Key Vault Access Denied

**Symptoms**:

- OAuth providers fail to load (Google, Facebook, Spotify)
- Error: `Forbidden` when accessing secrets
- App Config references to Key Vault fail

**Checks**:

1. ✅ Access policy created? → [Step 2.3](#23-azure-key-vault-access-policy)
2. ✅ Permissions include both **Get** AND **List**?
   - List is required for App Configuration to enumerate secrets
3. ✅ Policy assigned to correct managed identity?
   - Verify in Key Vault → Access policies → Find your app name

**Fix**: Edit access policy to add missing permission and save.

### Issue: Deployment Hangs or Fails

**Symptoms**:

- ZIP deployment starts but never completes
- Kudu service unresponsive
- Log stream shows: "Cleaning up temp folders... extracting..." then hangs

**Causes**:

- App crashed on previous startup
- Conflicting environment variables (e.g., from Service Connector)
- Disk space full

**Recovery Steps**:

1. **Stop** the app service (Overview → Stop)
2. Check for **Service Connector** connections (Service Connector blade)
   - If present and not intentional: Delete it
3. Check Application Settings for unexpected variables:
   - `AZURE_SQL_CONNECTIONSTRING` ← Delete if present
   - `SQLCONNSTR_*` ← Delete if present
   - Any SQL-related vars you didn't create ← Delete
4. **Start** the app service
5. **Retry deployment**

### Issue: Environment Variables Conflict

**Symptoms**:

- App behavior differs from expected
- Multiple connection string sources
- Connection string precedence issues

**ASP.NET Core Connection String Precedence** (highest to lowest):

1. `SQLCONNSTR_<name>` environment variable (created by Service Connector)
2. `AZURE_SQL_CONNECTIONSTRING` environment variable
3. `ConnectionStrings:<name>` in Application Settings ← **We use this**
4. `appsettings.json` file

**Fix**: Remove higher-precedence variables if present to avoid conflicts.

### Issue: App Starts But Shows Generic Errors

**Symptoms**:

- App starts without errors in logs
- Browsing to site shows "Application Error" or 500 errors
- No helpful log messages

**Checks**:

1. Check **Application Insights** → Failures blade
   - Look for exceptions with stack traces
2. Enable detailed errors:
   - Add setting: `ASPNETCORE_DETAILEDERRORS = true`
   - Restart app
   - Browse site again
3. Check Log stream for unhandled exceptions
4. Verify all Azure service permissions (App Config, Search, Key Vault, SQL)

## Phase 7: Success Criteria

Your new App Service instance is fully operational when:

✅ **Deployment**:

- Azure Pipeline completes successfully
- ZIP deployment shows no errors
- No deployment warnings or failures

✅ **Startup Logs**:

- App starts without errors
- All services show "configured successfully"
- No authentication failures

✅ **Functionality**:

- Home page loads (App Config, Search)
- Search works (Search indexes)
- User login/registration works (SQL Database)
- OAuth providers work (Key Vault)

✅ **Service Health**:

- All health checks pass (if available)
- Application Insights shows no failures
- No dependency failures

✅ **Security**:

- No secrets in Application Settings (only endpoint URLs)
- SQL connection uses managed identity (no username/password)
- All authentication uses DefaultAzureCredential

## Post-Deployment Tasks

### Set Up Monitoring Alerts

1. Application Insights → **Alerts** → **New alert rule**
2. Create alerts for:
   - HTTP 5xx errors > threshold
   - Failed dependency calls (SQL, App Config, Search)
   - High response time

### Configure Custom Domain (Production)

1. App Service → **Custom domains**
2. Add custom domain: `www.music4dance.net` or `staging.music4dance.net`
3. Configure SSL certificate (App Service Managed Certificate)

### Configure Deployment Slots (Production)

1. App Service → **Deployment slots**
2. Add slot: `staging`
3. Use slot for blue-green deployments

### Review and Optimize

- Review Application Insights performance metrics
- Optimize App Service plan size based on load
- Configure auto-scaling rules if needed

## Configuration Reference

### Required Application Settings Summary

| Setting                     | Value                             | Purpose                               |
| --------------------------- | --------------------------------- | ------------------------------------- |
| `SELF_CONTAINED_DEPLOYMENT` | `true`                            | Enables self-contained mode (Kestrel) |
| `ASPNETCORE_ENVIRONMENT`    | `Staging` or `Production`         | Environment-specific configuration    |
| `AppConfig__Endpoint`       | `https://music4dance.azconfig.io` | App Configuration endpoint            |

### Connection Strings Summary

| Name                          | Value Format                                                                              | Notes                |
| ----------------------------- | ----------------------------------------------------------------------------------------- | -------------------- |
| `DanceMusicContextConnection` | `Data Source=<server>;Initial Catalog=<db>;Authentication=ActiveDirectoryManagedIdentity` | No username/password |

### Azure Service Dependencies

| Service              | Resource Name         | Type                    | Authentication                   |
| -------------------- | --------------------- | ----------------------- | -------------------------------- |
| App Configuration    | `music4dance`         | Azure App Configuration | Managed Identity (RBAC)          |
| Search (Songs)       | `music4dance`         | Cognitive Search        | Managed Identity (RBAC)          |
| Search (Pages)       | `m4d`                 | Cognitive Search        | Managed Identity (RBAC)          |
| Key Vault            | `music4dance`         | Key Vault               | Managed Identity (Access Policy) |
| SQL Server           | `n8a541qjnq`          | Azure SQL               | Managed Identity (SQL User)      |
| Application Insights | `<app-name>-insights` | App Insights            | Automatic                        |

## Appendix: Comparing to Existing Instances

When setting up a new instance, you can reference existing working instances:

### Copy Configuration From Production (msc4dnc)

**Safe to copy**:

- Application setting values (except connection strings)
- App Configuration labels and values
- Key Vault secret names (but not values)
- Search index configurations

**Different per environment**:

- Connection string database name (`music4dance` vs `music4dance_test`)
- `ASPNETCORE_ENVIRONMENT` value
- App Configuration environment label
- Managed identity Object ID (unique per app)

### Audit Checklist

When troubleshooting or comparing environments, verify:

1. ✅ Managed identity enabled and Object ID recorded
2. ✅ RBAC roles on App Config and Search services
3. ✅ Key Vault access policy with Get+List permissions
4. ✅ SQL user exists with correct Object ID
5. ✅ Connection string uses `Authentication=ActiveDirectoryManagedIdentity`
6. ✅ No API keys or connection strings for App Config/Search in settings
7. ✅ Application Insights connected and logging

## Related Documentation

- [Managed Identity Self-Contained Plan](managed-identity-self-contained-plan.md) - Overall migration strategy and troubleshooting log
- [Self-Contained Deployment Guide](../SELF_CONTAINED_DEPLOYMENT.md) - General deployment documentation
- [GitHub Copilot Instructions](.github/copilot-instructions.md) - Project-specific development guidelines

## Change Log

| Date       | Change                                              | Author                               |
| ---------- | --------------------------------------------------- | ------------------------------------ |
| 2025-12-31 | Initial version based on troubleshooting experience | Extracted from managed-identity plan |
