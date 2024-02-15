using m4dModels;
using Microsoft.AspNetCore.Identity;

namespace m4d.Areas.Identity;

public static class UserManagerHelpers
{
    public static void SeedData(this UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager)
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

        var user = new ApplicationUser
        {
            ***REMOVED***, ***REMOVED***,
            EmailConfirmed = true
        };
        var result = userManager.CreateAsync(user, "***REMOVED***").Result;

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
