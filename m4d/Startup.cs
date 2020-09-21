using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using AutoMapper;
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
using Microsoft.AspNetCore.Mvc;
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
        public IWebHostEnvironment Environment { get; }
        public IConfiguration Configuration { get; }


        public Startup(IWebHostEnvironment env, IConfiguration configuration)
        {
            Environment = env;
            Configuration = configuration;
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services.Configure<CookiePolicyOptions>(options =>
            {
                // This lambda determines whether user consent for non-essential cookies is needed for a given request.
                options.CheckConsentNeeded = context => true;
                options.MinimumSameSitePolicy = SameSiteMode.None;
            });

            services.ConfigureApplicationCookie(options =>
            {
                options.AccessDeniedPath = "/Identity/Account/AccessDenied";
                options.Cookie.Name = "music4dance";
                options.Cookie.HttpOnly = true;
                options.ExpireTimeSpan = TimeSpan.FromMinutes(60);
                options.LoginPath = "/Identity/Account/Login";
                options.ReturnUrlParameter = CookieAuthenticationDefaults.ReturnUrlParameter;
                options.SlidingExpiration = true;
            });

            services.Configure<PasswordHasherOptions>(option =>
                option.CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV2);

            var builder = services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_3_0);

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

            services.Configure<AuthorizationOptions>(options =>
            {
                options.AddPolicy("TokenAuthorization",
                    policy => policy.AddRequirements(new TokenRequirement(Configuration)));
            });

            services.AddAuthentication()
                .AddGoogle(options =>
                {
                    var googleAuthNSection =
                        Configuration.GetSection("Authentication:Google");

                    options.ClientId = googleAuthNSection["ClientId"];
                    options.ClientSecret = googleAuthNSection["ClientSecret"];
                })
                .AddFacebook(options =>
                {
                    options.AppId = Configuration["Authentication:Facebook:ClientId"];
                    options.AppSecret = Configuration["Authentication:Facebook:ClientSecret"];
                    options.Scope.Add("email");
                    options.Fields.Add("name");
                    options.Fields.Add("email");
                })
                .AddSpotify(options =>
                {
                    options.ClientId = Configuration["Authentication:Spotify:ClientId"];
                    options.ClientSecret = Configuration["Authentication:Spotify:ClientSecret"];

                    options.Scope.Add("user-read-email");
                    options.Scope.Add("playlist-modify-public");
                    options.Scope.Add("ugc-image-upload");

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

            services.AddreCAPTCHAV2(x =>
            {
                x.SiteKey = Configuration["Authentication:reCAPTCHA:SiteKey"];
                x.SiteSecret = Configuration["Authentication:reCAPTCHA:SecretKey"];
            });

            var appData = Path.Combine(Environment.WebRootPath, "AppData");

            services.AddSingleton<ISearchServiceManager>(new SearchServiceManager(Configuration));
            services.AddSingleton<IDanceStatsManager>(new DanceStatsManager(appData));

            services.AddSingleton(new RecomputeMarkerService(appData));

            services.AddControllers().AddNewtonsoftJson()
                .AddNewtonsoftJson(options => {
                    options.SerializerSettings.ContractResolver =
                        new DefaultContractResolver();
                });

            services.AddAutoMapper(
                typeof(SongProfile),
                typeof(SongFilterProfile), 
                typeof(SongPropertyProfile),
                typeof(TagProfile));
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.Use(async (context, next) =>
            {
                var url = context.Request.Path.Value;
                string blog = "/blog";
                if (url != null)
                {
                    var idx = url.IndexOf(blog);
                    if (idx != -1)
                    {
                        var path = url.Substring(idx + blog.Length);
                        context.Response.Redirect($"https://music4dance.blog{path}");
                        return;
                    }
                }

                await next();
            });

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    "Dances",
                    pattern: "dances/{group}/{dance}",
                    defaults: new { controller = "dance", action = "GroupRedirect" });
                endpoints.MapControllerRoute(
                    "DanceGroup",
                    pattern: "dances/{dance?}",
                    defaults: new { controller = "dance", action = "index" });
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");
                endpoints.MapRazorPages();
            });
        }
    }
}
