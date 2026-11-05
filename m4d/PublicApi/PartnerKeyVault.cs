using Azure;
using Azure.Security.KeyVault.Secrets;

using m4dApi;

namespace m4d.PublicApi;

/// <summary>
/// Writes to the partner-keys vault that m4d-api reads: key hashes (key-{keyId}) and client limits
/// (client-{clientId}). The web app's managed identity has Key Vault Secrets Officer on that vault
/// only. See architecture/plans/partner-api-standalone-host.md.
/// </summary>
public interface IPartnerKeyVault
{
    /// <summary>False when no vault is configured: keys are still issued, and an admin adds the
    /// hash by hand (the plan's manual fallback).</summary>
    bool IsConnected { get; }

    string VaultName { get; }

    Task SetKey(string keyId, string hash, string clientId, string label);

    /// <summary>Revokes a key by disabling its secret; the API drops it within 5 minutes.</summary>
    Task DisableKey(string keyId);

    Task SetClient(string clientId, ClientConfig config);
}

public class PartnerKeyVault(SecretClient client) : IPartnerKeyVault
{
    public bool IsConnected => true;

    public string VaultName => client.VaultUri.Host.Split('.')[0];

    public async Task SetKey(string keyId, string hash, string clientId, string label)
    {
        var secret = new KeyVaultSecret(PartnerKeyFormat.KeySecretName(keyId), hash);
        secret.Properties.Tags[PartnerKeyFormat.ClientTag] = clientId;
        secret.Properties.Tags["issuedBy"] = "music4dance";
        if (!string.IsNullOrWhiteSpace(label))
        {
            // Tag values are limited to 256 characters
            secret.Properties.Tags["label"] = label.Length > 256 ? label[..256] : label;
        }

        _ = await client.SetSecretAsync(secret);
    }

    public async Task DisableKey(string keyId)
    {
        // Every version, without reading values (the vault refuses a disabled secret's value)
        try
        {
            await foreach (var version in client.GetPropertiesOfSecretVersionsAsync(
                               PartnerKeyFormat.KeySecretName(keyId)))
            {
                if (version.Enabled != false)
                {
                    version.Enabled = false;
                    _ = await client.UpdateSecretPropertiesAsync(version);
                }
            }
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            // Already deleted from the vault, so already unusable
        }
    }

    public async Task SetClient(string clientId, ClientConfig config) =>
        _ = await client.SetSecretAsync(PartnerKeyFormat.ClientSecretName(clientId), config.ToJson());
}

/// <summary>No vault configured (local development, or the manual fallback).</summary>
public class UnconnectedPartnerKeyVault(string vaultName) : IPartnerKeyVault
{
    public bool IsConnected => false;
    public string VaultName => vaultName;
    public Task SetKey(string keyId, string hash, string clientId, string label) => Task.CompletedTask;
    public Task DisableKey(string keyId) => Task.CompletedTask;
    public Task SetClient(string clientId, ClientConfig config) => Task.CompletedTask;
}
