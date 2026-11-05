using m4dApi;

using Microsoft.EntityFrameworkCore;

namespace m4d.PublicApi;

/// <summary>A newly issued key. <see cref="Key"/> is shown once and never stored.</summary>
public record IssuedKey(string Key, string KeyId, string ClientId, string ManualCommand);

/// <summary>
/// Issues and revokes partner API keys and keeps client limits in step with the partner-keys
/// vault. SQL holds metadata only (who, when, label); the key's hash goes only to the vault and
/// the key only to the person who generated it. See the Authentication section of
/// architecture/plans/partner-api-standalone-host.md.
/// </summary>
public class PartnerKeyService(DanceMusicContext context, IPartnerKeyVault vault, IConfiguration configuration)
{
    // Rotation is "add a new key, deploy it, revoke the old one", which needs two at once
    public const int MaxActiveKeys = 2;

    /// <summary>"live" keys in production (PartnerApi:Environment), "test" everywhere else.</summary>
    public bool IsLive => string.Equals(configuration["PartnerApi:Environment"], "live", StringComparison.OrdinalIgnoreCase);

    public string KeyPrefix => IsLive ? PartnerKeyFormat.LivePrefix : PartnerKeyFormat.TestPrefix;

    public bool VaultConnected => vault.IsConnected;

    public Task<List<PartnerClient>> AllClients() =>
        context.PartnerClients
            .Include(c => c.Keys)
            .Include(c => c.Developers).ThenInclude(d => d.User)
            .OrderBy(c => c.Name)
            .AsSplitQuery()
            .ToListAsync();

    public Task<List<PartnerClient>> ClientsForUser(string userId) =>
        context.PartnerClients
            .Where(c => c.Developers.Any(d => d.UserId == userId))
            .Include(c => c.Keys)
            .OrderBy(c => c.Name)
            .ToListAsync();

    public Task<bool> IsDeveloper(string clientId, string userId) =>
        context.PartnerDevelopers.AnyAsync(d => d.ClientId == clientId && d.UserId == userId);

    /// <summary>Issues a key for <paramref name="clientId"/>, or throws with a message for the user.</summary>
    public async Task<IssuedKey> Issue(string clientId, string label, string userName)
    {
        var client = await context.PartnerClients.Include(c => c.Keys).FirstOrDefaultAsync(c => c.Id == clientId)
            ?? throw new PartnerKeyException("No such client.");
        if (client.Disabled)
        {
            throw new PartnerKeyException("This client is disabled.");
        }

        if (client.Keys.Count(k => k.IsActive) >= MaxActiveKeys)
        {
            throw new PartnerKeyException(
                $"A client can have at most {MaxActiveKeys} active keys. Revoke one before generating another.");
        }

        label = string.IsNullOrWhiteSpace(label) ? null : label.Trim();
        if (label?.Length > 100)
        {
            throw new PartnerKeyException("The label can be at most 100 characters.");
        }

        var key = PartnerKeyFormat.Generate(KeyPrefix, out var keyId);
        var hash = PartnerKeyFormat.Hash(key);

        // Vault first: if it fails nothing is recorded, and the key the user never saw is gone
        await vault.SetKey(keyId, hash, clientId, label);
        context.PartnerApiKeys.Add(new PartnerApiKey
        {
            KeyId = keyId,
            ClientId = clientId,
            Label = label,
            Created = DateTimeOffset.UtcNow,
            CreatedBy = userName
        });
        try
        {
            _ = await context.SaveChangesAsync();
        }
        catch
        {
            // Don't leave a working key nobody can see or revoke
            await vault.DisableKey(keyId);
            throw;
        }

        return new IssuedKey(key, keyId, clientId, vault.IsConnected ? null : ManualKeyCommand(keyId, hash, clientId, label));
    }

    /// <summary>Revokes a key; the API stops accepting it within 5 minutes.</summary>
    public async Task<PartnerApiKey> Revoke(string keyId, string userName)
    {
        var key = await context.PartnerApiKeys.FirstOrDefaultAsync(k => k.KeyId == keyId)
            ?? throw new PartnerKeyException("No such key.");
        if (!key.IsActive)
        {
            return key;
        }

        // Vault first: a key marked revoked here but still enabled there would keep working
        await vault.DisableKey(keyId);
        key.Revoked = DateTimeOffset.UtcNow;
        key.RevokedBy = userName;
        _ = await context.SaveChangesAsync();
        return key;
    }

    /// <summary>
    /// Creates or updates a client and writes its limits to the vault. Returns the command to run
    /// by hand when the vault isn't connected, otherwise null.
    /// </summary>
    public async Task<string> SaveClient(string id, string name, double? ratePerSecond, int? burst,
        long? dailyCap, bool disabled, bool create)
    {
        id = id?.Trim().ToLowerInvariant();
        name = name?.Trim();
        if (!PartnerKeyFormat.IsValidClientId(id))
        {
            throw new PartnerKeyException(
                "The client id must be 2 to 40 lowercase letters, digits and dashes, starting with a letter or digit.");
        }

        if (string.IsNullOrEmpty(name) || name.Length > 100)
        {
            throw new PartnerKeyException("The name is required and can be at most 100 characters.");
        }

        if (ratePerSecond <= 0 || burst < 1 || dailyCap < 1)
        {
            throw new PartnerKeyException("Limits must be positive; leave one empty for the API's default.");
        }

        var client = await context.PartnerClients.FindAsync(id);
        if (create)
        {
            if (client != null)
            {
                throw new PartnerKeyException($"Client {id} already exists.");
            }

            client = new PartnerClient { Id = id, Created = DateTimeOffset.UtcNow };
            context.PartnerClients.Add(client);
        }
        else if (client == null)
        {
            throw new PartnerKeyException("No such client.");
        }

        client.Name = name;
        client.RatePerSecond = ratePerSecond;
        client.Burst = burst;
        client.DailyCap = dailyCap;
        client.Disabled = disabled;

        var config = ToConfig(client);
        await vault.SetClient(id, config);
        _ = await context.SaveChangesAsync();
        return vault.IsConnected ? null : ManualClientCommand(id, config);
    }

    /// <summary>Links an account to a client. The caller grants the partnerDeveloper role.</summary>
    public async Task AddDeveloper(string clientId, ApplicationUser user)
    {
        if (await context.PartnerClients.FindAsync(clientId) == null)
        {
            throw new PartnerKeyException("No such client.");
        }

        if (!await IsDeveloper(clientId, user.Id))
        {
            context.PartnerDevelopers.Add(new PartnerDeveloper { ClientId = clientId, UserId = user.Id });
            _ = await context.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Unlinks an account from a client; returns true if it's still a developer for another one
    /// (so the caller knows whether to remove the role). Keys it issued stay active.
    /// </summary>
    public async Task<bool> RemoveDeveloper(string clientId, string userId)
    {
        var link = await context.PartnerDevelopers.FindAsync(clientId, userId);
        if (link != null)
        {
            context.PartnerDevelopers.Remove(link);
            _ = await context.SaveChangesAsync();
        }

        return await context.PartnerDevelopers.AnyAsync(d => d.UserId == userId);
    }

    public static ClientConfig ToConfig(PartnerClient client) =>
        new(client.Name, client.RatePerSecond, client.Burst, client.DailyCap,
            client.Disabled ? ClientConfig.Disabled : ClientConfig.Active);

    private string ManualKeyCommand(string keyId, string hash, string clientId, string label) =>
        $"az keyvault secret set --vault-name {vault.VaultName} --name {PartnerKeyFormat.KeySecretName(keyId)} " +
        $"--value {hash} --tags {PartnerKeyFormat.ClientTag}={clientId}" +
        (label == null ? "" : $" \"label={label.Replace("\"", "")}\"");

    private string ManualClientCommand(string clientId, ClientConfig config) =>
        $"az keyvault secret set --vault-name {vault.VaultName} --name {PartnerKeyFormat.ClientSecretName(clientId)} " +
        $"--file client.json   # client.json containing {config.ToJson()}";
}

/// <summary>A key or client request the user can fix; the message is shown to them.</summary>
public class PartnerKeyException(string message) : Exception(message);
