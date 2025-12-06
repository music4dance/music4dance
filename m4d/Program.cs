using Azure.Identity;
using Azure.Search.Documents;

using m4d.Areas.Identity;
using m4d.Services;
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
using System.Security.Cryptography.X509Certificates;

using Vite.AspNetCore;


// TODO: Figure out how to add a design time factory for the context https://learn.microsoft.com/en-us/ef/core/cli/dbcontext-creation?tabs=dotnet-core-cli#from-a-design-time-factory
//  Or maybe implement https://learn.microsoft.com/en-us/ef/core/cli/dbcontext-creation?tabs=dotnet-core-cli#from-application-services
//  Think that's working, but now have to figure out what is going on with UsageSummary and the aspnet fields that changed length

Console.WriteLine("Entering Main");

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("DanceMusicContextConnection")
    ?? throw new InvalidOperationException("Connection string 'DanceMusicContexstConnection' not found.");

var services = builder.Services;
var environment = builder.Environment;
var configuration = builder.Configuration;

var logging = builder.Logging;
logging.ClearProviders();
logging.AddConsole();
logging.AddAzureWebAppDiagnostics();

// Log level filters should be set via configuration (see appsettings.json / appsettings.Production.json)

Console.WriteLine($"Environment: {environment.EnvironmentName}");

// Configure Kestrel for self-contained deployments on Azure Linux
var isSelfContained = configuration.GetValue<bool>("SELF_CONTAINED_DEPLOYMENT");
Console.WriteLine($"SELF_CONTAINED_DEPLOYMENT flag: {isSelfContained}");
if (isSelfContained)
{
    Console.WriteLine("Running in self-contained mode");

    builder.WebHost.ConfigureKestrel(serverOptions =>
    {
        // Azure Web Apps use environment variables for port configuration
        var port = Environment.GetEnvironmentVariable("PORT") ??
                   Environment.GetEnvironmentVariable("WEBSITES_PORT") ?? "8080";

        Console.WriteLine($"Binding to port {port}");
        if (!int.TryParse(port, out var portNumber))
        {
            Console.WriteLine($"Invalid PORT value '{port}', using default 8080");
            portNumber = 8080;
        }
        serverOptions.ListenAnyIP(portNumber);

        // Load HTTPS certificate if available (Azure provides certificates via environment)
        var certPath = Environment.GetEnvironmentVariable("WEBSITE_LOAD_CERTIFICATES");
        var httpsPort = Environment.GetEnvironmentVariable("HTTPS_PORT");

        if (!string.IsNullOrEmpty(certPath) && !string.IsNullOrEmpty(httpsPort))
        {
            try
            {
                // Azure Linux Web Apps place certificates in a specific location
                var certFile = $"/var/ssl/private/{certPath}.p12";
                if (File.Exists(certFile))
                {
                    if (int.TryParse(httpsPort, out var httpsPortNumber))
                    {
                    using var cert = X509CertificateLoader.LoadPkcs12FromFile(certFile, null);
                    serverOptions.ListenAnyIP(int.Parse(httpsPort), listenOptions =>
                        {
                            listenOptions.UseHttps(cert);
                        });
                        Console.WriteLine($"HTTPS configured on port {httpsPortNumber}");
                    }
                    else
                    {
                        Console.WriteLine($"Invalid HTTPS_PORT value '{httpsPort}', skipping HTTPS configuration");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load certificate: {ex.Message}");
            }
        }
    });
}

services.AddHttpLogging(o => { });
builder.Services.AddFeatureManagement();

var useVite = configuration.UseVite();
var isDevelopment = environment.IsDevelopment();

if (!isDevelopment)
{
    Console.WriteLine($"Production environment detected. isSelfContained={isSelfContained}");
    if (isSelfContained)
    {
        Console.WriteLine("Using connection string authentication for App Configuration");
        // Use connection string for App Configuration in self-contained mode
        // Azure env vars: Use AppConfig__ConnectionString (double underscore becomes colon)
        var appConfigConnectionString = configuration["AppConfig:ConnectionString"];
        Console.WriteLine($"AppConfig:ConnectionString present: {!string.IsNullOrEmpty(appConfigConnectionString)}");
        if (!string.IsNullOrEmpty(appConfigConnectionString))
        {
            _ = configuration.AddAzureAppConfiguration(options =>
            {
                _ = options.Connect(appConfigConnectionString)
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
        }
        else
        {
            Console.WriteLine("Warning: AppConfig:ConnectionString not set for self-contained deployment");
        }
    }
    else
    {
        Console.WriteLine("Using managed identity (DefaultAzureCredential) for App Configuration");
        // Use managed identity for App Configuration
        try
        {
            var credentials = new DefaultAzureCredential();
            Console.WriteLine("DefaultAzureCredential created successfully for App Configuration");
        _ = configuration.AddAzureAppConfiguration(options =>
        {
            _ = options.Connect(
                new Uri(configuration["AppConfig:Endpoint"]),
                credentials)
            .ConfigureKeyVault(
                kv => { _ = kv.SetCredential(credentials); })
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
            Console.WriteLine("Azure App Configuration added successfully with managed identity");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR creating DefaultAzureCredential for App Configuration: {ex.GetType().Name}: {ex.Message}");
            throw;
        }
    }
}

// Dynamically add all configuration sections with an "indexname" field
var indexSections = configuration.GetChildren()
    .Where(s => s.GetChildren().Any(child => child.Key.Equals("indexname", StringComparison.OrdinalIgnoreCase)))
    .ToList();
Console.WriteLine($"Found {indexSections.Count} search index configuration sections");

if (isSelfContained)
{
    Console.WriteLine("Configuring Azure Search with API key authentication (self-contained mode)");
    // For self-contained deployments, register Azure Search clients with API key authentication
    // Azure env vars: Use AzureSearch__ApiKey (double underscore becomes colon)
    var searchApiKey = configuration["AzureSearch:ApiKey"];
    Console.WriteLine($"AzureSearch:ApiKey present: {!string.IsNullOrEmpty(searchApiKey)}");
    if (!string.IsNullOrEmpty(searchApiKey))
    {
        var keyCredential = new Azure.AzureKeyCredential(searchApiKey);

        // Add SearchIndexClient for SongIndex
        var songIndexSections = indexSections
            .Where(s => s.Key.StartsWith("SongIndex", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (songIndexSections.Any())
        {
            var firstSongIndexSection = songIndexSections.First();
            var endpoint = new Uri(firstSongIndexSection["endpoint"]);

            // Verify all SongIndex sections have the same endpoint
            foreach (var section in songIndexSections)
            {
                var sectionEndpoint = section["endpoint"];
                if (!string.Equals(endpoint.ToString(), sectionEndpoint, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"All SongIndex sections must have the same endpoint. Mismatch found in section '{section.Key}'.");
                }
            }

            var indexClient = new Azure.Search.Documents.Indexes.SearchIndexClient(endpoint, keyCredential);
            services.AddSingleton(indexClient);
        }

        Console.WriteLine($"Azure Search clients configured with API key authentication ({indexSections.Count} indexes)");
    }
    else
    {
        Console.WriteLine("Warning: AzureSearch:ApiKey not set for self-contained deployment");
    }
}
else
{
    Console.WriteLine("Configuring Azure Search with managed identity (framework-dependent mode)");
    // For framework-dependent deployments, use Azure client builder with managed identity
    try
    {
        services.AddAzureClients(clientBuilder =>
        {
            Console.WriteLine("Creating DefaultAzureCredential for Azure Search clients");
            var credentials = new DefaultAzureCredential();
            Console.WriteLine("DefaultAzureCredential created successfully for Azure Search");
            _ = clientBuilder.UseCredential(credentials);

        foreach (var section in indexSections)
        {
            Console.WriteLine($"Adding search client for index: {section.Key}, endpoint: {section["endpoint"]}");
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

            // Verify all SongIndex sections have the same endpoint
            foreach (var section in songIndexSections)
            {
                var sectionEndpoint = section["endpoint"];
                if (!string.Equals(endpoint, sectionEndpoint, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"All SongIndex sections must have the same endpoint. Mismatch found in section '{section.Key}'.");
                }
            }

            Console.WriteLine($"Adding SearchIndexClient for SongIndex, endpoint: {firstSongIndexSection["endpoint"]}");
            _ = clientBuilder.AddSearchIndexClient(firstSongIndexSection).WithName("SongIndex");
        }
        Console.WriteLine("Azure Search clients configured successfully with managed identity");
    });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"ERROR configuring Azure Search with managed identity: {ex.GetType().Name}: {ex.Message}");
        Console.WriteLine($"Stack trace: {ex.StackTrace}");
        throw;
    }
}

Console.WriteLine("Configuring SQL Server database context");
services.AddDbContext<DanceMusicContext>(options => options.UseSqlServer(connectionString));
Console.WriteLine("Database context configured successfully");

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

services.AddTransient<IEmailSender, EmailSender>(provider =>
    new EmailSender(configuration["Authentication:AzureCommunicationServices:ConnectionString"]));

services.Configure<AuthorizationOptions>(
    options =>
    {
        options.AddPolicy(
            "TokenAuthorization",
            policy => policy.AddRequirements(new TokenRequirement(configuration)));
    });

services.AddAuthentication()
    .AddGoogle(
        options =>
        {
            var googleAuthNSection =
                configuration.GetSection("Authentication:Google");

            options.ClientId = googleAuthNSection["ClientId"];
            options.ClientSecret = googleAuthNSection["ClientSecret"];
        })
    .AddFacebook(
        options =>
        {
            options.AppId = configuration["Authentication:Facebook:ClientId"];
            options.AppSecret = configuration["Authentication:Facebook:ClientSecret"];
            options.Scope.Add("email");
            options.Fields.Add("name");
            options.Fields.Add("email");
        })
    .AddSpotify(
        options =>
        {
            options.ClientId = configuration["Authentication:Spotify:ClientId"];
            options.ClientSecret = configuration["Authentication:Spotify:ClientSecret"];

            options.Scope.Add("user-read-email");
            options.Scope.Add("playlist-modify-public");
            options.Scope.Add("ugc-image-upload");
            //options.Scope.Add("user-read-playback-state");
            //options.Scope.Add("user-read-playback-position");

            //options.ClaimActions.MapJsonKey("urn:google:picture", "picture", "url");
            //options.ClaimActions.MapJsonKey("urn:google:locale", "locale", "string");

            options.SaveTokens = true;

            options.Events.OnCreatingTicket = cxt =>
            {
                var tokens = cxt.Properties.GetTokens().ToList();
                cxt.Properties.StoreTokens(tokens);

                return Task.CompletedTask;
            };
        });

services.AddreCAPTCHAV2(
    x =>
    {
        x.SiteKey = configuration["Authentication:reCAPTCHA:SiteKey"];
        x.SiteSecret = configuration["Authentication:reCAPTCHA:SecretKey"];
    });

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

Console.WriteLine("Building application...");
var app = builder.Build();
Console.WriteLine("Application built successfully");

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

    _ = app.UseAzureAppConfiguration();
}

app.Logger.LogInformation("Builder Built");
app.Logger.LogInformation($"Environment = {environment.EnvironmentName}");
var sentinel = configuration["Configuration:Sentinel"];
app.Logger.LogInformation($"Sentinel = {sentinel}");

using (var scope = app.Services.CreateScope())
{
    var sp = scope.ServiceProvider;
    var db = sp.GetRequiredService<DanceMusicContext>().Database;

    db.Migrate();

    if (isDevelopment)
    {
        var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = sp.GetRequiredService<RoleManager<IdentityRole>>();
        await UserManagerHelpers.SeedData(userManager, roleManager, configuration);
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
app.MapStaticAssets();
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

ApplicationLogging.LoggerFactory = app.Services.GetRequiredService<ILoggerFactory>();

GlobalState.UseTestKeys = isDevelopment;

app.Run();
