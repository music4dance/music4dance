using Microsoft.EntityFrameworkCore;
using m4dModels;
using m4d.Areas.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Azure.Identity;
using Vite.AspNetCore.Extensions;
using Microsoft.AspNetCore.Rewrite;
using m4d.Utilities;
using m4d.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.FileProviders;
using System.Reflection;
using m4d.ViewModels;
using Newtonsoft.Json.Serialization;
using Owl.reCAPTCHA;
using Microsoft.FeatureManagement;

Console.WriteLine("Entering Main");

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("DanceMusicContextConnection") ?? throw new InvalidOperationException("Connection string 'DanceMusicContexstConnection' not found.");

var services = builder.Services;
var environment = builder.Environment;
var configuration = builder.Configuration;

var logging = builder.Logging;
logging.ClearProviders();
logging.AddConsole();
logging.AddAzureWebAppDiagnostics();

Console.WriteLine($"Environment: {environment.EnvironmentName}");

builder.Services.AddFeatureManagement();

if (!environment.IsDevelopment())
{
    var credentials = new ManagedIdentityCredential();
    configuration.AddAzureAppConfiguration(options =>
    {
        options.Connect(
            new Uri(configuration["AppConfig:Endpoint"]),
            credentials)
        .ConfigureKeyVault(
            kv => { kv.SetCredential(credentials); })
        .UseFeatureFlags(featureFlagOptions => {
            featureFlagOptions.CacheExpirationInterval = TimeSpan.FromMinutes(5);
        })
        .Select(KeyFilter.Any, LabelFilter.Null)
        .Select(KeyFilter.Any, environment.EnvironmentName)
        .ConfigureRefresh(refresh =>
        {
            refresh.Register("Configuration:Sentinel", environment.EnvironmentName, refreshAll: true)
                .SetCacheExpiration(TimeSpan.FromMinutes(5));
        });
    });

    services.AddAzureAppConfiguration();
}

services.AddDbContext<DanceMusicContext>(options => options.UseSqlServer(connectionString));

services.AddDefaultIdentity<ApplicationUser>(
        options =>
        {
            options.SignIn.RequireConfirmedAccount = true;
            options.User.RequireUniqueEmail = true;
            options.User.AllowedUserNameCharacters = string.Empty;
        })
    .AddUserValidator<UsernameValidator<ApplicationUser>>()
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<DanceMusicContext>();

// Add services to the container.
services.AddControllersWithViews();

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
services.Configure<AuthMessageSenderOptions>(options => sendGrid.Bind(options));

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
services.AddSingleton<ISearchServiceManager>(new SearchServiceManager(configuration));
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

if (!environment.IsDevelopment())
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
    sp.GetRequiredService<DanceMusicContext>().Database.Migrate();

    if (environment.IsDevelopment())
    {
        var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = sp.GetRequiredService<RoleManager<IdentityRole>>();
        UserManagerHelpers.SeedData(userManager, roleManager);
    }
}

// Configure the HTTP request pipeline.
app.Logger.LogInformation(@"Configuring request pipeline");

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseViteDevMiddleware();
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
app.UseStaticFiles();
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
                var path = url.Substring(idx + blog.Length);
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

app.Run();
