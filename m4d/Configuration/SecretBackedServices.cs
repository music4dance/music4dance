using AspNet.Security.OAuth.Spotify;

using m4d.Services.ServiceHealth;

using Microsoft.AspNetCore.Authentication.Facebook;
using Microsoft.AspNetCore.Authentication.Google;

namespace m4d.Configuration;

/// <summary>
/// The services whose credentials come from App Configuration (and its Key Vault references) in
/// Azure. Each one reads its credentials from <see cref="IConfiguration"/> when it's used rather
/// than once at registration, so when App Configuration fails to load at startup and recovers
/// later (see AppConfigurationRecoveryService), they start working without a restart. This class
/// is the one place that knows which configuration keys each service needs.
/// </summary>
public static class SecretBackedServices
{
    public const string EmailConnectionStringKey = "Authentication:AzureCommunicationServices:ConnectionString";
    public const string ReCaptchaSiteKeyKey = "Authentication:reCAPTCHA:SiteKey";
    public const string ReCaptchaSecretKeyKey = "Authentication:reCAPTCHA:SecretKey";

    public record ExternalLoginProvider(
        string Scheme, string ServiceName, string DisplayName, string ClientIdKey, string ClientSecretKey);

    public static readonly IReadOnlyList<ExternalLoginProvider> ExternalLoginProviders =
    [
        new(GoogleDefaults.AuthenticationScheme, "GoogleOAuth", "Google",
            "Authentication:Google:ClientId", "Authentication:Google:ClientSecret"),
        new(FacebookDefaults.AuthenticationScheme, "FacebookOAuth", "Facebook",
            "Authentication:Facebook:ClientId", "Authentication:Facebook:ClientSecret"),
        new(SpotifyAuthenticationDefaults.AuthenticationScheme, "SpotifyOAuth", "Spotify",
            "Authentication:Spotify:ClientId", "Authentication:Spotify:ClientSecret"),
    ];

    private record Requirement(string ServiceName, string Problem, string[] Keys);

    private static readonly IReadOnlyList<Requirement> Requirements =
    [
        new("EmailService", "Azure Communication Services connection string not configured",
            [EmailConnectionStringKey]),
        new("ReCaptcha", "reCAPTCHA SiteKey or SecretKey not configured",
            [ReCaptchaSiteKeyKey, ReCaptchaSecretKeyKey]),
        .. ExternalLoginProviders.Select(p => new Requirement(
            p.ServiceName, $"{p.DisplayName} ClientId or ClientSecret not configured",
            [p.ClientIdKey, p.ClientSecretKey])),
    ];

    public static bool IsEmailConfigured(IConfiguration configuration) =>
        HasAll(configuration, EmailConnectionStringKey);

    public static bool IsReCaptchaConfigured(IConfiguration configuration) =>
        HasAll(configuration, ReCaptchaSiteKeyKey, ReCaptchaSecretKeyKey);

    public static bool IsExternalLoginConfigured(IConfiguration configuration, string scheme) =>
        ExternalLoginProviders
            .Where(p => p.Scheme == scheme)
            .Any(p => HasAll(configuration, p.ClientIdKey, p.ClientSecretKey));

    /// <summary>
    /// Marks each secret-backed service healthy when its credentials are present. With
    /// <paramref name="markMissing"/> (startup) a service whose credentials are missing is marked
    /// unavailable; without it (recovery) a missing one is left as it is. Returns the names of
    /// the services that are still missing credentials.
    /// </summary>
    public static IReadOnlyList<string> UpdateHealth(
        IConfiguration configuration, ServiceHealthManager serviceHealth, bool markMissing)
    {
        var missing = Requirements.Where(r => !HasAll(configuration, r.Keys)).ToList();

        foreach (var requirement in Requirements.Except(missing))
        {
            serviceHealth.MarkHealthy(requirement.ServiceName);
        }

        if (markMissing)
        {
            foreach (var requirement in missing)
            {
                serviceHealth.MarkUnavailable(
                    requirement.ServiceName, $"InvalidOperationException: {requirement.Problem}");
                Console.WriteLine($"WARNING: {requirement.ServiceName} not configured: {requirement.Problem}");
            }
        }

        return missing.Select(r => r.ServiceName).ToList();
    }

    private static bool HasAll(IConfiguration configuration, params string[] keys) =>
        keys.All(key => !string.IsNullOrEmpty(configuration[key]));
}
