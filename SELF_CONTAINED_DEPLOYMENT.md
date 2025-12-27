# Azure Deployment Guide for music4dance.net

This guide covers deploying music4dance.net to Azure Linux Web Apps with support for both framework-dependent and self-contained deployments across production and test environments.

## Table of Contents

1. [Overview](#overview)
2. [Deployment Scenarios](#deployment-scenarios)
3. [Azure DevOps Pipeline Setup](#azure-devops-pipeline-setup)
4. [Azure Web App Configuration](#azure-web-app-configuration)
5. [Application Configuration](#application-configuration)
6. [Testing & Validation](#testing--validation)
7. [Troubleshooting](#troubleshooting)

## Overview

### Deployment Modes

#### Framework-Dependent Deployment

- Relies on .NET runtime installed on the host
- Package size: ~20-30MB
- Faster deployment
- Requires .NET 10 runtime available on Azure
- Best for: Production when runtime is available

#### Self-Contained Deployment

- Bundles .NET runtime with the application
- Package size: ~100-150MB
- Works when runtime isn't available on host
- Complete isolation from system runtime
- Optimized with ReadyToRun (R2R) compilation
- Best for: .NET 10 availability (current scenario)

## Deployment Scenarios

The application supports four deployment combinations:

| Scenario                      | Mode                | Environment    | Target App  | Use Case                              |
| ----------------------------- | ------------------- | -------------- | ----------- | ------------------------------------- |
| Production Hosted             | Framework-Dependent | Production     | msc4dnc     | When .NET 10 runtime available        |
| **Production Self-Contained** | **Self-Contained**  | **Production** | **msc4dnc** | **Current: .NET 10 not yet on Azure** |
| Test Hosted                   | Framework-Dependent | Test           | m4d-linux   | Framework testing                     |
| Test Self-Contained           | Self-Contained      | Test           | m4d-linux   | Self-contained testing                |

## Azure DevOps Pipeline Setup

### Unified Pipeline (Recommended)

The unified pipeline (`azure-pipelines.yml`) supports all scenarios through **runtime parameters**.

#### Quick Setup - Single Pipeline with Runtime Selection

1. **Navigate to Azure DevOps Pipelines**

   - Go to: `https://dev.azure.com/{organization}/{project}/_build`
   - Click "New Pipeline"

2. **Connect to Repository**

   - Select your repository source (Azure Repos Git or GitHub)
   - Choose repository: `music4dance`

3. **Select Pipeline File**

   - Choose "Existing Azure Pipelines YAML file"
   - Select: `/azure-pipelines.yml`
   - Click "Continue"

4. **Save the Pipeline**

   - Click "Save" (dropdown arrow) → "Save"
   - Name it: `Deploy - music4dance`
   - That's it! No variables to configure.

5. **Running the Pipeline**

   Each time you run the pipeline, Azure DevOps will prompt you to select:

   - **Deployment Mode**: `framework-dependent` or `self-contained`
   - **Target Environment**: `production` or `test`

   The pipeline automatically:

   - Deploys to `msc4dnc` when environment = `production`
   - Deploys to `m4d-linux` when environment = `test`
   - Uses appropriate publish method based on deployment mode

#### Alternative: Multiple Named Pipelines (Future Enhancement)

If you prefer dedicated pipelines for each scenario without runtime selection, you could modify the pipeline to use variables instead of parameters. This would allow creating 4 separate pipeline instances:

- Deploy - Production (Self-Contained)
- Deploy - Production (Hosted)
- Deploy - Test (Self-Contained)
- Deploy - Test (Hosted)

This approach requires modifying `azure-pipelines.yml` to check variables instead of parameters. The current parameter-based approach is simpler and provides better visibility into what's being deployed.

### Legacy Pipeline Files

Individual pipeline files exist for backward compatibility:

| File                                      | Mode                | Environment | Target App |
| ----------------------------------------- | ------------------- | ----------- | ---------- |
| `azure-pipelines-1.yml`                   | Framework-Dependent | Production  | msc4dnc    |
| `azure-pipelines-self-contained.yml`      | Self-Contained      | Production  | msc4dnc    |
| `azure-pipelines-release.yml`             | Flexible (variable) | Test        | m4d-linux  |
| `azure-pipelines-test-self-contained.yml` | Self-Contained      | Test        | m4d-linux  |

**Note:** The unified pipeline is recommended for easier maintenance.

## Azure Web App Configuration

### Required Application Settings

Configure these in Azure Portal → Web App → Configuration → Application Settings.

#### For Both Framework-Dependent and Self-Contained Deployments

**Application Settings:**

```text
SELF_CONTAINED_DEPLOYMENT = true   # For self-contained; false or unset for framework-dependent
ASPNETCORE_ENVIRONMENT = Production
```

**Managed Identity Setup (Required for Both Modes):**

1. **Enable Managed Identity:**

   - Azure Portal → Your **Web App** (msc4dnc or m4d-linux)
   - Settings → **Identity**
   - Under **System assigned** tab: Set **Status** to **On**
   - Click **Save**
   - Copy the **Object (principal) ID** for use in next steps

2. **Grant Azure AI Search Permissions:**

   - Azure Portal → Your **Azure AI Search** service
   - Access control (IAM) → Role assignments
   - Click "+ Add" → "Add role assignment"
   - Role: **Search Index Data Reader** (required)
   - Members: Select **Managed identity** → Select your web app
   - Review + assign

3. **Grant Azure App Configuration Permissions:**

   - Azure Portal → Your **Azure App Configuration** resource
   - Access control (IAM) → Role assignments
   - Click "+ Add" → "Add role assignment"
   - Role: **App Configuration Data Reader** (required)
   - Members: Select **Managed identity** → Select your web app
   - Review + assign

4. **Grant Azure Key Vault Permissions:**
   - Azure Portal → **Key vaults** → Your Key Vault (e.g., `music4dance`)
   - Access control (IAM) → Role assignments
   - Click "+ Add" → "Add role assignment"
   - Role: **Key Vault Secrets User** (required for App Configuration Key Vault references)
   - Members: Select **Managed identity** → Select your web app
   - Review + assign
   - **Note**: App Configuration stores some secrets as Key Vault references. Without this permission, App Configuration will fail to load those values.

**Authentication Method:**

Both deployment modes now use **managed identity** for:

- Azure App Configuration (feature flags, configuration)
- Azure Cognitive Search (all search indexes)

No connection strings or API keys needed for these services!

**Self-Contained Additional Configuration:**

For self-contained deployments, also set:

- **Startup Command**: `/home/site/wwwroot/m4d`
  - Azure Portal → Web App → Configuration → General settings
  - Click **Save**

### Connection Strings & Secrets

Ensure these are configured:

- **DanceMusicContextConnection** - SQL Server connection string
- **AppConfig:Endpoint** - Azure App Configuration endpoint (e.g., `https://music4dance.azconfig.io`)
- Authentication provider credentials:
  - Google: ClientId, ClientSecret
  - Facebook: ClientId, ClientSecret
  - Spotify: ClientId, ClientSecret
  - reCAPTCHA: SiteKey, SecretKey

### Environment-Specific Settings

**Self-Contained Mode Additional Settings:**

| Setting                     | Value             | Description                                    |
| --------------------------- | ----------------- | ---------------------------------------------- |
| `PORT` or `WEBSITES_PORT`   | `8080`            | Azure auto-sets, can override                  |
| `HOME`                      | Auto-set by Azure | Used for data protection keys                  |
| `WEBSITE_LOAD_CERTIFICATES` | `{thumbprint}`    | Optional: Certificate thumbprint for HTTPS     |
| `HTTPS_PORT`                | `443`             | Optional: HTTPS port if certificate configured |

## Application Configuration

### Code Changes for Self-Contained Support

#### 1. Project File (`m4d.csproj`)

Conditional property group activates when building self-contained:

```xml
<PropertyGroup Condition="'$(SelfContained)' == 'true'">
  <PublishSingleFile>true</PublishSingleFile>
  <PublishReadyToRun>true</PublishReadyToRun>
  <PublishTrimmed>false</PublishTrimmed>
  <IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
  <IncludeAllContentForSelfExtract>true</IncludeAllContentForSelfExtract>
</PropertyGroup>
```

**Note:** `PublishTrimmed` disabled due to reflection and dynamic assembly loading.

#### 2. Program.cs

**Self-Contained Detection:**

```csharp
var isSelfContained = configuration.GetValue<bool>("SELF_CONTAINED_DEPLOYMENT");
```

**Kestrel Port Configuration:**

- Reads `PORT` or `WEBSITES_PORT` environment variable
- Defaults to 8080 if not set
- Azure Web Apps on Linux require explicit port binding

**HTTPS Certificate Loading:**

- Checks `WEBSITE_LOAD_CERTIFICATES` environment variable
- Loads from `/var/ssl/private/{thumbprint}.p12`
- Configures HTTPS on `HTTPS_PORT` if available

**Data Protection:**

- Persists keys to `$HOME/site/keys`
- Required for session consistency across restarts
- Ensures authentication cookies remain valid

#### 3. appsettings.SelfContained.json

Environment-specific configuration:

```json
{
  "SELF_CONTAINED_DEPLOYMENT": true,
  "ASPNETCORE_ENVIRONMENT": "Production",
  "ASPNETCORE_FORWARDEDHEADERS_ENABLED": true,
  "ASPNETCORE_URLS": "http://+:8080"
}
```

### Pipeline Architecture

The unified pipeline uses:

- **Parameters**: User-selectable deployment mode and environment
- **Conditional Variables**:
  - `appName`: `msc4dnc` (production) or `m4d-linux` (test)
  - `useSelfContained`: `true` or `false`
- **Conditional Steps**: Different publish commands based on mode

**Build Process:**

1. Install Node.js 22.x and enable Corepack
2. Install Yarn dependencies and build Vue.js client
3. Install .NET 10 SDK
4. Build .NET project
5. Publish (self-contained or framework-dependent)
6. Deploy to target Azure Web App

## Testing & Validation

### Local Testing

**Build self-contained:**

```powershell
dotnet publish m4d/m4d.csproj -c Release -r linux-x64 --self-contained true /p:SelfContained=true
```

**Test in Docker:**

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:10.0
COPY ./m4d/bin/Release/net10.0/linux-x64/publish /app
WORKDIR /app
ENV SELF_CONTAINED_DEPLOYMENT=true
ENV PORT=8080
EXPOSE 8080
ENTRYPOINT ["./m4d"]
```

### Azure Deployment Validation

After deployment, monitor startup logs for:

- "Production environment detected. Deployment mode: {mode}"
- "Configuring Azure App Configuration with managed identity"
- "DefaultAzureCredential created successfully"
- "Configuring Azure Search with managed identity"
- Self-contained only: "Running in self-contained mode"
- Self-contained only: "Binding to port {port}"
- Self-contained only: "Data protection keys stored at: {path}"

## Troubleshooting

### Common Issues

#### Pipeline runs wrong deployment mode

**Symptom:** Pipeline runs framework-dependent publish instead of self-contained (or vice versa)

**Solution:** Parameters must be selected at **runtime**, not set as variables.

- When you click "Run pipeline", Azure DevOps shows parameter dropdowns
- **Don't set variables** named `deploymentMode` or `environment` - they won't work
- Select the correct values from the dropdowns when running

#### Pipeline targets wrong app

#### App doesn't start after deployment

**Both deployment modes:**

- ✓ Verify **Managed Identity is enabled** (Settings → Identity → System assigned = On)
- ✓ Verify RBAC permissions granted for:
  - **App Configuration** (App Configuration Data Reader)
  - **Key Vault** (Key Vault Secrets User) - required for Key Vault references
  - **Search** (Search Index Data Reader)
- ✓ Check application logs at `/home/LogFiles/Application/console.log` in Kudu
- ✓ Look for "DefaultAzureCredential created successfully" messages
- ✓ Review startup logs for authentication errors

#### Key Vault "Forbidden" errors

**Symptom:** `KeyVaultReferenceException: Key vault error. ErrorCode:'Forbidden'`

**Cause:** App Configuration contains references to Key Vault secrets, but managed identity doesn't have permission to read them.

**Solution:**

1. Azure Portal → **Key vaults** → Your Key Vault (NOT App Configuration)
2. Access control (IAM) → Add role assignment
3. Role: **Key Vault Secrets User**
4. Members: Select your web app's managed identity
5. After granting permission, **restart the app** (no redeployment needed)
6. Allow 1-2 minutes for Azure AD permission propagation

**Self-contained specific:**

- ✓ Verify `SELF_CONTAINED_DEPLOYMENT=true` in Azure App Settings
- ✓ Check startup command is set to `/home/site/wwwroot/m4d` (Configuration → General settings)
- ✓ Check port 8080 is accessible (Azure handles automatically)
- ✓ Review deployment logs for missing dependencies

**Framework-dependent specific:**

- ✓ Verify .NET 10 runtime available on Azure Linux
- ✓ Check runtime version compatibility

#### Certificate errors

- ✓ Verify `WEBSITE_LOAD_CERTIFICATES` contains valid thumbprint
- ✓ Check certificate exists in `/var/ssl/private/`
- ✓ Ensure `HTTPS_PORT` is configured

#### Authentication fails after restart

- ✓ Verify `HOME` environment variable is set
- ✓ Check data protection keys at `$HOME/site/keys`
- ✓ Ensure directory has write permissions

#### Package too large / deployment timeout

**Expected sizes:**

- Self-contained: ~100-150MB (normal)
- Framework-dependent: ~20-30MB

**Solutions:**

- Increase pipeline timeout if needed
- Consider framework-dependent when .NET 10 is available on Azure

### Diagnostic Commands

In Azure SSH/Console:

```bash
# Check environment variables
printenv | grep -E 'PORT|HOME|SELF_CONTAINED|WEBSITE'

# Verify data protection keys
ls -la $HOME/site/keys/

# Check certificate location
ls -la /var/ssl/private/

# View application logs
tail -f /home/LogFiles/Application/console.log
**To Self-Contained:**

1. Set pipeline parameter: `deploymentMode: self-contained`
2. In Azure Portal → Web App → Configuration → Application settings:
   - Add: `SELF_CONTAINED_DEPLOYMENT=true`
### Switching Deployment Modes

**To Self-Contained:**

1. Set pipeline parameter: `deploymentMode: self-contained`
2. In Azure Portal → Web App → Configuration:
   - Application settings: Set `SELF_CONTAINED_DEPLOYMENT=true`
   - General settings → Startup Command: `/home/site/wwwroot/m4d`
3. Ensure managed identity is **enabled** (both modes use it)
4. Redeploy

**To Framework-Dependent:**

1. Set pipeline parameter: `deploymentMode: framework-dependent`
2. In Azure Portal → Web App → Configuration:
   - Application settings: Set `SELF_CONTAINED_DEPLOYMENT=false` (or remove)
   - General settings → Startup Command: (clear/remove)
3. Ensure .NET 10 runtime available on Azure
4. Ensure managed identity is **enabled** (both modes use it)
5. Redeploy

### Performance Considerations

**Self-Contained Advantages:**

- ✓ Faster startup (ReadyToRun compilation)
- ✓ No runtime version conflicts
- ✓ Predictable behavior
- ✓ Works immediately on any Linux host

**Self-Contained Disadvantages:**

- ✗ Larger deployment size (~100-150MB vs ~20-30MB)
- ✗ Longer deployment time
- ✗ Cannot auto-benefit from runtime security patches

### Security Notes

**Both deployment modes:**
- Use managed identity for Azure services (App Configuration, Search)
- No secrets stored in application settings
- Automatic credential rotation via Azure
- RBAC-based access control

**Additional security features:**
- Certificates loaded from Azure-managed locations (`/var/ssl/private/`)
- Data protection keys stored in `$HOME/site/keys` (persisted across deployments)
- SQL connection strings can use Azure Key Vault references
- HTTPS redirection controlled by `DISABLE_HTTPS_REDIRECT` flag (disable for local Spotify OAuth testing only)

### Quick Reference: Pipeline Parameters

| Parameter        | Values                                  | Controls        |
| ---------------- | --------------------------------------- | --------------- |
| `deploymentMode` | `framework-dependent`, `self-contained` | Publish method  |
| `environment`    | `production`, `test`                    | Target app name |

**Resulting app names:**

- `production` → `msc4dnc`
- `test` → `m4d-linux`
```
