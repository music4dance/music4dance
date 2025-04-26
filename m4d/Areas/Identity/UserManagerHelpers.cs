using m4dModels;
using Microsoft.AspNetCore.Identity;

namespace m4d.Areas.Identity;

public static class UserManagerHelpers
{
    public static void SeedData(this UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager, IConfiguration config)
    {
        SeedRoles(roleManager);
        SeedUsers(userManager, config);
    }

    private static void SeedUsers(UserManager<ApplicationUser> userManager, IConfiguration config)
    {
        if (userManager.FindByNameAsync("administrator").Result != null)
        {
            return;
        }

        var name = config["M4D_ADMIN_USER"];
        if (string.IsNullOrEmpty(name))
        {
            throw new Exception("No user name for admin user");
        }
        var user = new ApplicationUser
        {
            UserName = name, Email = $"{name}@music4dance.net",
            EmailConfirmed = true
        };
        var password = config["M4D_ADMIN_PASSWORD"];
        if (string.IsNullOrEmpty(password))
        {
            throw new Exception("No password for admin user");
        }
        var result = userManager.CreateAsync(user, password).Result;

        if (result.Succeeded)
        {
            string[] roles =
            {
                DanceMusicCoreService.TagRole, DanceMusicCoreService.EditRole,
                DanceMusicCoreService.DiagRole, DanceMusicCoreService.DbaRole
            };

            foreach (var role in roles)
            {
                userManager.AddToRoleAsync(user, role).Wait();
            }
        }
    }

    private static void SeedRoles(RoleManager<IdentityRole> roleManager)
    {
        foreach (var roleName in DanceMusicCoreService.Roles)
        {
            if (!roleManager.RoleExistsAsync(roleName).Result)
            {
                var result = roleManager.CreateAsync(new IdentityRole { Name = roleName })
                    .Result;
            }
        }
    }
}
