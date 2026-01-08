using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using m4d.Services.ServiceHealth;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;

namespace m4d.Services;

/// <summary>
/// Background service that performs expensive initialization tasks after the app starts accepting requests
/// This allows the app to pass health checks quickly while Azure services are still connecting
/// </summary>
public class StartupInitializationService : BackgroundService
{
    private readonly ILogger<StartupInitializationService> _logger;
    private readonly ServiceHealthManager _serviceHealth;
    private readonly IConfiguration _configuration;
    private readonly IConfigurationRefresher? _configurationRefresher;

    public StartupInitializationService(
        ILogger<StartupInitializationService> logger,
        ServiceHealthManager serviceHealth,
        IConfiguration configuration,
        IConfigurationRefresherProvider? configRefresherProvider = null)
    {
        _logger = logger;
        _serviceHealth = serviceHealth;
        _configuration = configuration;

        // Get the configuration refresher if App Configuration is registered
        _configurationRefresher = configRefresherProvider?.Refreshers.FirstOrDefault();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[Startup] Background initialization starting...");

        // Wait for app to start accepting requests
        await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);

        // Trigger App Configuration connection if available
        if (_configurationRefresher != null && _serviceHealth.IsServiceAvailable("AppConfiguration"))
        {
            try
            {
                _logger.LogInformation("[AppConfig] Triggering initial configuration refresh...");
                await _configurationRefresher.RefreshAsync(stoppingToken);
                _logger.LogInformation("[AppConfig] Initial configuration loaded from Azure App Configuration");

                var sentinel = _configuration["Configuration:Sentinel"];
                _logger.LogInformation($"[AppConfig] Remote sentinel value: {sentinel}");
            }
            catch (Exception ex)
            {
                _serviceHealth.MarkUnavailable("AppConfiguration", $"Connection failed: {ex.GetType().Name}: {ex.Message}");
                _logger.LogWarning(ex, "[AppConfig] Failed to connect - continuing with local configuration");
            }
        }
        else
        {
            _logger.LogInformation("[AppConfig] No remote configuration to load - using local appsettings only");
        }

        _logger.LogInformation("[Startup] Background initialization complete");
    }
}
