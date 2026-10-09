using m4d.Services.ServiceHealth;
using m4d.Utilities;

namespace m4d.Services;

/// <summary>
/// Watches the Spotify service account's connection. Spotify refresh tokens expire six months
/// after the account is connected, so starting <see cref="WarningWindow"/> before that, and as soon
/// as Spotify rejects a refresh, this logs a warning (for Application Insights) and emails the
/// admins at most once a day. Runs from the songstats recompute and after each service-account
/// playlist batch.
/// </summary>
public class ServiceAccountMonitor(
    IConfiguration configuration, IServiceAccountTokenStore store,
    ServiceHealthManager serviceHealth, ILogger<ServiceAccountMonitor> logger)
{
    public static readonly TimeSpan WarningWindow = TimeSpan.FromDays(14);
    private static readonly TimeSpan AlertInterval = TimeSpan.FromDays(1);

    public ServiceAccountPrincipal SpotifyPrincipal => new(ServiceType.Spotify, store);

    /// <summary>
    /// Check the Spotify connection, alerting if it needs attention. Returns the problem, or null
    /// when the account is connected and not close to expiring (or was never connected).
    /// </summary>
    public async Task<string> CheckSpotify(DateTimeOffset? now = null)
    {
        var token = await store.Get(ServiceType.Spotify);
        if (token == null)
        {
            // Never connected: nothing to warn about, and UpdateBatch reports it if it's asked
            // to write
            return null;
        }

        if (token.IsValid)
        {
            // Make sure Spotify still accepts the refresh token. It's only exchanged when there's
            // no cached access token (about once an hour), and a rejection marks the token
            // invalid in the store.
            try
            {
                _ = await AdmAuthentication.GetServiceAuthorization(
                    configuration, ServiceType.Spotify, SpotifyPrincipal);
            }
            catch (SpotifyAuthExpiredException)
            {
                token = await store.Get(ServiceType.Spotify);
            }
            catch (Exception e)
            {
                // A network problem isn't a reason to ask for a reconnect
                logger.LogWarning(e, "Unable to check the Spotify service account");
            }
        }

        var current = now ?? DateTimeOffset.UtcNow;
        var problem = Problem(token, current);
        if (problem == null)
        {
            return null;
        }

        logger.LogWarning("Spotify service account needs attention: {Problem}", problem);

        if (!ShouldAlert(token, current))
        {
            return problem;
        }

        var sent = serviceHealth != null && await serviceHealth.SendNotificationAsync(
            HealthNotificationKind.ActionNeeded, "Reconnect the Spotify service account",
            $"{problem} Until it's reconnected, the scheduled SpotifyFromSearch playlist refreshes " +
            "fail. Sign in to the site as an admin, open /Admin/SpotifyServiceAccount, and choose " +
            "Connect; in Spotify's dialog, sign in as the music4dance account.");
        if (sent)
        {
            await store.MarkAlertSent(ServiceType.Spotify);
        }

        return problem;
    }

    internal static string Problem(ServiceAccountToken token, DateTimeOffset now)
    {
        if (!token.IsValid)
        {
            return $"Spotify rejected the service account's refresh token on {token.InvalidatedAt:yyyy-MM-dd} " +
                $"({token.InvalidReason}).";
        }

        if (now >= token.ExpiresAt)
        {
            return $"The Spotify service account's connection expired on {token.ExpiresAt:yyyy-MM-dd}.";
        }

        if (now >= token.ExpiresAt - WarningWindow)
        {
            var days = (int)Math.Ceiling((token.ExpiresAt - now).TotalDays);
            return $"The Spotify service account's connection expires on {token.ExpiresAt:yyyy-MM-dd} " +
                $"(in {days} day{(days == 1 ? "" : "s")}).";
        }

        return null;
    }

    // Once a day while the problem lasts, and right away when a refresh is first rejected
    internal static bool ShouldAlert(ServiceAccountToken token, DateTimeOffset now)
    {
        if (token.LastAlertSent == null)
        {
            return true;
        }

        if (token.InvalidatedAt != null && token.LastAlertSent < token.InvalidatedAt)
        {
            return true;
        }

        return now - token.LastAlertSent >= AlertInterval;
    }
}
