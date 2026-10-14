namespace m4d.Services;

/// <summary>
/// A credential that expires and has to be renewed by hand: the Spotify service account's
/// connection, or a commercial license key. <see cref="CredentialExpiryMonitor"/> checks each one
/// on the songstats recompute; the credential logs and emails the admins when it needs attention.
/// </summary>
public interface IExpiringCredential
{
    string Name { get; }

    /// <summary>
    /// When the credential expires, or null when it isn't configured (or its expiry can't be read).
    /// </summary>
    Task<DateTimeOffset?> GetExpiry();

    /// <summary>
    /// Check the credential, alerting if it needs attention. Returns the problem, or null when
    /// there's nothing to do.
    /// </summary>
    Task<string> Check(DateTimeOffset? now = null);
}
