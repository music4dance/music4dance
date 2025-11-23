using m4dModels;

using Microsoft.AspNetCore.Identity;
namespace m4d.Areas.Identity;

public static class UserManagerHelpers
{
    public static async Task SeedData(this UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager, IConfiguration configuration)
    {
        await SeedRoles(roleManager);
        await SeedUsers(userManager, configuration);
    }

    private static async Task SeedUsers(UserManager<ApplicationUser> userManager, IConfiguration configuration)
    {
        var adminUser = configuration["M4D_ADMIN_USER"];
        if (string.IsNullOrEmpty(adminUser) || await userManager.FindByNameAsync(adminUser) != null)
        {
            return;
        }

        var adminPassword = configuration["M4D_ADMIN_PASSWORD"];

        if (string.IsNullOrWhiteSpace(adminPassword))
        {
            throw new Exception("M4D_ADMIN_USER and M4D_ADMIN_PASSWORD must be set in the configuration.");
        }

        var user = new ApplicationUser
        {
            UserName = adminUser, Email = $"{adminUser}@music4dance.net",
            EmailConfirmed = true
        };
        var result = await userManager.CreateAsync(user, adminPassword);

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

    private static async Task SeedRoles(RoleManager<IdentityRole> roleManager)
    {
        foreach (var roleName in DanceMusicCoreService.Roles)
        {
            if (!roleManager.RoleExistsAsync(roleName).Result)
            {
                _ = await roleManager.CreateAsync(new IdentityRole { Name = roleName });
            }
        }
    }
}
