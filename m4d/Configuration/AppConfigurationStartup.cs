using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Tracing;

using Azure.Core;
using Azure.Core.Diagnostics;
using Azure.Core.Pipeline;

using m4d.Services;
using m4d.Services.ServiceHealth;

using Microsoft.Extensions.Configuration.AzureAppConfiguration;

namespace m4d.Configuration;

/// <summary>
/// Adds Azure App Configuration (with its Key Vault references) to the app's configuration so
/// that a failed startup load can recover in place.
///
/// Adding the provider to builder.Configuration (a ConfigurationManager) loads it synchronously,
/// right here: startup blocks until the load succeeds or <see cref="StartupTimeout"/> expires.
/// The provider is optional, so a failed load leaves it in the configuration chain with no data
/// instead of throwing. Its refresher then retries the full load (at most once per refresh
/// interval), and on success the data appears in IConfiguration and the reload token fires.
/// AppConfigurationRecoveryService drives those retries and updates service health; the
/// secret-backed services read their credentials at use time (see SecretBackedServices), so
/// nothing needs a restart.
/// </summary>
public static class AppConfigurationStartup
{
    /// <summary>
    /// How long startup waits for the first load. A failure is recovered in the background, so
    /// keep this well inside App Service's container start limit rather than waiting it out.
    /// </summary>
    public static readonly TimeSpan StartupTimeout = TimeSpan.FromSeconds(100);

    /// <summary>
    /// Per-request network timeout during the startup load: short enough that a stuck request
    /// gets retried inside <see cref="StartupTimeout"/>.
    /// </summary>
    public static readonly TimeSpan StartupNetworkTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Per-request network timeout once startup is over. Refreshes run in the background (the
    /// recovery service and the refresh middleware don't block requests), so waiting out a slow
    /// store costs nothing, while timing out means another 5+ minutes before the provider tries
    /// again.
    /// </summary>
    public static readonly TimeSpan RefreshNetworkTimeout = TimeSpan.FromMinutes(2);

    // Azure SDK events from the startup load are buffered and printed only when it fails, so a
    // timeout says which call (managed identity, App Configuration or Key Vault) was failing.
    private const int MaxDiagnosticLines = 200;

    /// <summary>
    /// Adds the provider and, when one is in the configuration chain, the refresher services and
    /// AppConfigurationRecoveryService. Returns whether a provider was added.
    /// </summary>
    public static bool AddM4dAppConfiguration(
        this ConfigurationManager configuration,
        IServiceCollection services,
        Uri endpoint,
        TokenCredential credential,
        string environmentName,
        ServiceHealthManager serviceHealth,
        TimeSpan? startupTimeout = null)
    {
        var timeout = startupTimeout ?? StartupTimeout;
        var networkTimeout = new PhaseNetworkTimeoutPolicy();

        void Configure(AzureAppConfigurationOptions options) => ConfigureWith(options, credential);

        void ConfigureWith(AzureAppConfigurationOptions options, TokenCredential storeCredential)
        {
            _ = options.Connect(endpoint, storeCredential)
                .ConfigureKeyVault(kv => { _ = kv.SetCredential(credential); })
                .UseFeatureFlags(featureFlagOptions =>
                {
                    // Both arguments matter. FeatureFlagOptions.Select takes the flag NAME
                    // filter first and the label second - it is not the single-argument
                    // label overload it looks like, and there is no such overload. Passing a
                    // label alone asks for a flag literally named "Staging", matches nothing,
                    // and fails silently: flags simply fall back to appsettings.json, which
                    // looks exactly like a flag that is switched off. Mirror the key-value
                    // Select calls below, which had it right.
                    _ = featureFlagOptions.Select(KeyFilter.Any, LabelFilter.Null);
                    _ = featureFlagOptions.Select(KeyFilter.Any, environmentName);
                    _ = featureFlagOptions.SetRefreshInterval(TimeSpan.FromMinutes(5));
                })
                .Select(KeyFilter.Any, LabelFilter.Null)
                .Select(KeyFilter.Any, environmentName)
                .ConfigureRefresh(refresh =>
                {
                    _ = refresh.Register("Configuration:Sentinel", environmentName, refreshAll: true)
                        .SetRefreshInterval(TimeSpan.FromMinutes(5));
                })
                .ConfigureClientOptions(clientOptions =>
                {
                    // Per HTTP request: initial try + 1 retry. The network timeout is
                    // StartupNetworkTimeout during the startup load (the provider retries failed
                    // loads on its own until StartupOptions.Timeout, so it doesn't cap how long
                    // startup waits) and RefreshNetworkTimeout afterwards.
                    clientOptions.Retry.NetworkTimeout = StartupNetworkTimeout;
                    clientOptions.AddPolicy(networkTimeout, HttpPipelinePosition.PerCall);
                    clientOptions.Retry.MaxRetries = 1;
                    clientOptions.Retry.Delay = TimeSpan.FromSeconds(2);
                    clientOptions.Retry.MaxDelay = TimeSpan.FromSeconds(5);
                    clientOptions.Retry.Mode = RetryMode.Exponential;
                });
        }

        Console.WriteLine($"[AppConfig] Loading (blocks startup up to {timeout.TotalSeconds:F0}s)");

        var diagnostics = new ConcurrentQueue<string>();
        var timer = Stopwatch.StartNew();
        var providerAdded = false;
        using (new AzureEventSourceListener(
                   (args, message) =>
                   {
                       diagnostics.Enqueue($"[{timer.Elapsed.TotalSeconds:F1}s] {args.EventSource.Name}: {message}");
                       while (diagnostics.Count > MaxDiagnosticLines)
                       {
                           _ = diagnostics.TryDequeue(out _);
                       }
                   },
                   EventLevel.Informational))
        {
            providerAdded = TryAdd(configuration, Configure, timeout, timer);

            if (!providerAdded)
            {
                // The provider treats only some failures as optional (timeouts, request
                // failures, Key Vault reference errors). Anything else - a credential error, say -
                // throws out of the load and leaves the provider out of the configuration chain,
                // where nothing can refresh it. Add a second one whose startup load can't reach
                // the network: its credential holds every token request until the startup timeout
                // cancels it, which the provider treats as an ignorable timeout. Once it's in the
                // chain the gate opens and the recovery service's refreshes go through.
                Console.WriteLine("[AppConfig] Adding a deferred provider for background recovery");
                var gate = new GatedTokenCredential(credential);
                providerAdded = TryAdd(
                    configuration, options => ConfigureWith(options, gate), TimeSpan.FromMilliseconds(100), timer);
                gate.Open();
            }
        }

        networkTimeout.StartupComplete = true;

        if (HasLoaded(configuration))
        {
            serviceHealth.MarkHealthy("AppConfiguration", timer.Elapsed);
            Console.WriteLine($"[AppConfig] ✓ Loaded in {timer.Elapsed.TotalSeconds:F1}s");
        }
        else
        {
            serviceHealth.MarkUnavailable(
                "AppConfiguration",
                $"Initial load failed or timed out after {timer.Elapsed.TotalSeconds:F1}s");
            Console.WriteLine(
                $"WARNING: App Configuration did not load after {timer.Elapsed.TotalSeconds:F1}s; continuing with local configuration only");
            Console.WriteLine($"[AppConfig] Azure SDK events during the load (last {MaxDiagnosticLines}):");
            foreach (var line in diagnostics)
            {
                Console.WriteLine($"  {line}");
            }
        }

        if (providerAdded)
        {
            _ = services.AddAzureAppConfiguration();
            services.AddHostedService<AppConfigurationRecoveryService>();
        }
        else
        {
            Console.WriteLine("WARNING: No App Configuration provider could be added; a restart is needed to load it");
        }

        return providerAdded;
    }

    /// <summary>
    /// True once an App Configuration provider in the chain holds data. A provider whose load
    /// failed is present but empty.
    /// </summary>
    public static bool HasLoaded(IConfiguration configuration) =>
        AppConfigurationProviders(configuration)
            .Any(p => p.GetChildKeys([], null).Any());

    /// <summary>
    /// True when an App Configuration provider is in the chain, so the refresher services and
    /// middleware can be used.
    /// </summary>
    public static bool HasProvider(IConfiguration configuration) =>
        AppConfigurationProviders(configuration).Any();

    private static IEnumerable<IConfigurationProvider> AppConfigurationProviders(IConfiguration configuration) =>
        (configuration as IConfigurationRoot)?.Providers.Where(p => p is IConfigurationRefresher) ?? [];

    private static bool TryAdd(
        ConfigurationManager configuration, Action<AzureAppConfigurationOptions> configure,
        TimeSpan timeout, Stopwatch timer)
    {
        try
        {
            _ = configuration.AddAzureAppConfiguration(
                options =>
                {
                    configure(options);
                    _ = options.ConfigureStartupOptions(startup => startup.Timeout = timeout);
                },
                optional: true);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"WARNING: App Configuration load threw after {timer.Elapsed.TotalSeconds:F1}s: {ex}");
            return false;
        }
    }

    /// <summary>
    /// Switches App Configuration requests from <see cref="StartupNetworkTimeout"/> (the client
    /// options' value) to <see cref="RefreshNetworkTimeout"/> once the startup load is over.
    /// </summary>
    private sealed class PhaseNetworkTimeoutPolicy : HttpPipelineSynchronousPolicy
    {
        public volatile bool StartupComplete;

        public override void OnSendingRequest(HttpMessage message)
        {
            if (StartupComplete)
            {
                message.NetworkTimeout = RefreshNetworkTimeout;
            }
        }
    }

    /// <summary>
    /// Holds token requests (until cancelled) while closed, and passes them to the inner
    /// credential once opened.
    /// </summary>
    private sealed class GatedTokenCredential(TokenCredential inner) : TokenCredential
    {
        private readonly TaskCompletionSource _opened =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Open() => _opened.TrySetResult();

        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            _opened.Task.Wait(cancellationToken);
            return inner.GetToken(requestContext, cancellationToken);
        }

        public override async ValueTask<AccessToken> GetTokenAsync(
            TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            await _opened.Task.WaitAsync(cancellationToken);
            return await inner.GetTokenAsync(requestContext, cancellationToken);
        }
    }
}
