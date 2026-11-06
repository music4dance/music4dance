#nullable disable

using m4d.Controllers;
using m4d.PublicApi;
using m4d.Services.ServiceHealth;
using m4d.Tests.TestHelpers;

using m4dApi;

using m4dModels;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.FeatureManagement;

using Moq;

namespace m4d.Tests.Controllers;

// Only Development seeds roles, so test and production had no partnerDeveloper role and the first
// Add developer threw from AddToRoleAsync. These start from a database with no roles, like those.
[TestClass]
public class PartnerKeysControllerTests
{
    private sealed record Setup(
        PartnerKeysController Controller, UserManager<ApplicationUser> Users,
        RoleManager<IdentityRole> Roles, ApplicationUser Developer);

    private static async Task<Setup> Create()
    {
        var context = new DanceMusicContext(new DbContextOptionsBuilder<DanceMusicContext>()
            .UseInMemoryDatabase($"partner-keys-controller-{Guid.NewGuid()}")
            .Options);
        var normalizer = new UpperInvariantLookupNormalizer();
        var users = new UserManager<ApplicationUser>(
            new UserStore<ApplicationUser>(context), Options.Create(new IdentityOptions()),
            new PasswordHasher<ApplicationUser>(), [], [], normalizer, new IdentityErrorDescriber(),
            null, NullLogger<UserManager<ApplicationUser>>.Instance);
        var roles = new RoleManager<IdentityRole>(
            new RoleStore<IdentityRole>(context), [], normalizer, new IdentityErrorDescriber(),
            NullLogger<RoleManager<IdentityRole>>.Instance);

        var developer = new ApplicationUser { UserName = "partnerdev", Email = "partnerdev@example.com" };
        _ = await users.CreateAsync(developer);

        var configuration = new ConfigurationBuilder().Build();
        var keys = new PartnerKeyService(context, new Mock<IPartnerKeyVault>().Object, configuration);
        _ = await keys.SaveClient("danzq", "DanzQ", null, null, null, disabled: false, create: true);

        var controller = new PartnerKeysController(
            context, users, new Mock<ISearchServiceManager>().Object,
            new Mock<IDanceStatsManager>().Object, configuration, new Mock<IFileProvider>().Object,
            new TestBackgroundTaskQueue(), new Mock<IFeatureManagerSnapshot>().Object,
            NullLogger<PartnerKeysController>.Instance,
            new ServiceHealthManager(NullLogger<ServiceHealthManager>.Instance), keys)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        return new Setup(controller, users, roles, developer);
    }

    private static string RouteValue(ActionResult result, string key) =>
        ((RedirectToActionResult)result).RouteValues?[key] as string;

    [TestMethod]
    public async Task AddDeveloper_RoleMissing_CreatesItAndGrantsIt()
    {
        var setup = await Create();
        Assert.IsFalse(await setup.Roles.RoleExistsAsync(DanceMusicCoreService.PartnerDeveloperRole));

        var result = await setup.Controller.AddDeveloper("danzq", "partnerdev", setup.Roles);

        Assert.IsNull(RouteValue(result, "error"));
        Assert.IsTrue(await setup.Roles.RoleExistsAsync(DanceMusicCoreService.PartnerDeveloperRole));
        Assert.IsTrue(await setup.Users.IsInRoleAsync(setup.Developer, DanceMusicCoreService.PartnerDeveloperRole));
    }

    [TestMethod]
    public async Task AddDeveloper_Twice_StillOneRoleAndNoError()
    {
        // Retrying after the old failure: the developer link already exists.
        var setup = await Create();

        _ = await setup.Controller.AddDeveloper("danzq", "partnerdev", setup.Roles);
        var result = await setup.Controller.AddDeveloper("danzq", "partnerdev@example.com", setup.Roles);

        Assert.IsNull(RouteValue(result, "error"));
        Assert.AreEqual(1, setup.Roles.Roles.Count(r => r.Name == DanceMusicCoreService.PartnerDeveloperRole));
        Assert.IsTrue(await setup.Users.IsInRoleAsync(setup.Developer, DanceMusicCoreService.PartnerDeveloperRole));
    }
}
