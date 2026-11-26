using System.Security.Cryptography;
using System.Text;

using Newtonsoft.Json;

namespace m4d.Utilities;

// The Apple Music API developer token: an ES256 JWT signed with the MusicKit private key rather
// than fetched from a token endpoint. It plugs into AdmAuthentication so that caching, the renewal
// timer and the invalidate-on-401 path are shared with Spotify. The configuration is read each time
// a token is signed, so a key that arrives after startup (App Configuration recovery) is picked up.
public class AppleMusicAuthentication(IConfiguration configuration) : AdmAuthentication(configuration)
{
    public const string TeamIdKey = "Authentication:AppleMusic:TeamId";
    public const string KeyIdKey = "Authentication:AppleMusic:KeyId";
    public const string PrivateKeyKey = "Authentication:AppleMusic:PrivateKey";

    // Apple allows up to about 6 months. A short lifetime limits the damage if a token leaks, and
    // signing a new one costs nothing.
    private static readonly TimeSpan Lifetime = TimeSpan.FromHours(12);

    private readonly IConfiguration _configuration = configuration;

    protected override string Client => "AppleMusic";
    protected override string RequestBody => string.Empty;
    protected override string RequestUrl => string.Empty;

    public static bool IsConfigured(IConfiguration configuration) =>
        !string.IsNullOrWhiteSpace(configuration[TeamIdKey]) &&
        !string.IsNullOrWhiteSpace(configuration[KeyIdKey]) &&
        !string.IsNullOrWhiteSpace(configuration[PrivateKeyKey]);

    // Only requests to the Apple Music API carry the token. The legacy iTunes Search/Lookup API
    // shares ServiceType.ITunes but takes no authorization.
    public static bool IsAppleMusicRequest(Uri request) =>
        string.Equals(request.Host, "api.music.apple.com", StringComparison.OrdinalIgnoreCase);

    protected override Task<AccessToken> CreateToken()
    {
        if (!IsConfigured(_configuration))
        {
            throw new InvalidOperationException(
                $"Apple Music isn't configured: set {TeamIdKey}, {KeyIdKey} and {PrivateKeyKey}");
        }

        Logger.LogInformation("Signing Apple Music developer token");
        var token = CreateDeveloperToken(
            _configuration[TeamIdKey], _configuration[KeyIdKey], _configuration[PrivateKeyKey],
            DateTimeOffset.UtcNow, Lifetime);
        return Task.FromResult(new AccessToken
        {
            access_token = token,
            token_type = "Bearer",
            expires_in = (int)Lifetime.TotalSeconds
        });
    }

    // Header {alg: ES256, kid}, claims {iss, iat, exp}. ECDsa.SignData produces the raw r||s
    // (IEEE P1363) signature that JWT needs, not DER.
    public static string CreateDeveloperToken(string teamId, string keyId, string privateKey,
        DateTimeOffset now, TimeSpan lifetime)
    {
        using var key = ImportPrivateKey(privateKey);

        var header = JsonConvert.SerializeObject(new { alg = "ES256", kid = keyId.Trim() });
        var issuedAt = now.ToUnixTimeSeconds();
        var claims = JsonConvert.SerializeObject(new
        {
            iss = teamId.Trim(),
            iat = issuedAt,
            exp = issuedAt + (long)lifetime.TotalSeconds
        });

        var signingInput = $"{Base64Url(Encoding.UTF8.GetBytes(header))}.{Base64Url(Encoding.UTF8.GetBytes(claims))}";
        var signature = key.SignData(Encoding.ASCII.GetBytes(signingInput), HashAlgorithmName.SHA256);
        return $"{signingInput}.{Base64Url(signature)}";
    }

    // Accepts the .p8 PEM, or the one-line form (the base64 PKCS#8 body with the BEGIN/END lines
    // and line breaks removed) that survives a trip through a password manager. Exceptions never
    // include the key text, so they're safe to log.
    public static ECDsa ImportPrivateKey(string privateKey)
    {
        var key = ECDsa.Create();
        try
        {
            if (privateKey.Contains("-----BEGIN", StringComparison.Ordinal))
            {
                key.ImportFromPem(privateKey);
            }
            else
            {
                var body = new string([.. privateKey.Where(c => !char.IsWhiteSpace(c))]);
                key.ImportPkcs8PrivateKey(Convert.FromBase64String(body), out _);
            }

            return key;
        }
        catch (Exception e) when (e is FormatException or CryptographicException or ArgumentException)
        {
            key.Dispose();
            throw new InvalidOperationException(
                $"The Apple Music private key ({PrivateKeyKey}) couldn't be read as a PKCS#8 EC key ({e.GetType().Name})");
        }
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
