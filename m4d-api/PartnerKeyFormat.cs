using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace m4dApi;

/// <summary>
/// The partner API key format and the Key Vault layout, shared by the function (which validates
/// keys) and the web app (which issues them). The web app links this file rather than referencing
/// the function project, so it must stay dependency-free. See the Authentication section of
/// architecture/plans/partner-api-standalone-host.md.
/// </summary>
/// <remarks>
/// A key reads <c>m4d_live_7KQ2M9XA4PZT_hR3v…</c>: a prefix naming the environment, a 12
/// character keyId that names the key in logs and the vault, and a 256-bit random secret in
/// base64url. Only the SHA-256 of the whole key is stored.
/// </remarks>
public static partial class PartnerKeyFormat
{
    public const string TestPrefix = "m4d_test_";
    public const string LivePrefix = "m4d_live_";

    public const int KeyIdLength = 12;
    private const int SecretBytes = 32;

    // base64url of 32 bytes, without padding
    private const int SecretLength = 43;

    // No 0/O or 1/I, so a keyId read aloud or retyped survives
    private const string KeyIdAlphabet = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ";

    // Vault secret: the hash of one key. Tagged with its client; disable or delete it to revoke.
    public static string KeySecretName(string keyId) => $"key-{keyId}";

    // Vault secret: one client's limits, as ClientConfig JSON
    public static string ClientSecretName(string clientId) => $"client-{clientId}";

    public const string KeySecretPrefix = "key-";
    public const string ClientSecretPrefix = "client-";
    public const string ClientTag = "client";

    public static JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>A new key for the environment named by <paramref name="prefix"/>.</summary>
    public static string Generate(string prefix, out string keyId)
    {
        if (prefix != TestPrefix && prefix != LivePrefix)
        {
            throw new ArgumentException($"Prefix must be {TestPrefix} or {LivePrefix}", nameof(prefix));
        }

        keyId = RandomNumberGenerator.GetString(KeyIdAlphabet, KeyIdLength);
        var secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(SecretBytes))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return $"{prefix}{keyId}_{secret}";
    }

    /// <summary>
    /// The keyId of a well-formed key, without checking whether it exists. The secret can
    /// contain underscores, so the parts are found by position, not by splitting.
    /// </summary>
    public static bool TryParse(string key, out string keyId)
    {
        keyId = null;
        if (key == null || !KeyPattern().IsMatch(key))
        {
            return false;
        }

        keyId = key.Substring(TestPrefix.Length, KeyIdLength);
        return true;
    }

    /// <summary>True if <paramref name="text"/> looks like an API key, for spotting one in a URL.</summary>
    public static bool LooksLikeKey(string text) =>
        text != null && (text.Contains(TestPrefix, StringComparison.OrdinalIgnoreCase) ||
                         text.Contains(LivePrefix, StringComparison.OrdinalIgnoreCase));

    /// <summary>The stored form of a key: lowercase hex SHA-256 of the whole key.</summary>
    public static string Hash(string key) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(key)));

    /// <summary>
    /// True if <paramref name="key"/> hashes to <paramref name="storedHash"/>, compared in
    /// constant time.
    /// </summary>
    public static bool Matches(string key, string storedHash)
    {
        if (storedHash == null || storedHash.Length != 64)
        {
            return false;
        }

        try
        {
            return CryptographicOperations.FixedTimeEquals(
                SHA256.HashData(Encoding.UTF8.GetBytes(key)), Convert.FromHexString(storedHash));
        }
        catch (FormatException)
        {
            return false;
        }
    }

    /// <summary>A client id: lowercase letters, digits and dashes, usable in a vault secret name.</summary>
    public static bool IsValidClientId(string clientId) =>
        clientId != null && ClientIdPattern().IsMatch(clientId);

    [GeneratedRegex("^m4d_(test|live)_[23456789A-HJ-NP-Z]{12}_[A-Za-z0-9_-]{43}$")]
    private static partial Regex KeyPattern();

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{1,39}$")]
    private static partial Regex ClientIdPattern();
}

/// <summary>
/// One client's limits, stored as JSON in the client's vault secret, so they change without a
/// redeploy. A null limit means the function's default.
/// </summary>
public record ClientConfig(
    string Name,
    double? RatePerSecond = null,
    int? Burst = null,
    long? DailyCap = null,
    string Status = ClientConfig.Active)
{
    public const string Active = "active";
    public const string Disabled = "disabled";

    [JsonIgnore]
    public bool IsActive => !string.Equals(Status, Disabled, StringComparison.OrdinalIgnoreCase);

    public string ToJson() => JsonSerializer.Serialize(this, PartnerKeyFormat.JsonOptions);

    public static ClientConfig FromJson(string json) =>
        JsonSerializer.Deserialize<ClientConfig>(json, PartnerKeyFormat.JsonOptions);
}
