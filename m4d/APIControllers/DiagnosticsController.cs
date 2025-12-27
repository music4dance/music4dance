using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.AspNetCore.Mvc;
using System.Text;

namespace m4d.APIControllers;

[ApiController]
[Route("api/[controller]")]
public class DiagnosticsController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<DiagnosticsController> _logger;

    public DiagnosticsController(IConfiguration configuration, ILogger<DiagnosticsController> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Test managed identity access to Key Vault
    /// WARNING: Remove this endpoint after testing! It exposes diagnostic information.
    /// </summary>
    [HttpGet("test-keyvault")]
    public async Task<IActionResult> TestKeyVaultAccess()
    {
        var result = new StringBuilder();
        result.AppendLine("=== Key Vault Managed Identity Test ===\n");

        try
        {
            // Get Key Vault name from configuration or hardcode for testing
            var keyVaultName = "music4dance";
            var secretName = "Authentication--Amazon--ClientId";
            var keyVaultUri = new Uri($"https://{keyVaultName}.vault.azure.net/");

            result.AppendLine($"Key Vault URI: {keyVaultUri}");
            result.AppendLine($"Secret Name: {secretName}\n");

            // Create credential
            var credential = new DefaultAzureCredential();
            result.AppendLine("✓ DefaultAzureCredential created\n");

            // Try to get access token for Key Vault
            result.AppendLine("Attempting to get access token for Key Vault...");
            var tokenRequestContext = new Azure.Core.TokenRequestContext(
                new[] { "https://vault.azure.net/.default" });

            var tokenResult = await credential.GetTokenAsync(tokenRequestContext, CancellationToken.None);
            result.AppendLine($"✓ Access token obtained successfully");
            result.AppendLine($"  Token expires: {tokenResult.ExpiresOn:u}\n");

            // Create Key Vault client
            var client = new SecretClient(keyVaultUri, credential);
            result.AppendLine("✓ SecretClient created\n");

            // Try to get the secret
            result.AppendLine($"Attempting to read secret '{secretName}'...");
            var secret = await client.GetSecretAsync(secretName);

            result.AppendLine($"✓ SECRET READ SUCCESSFULLY!");
            result.AppendLine($"  Secret Name: {secret.Value.Name}");
            result.AppendLine($"  Secret Value: {secret.Value.Value[..Math.Min(10, secret.Value.Value.Length)]}... (truncated)");
            result.AppendLine($"  Content Type: {secret.Value.Properties.ContentType}");
            result.AppendLine($"  Enabled: {secret.Value.Properties.Enabled}");
            result.AppendLine($"  Created: {secret.Value.Properties.CreatedOn:u}");
            result.AppendLine($"  Updated: {secret.Value.Properties.UpdatedOn:u}\n");

            result.AppendLine("=== TEST PASSED ===");
            result.AppendLine("Managed identity has correct permissions to read Key Vault secrets.");

            return Ok(result.ToString());
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 403)
        {
            result.AppendLine($"\n✗ PERMISSION DENIED (403 Forbidden)");
            result.AppendLine($"  Error Code: {ex.ErrorCode}");
            result.AppendLine($"  Message: {ex.Message}\n");
            result.AppendLine("=== TEST FAILED ===");
            result.AppendLine("The managed identity does NOT have permission to read Key Vault secrets.");
            result.AppendLine("\nRequired actions:");
            result.AppendLine("1. Verify managed identity is enabled for this app");
            result.AppendLine("2. Grant 'Key Vault Secrets User' role to the managed identity on the Key Vault");
            result.AppendLine("3. Wait 5-10 minutes for Azure AD propagation");
            result.AppendLine("4. Check if Key Vault uses Access Policies instead of RBAC");

            _logger.LogError(ex, "Key Vault access denied");
            return StatusCode(403, result.ToString());
        }
        catch (Azure.Identity.CredentialUnavailableException ex)
        {
            result.AppendLine($"\n✗ MANAGED IDENTITY NOT AVAILABLE");
            result.AppendLine($"  Message: {ex.Message}\n");
            result.AppendLine("=== TEST FAILED ===");
            result.AppendLine("Managed identity is not enabled or not working.");
            result.AppendLine("\nRequired actions:");
            result.AppendLine("1. Enable System-assigned managed identity in App Service settings");
            result.AppendLine("2. Verify SELF_CONTAINED_DEPLOYMENT environment variable is set correctly");

            _logger.LogError(ex, "Managed identity unavailable");
            return StatusCode(500, result.ToString());
        }
        catch (Exception ex)
        {
            result.AppendLine($"\n✗ UNEXPECTED ERROR");
            result.AppendLine($"  Type: {ex.GetType().Name}");
            result.AppendLine($"  Message: {ex.Message}");
            if (ex.InnerException != null)
            {
                result.AppendLine($"  Inner Exception: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
            }
            result.AppendLine($"\n  Stack Trace:\n{ex.StackTrace}");

            _logger.LogError(ex, "Unexpected error testing Key Vault access");
            return StatusCode(500, result.ToString());
        }
    }

    /// <summary>
    /// Test what credential methods DefaultAzureCredential will try
    /// </summary>
    [HttpGet("test-credential")]
    public IActionResult TestCredentialChain()
    {
        var result = new StringBuilder();
        result.AppendLine("=== DefaultAzureCredential Chain Test ===\n");
        result.AppendLine("DefaultAzureCredential will try these methods in order:\n");

        result.AppendLine("1. EnvironmentCredential");
        result.AppendLine($"   AZURE_TENANT_ID: {Environment.GetEnvironmentVariable("AZURE_TENANT_ID") ?? "(not set)"}");
        result.AppendLine($"   AZURE_CLIENT_ID: {Environment.GetEnvironmentVariable("AZURE_CLIENT_ID") ?? "(not set)"}");
        result.AppendLine($"   AZURE_CLIENT_SECRET: {(string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AZURE_CLIENT_SECRET")) ? "(not set)" : "***SET***")}");
        result.AppendLine();

        result.AppendLine("2. WorkloadIdentityCredential");
        result.AppendLine($"   AZURE_FEDERATED_TOKEN_FILE: {Environment.GetEnvironmentVariable("AZURE_FEDERATED_TOKEN_FILE") ?? "(not set)"}");
        result.AppendLine();

        result.AppendLine("3. ManagedIdentityCredential ⭐ (This should work for Azure App Service)");
        result.AppendLine($"   MSI_ENDPOINT: {Environment.GetEnvironmentVariable("MSI_ENDPOINT") ?? "(not set)"}");
        result.AppendLine($"   MSI_SECRET: {(string.IsNullOrEmpty(Environment.GetEnvironmentVariable("MSI_SECRET")) ? "(not set)" : "***SET***")}");
        result.AppendLine($"   IDENTITY_ENDPOINT: {Environment.GetEnvironmentVariable("IDENTITY_ENDPOINT") ?? "(not set)"}");
        result.AppendLine($"   IDENTITY_HEADER: {(string.IsNullOrEmpty(Environment.GetEnvironmentVariable("IDENTITY_HEADER")) ? "(not set)" : "***SET***")}");
        result.AppendLine();

        result.AppendLine("4. SharedTokenCacheCredential");
        result.AppendLine("   (Not applicable in Azure App Service)");
        result.AppendLine();

        result.AppendLine("5. VisualStudioCredential");
        result.AppendLine("   (Not applicable in Azure App Service)");
        result.AppendLine();

        result.AppendLine("6. VisualStudioCodeCredential");
        result.AppendLine("   (Not applicable in Azure App Service)");
        result.AppendLine();

        result.AppendLine("7. AzureCliCredential");
        result.AppendLine("   (Not applicable in Azure App Service)");
        result.AppendLine();

        result.AppendLine("8. AzurePowerShellCredential");
        result.AppendLine("   (Not applicable in Azure App Service)");
        result.AppendLine();

        result.AppendLine("=== Environment Variables (Filtered) ===\n");
        var envVars = Environment.GetEnvironmentVariables();
        foreach (var key in envVars.Keys.Cast<string>().OrderBy(k => k))
        {
            if (key.StartsWith("AZURE_") || key.StartsWith("MSI_") ||
                key.StartsWith("IDENTITY_") || key.StartsWith("WEBSITE_") ||
                key == "ASPNETCORE_ENVIRONMENT" || key == "SELF_CONTAINED_DEPLOYMENT")
            {
                var value = envVars[key]?.ToString() ?? "";
                // Redact sensitive values
                if (key.Contains("SECRET") || key.Contains("PASSWORD") || key.Contains("KEY"))
                {
                    value = string.IsNullOrEmpty(value) ? "(not set)" : "***REDACTED***";
                }
                result.AppendLine($"{key}: {value}");
            }
        }

        return Ok(result.ToString());
    }
}
