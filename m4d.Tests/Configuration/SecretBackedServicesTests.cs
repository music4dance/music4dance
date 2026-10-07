using m4d.Areas.Identity;
using m4d.Configuration;
using m4d.Services;
using m4d.Services.ServiceHealth;

using m4dModels;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Moq;

namespace m4d.Tests.Configuration;

// When App Configuration recovers after a failed startup load, its secrets arrive in
// IConfiguration while the app is running. These tests pin that each secret-backed service
// picks them up without a restart, and that an unconfigured login provider stays usable-safe
// (valid placeholder options, hidden from the login pages).
[TestClass]
public class SecretBackedServicesTests
{
    private const string GoogleId = "Authentication:Google:ClientId";
    private const string GoogleSecret = "Authentication:Google:ClientSecret";

    private static ConfigurationManager CreateConfiguration()
    {
        var configuration = new ConfigurationManager();
        _ = configuration.AddInMemoryCollection();
        return configuration;
    }

    // Simulates App Configuration's late load: set the values, then fire the reload token
    private static void ArriveLate(ConfigurationManager configuration, params (string key, string value)[] values)
    {
        foreach (var (key, value) in values)
        {
            configuration[key] = value;
        }
        ((IConfigurationRoot)configuration).Reload();
    }

    [TestMethod]
    public void GoogleOptions_UsePlaceholdersUntilCredentialsArrive()
    {
        var configuration = CreateConfiguration();
        var services = new ServiceCollection();
        _ = services.AddSingleton<IConfiguration>(configuration);
        _ = services.AddDataProtection();
        _ = services.AddAuthentication().AddGoogleWithResilience(configuration);
        using var provider = services.BuildServiceProvider();
        var monitor = provider.GetRequiredService<IOptionsMonitor<GoogleOptions>>();

        var before = monitor.Get(GoogleDefaults.AuthenticationScheme);
        before.Validate(GoogleDefaults.AuthenticationScheme); // must not throw: the middleware validates on every request
        Assert.AreEqual("unconfigured", before.ClientId);

        ArriveLate(configuration, (GoogleId, "real-id"), (GoogleSecret, "real-secret"));

        var after = monitor.Get(GoogleDefaults.AuthenticationScheme);
        Assert.AreEqual("real-id", after.ClientId);
        Assert.AreEqual("real-secret", after.ClientSecret);
    }

    [TestMethod]
    public void EmailSender_SwitchesFromNullOnceConnectionStringArrives()
    {
        var configuration = CreateConfiguration();
        var services = new ServiceCollection();
        _ = services.AddEmailSenderWithResilience(configuration);
        using var provider = services.BuildServiceProvider();

        Assert.IsInstanceOfType<NullEmailSender>(provider.GetRequiredService<IEmailSender>());

        ArriveLate(configuration,
            (SecretBackedServices.EmailConnectionStringKey, "endpoint=https://example.invalid/;accesskey=a2V5"));

        Assert.IsInstanceOfType<EmailSender>(provider.GetRequiredService<IEmailSender>());
    }

    [TestMethod]
    public void UpdateHealth_MarksMissingOnlyAtStartup()
    {
        var configuration = CreateConfiguration();
        var health = new ServiceHealthManager(NullLogger<ServiceHealthManager>.Instance);

        var missing = SecretBackedServices.UpdateHealth(configuration, health, markMissing: false);
        Assert.AreEqual(ServiceStatus.Unknown, health.GetServiceStatus("GoogleOAuth").Status);

        _ = SecretBackedServices.UpdateHealth(configuration, health, markMissing: true);
        Assert.AreEqual(ServiceStatus.Unavailable, health.GetServiceStatus("GoogleOAuth").Status);
        CollectionAssert.Contains(missing.ToList(), "GoogleOAuth");

        ArriveLate(configuration, (GoogleId, "real-id"), (GoogleSecret, "real-secret"));
        missing = SecretBackedServices.UpdateHealth(configuration, health, markMissing: false);
        Assert.AreEqual(ServiceStatus.Healthy, health.GetServiceStatus("GoogleOAuth").Status);
        CollectionAssert.DoesNotContain(missing.ToList(), "GoogleOAuth");
    }

    [TestMethod]
    public async Task SignInManager_HidesUnconfiguredProviders()
    {
        var configuration = CreateConfiguration();
        var schemes = new Mock<IAuthenticationSchemeProvider>();
        _ = schemes.Setup(s => s.GetAllSchemesAsync()).ReturnsAsync(
        [
            new AuthenticationScheme(GoogleDefaults.AuthenticationScheme, "Google", typeof(GoogleHandler)),
            new AuthenticationScheme("Other", "Other", typeof(GoogleHandler)),
        ]);

#pragma warning disable CS8625 // UserManager's optional dependencies aren't used here
        var userManager = new Mock<UserManager<ApplicationUser>>(
            Mock.Of<IUserStore<ApplicationUser>>(), null, null, null, null, null, null, null, null);
#pragma warning restore CS8625
        var signInManager = new M4dSignInManager(
            userManager.Object,
            Mock.Of<IHttpContextAccessor>(),
            Mock.Of<IUserClaimsPrincipalFactory<ApplicationUser>>(),
            Options.Create(new IdentityOptions()),
            NullLogger<SignInManager<ApplicationUser>>.Instance,
            schemes.Object,
            Mock.Of<IUserConfirmation<ApplicationUser>>(),
            configuration);

        var before = (await signInManager.GetExternalAuthenticationSchemesAsync()).Select(s => s.Name).ToList();
        CollectionAssert.AreEquivalent(new[] { "Other" }, before);

        ArriveLate(configuration, (GoogleId, "real-id"), (GoogleSecret, "real-secret"));

        var after = (await signInManager.GetExternalAuthenticationSchemesAsync()).Select(s => s.Name).ToList();
        CollectionAssert.AreEquivalent(new[] { GoogleDefaults.AuthenticationScheme, "Other" }, after);
    }
}
