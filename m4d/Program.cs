using Azure.Identity;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;

using m4d.Areas.Identity;
using m4d.Services;
using m4d.Services.ServiceHealth;
using m4d.Configuration;
using m4d.Utilities;
using m4d.ViewModels;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Rewrite;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Microsoft.Extensions.FileProviders;
using Microsoft.FeatureManagement;

using Newtonsoft.Json.Serialization;

using Owl.reCAPTCHA;

using System.Reflection;

using Vite.AspNetCore;


// TODO: Figure out how to add a design time factory for the context https://learn.microsoft.com/en-us/ef/core/cli/dbcontext-creation?tabs=dotnet-core-cli#from-a-design-time-factory
//  Or maybe implement https://learn.microsoft.com/en-us/ef/core/cli/dbcontext-creation?tabs=dotnet-core-cli#from-application-services
//  Think that's working, but now have to figure out what is going on with UsageSummary and the aspnet fields that changed length

var startupTimer = System.Diagnostics.Stopwatch.StartNew();
var processStart = System.Diagnostics.Process.GetCurrentProcess().StartTime;
var timeSinceProcessStart = DateTime.Now - processStart;
var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies().Length;

Console.WriteLine($"[Process started {timeSinceProcessStart.TotalSeconds:F2}s ago]");
Console.WriteLine($"[{startupTimer.Elapsed.TotalSeconds:F2}s] Entering Main");
Console.WriteLine($"[Loaded assemblies at Main entry: {loadedAssemblies}]");

var builder = WebApplication.CreateBuilder(args);

Console.WriteLine($"[{startupTimer.Elapsed.TotalSeconds:F2}s] Created Builder");

// Smoke test mode - bypasses all Azure service configuration for container diagnostics
var smokeTestMode = builder.Configuration.GetValue<bool>("SMOKE_TEST_MODE");

Console.WriteLine($"SMOKE_TEST_MODE = {smokeTestMode}");

if (smokeTestMode)
{
    Console.WriteLine("⚠️  SMOKE TEST MODE ENABLED - Running minimal configuration");
    Console.WriteLine("Note: Azure automatically configures port binding via PORT/WEBSITES_PORT environment variables");

    var smokeApp = builder.Build();

    smokeApp.MapGet("/", () => Results.Content(
        $"""
        <!DOCTYPE html>
        <html>
        <head>
            <title>m4d-staging Smoke Test</title>
            <meta charset="utf-8">
        </head>
        <body style="font-family: monospace; padding: 40px; background: #f0f0f0;">
            <h1 style="color: #28a745;">[OK] Container is Running</h1>
            <p><strong>Environment:</strong> {builder.Environment.EnvironmentName}</p>
            <p><strong>Time:</strong> {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC</p>
            <p><strong>Mode:</strong> Smoke Test (bypassing Azure services)</p>
            <hr>
            <p><em>If you see this, the container and .NET runtime are working correctly.</em></p>
        </body>
        </html>
        """, "text/html"));

    smokeApp.MapGet("/health", () => Results.Json(new
    {
        status = "healthy",
        mode = "smoke-test",
        environment = builder.Environment.EnvironmentName,
        timestamp = DateTime.UtcNow
    }));

    Console.WriteLine("✓ Smoke test app configured, starting...");
    await smokeApp.RunAsync();
    return;
}

Console.WriteLine("Proceeding with normal startup - Finding connection string");

// Prioritize Service Connector environment variable (Azure), fall back to appsettings.json (local development)
var connectionString = builder.Configuration["AZURE_SQL_CONNECTIONSTRING"]
    ?? builder.Configuration.GetConnectionString("DanceMusicContextConnection");

if (!string.IsNullOrEmpty(connectionString))
{
    if (!string.IsNullOrEmpty(builder.Configuration["AZURE_SQL_CONNECTIONSTRING"]))
    {
        Console.WriteLine($"[Database] Using AZURE_SQL_CONNECTIONSTRING from Service Connector");
    }
    else
    {
        Console.WriteLine($"[Database] Using DanceMusicContextConnection from appsettings.json");
    }
}
else
{
    Console.WriteLine("[Database] WARNING: No connection string found - database will be unavailable");
    Console.WriteLine("[Database] Expected 'AZURE_SQL_CONNECTIONSTRING' (Azure Service Connector) or 'DanceMusicContextConnection' (local)");
}

var services = builder.Services;
var environment = builder.Environment;
var configuration = builder.Configuration;

Console.WriteLine($"[{startupTimer.Elapsed.TotalSeconds:F2}s] Configuring logging");

var logging = builder.Logging;
logging.ClearProviders();
logging.AddConsole();
logging.AddAzureWebAppDiagnostics();

// Log level filters should be set via configuration (see appsettings.json / appsettings.Production.json)

Console.WriteLine($"[{startupTimer.Elapsed.TotalSeconds:F2}s] Environment: {environment.EnvironmentName}");

// Determine if running in development
var isDevelopment = environment.IsDevelopment();
var useVite = configuration.UseVite();

// Configure Kestrel for Azure Linux deployments (both self-contained and framework-dependent)
// Note: SELF_CONTAINED_DEPLOYMENT is automatically set by the deployment pipeline
// based on the deploymentMode parameter (self-contained vs framework-dependent)
var isSelfContained = configuration.GetValue<bool>("SELF_CONTAINED_DEPLOYMENT");
Console.WriteLine($"SELF_CONTAINED_DEPLOYMENT flag: {isSelfContained}");

services.AddHttpLogging(o => { });
builder.Services.AddFeatureManagement();

// Initialize ServiceHealthManager early to track service registration health
var serviceHealthLogger = LoggerFactory.Create(builder => builder.AddConsole())
    .CreateLogger<ServiceHealthManager>();
var serviceHealth = new ServiceHealthManager(serviceHealthLogger);
services.AddSingleton(serviceHealth);
Console.WriteLine($"[{startupTimer.Elapsed.TotalSeconds:F2}s] ServiceHealthManager initialized");
Console.WriteLine("Note: Email notifications will be initialized only if startup failures are detected");

// Create optimized DefaultAzureCredential once for reuse across all Azure services
// Excludes slower credential types (VisualStudio, AzureCLI, AzurePowerShell) for faster startup
DefaultAzureCredential? azureCredential = null;
if (!isDevelopment)
{
    Console.WriteLine($"[{startupTimer.Elapsed.TotalSeconds:F2}s] [Azure] Creating DefaultAzureCredential with optimized chain...");
    azureCredential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
    {
        ExcludeVisualStudioCredential = true,
        ExcludeVisualStudioCodeCredential = true,
        ExcludeAzureCliCredential = true,
        ExcludeAzurePowerShellCredential = true,
        ExcludeInteractiveBrowserCredential = true
        // Only try ManagedIdentityCredential and EnvironmentCredential in Azure
    });
    Console.WriteLine($"[{startupTimer.Elapsed.TotalSeconds:F2}s] [Azure] DefaultAzureCredential created successfully (optimized)");
}

if (!isDevelopment)
{
    Console.WriteLine($"[{startupTimer.Elapsed.TotalSeconds:F2}s] Production environment detected. Deployment mode: {(isSelfContained ? "self-contained" : "framework-dependent")}");

    var appConfigEndpoint = configuration["AppConfig:Endpoint"];

    // Register App Configuration service WITHOUT connecting synchronously (for fast startup)
    // Connection will be triggered by StartupInitializationService in background after app starts
    if (!string.IsNullOrEmpty(appConfigEndpoint))
    {
        Console.WriteLine($"[{startupTimer.Elapsed.TotalSeconds:F2}s] [AppConfig] Endpoint configured: {appConfigEndpoint}");
        Console.WriteLine("[AppConfig] Registering service (connection deferred to background)");

        try
        {
            _ = configuration.AddAzureAppConfiguration(options =>
            {
                _ = options.Connect(
                    new Uri(appConfigEndpoint),
                    azureCredential!)
                .ConfigureKeyVault(
                    kv => { _ = kv.SetCredential(azureCredential!); })
                .UseFeatureFlags(featureFlagOptions =>
                {
                    _ = featureFlagOptions.Select(LabelFilter.Null);
                    _ = featureFlagOptions.Select(environment.EnvironmentName);
                    _ = featureFlagOptions.SetRefreshInterval(TimeSpan.FromMinutes(5));
                })
                .Select(KeyFilter.Any, LabelFilter.Null)
                .Select(KeyFilter.Any, environment.EnvironmentName)
                .ConfigureRefresh(refresh =>
                {
                    _ = refresh.Register("Configuration:Sentinel", environment.EnvironmentName, refreshAll: true)
                        .SetRefreshInterval(TimeSpan.FromMinutes(5));
                });
            });

            _ = services.AddAzureAppConfiguration();
            serviceHealth.MarkHealthy("AppConfiguration");
            Console.WriteLine($"[{startupTimer.Elapsed.TotalSeconds:F2}s] [AppConfig] ✓ Service registered (will connect in background)");
        }
        catch (Exception ex)
        {
            serviceHealth.MarkUnavailable("AppConfiguration", $"{ex.GetType().Name}: {ex.Message}");
            Console.WriteLine($"WARNING: App Configuration registration failed: {ex.Message}");
            Console.WriteLine("Continuing with local configuration only");
        }
    }
    else
    {
        serviceHealth.MarkUnavailable("AppConfiguration", "Endpoint not configured");
        Console.WriteLine("[AppConfig] Not configured - using local configuration only");
    }
}

// Dynamically add all configuration sections with an "indexname" field
var indexSections = configuration.GetChildren()
    .Where(s => s.GetChildren().Any(child => child.Key.Equals("indexname", StringComparison.OrdinalIgnoreCase)))
    .ToList();
Console.WriteLine($"[{startupTimer.Elapsed.TotalSeconds:F2}s] Found {indexSections.Count} search index configuration sections");

Console.WriteLine("[Search] Configuring Azure Search with managed identity");
try
{
    // Fast registration - skip validation, Azure SDK will handle invalid endpoints
    Console.WriteLine($"[Search] Registering {indexSections.Count} search index clients (fast startup mode)");

    services.AddAzureClients(clientBuilder =>
    {
        Console.WriteLine("[Search] Using shared DefaultAzureCredential");
        _ = clientBuilder.UseCredential(azureCredential!);

        foreach (var section in indexSections)
        {
            var endpoint = section["endpoint"];
            Console.WriteLine($"[Search] Registering: {section.Key} -> {endpoint}");
            _ = clientBuilder.AddSearchClient(section).WithName(section.Key);
        }

        // Add a single SearchIndexClient named "SongIndex"
        var songIndexSections = indexSections
            .Where(s => s.Key.StartsWith("SongIndex", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (songIndexSections.Any())
        {
            var firstSongIndexSection = songIndexSections.First();
            var endpoint = firstSongIndexSection["endpoint"];

            Console.WriteLine($"[Search] Registering SearchIndexClient: SongIndex -> {endpoint}");
            _ = clientBuilder.AddSearchIndexClient(firstSongIndexSection).WithName("SongIndex");
        }
        Console.WriteLine("[Search] All search clients registered");
    });
    serviceHealth.MarkHealthy("SearchService");
    Console.WriteLine("[Search] ✓ Clients registered (connections will be lazy-loaded)");
}
catch (Exception ex)
{
    serviceHealth.MarkUnavailable("SearchService", $"{ex.GetType().Name}: {ex.Message}");
    Console.WriteLine($"ERROR configuring Azure Search: {ex.GetType().Name}: {ex.Message}");
    Console.WriteLine("WARNING: Continuing without Azure Search - search features will be unavailable");

    // Register null/fallback clients to prevent dependency injection failures
    // This allows the app to start even if search service configuration is invalid
    services.AddSingleton<IAzureClientFactory<SearchClient>>(sp =>
    {
        return new NullSearchClientFactory();
    });
    services.AddSingleton<IAzureClientFactory<SearchIndexClient>>(sp =>
    {
        return new NullSearchIndexClientFactory();
    });
    Console.WriteLine("Registered fallback search client factories");
}

Console.WriteLine("Configuring SQL Server database context");
if (string.IsNullOrEmpty(connectionString))
{
    serviceHealth.MarkUnavailable("Database", "Connection string not configured");
    Console.WriteLine("WARNING: Database unavailable - no connection string configured");
    Console.WriteLine("App will continue without database - using cached/static content where available");

    // Register a placeholder DbContext to prevent dependency injection failures
    services.AddDbContext<DanceMusicContext>(options =>
        options.UseSqlServer("Server=(placeholder);Database=placeholder;", sqlOptions =>
            sqlOptions.EnableRetryOnFailure(maxRetryCount: 0)));
}
else
{
    try
    {
        services.AddDbContext<DanceMusicContext>(options =>
            options.UseSqlServer(connectionString, sqlOptions =>
                sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorNumbersToAdd: null)));
        serviceHealth.MarkHealthy("Database");
        Console.WriteLine("Database context configured successfully");
    }
    catch (Exception ex)
    {
        serviceHealth.MarkUnavailable("Database", $"{ex.GetType().Name}: {ex.Message}");
        Console.WriteLine($"ERROR configuring database: {ex.GetType().Name}: {ex.Message}");
        Console.WriteLine("WARNING: Continuing without database - using cached content where available");

        // Register a placeholder DbContext to prevent dependency injection failures
        services.AddDbContext<DanceMusicContext>(options =>
            options.UseSqlServer("Server=(placeholder);Database=placeholder;", sqlOptions =>
                sqlOptions.EnableRetryOnFailure(maxRetryCount: 0)));
    }
}

// Configure data protection for self-contained deployments
if (isSelfContained)
{
    var dataProtectionPath = Environment.GetEnvironmentVariable("HOME");
    if (!string.IsNullOrEmpty(dataProtectionPath))
    {
        var keyPath = Path.Combine(dataProtectionPath, "site", "keys");
        Directory.CreateDirectory(keyPath);

        services.AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo(keyPath))
            .SetApplicationName("music4dance");

        Console.WriteLine($"Data protection keys stored at: {keyPath}");
    }
    else
    {
        Console.WriteLine("Warning: HOME environment variable not set, using default data protection");
    }
}

services.AddDefaultIdentity<ApplicationUser>(
        options =>
        {
            options.SignIn.RequireConfirmedAccount = true;
            options.User.RequireUniqueEmail = true;
            options.User.AllowedUserNameCharacters = string.Empty;
            options.Stores.MaxLengthForKeys = 128;
        })
    .AddUserValidator<UsernameValidator<ApplicationUser>>()
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<DanceMusicContext>();

// Add services to the container.
services.AddControllersWithViews();

services.Configure<KestrelServerOptions>(options =>
{
    options.Limits.MaxRequestBodySize = 1000000000; // 1GB
});

services.Configure<CookiePolicyOptions>(
    options =>
    {
        // This lambda determines whether user consent for non-essential cookies is needed for a given request.
        options.CheckConsentNeeded = context => true;
        options.MinimumSameSitePolicy = SameSiteMode.Strict;
    });

services.ConfigureApplicationCookie(
    options =>
    {
        options.AccessDeniedPath = "/Identity/Account/AccessDenied";
        options.Cookie.Name = "music4dance";
        options.Cookie.HttpOnly = true;
        options.ExpireTimeSpan = TimeSpan.FromDays(1);
        options.LoginPath = "/Identity/Account/Login";
        options.ReturnUrlParameter = CookieAuthenticationDefaults.ReturnUrlParameter;
        options.SlidingExpiration = true;
    });

services.Configure<PasswordHasherOptions>(
    option =>
        option.CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV2);

services.AddResponseCaching();

GlobalState.SetMarketing(configuration.GetSection("Configuration:Marketing"));

var physicalProvider = environment.ContentRootFileProvider;
var embeddedProvider = new EmbeddedFileProvider(Assembly.GetEntryAssembly());
var compositeProvider = new CompositeFileProvider(physicalProvider, embeddedProvider);
services.AddSingleton<IFileProvider>(compositeProvider);

// Email Service
services.AddEmailSenderWithResilience(configuration, serviceHealth);

services.Configure<AuthorizationOptions>(
    options =>
    {
        options.AddPolicy(
            "TokenAuthorization",
            policy => policy.AddRequirements(new TokenRequirement(configuration)));
    });

var authBuilder = services.AddAuthentication();

// Google OAuth
authBuilder.AddGoogleWithResilience(configuration, serviceHealth);

// Facebook OAuth
authBuilder.AddFacebookWithResilience(configuration, serviceHealth);

// Spotify OAuth
authBuilder.AddSpotifyWithResilience(configuration, serviceHealth);

// reCAPTCHA
services.AddReCaptchaWithResilience(configuration, serviceHealth);

var appRoot = environment.WebRootPath;
services.AddSingleton<ISearchServiceManager, SearchServiceManager>();
services.AddSingleton<IDanceStatsManager>(new DanceStatsManager(new DanceStatsFileManager(appRoot)));

services.AddScoped<m4d.Services.SpotifyAuthService>();

services.AddSingleton<IBackgroundTaskQueue, BackgroundTaskQueue>();
services.AddHostedService<BackgroundQueueHostedService>();

services.AddControllers().AddNewtonsoftJson()
    .AddNewtonsoftJson(
        options =>
        {
            options.SerializerSettings.ContractResolver =
                new DefaultContractResolver();
        });

services.AddAutoMapper(
    cfg =>
    {
        cfg.LicenseKey = configuration["Authentication:AutoMapper:Key"];
    },
    typeof(SongFilterProfile),
    typeof(SongPropertyProfile),
    typeof(TagProfile));

services.AddViteServices();

services.AddHostedService<DanceStatsHostedService>();
services.AddHostedService<StartupInitializationService>();

Console.WriteLine($"[{startupTimer.Elapsed.TotalSeconds:F2}s] Building application...");
WebApplication app;
try
{
    app = builder.Build();
    Console.WriteLine($"[{startupTimer.Elapsed.TotalSeconds:F2}s] Application built successfully");
}
catch (Exception ex)
{
    Console.WriteLine($"ERROR: Application failed to build: {ex.GetType().Name}: {ex.Message}");
    Console.WriteLine("This may indicate a critical service configuration issue that prevents dependency injection.");
    Console.WriteLine("Common causes:");
    Console.WriteLine("  - Invalid Azure Search endpoint configuration");
    Console.WriteLine("  - Missing required service dependencies");
    Console.WriteLine("  - Service registration conflicts");
    Console.WriteLine();
    Console.WriteLine("To resolve:");
    Console.WriteLine("  1. Check appsettings.Development.json for invalid endpoints");
    Console.WriteLine("  2. Verify all required configuration values are present");
    Console.WriteLine("  3. Check the startup health report above for failed services");
    Console.WriteLine();
    Console.WriteLine("Full exception details:");
    Console.WriteLine(ex);
    throw; // Re-throw to stop the application
}

// Set ApplicationLogging.LoggerFactory early so static loggers in services can initialize
ApplicationLogging.LoggerFactory = app.Services.GetRequiredService<ILoggerFactory>();

// Add lightweight health check endpoint BEFORE any middleware for fast Azure health probes
// This endpoint responds immediately, even if App Configuration or other services are still loading
app.MapGet("/health/startup", () => Results.Json(new
{
    status = "healthy",
    timestamp = DateTime.UtcNow,
    message = "Application is accepting requests"
})).AllowAnonymous();

if (!isDevelopment)
{
    // Add custom exception logging middleware BEFORE UseExceptionHandler
    _ = app.Use(async (context, next) =>
    {
        try
        {
            await next();
        }
        catch (Exception ex)
        {
            var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
            var url = $"{context.Request.Method} {context.Request.Path}{context.Request.QueryString}";
            logger.LogError(ex, "Unhandled exception for request: {Url}", url);
            throw; // Let UseExceptionHandler handle the exception as usual
        }
    });

    // Only use Azure App Configuration middleware if the service is available
    if (serviceHealth.IsServiceAvailable("AppConfiguration"))
    {
        _ = app.UseAzureAppConfiguration();
        Console.WriteLine("Azure App Configuration middleware enabled");
    }
    else
    {
        Console.WriteLine("Azure App Configuration middleware disabled - service unavailable");
    }
}

app.Logger.LogInformation("Builder Built");
app.Logger.LogInformation($"Environment = {environment.EnvironmentName}");
var sentinel = configuration["Configuration:Sentinel"];
app.Logger.LogInformation($"Sentinel (local/startup) = {sentinel}");

// App Configuration and database migrations will run in background after app starts accepting requests
// This reduces startup time and allows health checks to pass sooner

// Generate and log startup health report AFTER database migrations
Console.WriteLine();
Console.WriteLine(serviceHealth.GenerateStartupReport());
Console.WriteLine();

// Initialize email notifier and check for startup failures
var summary = serviceHealth.GetHealthSummary();
if (summary.UnavailableCount > 0 || summary.DegradedCount > 0)
{
    Console.WriteLine($"WARNING: {summary.UnavailableCount} service(s) unavailable, {summary.DegradedCount} degraded");

    // Initialize email notifier now (only if failures detected) to send one consolidated email
    try
    {
        var emailSender = app.Services.GetService<IEmailSender>();
        // Create a simple console logger for startup notifications (ApplicationLogging.LoggerFactory isn't set yet)
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var notifierLogger = loggerFactory.CreateLogger<ServiceHealthNotifier>();
        var notifier = new ServiceHealthNotifier(configuration, emailSender, notifierLogger);
        serviceHealth.SetNotifier(notifier);

        // Send consolidated startup failure notification (async, don't block startup)
        _ = Task.Run(async () =>
        {
            try
            {
                await serviceHealth.SendStartupFailureNotificationAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to send startup failure notification: {ex.Message}");
            }
        });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"WARNING: Failed to configure email notifications for startup failures: {ex.Message}");
    }
}

// Configure the HTTP request pipeline.
app.Logger.LogInformation(@"Configuring request pipeline");

if (isDevelopment)
{
    _ = app.UseDeveloperExceptionPage();
    if (useVite)
    {
        _ = app.UseViteDevelopmentServer(true);
    }
}
else
{
    _ = app.UseExceptionHandler("/Error");
    _ = app.UseStatusCodePagesWithReExecute("/Error/{0}");
    _ = app.UseHsts();
}

// Conditionally disable HTTPS redirection for Spotify OAuth testing (Spotify rejects https://localhost)
var disableHttpsRedirect = configuration.GetValue<bool>("DISABLE_HTTPS_REDIRECT");
if (!disableHttpsRedirect)
{
    app.UseHttpsRedirection();
    var options = new RewriteOptions();
    options.AddRedirectToHttps();
    options.AddRedirectToWwwPermanent("music4dance.net");
    app.UseRewriter(options);
}
else
{
    app.Logger.LogWarning("HTTPS redirection is DISABLED for Spotify OAuth testing. Do not use in production!");
}

// MapStaticAssets() requires a manifest file that doesn't get properly included
// in single-file (PublishSingleFile=true) self-contained deployments.
// Use UseStaticFiles() instead for self-contained mode.
if (isSelfContained)
{
    app.UseStaticFiles();
    Console.WriteLine("Using UseStaticFiles() for self-contained deployment");
}
else
{
    app.MapStaticAssets();
    Console.WriteLine("Using MapStaticAssets() for framework-dependent deployment");
}

app.UseHttpLogging();
app.UseRouting();

app.UseAuthorization();
app.UseResponseCaching();

app.Use(
    async (cxt, next) =>
    {
        var url = cxt.Request.Path.Value;
        var blog = "/blog";
        if (url != null)
        {
            var idx = url?.IndexOf(blog) ?? -1;
            if (idx != -1)
            {
                var path = url[(idx + blog.Length)..];
                cxt.Response.Redirect($"https://music4dance.blog{path}");
                return;
            }
        }

        await next();
    });

app.Logger.LogInformation(@"Mapping Routes");

app.MapControllerRoute(
    "Dances",
    "dances/{group}/{dance}",
    new { controller = "dance", action = "GroupRedirect" });
app.MapControllerRoute(
    "DanceEdit",
    "dances/edit",
    new { controller = "dance", action = "edit" });
app.MapControllerRoute(
    "DanceGroup",
    "dances/{dance?}",
    new { controller = "dance", action = "index" });
app.MapControllerRoute(
    "default",
    "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();

GlobalState.UseTestKeys = isDevelopment;

// Run database migrations in background after app starts accepting requests
_ = Task.Run(async () =>
{
    // Wait a moment for app to start accepting HTTP requests
    await Task.Delay(TimeSpan.FromSeconds(2));

    if (!serviceHealth.IsServiceAvailable("Database"))
    {
        Console.WriteLine("Skipping database migrations - database service is unavailable");
        return;
    }

    Console.WriteLine("Running database migrations in background...");
    try
    {
        using var scope = app.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<DanceMusicContext>().Database;

        db.Migrate();
        Console.WriteLine("Database migrations completed successfully");

        if (isDevelopment)
        {
            var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = sp.GetRequiredService<RoleManager<IdentityRole>>();
            await UserManagerHelpers.SeedData(userManager, roleManager, configuration);
            Console.WriteLine("Development seed data applied successfully");
        }
    }
    catch (Exception ex)
    {
        serviceHealth.MarkUnavailable("Database", $"Migration failed: {ex.GetType().Name}: {ex.Message}");
        Console.WriteLine($"ERROR: Database migration failed: {ex.GetType().Name}: {ex.Message}");
        Console.WriteLine("WARNING: Continuing without database - data features will be unavailable");
    }
});

app.Run();
