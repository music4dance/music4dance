using m4d.Utilities;

using Microsoft.AspNetCore.Authorization;

using static OpenIddict.Abstractions.OpenIddictConstants;

namespace m4d.PublicApi;

public static class PublicApiServiceCollectionExtensions
{
    public static IServiceCollection AddPublicApiFoundation(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        if (!configuration.GetValue(
                $"FeatureManagement:{FeatureFlags.PublicApi}", false))
        {
            return services;
        }

        if (!environment.IsDevelopment())
        {
            throw new InvalidOperationException(
                "The public API cannot be enabled until production signing keys are configured.");
        }

        services.AddOpenIddict()
            .AddCore(options =>
            {
                options.UseEntityFrameworkCore()
                    .UseDbContext<DanceMusicContext>();
            })
            .AddServer(options =>
            {
                options.AllowAuthorizationCodeFlow()
                    .AllowRefreshTokenFlow()
                    .RequireProofKeyForCodeExchange()
                    .SetAuthorizationEndpointUris(PublicApiDefaults.Endpoints.Authorization)
                    .SetRevocationEndpointUris(PublicApiDefaults.Endpoints.Revocation)
                    .SetTokenEndpointUris(PublicApiDefaults.Endpoints.Token)
                    .UseReferenceAccessTokens()
                    .RegisterScopes(
                        PublicApiDefaults.Scopes.AccountRead,
                        PublicApiDefaults.Scopes.SongsRead)
                    .AddEphemeralEncryptionKey()
                    .AddEphemeralSigningKey();
                options.Configure(server =>
                {
                    server.CodeChallengeMethods.Clear();
                    server.CodeChallengeMethods.Add(CodeChallengeMethods.Sha256);
                });
            })
            .AddValidation(options =>
            {
                options.UseLocalServer();
                options.UseAspNetCore();
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy(
                PublicApiDefaults.SubscriberPolicy,
                policy => policy
                    .AddAuthenticationSchemes(PublicApiDefaults.BearerScheme)
                    .RequireAuthenticatedUser()
                    .AddRequirements(new SubscriberEntitlementRequirement()));
        });
        services.AddHostedService<DanzQClientInitializer>();

        return services;
    }
}
