using System.Security.Claims;

using m4d.PublicApi;
using m4d.Utilities;

using m4dModels;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using Moq;

using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;
using OpenIddict.Server;
using OpenIddict.Validation.AspNetCore;

using static OpenIddict.Abstractions.OpenIddictConstants;

namespace m4d.Tests.Configuration;

[TestClass]
public class PublicApiFoundationTests
{
    private const string ExistingScheme = "ExistingScheme";

    [TestMethod]
    public void Disabled_DoesNotChangeServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton<object>();
        var count = services.Count;

        var result = services.AddPublicApiFoundation(
            CreateConfiguration(enabled: false, useProductionDatabase: true),
            CreateEnvironment(Environments.Production));

        Assert.AreSame(services, result);
        Assert.AreEqual(count, services.Count);
    }

    [TestMethod]
    public void EnabledOutsideDevelopment_Throws()
    {
        var services = new ServiceCollection();

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            services.AddPublicApiFoundation(
                CreateConfiguration(enabled: true),
                CreateEnvironment(Environments.Production)));
    }

    [TestMethod]
    public void EnabledWithProductionDatabase_Throws()
    {
        var services = new ServiceCollection();

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            services.AddPublicApiFoundation(
                CreateConfiguration(enabled: true, useProductionDatabase: true),
                CreateEnvironment(Environments.Development)));
    }

    [TestMethod]
    public async Task Enabled_RegistersBearerPolicyWithoutChangingExistingDefaults()
    {
        var services = CreateEnabledServices();
        await using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

        var schemeProvider = provider.GetRequiredService<IAuthenticationSchemeProvider>();
        Assert.IsNotNull(await schemeProvider.GetSchemeAsync(PublicApiDefaults.BearerScheme));

        var authentication = provider.GetRequiredService<IOptions<AuthenticationOptions>>().Value;
        Assert.AreEqual(ExistingScheme, authentication.DefaultAuthenticateScheme);
        Assert.AreEqual(ExistingScheme, authentication.DefaultChallengeScheme);

        var authorization = provider.GetRequiredService<IOptions<AuthorizationOptions>>().Value;
        var policy = authorization.GetPolicy(PublicApiDefaults.SubscriberPolicy);
        Assert.IsNotNull(policy);
        CollectionAssert.AreEquivalent(
            new[] { PublicApiDefaults.BearerScheme },
            policy.AuthenticationSchemes.ToArray());
        Assert.HasCount(1, policy.Requirements.OfType<DenyAnonymousAuthorizationRequirement>());
        Assert.HasCount(1, policy.Requirements.OfType<SubscriberEntitlementRequirement>());

        using var scope = provider.CreateScope();
        Assert.IsNotNull(scope.ServiceProvider.GetService<IOpenIddictApplicationManager>());
    }

    [TestMethod]
    public void Enabled_ConfiguresTheRequiredGrantsScopesAndEndpoints()
    {
        var services = CreateEnabledServices();
        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<OpenIddictServerOptions>>().Value;
        CollectionAssert.AreEquivalent(
            new[] { GrantTypes.AuthorizationCode, GrantTypes.RefreshToken },
            options.GrantTypes.ToArray());
        CollectionAssert.AreEquivalent(
            new[]
            {
                Scopes.OfflineAccess,
                PublicApiDefaults.Scopes.AccountRead,
                PublicApiDefaults.Scopes.SongsRead
            },
            options.Scopes.ToArray(),
            $"Configured scopes: {string.Join(", ", options.Scopes)}");
        Assert.IsTrue(options.RequireProofKeyForCodeExchange);
        Assert.IsTrue(options.UseReferenceAccessTokens);
        Assert.AreEqual(TimeSpan.FromMinutes(1), options.AuthorizationCodeLifetime);
        Assert.AreEqual(TimeSpan.FromHours(1), options.AccessTokenLifetime);
        CollectionAssert.AreEquivalent(
            new[] { CodeChallengeMethods.Sha256 },
            options.CodeChallengeMethods.ToArray());
        Assert.AreEqual(
            PublicApiDefaults.Endpoints.Authorization,
            options.AuthorizationEndpointUris.Single().OriginalString);
        Assert.AreEqual(
            PublicApiDefaults.Endpoints.Revocation,
            options.RevocationEndpointUris.Single().OriginalString);
        Assert.AreEqual(
            PublicApiDefaults.Endpoints.Token,
            options.TokenEndpointUris.Single().OriginalString);

        var aspNetCore = provider
            .GetRequiredService<IOptions<OpenIddictValidationAspNetCoreOptions>>()
            .Value;
        Assert.IsFalse(aspNetCore.DisableAccessTokenExtractionFromAuthorizationHeader);
        Assert.IsTrue(aspNetCore.DisableAccessTokenExtractionFromBodyForm);
        Assert.IsTrue(aspNetCore.DisableAccessTokenExtractionFromQueryString);
    }

    [TestMethod]
    public void DanzQDescriptor_IsARestrictedPkcePublicClient()
    {
        AssertDanzQDescriptor(DanzQClient.CreateDescriptor());
    }

    [TestMethod]
    public async Task DanzQInitializer_CreatesAndRepairsRegistration()
    {
        var services = CreateEnabledServices();
        await using var provider = services.BuildServiceProvider();
        var initializer = provider.GetServices<IHostedService>()
            .OfType<DanzQClientInitializer>()
            .Single();

        await initializer.StartAsync(CancellationToken.None);

        using (var setupScope = provider.CreateScope())
        {
            var setupManager = setupScope.ServiceProvider
                .GetRequiredService<IOpenIddictApplicationManager>();
            var setupApplication = await setupManager.FindByClientIdAsync(
                PublicApiDefaults.Clients.DanzQ,
                CancellationToken.None);
            Assert.IsNotNull(setupApplication);

            var stale = new OpenIddictApplicationDescriptor
            {
                ClientId = PublicApiDefaults.Clients.DanzQ,
                ClientType = ClientTypes.Public,
                DisplayName = "Stale DanzQ registration"
            };
            stale.RedirectUris.Add(new Uri("com.example.danzq:/old-callback"));
            await setupManager.UpdateAsync(setupApplication, stale, CancellationToken.None);
        }

        await initializer.StartAsync(CancellationToken.None);
        await initializer.StartAsync(CancellationToken.None);

        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DanceMusicContext>();
        Assert.AreEqual(
            1,
            await context.Set<OpenIddictEntityFrameworkCoreApplication>().CountAsync());

        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var application = await manager.FindByClientIdAsync(
            PublicApiDefaults.Clients.DanzQ,
            CancellationToken.None);
        Assert.IsNotNull(application);

        var descriptor = new OpenIddictApplicationDescriptor();
        await manager.PopulateAsync(descriptor, application, CancellationToken.None);
        AssertDanzQDescriptor(descriptor);
    }

    [TestMethod]
    public async Task SubscriberPolicy_FailsClosedWithoutEntitlementHandler()
    {
        var services = CreateEnabledServices();
        await using var provider = services.BuildServiceProvider();
        var authorization = provider.GetRequiredService<IAuthorizationService>();
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "subscriber")],
            PublicApiDefaults.BearerScheme));

        var result = await authorization.AuthorizeAsync(
            user,
            resource: null,
            PublicApiDefaults.SubscriberPolicy);

        Assert.IsFalse(result.Succeeded);
    }

    private static void AssertDanzQDescriptor(OpenIddictApplicationDescriptor descriptor)
    {
        Assert.AreEqual(PublicApiDefaults.Clients.DanzQ, descriptor.ClientId);
        Assert.AreEqual(PublicApiDefaults.Clients.DanzQDisplayName, descriptor.DisplayName);
        Assert.AreEqual(ApplicationTypes.Native, descriptor.ApplicationType);
        Assert.AreEqual(ClientTypes.Public, descriptor.ClientType);
        Assert.AreEqual(ConsentTypes.Explicit, descriptor.ConsentType);
        Assert.IsNull(descriptor.ClientSecret);
        Assert.IsTrue(descriptor.RedirectUris.SetEquals(
            [new Uri(PublicApiDefaults.Clients.DanzQRedirectUri)]));
        Assert.IsTrue(descriptor.Permissions.SetEquals(
        [
            Permissions.Endpoints.Authorization,
            Permissions.Endpoints.Revocation,
            Permissions.Endpoints.Token,
            Permissions.GrantTypes.AuthorizationCode,
            Permissions.GrantTypes.RefreshToken,
            Permissions.ResponseTypes.Code,
            Permissions.Prefixes.Scope + PublicApiDefaults.Scopes.AccountRead,
            Permissions.Prefixes.Scope + PublicApiDefaults.Scopes.SongsRead
        ]));
        Assert.IsTrue(descriptor.Requirements.SetEquals(
            [Requirements.Features.ProofKeyForCodeExchange]));
    }

    private static ServiceCollection CreateEnabledServices()
    {
        var services = new ServiceCollection();
        var databaseName = $"public-api-{Guid.NewGuid()}";
        services.AddLogging();
        services.AddRouting();
        services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = ExistingScheme;
                options.DefaultChallengeScheme = ExistingScheme;
            })
            .AddCookie(ExistingScheme);
        services.AddDbContext<DanceMusicContext>(options =>
            options.UseInMemoryDatabase(databaseName));
        services.AddPublicApiFoundation(
            CreateConfiguration(enabled: true),
            CreateEnvironment(Environments.Development));
        return services;
    }

    private static IConfiguration CreateConfiguration(
        bool enabled,
        bool useProductionDatabase = false) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"FeatureManagement:{FeatureFlags.PublicApi}"] = enabled.ToString(),
                ["PROD_DB"] = useProductionDatabase.ToString()
            })
            .Build();

    private static IHostEnvironment CreateEnvironment(string name)
    {
        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(value => value.EnvironmentName).Returns(name);
        return environment.Object;
    }
}
