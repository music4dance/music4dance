using m4d.Services.ServiceHealth;
using Microsoft.AspNetCore.Authentication;

namespace m4d.Configuration;

/// <summary>
/// Extension methods for configuring authentication providers with resilience
/// Validates credentials before registering authentication handlers to prevent
/// runtime exceptions when middleware initializes
/// </summary>
public static class AuthenticationBuilderExtensions
{
    /// <summary>
    /// Configure Google OAuth authentication with resilience
    /// </summary>
    public static AuthenticationBuilder AddGoogleWithResilience(
        this AuthenticationBuilder authBuilder,
        IConfiguration configuration,
        ServiceHealthManager serviceHealth)
    {
        try
        {
            var googleAuthNSection = configuration.GetSection("Authentication:Google");
            var googleClientId = googleAuthNSection["ClientId"];
            var googleClientSecret = googleAuthNSection["ClientSecret"];

            if (string.IsNullOrEmpty(googleClientId) || string.IsNullOrEmpty(googleClientSecret))
            {
                throw new InvalidOperationException("Google ClientId or ClientSecret not configured");
            }

            authBuilder.AddGoogle(options =>
            {
                options.ClientId = googleClientId;
                options.ClientSecret = googleClientSecret;
            });

            serviceHealth.MarkHealthy("GoogleOAuth");
        }
        catch (Exception ex)
        {
            serviceHealth.MarkUnavailable("GoogleOAuth", $"{ex.GetType().Name}: {ex.Message}");
            Console.WriteLine($"WARNING: Google OAuth not configured: {ex.Message}");
        }

        return authBuilder;
    }

    /// <summary>
    /// Configure Facebook OAuth authentication with resilience
    /// </summary>
    public static AuthenticationBuilder AddFacebookWithResilience(
        this AuthenticationBuilder authBuilder,
        IConfiguration configuration,
        ServiceHealthManager serviceHealth)
    {
        try
        {
            var facebookAppId = configuration["Authentication:Facebook:ClientId"];
            var facebookAppSecret = configuration["Authentication:Facebook:ClientSecret"];

            if (string.IsNullOrEmpty(facebookAppId) || string.IsNullOrEmpty(facebookAppSecret))
            {
                throw new InvalidOperationException("Facebook AppId or AppSecret not configured");
            }

            authBuilder.AddFacebook(options =>
            {
                options.AppId = facebookAppId;
                options.AppSecret = facebookAppSecret;
                options.Scope.Add("email");
                options.Fields.Add("name");
                options.Fields.Add("email");
            });

            serviceHealth.MarkHealthy("FacebookOAuth");
        }
        catch (Exception ex)
        {
            serviceHealth.MarkUnavailable("FacebookOAuth", $"{ex.GetType().Name}: {ex.Message}");
            Console.WriteLine($"WARNING: Facebook OAuth not configured: {ex.Message}");
        }

        return authBuilder;
    }

    /// <summary>
    /// Configure Spotify OAuth authentication with resilience
    /// </summary>
    public static AuthenticationBuilder AddSpotifyWithResilience(
        this AuthenticationBuilder authBuilder,
        IConfiguration configuration,
        ServiceHealthManager serviceHealth)
    {
        try
        {
            var spotifyClientId = configuration["Authentication:Spotify:ClientId"];
            var spotifyClientSecret = configuration["Authentication:Spotify:ClientSecret"];

            if (string.IsNullOrEmpty(spotifyClientId) || string.IsNullOrEmpty(spotifyClientSecret))
            {
                throw new InvalidOperationException("Spotify ClientId or ClientSecret not configured");
            }

            authBuilder.AddSpotify(options =>
            {
                options.ClientId = spotifyClientId;
                options.ClientSecret = spotifyClientSecret;

                options.Scope.Add("user-read-email");
                options.Scope.Add("playlist-modify-public");
                options.Scope.Add("ugc-image-upload");
                //options.Scope.Add("user-read-playback-state");
                //options.Scope.Add("user-read-playback-position");

                //options.ClaimActions.MapJsonKey("urn:spotify:url", "uri", "url");
                //options.ClaimActions.MapJsonKey("urn:spotify:id", "id", "id");

                options.SaveTokens = true;

                options.Events.OnCreatingTicket = cxt =>
                {
                    var tokens = cxt.Properties.GetTokens().ToList();
                    cxt.Properties.StoreTokens(tokens);

                    return Task.CompletedTask;
                };
            });

            serviceHealth.MarkHealthy("SpotifyOAuth");
        }
        catch (Exception ex)
        {
            serviceHealth.MarkUnavailable("SpotifyOAuth", $"{ex.GetType().Name}: {ex.Message}");
            Console.WriteLine($"WARNING: Spotify OAuth not configured: {ex.Message}");
        }

        return authBuilder;
    }
}
