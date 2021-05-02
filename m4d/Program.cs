using System;
using System.Diagnostics;
using Azure.Identity;
using m4d.Areas.Identity;
using m4dModels;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace m4d
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var host = CreateHostBuilder(args).Build();

            using (var scope = host.Services.CreateScope())
            {
                var serviceProvider = scope.ServiceProvider;
                try
                {
                    //using var context = scope.ServiceProvider.GetRequiredService<DanceMusicContext>();
                    //context.Database.Migrate();

                    var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                    var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
                    IdentityHostingStartup.SeedData(userManager, roleManager);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.Message);
                }
            }
            host.Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder => webBuilder
                .ConfigureAppConfiguration((hostingContext, config) =>
                {
                    var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
                    var isDevelopment = environment == Environments.Development;

                    if (!isDevelopment) 
                    {
                        var settings = config.Build();
                        var credentials = new ManagedIdentityCredential();

                        config.AddAzureAppConfiguration(options =>
                            options.Connect(new Uri(settings["AppConfig:Endpoint"]), credentials)
                                .ConfigureKeyVault(kv => { kv.SetCredential(credentials); }));
                    }

                    // Working version of app config/keyvault for dev environment 
                    //var settings = config.Build();
                    //config.AddAzureAppConfiguration(options =>
                    //    options.Connect(settings["ConnectionStrings:AppConfig"])
                    //        .ConfigureKeyVault(kv => { kv.SetCredential(new DefaultAzureCredential()); }));

                })
                .UseStartup<Startup>());
    }
}
