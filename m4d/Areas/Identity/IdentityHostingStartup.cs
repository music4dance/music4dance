using m4dModels;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

[assembly: HostingStartup(typeof(m4d.Areas.Identity.IdentityHostingStartup))]
namespace m4d.Areas.Identity
{
    public class IdentityHostingStartup : IHostingStartup
    {
        public void Configure(IWebHostBuilder builder)
        {
            builder.ConfigureServices((context, services) => {
                services.AddDbContext<DanceMusicContext>(options =>
                    options.UseSqlServer(
                        context.Configuration.GetConnectionString("DanceMusicContextConnection")));

                services.AddDefaultIdentity<ApplicationUser>(options =>
                        {
                            options.SignIn.RequireConfirmedAccount = true;
                            options.User.RequireUniqueEmail = true; 
                            options.User.AllowedUserNameCharacters = string.Empty;

                        })
                    .AddUserValidator<UsernameValidator<ApplicationUser>>()
                    .AddRoles<IdentityRole>()
                    .AddEntityFrameworkStores<DanceMusicContext>();
            });
        }

        public static void SeedData(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            SeedRoles(roleManager);
            SeedUsers(userManager);
        }

        private static void SeedUsers(UserManager<ApplicationUser> userManager)
        {
            if (userManager.FindByNameAsync("administrator").Result != null)
            {
                return;
            }

            var user = new ApplicationUser {***REMOVED***, ***REMOVED***, EmailConfirmed = true };
            var result = userManager.CreateAsync(user, "***REMOVED***").Result;

            if (result.Succeeded)
            {
                string[] roles =
                {
                    DanceMusicService.TagRole, DanceMusicService.EditRole, DanceMusicService.DiagRole, DanceMusicService.DbaRole
                };

                foreach (var role in roles)
                {
                    userManager.AddToRoleAsync(user, role).Wait();
                }
            }
        }

        private static void SeedRoles(RoleManager<IdentityRole> roleManager)
        {
            foreach (var roleName in DanceMusicService.Roles)
            {
                if (!roleManager.RoleExistsAsync(roleName).Result)
                {
                    var result = roleManager.CreateAsync(new IdentityRole {Name = roleName}).Result;
                }
            }
        }

        private static void AddToRole(UserManager<ApplicationUser> userManager, ApplicationUser user, string role)
        {
            userManager.AddToRoleAsync(user, role);
        }
    }
}