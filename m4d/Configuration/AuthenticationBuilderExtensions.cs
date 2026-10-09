using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace m4d.Configuration;

/// <summary>
/// Extension methods for configuring authentication providers with resilience.
///
/// Every provider is always registered, and its client id and secret are read from configuration
/// when its options are built, not captured at registration. A ConfigurationChangeTokenSource
/// rebuilds the options whenever configuration reloads, so credentials that arrive late (App
/// Configuration recovering after a failed startup load) take effect without a restart.
///
/// While credentials are missing the options get placeholder values: the authentication
/// middleware initializes every remote handler on every request and OAuthOptions.Validate throws
/// on an empty client id, which would fail the whole site. An unconfigured provider is hidden by
/// M4dSignInManager and refuses to redirect to the provider (see GuardUnconfigured).
/// </summary>
public static class AuthenticationBuilderExtensions
{
    private const string Unconfigured = "unconfigured";

    /// <summary>
    /// Set on the AuthenticationProperties of a Spotify challenge that connects the music4dance
    /// service account (AdminController.ConnectSpotifyServiceAccount) rather than signing in.
    /// It makes Spotify show its account dialog, so the admin can switch to the music4dance
    /// account, and records the granted scopes in <see cref="GrantedScopesItem"/>.
    /// </summary>
    public const string ServiceAccountItem = "m4d:service-account";

    public const string GrantedScopesItem = "m4d:granted-scopes";

    /// <summary>
    /// Configure Google OAuth authentication with resilience
    /// </summary>
    public static AuthenticationBuilder AddGoogleWithResilience(
        this AuthenticationBuilder authBuilder,
        IConfiguration configuration)
    {
        var provider = Provider("GoogleOAuth");
        authBuilder.AddGoogle(options =>
        {
            ApplyCredentials(options, configuration, provider);
        });
        return TrackConfigurationChanges<Microsoft.AspNetCore.Authentication.Google.GoogleOptions>(
            authBuilder, configuration, provider);
    }

    /// <summary>
    /// Configure Facebook OAuth authentication with resilience
    /// </summary>
    public static AuthenticationBuilder AddFacebookWithResilience(
        this AuthenticationBuilder authBuilder,
        IConfiguration configuration)
    {
        var provider = Provider("FacebookOAuth");
        authBuilder.AddFacebook(options =>
        {
            ApplyCredentials(options, configuration, provider);
            options.Scope.Add("email");
            options.Fields.Add("name");
            options.Fields.Add("email");
        });
        return TrackConfigurationChanges<Microsoft.AspNetCore.Authentication.Facebook.FacebookOptions>(
            authBuilder, configuration, provider);
    }

    /// <summary>
    /// Configure Spotify OAuth authentication with resilience
    /// </summary>
    public static AuthenticationBuilder AddSpotifyWithResilience(
        this AuthenticationBuilder authBuilder,
        IConfiguration configuration)
    {
        var provider = Provider("SpotifyOAuth");
        authBuilder.AddSpotify(options =>
        {
            ApplyCredentials(options, configuration, provider);

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

                if (cxt.Properties.Items.ContainsKey(ServiceAccountItem) &&
                    cxt.TokenResponse.Response?.RootElement.TryGetProperty("scope", out var scope) == true)
                {
                    cxt.Properties.Items[GrantedScopesItem] = scope.GetString();
                }

                return Task.CompletedTask;
            };

            var redirect = options.Events.OnRedirectToAuthorizationEndpoint;
            options.Events.OnRedirectToAuthorizationEndpoint = context =>
            {
                if (context.Properties.Items.ContainsKey(ServiceAccountItem))
                {
                    context.RedirectUri = QueryHelpers.AddQueryString(
                        context.RedirectUri, "show_dialog", "true");
                }

                return redirect(context);
            };
        });
        return TrackConfigurationChanges<AspNet.Security.OAuth.Spotify.SpotifyAuthenticationOptions>(
            authBuilder, configuration, provider);
    }

    private static SecretBackedServices.ExternalLoginProvider Provider(string serviceName) =>
        SecretBackedServices.ExternalLoginProviders.Single(p => p.ServiceName == serviceName);

    private static void ApplyCredentials(
        OAuthOptions options, IConfiguration configuration,
        SecretBackedServices.ExternalLoginProvider provider)
    {
        var clientId = configuration[provider.ClientIdKey];
        var clientSecret = configuration[provider.ClientSecretKey];
        var configured = !string.IsNullOrEmpty(clientId) && !string.IsNullOrEmpty(clientSecret);

        options.ClientId = configured ? clientId : Unconfigured;
        options.ClientSecret = configured ? clientSecret : Unconfigured;
        GuardUnconfigured(options.Events, configured);
    }

    /// <summary>
    /// Backstop for a crafted request to an unconfigured provider (the login pages don't list
    /// it): send the user back to the login page instead of to the provider with a placeholder
    /// client id. Otherwise this is the default OAuthEvents behavior.
    /// </summary>
    private static void GuardUnconfigured(OAuthEvents events, bool configured)
    {
        events.OnRedirectToAuthorizationEndpoint = context =>
        {
            context.Response.Redirect(configured ? context.RedirectUri : "/Identity/Account/Login");
            return Task.CompletedTask;
        };
    }

    private static AuthenticationBuilder TrackConfigurationChanges<TOptions>(
        AuthenticationBuilder authBuilder, IConfiguration configuration,
        SecretBackedServices.ExternalLoginProvider provider)
    {
        authBuilder.Services.AddSingleton<IOptionsChangeTokenSource<TOptions>>(
            new ConfigurationChangeTokenSource<TOptions>(provider.Scheme, configuration));
        return authBuilder;
    }
}
