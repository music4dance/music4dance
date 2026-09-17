using m4d.Utilities;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

using OpenIddict.Server;

using static OpenIddict.Abstractions.OpenIddictConstants;
using static OpenIddict.Server.OpenIddictServerEvents;

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

        if (environment.IsProduction())
        {
            throw new InvalidOperationException(
                "The public API cannot be enabled until durable signing and encryption keys are configured.");
        }

        if (configuration.GetValue<bool>("PROD_DB"))
        {
            throw new InvalidOperationException(
                "The public API cannot be enabled while PROD_DB is set.");
        }

        var requestsPerMinute = configuration.GetValue("PublicApi:RequestsPerMinute", 60);
        if (requestsPerMinute <= 0)
        {
            throw new InvalidOperationException("PublicApi:RequestsPerMinute must be positive.");
        }
        services.AddSingleton(new PublicApiOptions(requestsPerMinute));
        services.AddScoped<PublicApiUserService>();
        services.AddPublicApiRateLimiting(requestsPerMinute);
        services.AddLogging(logging => logging
            .AddFilter("OpenIddict", LogLevel.Warning)
            .AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.Warning));

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
                    .SetAuthorizationCodeLifetime(TimeSpan.FromMinutes(1))
                    .SetAccessTokenLifetime(TimeSpan.FromHours(1))
                    .SetRefreshTokenLifetime(TimeSpan.FromDays(30))
                    .SetRefreshTokenReuseLeeway(null)
                    .EnableAuthorizationRequestCaching()
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
                    server.Scopes.Remove(Scopes.OpenId);
                    server.RequestTokenLifetime = TimeSpan.FromMinutes(10);
                    server.PromptValues.IntersectWith([PromptValues.Consent, PromptValues.None]);
                });
                options.UseAspNetCore()
                    .EnableAuthorizationEndpointPassthrough()
                    .EnableTokenEndpointPassthrough();
                options.AddEventHandler<ValidateTokenContext>(builder => builder
                    .UseScopedHandler<RevokeReplayedAuthorization>()
                    .SetOrder(OpenIddictServerHandlers.Protection.ValidateTokenEntry.Descriptor.Order - 500));
                options.AddEventHandler<ValidateAuthorizationRequestContext>(builder => builder
                    .UseInlineHandler(context =>
                    {
                        if (string.IsNullOrEmpty(context.Request.State))
                        {
                            context.Reject(Errors.InvalidRequest, "The state parameter is required.");
                        }
                        return ValueTask.CompletedTask;
                    }).SetOrder(int.MaxValue - 1_000));
            })
            .AddValidation(options =>
            {
                options.UseLocalServer();
                options.AddAudiences(PublicApiDefaults.Resource);
                options.EnableTokenEntryValidation();
                options.EnableAuthorizationEntryValidation();
                options.UseAspNetCore()
                    .DisableAccessTokenExtractionFromBodyForm()
                    .DisableAccessTokenExtractionFromQueryString();
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy(PublicApiDefaults.BrowserPolicy, policy => policy
                .AddAuthenticationSchemes(IdentityConstants.ApplicationScheme)
                .RequireAuthenticatedUser());
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
