using Microsoft.AspNetCore.Identity;
namespace m4d.Areas.Identity;

public static class UserManagerHelpers
{
    public static async Task SeedData(this UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager, IConfiguration configuration)
    {
        await SeedRoles(roleManager);
        await SeedAdminUser(userManager, configuration);
        await SeedTestUser(userManager, configuration);
        await SeedEditorUser(userManager, configuration);
    }

    private static async Task SeedAdminUser(UserManager<ApplicationUser> userManager, IConfiguration configuration)
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

    /// <summary>
    /// A deliberately low-privilege account (no roles at all) for testing the ordinary
    /// voting/tagging path a real user hits - [Authorize]-only actions like
    /// SongController.UndoUserChanges, not the canEdit-gated bulk-edit surface. See
    /// architecture/contributor-test-environments.md, L1d.
    /// </summary>
    private static async Task SeedTestUser(UserManager<ApplicationUser> userManager, IConfiguration configuration)
    {
        var testUser = configuration["M4D_TEST_USER"];
        if (string.IsNullOrEmpty(testUser) || await userManager.FindByNameAsync(testUser) != null)
        {
            return;
        }

        var testPassword = configuration["M4D_TEST_PASSWORD"];

        if (string.IsNullOrWhiteSpace(testPassword))
        {
            throw new Exception("M4D_TEST_USER and M4D_TEST_PASSWORD must be set in the configuration.");
        }

        // Deliberately NOT @music4dance.net - that domain flips ApplicationUser.IsM4d/IsPseudo,
        // which would silently stop this account from exercising the real-user code path.
        var user = new ApplicationUser
        {
            UserName = testUser, Email = $"{testUser}@example.invalid",
            EmailConfirmed = true
        };
        await userManager.CreateAsync(user, testPassword);
    }

    /// <summary>
    /// Covers the canEdit-gated tag-removal/full-edit surface without handing out dbAdmin or
    /// showDiagnostics. See architecture/contributor-test-environments.md, L1d.
    /// </summary>
    private static async Task SeedEditorUser(UserManager<ApplicationUser> userManager, IConfiguration configuration)
    {
        var editorUser = configuration["M4D_EDITOR_USER"];
        if (string.IsNullOrEmpty(editorUser) || await userManager.FindByNameAsync(editorUser) != null)
        {
            return;
        }

        var editorPassword = configuration["M4D_EDITOR_PASSWORD"];

        if (string.IsNullOrWhiteSpace(editorPassword))
        {
            throw new Exception("M4D_EDITOR_USER and M4D_EDITOR_PASSWORD must be set in the configuration.");
        }

        var user = new ApplicationUser
        {
            UserName = editorUser, Email = $"{editorUser}@example.invalid",
            EmailConfirmed = true
        };
        var result = await userManager.CreateAsync(user, editorPassword);

        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(user, DanceMusicCoreService.EditRole);
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
