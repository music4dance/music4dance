using System.Net;
using System.Text;

using Azure.Core;
using Azure.Core.Pipeline;
using Azure.Identity;

using m4d.Configuration;
using m4d.Services;
using m4d.Services.ServiceHealth;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Moq;

namespace m4d.Tests.Services;

// A failed App Configuration load at startup used to leave the app without any of its secrets
// until someone restarted it. These tests pin the in-place recovery: the startup load leaves a
// refreshable provider in the chain even when it fails, and AppConfigurationRecoveryService keeps
// refreshing it - never restarting the app - and marks health once the data arrives.
[TestClass]
public class AppConfigurationRecoveryServiceTests
{
    private static (AppConfigurationRecoveryService service, Mock<IConfigurationRefresher> refresher)
        CreateService(ServiceHealthManager serviceHealth, IConfiguration configuration, Func<bool> hasLoaded)
    {
        var refresher = new Mock<IConfigurationRefresher>();
        var provider = new Mock<IConfigurationRefresherProvider>();
        _ = provider.Setup(p => p.Refreshers).Returns([refresher.Object]);

        var service = new AppConfigurationRecoveryService(
            NullLogger<AppConfigurationRecoveryService>.Instance, serviceHealth, configuration, provider.Object)
        {
            RetryInterval = TimeSpan.FromMilliseconds(10),
            HasLoaded = hasLoaded
        };
        return (service, refresher);
    }

    private static ServiceHealthManager CreateHealth() =>
        new(NullLogger<ServiceHealthManager>.Instance);

    [TestMethod]
    public async Task ExitsAtOnce_WhenStartupLoadSucceeded()
    {
        var health = CreateHealth();
        var (service, refresher) = CreateService(health, new ConfigurationBuilder().Build(), () => true);

        await service.StartAsync(CancellationToken.None);
        await service.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(10));

        refresher.Verify(r => r.TryRefreshAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task MarksServicesHealthy_WhenRefreshLoadsData()
    {
        var health = CreateHealth();
        health.MarkUnavailable("AppConfiguration", "timed out");
        health.MarkUnavailable("EmailService", "not configured");
        health.MarkUnavailable("GoogleOAuth", "not configured");

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [SecretBackedServices.EmailConnectionStringKey] = "endpoint=https://example.invalid/;accesskey=a2V5"
            })
            .Build();

        var refreshes = 0;
        var (service, refresher) = CreateService(health, configuration, () => Volatile.Read(ref refreshes) >= 3);
        _ = refresher.Setup(r => r.TryRefreshAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                _ = Interlocked.Increment(ref refreshes);
                return true;
            });

        await service.StartAsync(CancellationToken.None);
        await service.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.AreEqual(3, refreshes);
        Assert.AreEqual(ServiceStatus.Healthy, health.GetServiceStatus("AppConfiguration").Status);
        Assert.AreEqual(ServiceStatus.Healthy, health.GetServiceStatus("EmailService").Status);
        Assert.AreEqual(ServiceStatus.Unavailable, health.GetServiceStatus("GoogleOAuth").Status);
    }

    [TestMethod]
    public async Task KeepsRetrying_WhileNotLoaded()
    {
        var health = CreateHealth();
        health.MarkUnavailable("AppConfiguration", "timed out");

        var refreshes = 0;
        var (service, refresher) = CreateService(health, new ConfigurationBuilder().Build(), () => false);
        _ = refresher.Setup(r => r.TryRefreshAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                _ = Interlocked.Increment(ref refreshes);
                return false;
            });

        await service.StartAsync(CancellationToken.None);
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (Volatile.Read(ref refreshes) < 5 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }
        await service.StopAsync(CancellationToken.None);

        Assert.IsTrue(Volatile.Read(ref refreshes) >= 5, "Recovery should keep refreshing");
        Assert.IsTrue(service.ExecuteTask!.IsCompleted);
        Assert.AreEqual(ServiceStatus.Unavailable, health.GetServiceStatus("AppConfiguration").Status);
    }

    // The next two run the real provider against an unreachable store. They pin the provider
    // behavior the design relies on: a failed load must not throw out of startup, and it must
    // leave a provider in the chain for the refresher to complete later.

    private sealed class StaticTokenCredential : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            new("token", DateTimeOffset.UtcNow.AddHours(1));

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            new(GetToken(requestContext, cancellationToken));
    }

    private sealed class FailingTokenCredential : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            throw new AuthenticationFailedException("managed identity endpoint not ready");

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            throw new AuthenticationFailedException("managed identity endpoint not ready");
    }

    private static (ConfigurationManager configuration, ServiceCollection services, ServiceHealthManager health, bool added)
        AddUnreachableStore(TokenCredential credential)
    {
        var configuration = new ConfigurationManager();
        var services = new ServiceCollection();
        var health = CreateHealth();
        var added = configuration.AddM4dAppConfiguration(
            services, new Uri("https://m4d-unreachable.invalid"), credential, "Test", health,
            TimeSpan.FromSeconds(3));
        return (configuration, services, health, added);
    }

    [TestMethod]
    public void UnreachableStore_LeavesEmptyRefreshableProvider()
    {
        var (configuration, services, health, added) = AddUnreachableStore(new StaticTokenCredential());

        Assert.IsTrue(added);
        Assert.IsTrue(AppConfigurationStartup.HasProvider(configuration));
        Assert.IsFalse(AppConfigurationStartup.HasLoaded(configuration));
        Assert.AreEqual(ServiceStatus.Unavailable, health.GetServiceStatus("AppConfiguration").Status);
        Assert.IsTrue(services.Any(d => d.ImplementationType == typeof(AppConfigurationRecoveryService)));
    }

    [TestMethod]
    public void CredentialFailure_StillLeavesRefreshableProvider()
    {
        var (configuration, _, health, added) = AddUnreachableStore(new FailingTokenCredential());

        Assert.IsTrue(added);
        Assert.IsTrue(AppConfigurationStartup.HasProvider(configuration));
        Assert.IsFalse(AppConfigurationStartup.HasLoaded(configuration));
        Assert.AreEqual(ServiceStatus.Unavailable, health.GetServiceStatus("AppConfiguration").Status);
    }

    // A stand-in App Configuration store: fails every request until Available is set, then
    // serves a single key-value.
    private sealed class FakeStoreHandler : HttpMessageHandler
    {
        public volatile bool Available;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (!Available)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            }

            const string body = """{"items":[{"etag":"e1","key":"Configuration:Sentinel","label":null,"content_type":null,"value":"42","tags":{},"locked":false,"last_modified":"2026-10-06T00:00:00+00:00"}]}""";
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/vnd.microsoft.appconfig.kvset+json")
            };
            response.Headers.ETag = new System.Net.Http.Headers.EntityTagHeaderValue("\"e1\"");
            return Task.FromResult(response);
        }
    }

    // The library behavior the in-place design rests on (read from the decompiled 8.6 provider):
    // an optional provider whose startup load failed is completed by a later refresh, and the
    // data shows up in the same IConfiguration.
    [TestMethod]
    public async Task OptionalProvider_LoadsOnRefreshAfterFailedStartup()
    {
        var handler = new FakeStoreHandler();
        IConfigurationRefresher refresher = null!;
        var configuration = new ConfigurationManager();
        _ = configuration.AddAzureAppConfiguration(
            options =>
            {
                _ = options.Connect(new Uri("https://fake.azconfig.io"), new StaticTokenCredential())
                    .Select(KeyFilter.Any, LabelFilter.Null)
                    .ConfigureRefresh(refresh => refresh.Register("Configuration:Sentinel", refreshAll: true)
                        .SetRefreshInterval(TimeSpan.FromSeconds(1)))
                    .ConfigureClientOptions(client =>
                    {
                        client.Transport = new HttpClientTransport(new HttpClient(handler));
                        client.Retry.MaxRetries = 0;
                    })
                    .ConfigureStartupOptions(startup => startup.Timeout = TimeSpan.FromSeconds(2));
                // After a failed request the provider skips the endpoint for at least 30s
                // (doubling to 10 minutes); shorten that internal backoff so the test is quick
                var backoff = typeof(AzureAppConfigurationOptions).GetProperty(
                    "MinBackoffDuration",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                Assert.IsNotNull(backoff, "AzureAppConfigurationOptions.MinBackoffDuration no longer exists");
                backoff.SetValue(options, TimeSpan.FromMilliseconds(100));
                refresher = options.GetRefresher();
            },
            optional: true);

        Assert.IsFalse(AppConfigurationStartup.HasLoaded(configuration));
        Assert.IsNull(configuration["Configuration:Sentinel"]);

        handler.Available = true;
        await Task.Delay(TimeSpan.FromSeconds(1.5));
        Assert.IsTrue(await refresher.TryRefreshAsync());

        Assert.IsTrue(AppConfigurationStartup.HasLoaded(configuration));
        Assert.AreEqual("42", configuration["Configuration:Sentinel"]);
    }
}
