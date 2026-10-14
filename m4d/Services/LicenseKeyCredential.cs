using m4d.Services.ServiceHealth;

using Microsoft.IdentityModel.JsonWebTokens;

namespace m4d.Services;

/// <summary>
/// A commercial license key issued as a JWT (today AutoMapper's, from Lucky Penny Software). The
/// expiry comes from the key's exp claim, so renewing the key resets the clock with nothing else
/// to update. Starting <see cref="WarningWindow"/> before it expires, this logs a warning and emails
/// the admins at most once a day. The last alert time is kept in memory, so a restart can cost an
/// extra email.
/// </summary>
public class LicenseKeyCredential(
    string name, string configKey, string remedy, IConfiguration configuration,
    ServiceHealthManager serviceHealth, ILogger logger) : IExpiringCredential
{
    public static readonly TimeSpan WarningWindow = TimeSpan.FromDays(30);
    private static readonly TimeSpan AlertInterval = TimeSpan.FromDays(1);

    private DateTimeOffset? _lastAlertSent;

    public string Name { get; } = name;
    public string ConfigKey { get; } = configKey;

    public Task<DateTimeOffset?> GetExpiry()
    {
        return Task.FromResult(ReadExpiry(configuration[ConfigKey]));
    }

    public async Task<string> Check(DateTimeOffset? now = null)
    {
        var current = now ?? DateTimeOffset.UtcNow;
        var problem = Problem(Name, ConfigKey, configuration[ConfigKey], current);
        if (problem == null)
        {
            return null;
        }

        logger.LogWarning("{Credential} needs attention: {Problem}", Name, problem);

        if (_lastAlertSent != null && current - _lastAlertSent < AlertInterval)
        {
            return problem;
        }

        var sent = serviceHealth != null && await serviceHealth.SendNotificationAsync(
            HealthNotificationKind.ActionNeeded, $"Renew the {Name}", $"{problem} {remedy}");
        if (sent)
        {
            _lastAlertSent = current;
        }

        return problem;
    }

    internal static DateTimeOffset? ReadExpiry(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        try
        {
            // Lucky Penny writes exp as a string of Unix seconds rather than the number the JWT
            // spec calls for; the claim's Value reads either way
            var token = new JsonWebToken(key.Trim());
            return token.TryGetClaim("exp", out var exp) && long.TryParse(exp.Value, out var seconds)
                ? DateTimeOffset.FromUnixTimeSeconds(seconds)
                : null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    internal static string Problem(string name, string configKey, string key, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            // Unset everywhere but production (development, the sandbox, CI); the library runs
            // unlicensed and says so in its own log
            return null;
        }

        var expires = ReadExpiry(key);
        if (expires == null)
        {
            return $"The {name} in {configKey} isn't a key with an expiration date.";
        }

        if (now >= expires)
        {
            return $"The {name} expired on {expires:yyyy-MM-dd}.";
        }

        if (now >= expires - WarningWindow)
        {
            var days = (int)Math.Ceiling((expires.Value - now).TotalDays);
            return $"The {name} expires on {expires:yyyy-MM-dd} (in {days} day{(days == 1 ? "" : "s")}).";
        }

        return null;
    }
}
