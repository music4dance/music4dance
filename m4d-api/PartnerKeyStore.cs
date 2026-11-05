using Azure;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace m4dApi;

/// <summary>A vault secret's metadata, as listing the vault returns it (no value).</summary>
public record SecretInfo(
    string Name, string Version, bool Enabled, DateTimeOffset? ExpiresOn,
    IReadOnlyDictionary<string, string> Tags);

/// <summary>The partner-keys vault, behind an interface so tests can fake it.</summary>
public interface IKeySource
{
    Task<IReadOnlyList<SecretInfo>> List(CancellationToken cancellationToken);
    Task<string> Value(string name, string version, CancellationToken cancellationToken);
}

public class VaultKeySource(SecretClient client) : IKeySource
{
    public async Task<IReadOnlyList<SecretInfo>> List(CancellationToken cancellationToken)
    {
        var list = new List<SecretInfo>();
        await foreach (var secret in client.GetPropertiesOfSecretsAsync(cancellationToken))
        {
            list.Add(new SecretInfo(
                secret.Name, secret.Version, secret.Enabled ?? true, secret.ExpiresOn,
                secret.Tags.AsReadOnly()));
        }

        return list;
    }

    public async Task<string> Value(string name, string version, CancellationToken cancellationToken) =>
        (await client.GetSecretAsync(name, version, cancellationToken)).Value.Value;
}

public enum AuthStatus
{
    Ok,

    // No key, a malformed key, an unknown keyId, or a hash mismatch
    Unauthorized,

    // A valid key whose client is disabled
    Forbidden,

    // The key cache has never loaded, so no key can be checked
    Unavailable
}

public record AuthResult(AuthStatus Status, string ClientId = null, ClientConfig Client = null);

/// <summary>
/// The partner keys and client limits, cached in memory from the partner-keys vault. The request
/// path never calls the vault: an unknown keyId is rejected from the cache, so junk keys can't be
/// used to load it, and a vault outage after the first load only delays new keys and revocations.
/// </summary>
public class PartnerKeyStore(IKeySource source, ILogger<PartnerKeyStore> logger, TimeProvider time)
{
    private record KeyEntry(string Version, string Hash, string ClientId, DateTimeOffset? ExpiresOn);

    private record ClientEntry(string Version, ClientConfig Config);

    // Replaced whole on each refresh, so readers never see a half-built set
    private volatile IReadOnlyDictionary<string, KeyEntry> _keys;
    private volatile IReadOnlyDictionary<string, ClientEntry> _clients = new Dictionary<string, ClientEntry>();

    private readonly TaskCompletionSource _firstLoad = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public bool Loaded => _keys != null;

    /// <summary>
    /// Waits for the first load, so a request that wakes a cold instance isn't refused while the
    /// keys are still on their way. Returns false if they don't arrive within <paramref name="timeout"/>.
    /// </summary>
    public async Task<bool> WaitUntilLoaded(TimeSpan timeout, CancellationToken cancellationToken)
    {
        if (Loaded)
        {
            return true;
        }

        try
        {
            await _firstLoad.Task.WaitAsync(timeout, time, cancellationToken);
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    public int KeyCount => _keys?.Count ?? 0;

    public AuthResult Authenticate(string key)
    {
        var keys = _keys;
        if (keys == null)
        {
            return new AuthResult(AuthStatus.Unavailable);
        }

        if (!PartnerKeyFormat.TryParse(key, out var keyId) ||
            !keys.TryGetValue(keyId, out var entry) ||
            entry.ExpiresOn <= time.GetUtcNow() ||
            !PartnerKeyFormat.Matches(key, entry.Hash))
        {
            return new AuthResult(AuthStatus.Unauthorized);
        }

        // A key whose client has no config secret gets the default limits
        var config = _clients.TryGetValue(entry.ClientId, out var client)
            ? client.Config
            : new ClientConfig(entry.ClientId);
        return config.IsActive
            ? new AuthResult(AuthStatus.Ok, entry.ClientId, config)
            : new AuthResult(AuthStatus.Forbidden, entry.ClientId, config);
    }

    /// <summary>
    /// Lists the vault and reads only the secrets that are new or changed. Disabled secrets are
    /// dropped without reading them (the vault refuses to return a disabled secret's value).
    /// </summary>
    public async Task Refresh(CancellationToken cancellationToken)
    {
        var listed = await source.List(cancellationToken);
        var oldKeys = _keys ?? new Dictionary<string, KeyEntry>();
        var oldClients = _clients;
        var keys = new Dictionary<string, KeyEntry>();
        var clients = new Dictionary<string, ClientEntry>();

        foreach (var secret in listed.Where(s => s.Enabled))
        {
            try
            {
                if (secret.Name.StartsWith(PartnerKeyFormat.KeySecretPrefix, StringComparison.Ordinal))
                {
                    var keyId = secret.Name[PartnerKeyFormat.KeySecretPrefix.Length..];
                    if (!secret.Tags.TryGetValue(PartnerKeyFormat.ClientTag, out var clientId))
                    {
                        logger.LogWarning("Partner key {KeyId} has no client tag; ignoring it", keyId);
                        continue;
                    }

                    var hash = oldKeys.TryGetValue(keyId, out var old) && old.Version == secret.Version
                        ? old.Hash
                        : (await source.Value(secret.Name, secret.Version, cancellationToken)).Trim();
                    keys[keyId] = new KeyEntry(secret.Version, hash, clientId, secret.ExpiresOn);
                }
                else if (secret.Name.StartsWith(PartnerKeyFormat.ClientSecretPrefix, StringComparison.Ordinal))
                {
                    var clientId = secret.Name[PartnerKeyFormat.ClientSecretPrefix.Length..];
                    var config = oldClients.TryGetValue(clientId, out var old) && old.Version == secret.Version
                        ? old.Config
                        : ClientConfig.FromJson(await source.Value(secret.Name, secret.Version, cancellationToken));
                    clients[clientId] = new ClientEntry(secret.Version, config);
                }
            }
            catch (Exception ex) when (ex is System.Text.Json.JsonException or RequestFailedException)
            {
                // One bad secret shouldn't lock every partner out: keep what we had for it
                logger.LogError(ex, "Could not read partner secret {Name}", secret.Name);
                KeepOld(secret.Name, oldKeys, oldClients, keys, clients);
            }
        }

        _clients = clients;
        _keys = keys;
        _ = _firstLoad.TrySetResult();
    }

    private static void KeepOld(
        string name, IReadOnlyDictionary<string, KeyEntry> oldKeys, IReadOnlyDictionary<string, ClientEntry> oldClients,
        Dictionary<string, KeyEntry> keys, Dictionary<string, ClientEntry> clients)
    {
        if (name.StartsWith(PartnerKeyFormat.KeySecretPrefix, StringComparison.Ordinal))
        {
            var keyId = name[PartnerKeyFormat.KeySecretPrefix.Length..];
            if (oldKeys.TryGetValue(keyId, out var key))
            {
                keys[keyId] = key;
            }
        }
        else
        {
            var clientId = name[PartnerKeyFormat.ClientSecretPrefix.Length..];
            if (oldClients.TryGetValue(clientId, out var client))
            {
                clients[clientId] = client;
            }
        }
    }
}

/// <summary>
/// Loads the key cache at startup and refreshes it every 5 minutes; until the first load
/// succeeds it retries every 15 seconds, and the API answers 503.
/// </summary>
public class PartnerKeyRefresher(PartnerKeyStore store, ILogger<PartnerKeyRefresher> logger, TimeProvider time)
    : BackgroundService
{
    public static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await store.Refresh(stoppingToken);
                logger.LogInformation("Loaded {Count} partner keys", store.KeyCount);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Could not refresh partner keys; {State}",
                    store.Loaded ? "keeping the cached keys" : "no keys loaded yet");
            }

            try
            {
                await Task.Delay(store.Loaded ? RefreshInterval : RetryInterval, time, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }
}
