using System;
using Azure.Identity;
using m4d.Areas.Identity;
using m4dModels;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace m4d
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("Entering Main");
            try
            {
                var host = CreateHostBuilder(args).Build();

                var env = host.Services.GetRequiredService<IWebHostEnvironment>();
                using var scope = host.Services.CreateScope();
                var serviceProvider = scope.ServiceProvider;

                using var context =
                    scope.ServiceProvider.GetRequiredService<DanceMusicContext>();
                context.Database.Migrate();

                if (env.IsDevelopment())
                {
                    var userManager =
                        serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                    var roleManager =
                        serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
                    IdentityHostingStartup.SeedData(userManager, roleManager);
                }

                host.Run();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            Console.WriteLine("Exiting Main");
        }

        public static IHostBuilder CreateHostBuilder(string[] args)
        {
            var builder = Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(
                    webBuilder => webBuilder
                        .ConfigureAppConfiguration(
                            (hostingContext, config) =>
                            {
                                var environment =
                                    Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
                                Console.WriteLine($"Environment: {environment}");

                                var isDevelopment = environment == Environments.Development;

                                if (!isDevelopment)
                                {
                                    Console.WriteLine("Getting Configuration");
                                    var settings = config.Build();
                                    var credentials = new ManagedIdentityCredential();

                                    config.AddAzureAppConfiguration(
                                        options =>
                                            options.Connect(
                                                    new Uri(settings["AppConfig:Endpoint"]),
                                                    credentials)
                                                .ConfigureKeyVault(
                                                    kv => { kv.SetCredential(credentials); }));
                                }
                            })
                        .UseStartup<Startup>());

            builder.ConfigureLogging(
                logging =>
                {
                    logging.ClearProviders();
                    logging.AddConsole();
                    logging.AddAzureWebAppDiagnostics();
                });

            return builder;
        }
    }
}
