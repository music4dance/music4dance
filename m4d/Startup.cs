using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using m4d.Services;
using m4d.Utilities;
using m4d.ViewModels;
using m4dModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Rewrite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json.Serialization;
using Owl.reCAPTCHA;

namespace m4d
{
    public class Startup
    {
        public Startup(IWebHostEnvironment env, IConfiguration configuration)
        {
            Environment = env;
            Configuration = configuration;
        }

        public IWebHostEnvironment Environment { get; }
        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services.Configure<CookiePolicyOptions>(
                options =>
                {
                    // This lambda determines whether user consent for non-essential cookies is needed for a given request.
                    options.CheckConsentNeeded = context => true;
                    options.MinimumSameSitePolicy = SameSiteMode.None;
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

            var builder = services.AddMvc();

#if DEBUG
            builder.AddRazorRuntimeCompilation();
#endif

            var physicalProvider = Environment.ContentRootFileProvider;
            var embeddedProvider = new EmbeddedFileProvider(Assembly.GetEntryAssembly());
            var compositeProvider = new CompositeFileProvider(physicalProvider, embeddedProvider);
            services.AddSingleton<IFileProvider>(compositeProvider);

            services.AddTransient<IEmailSender, EmailSender>();
            var sendGrid = Configuration.GetSection("Authentication:SendGrid");
            services.Configure<AuthMessageSenderOptions>(options => sendGrid.Bind(options));

            services.Configure<AuthorizationOptions>(
                options =>
                {
                    options.AddPolicy(
                        "TokenAuthorization",
                        policy => policy.AddRequirements(new TokenRequirement(Configuration)));
                });

            services.AddAuthentication()
                .AddGoogle(
                    options =>
                    {
                        var googleAuthNSection =
                            Configuration.GetSection("Authentication:Google");

                        options.ClientId = googleAuthNSection["ClientId"];
                        options.ClientSecret = googleAuthNSection["ClientSecret"];
                    })
                .AddFacebook(
                    options =>
                    {
                        options.AppId = Configuration["Authentication:Facebook:ClientId"];
                        options.AppSecret = Configuration["Authentication:Facebook:ClientSecret"];
                        options.Scope.Add("email");
                        options.Fields.Add("name");
                        options.Fields.Add("email");
                    })
                .AddSpotify(
                    options =>
                    {
                        options.ClientId = Configuration["Authentication:Spotify:ClientId"];
                        options.ClientSecret = Configuration["Authentication:Spotify:ClientSecret"];

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
                    x.SiteKey = Configuration["Authentication:reCAPTCHA:SiteKey"];
                    x.SiteSecret = Configuration["Authentication:reCAPTCHA:SecretKey"];
                });

            var appRoot = Environment.WebRootPath;
            services.AddSingleton<ISearchServiceManager>(new SearchServiceManager(Configuration));
            services.AddSingleton<IDanceStatsManager>(new DanceStatsManager(appRoot));

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

            services.AddHostedService<DanceStatsHostedService>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
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

            app.UseAuthentication();
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

            app.UseEndpoints(
                endpoints =>
                {
                    endpoints.MapControllerRoute(
                        "Dances",
                        "dances/{group}/{dance}",
                        new { controller = "dance", action = "GroupRedirect" });
                    endpoints.MapControllerRoute(
                        "DanceEdit",
                        "dances/edit",
                        new { controller = "dance", action = "edit" });
                    endpoints.MapControllerRoute(
                        "DanceGroup",
                        "dances/{dance?}",
                        new { controller = "dance", action = "index" });
                    endpoints.MapControllerRoute(
                        "default",
                        "{controller=Home}/{action=Index}/{id?}");
                    endpoints.MapRazorPages();
                });
        }
    }
}
