namespace m4dModels;

/// <summary>
/// The stored OAuth connection that lets scheduled jobs act as a music4dance-owned account on an
/// external music service (today the music4dance Spotify account) without an admin browser
/// session. One row per service, keyed by the ServiceType name. The refresh token is stored only
/// as an ASP.NET Data Protection payload, never in clear text.
/// </summary>
public class ServiceAccountToken
{
    // Spotify refresh tokens expire six months after the original authorization, and refreshing
    // doesn't reset the clock. Apple Music user tokens have about the same lifetime.
    public static readonly int LifetimeMonths = 6;

    public string Service { get; set; }

    // The service's id and display name for the connected account
    public string AccountId { get; set; }
    public string AccountName { get; set; }

    // Space-separated, as the service granted them
    public string Scopes { get; set; }

    public string ProtectedRefreshToken { get; set; }

    // The start of the expiry clock
    public DateTimeOffset AuthorizedAt { get; set; }

    // The site user who connected the account
    public string AuthorizedBy { get; set; }

    // Set when the service rejects the refresh token; cleared by reconnecting
    public DateTimeOffset? InvalidatedAt { get; set; }
    public string InvalidReason { get; set; }

    public DateTimeOffset? LastAlertSent { get; set; }

    public DateTimeOffset ExpiresAt => AuthorizedAt.AddMonths(LifetimeMonths);

    public bool IsValid => InvalidatedAt == null && !string.IsNullOrEmpty(ProtectedRefreshToken);
}
