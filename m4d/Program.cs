using System.Reflection;

using Azure.Identity;

using m4d.Areas.Identity;
using m4d.Services;
using m4d.Utilities;
using m4d.ViewModels;

using m4dModels;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
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

Console.WriteLine($"Environment: {environment.EnvironmentName}");

services.AddHttpLogging(o => { });
builder.Services.AddFeatureManagement();

var useVite = configuration.UseVite();
var isDevelopment = environment.IsDevelopment();

var credentials = new DefaultAzureCredential();
if (!isDevelopment)
{
    configuration.AddAzureAppConfiguration(options =>
    {
        options.Connect(
            new Uri(configuration["AppConfig:Endpoint"]),
            credentials)
        .ConfigureKeyVault(
            kv => { kv.SetCredential(credentials); })
        .UseFeatureFlags(featureFlagOptions =>
        {
            featureFlagOptions.Select(environment.EnvironmentName);
            featureFlagOptions.Select(LabelFilter.Null);
            featureFlagOptions.SetRefreshInterval(TimeSpan.FromMinutes(5));
        })
        .Select(KeyFilter.Any, environment.EnvironmentName)
        .Select(KeyFilter.Any, LabelFilter.Null)
        .ConfigureRefresh(refresh =>
        {
            refresh.Register("Configuration:Sentinel", environment.EnvironmentName, refreshAll: true)
                .SetRefreshInterval(TimeSpan.FromMinutes(5));
        });
    });

    services.AddAzureAppConfiguration();
}

services.AddAzureClients(clientBuilder =>
{
    clientBuilder.UseCredential(credentials);
    clientBuilder.AddSearchClient(
        configuration.GetSection("PageIndex")).WithName("PageIndex");
    clientBuilder.AddSearchClient(
        configuration.GetSection("SongIndex")).WithName("SongIndex");
    clientBuilder.AddSearchClient(
        configuration.GetSection("SongIndexTest")).WithName("SongIndexTest");
});

services.AddDbContext<DanceMusicContext>(options => options.UseSqlServer(connectionString));

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

services.AddTransient<IEmailSender, EmailSender>();
var sendGrid = configuration.GetSection("Authentication:SendGrid");
services.Configure<AuthMessageSenderOptions>(sendGrid.Bind);

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
services.AddSingleton<ISearchServiceManager>(new SearchServiceManager(configuration, credentials));
services.AddSingleton<IDanceStatsManager>(new DanceStatsManager(new DanceStatsFileManager(appRoot)));

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
    typeof(SongFilterProfile),
    typeof(SongPropertyProfile),
    typeof(TagProfile));

services.AddViteServices();

services.AddHostedService<DanceStatsHostedService>();

var app = builder.Build();

if (!isDevelopment)
{
    app.UseAzureAppConfiguration();
}

app.Logger.LogInformation("Builder Built");
app.Logger.LogInformation($"Environment = {environment.EnvironmentName}");
var sentinel = configuration["Configuration:Sentinel"];
app.Logger.LogInformation($"Sentinel = {sentinel}");

using (var scope = app.Services.CreateScope())
{
    var sp = scope.ServiceProvider;
    var db = sp.GetRequiredService<DanceMusicContext>().Database;
    if (isDevelopment)
    {
        db.EnsureCreated();
    }

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
    app.UseDeveloperExceptionPage();
    if (useVite)
    {
        app.UseViteDevelopmentServer(true);
    }
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseStatusCodePagesWithReExecute("/Error/{0}");
    app.UseHsts();
}

app.UseHttpsRedirection();
var options = new RewriteOptions();
options.AddRedirectToHttps();
options.AddRedirectToWwwPermanent("music4dance.net");
app.UseRewriter(options);
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
