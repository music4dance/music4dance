using m4dModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.FileProviders;

namespace m4d.Controllers;

public class ContentController : DanceMusicController
{
    public ContentController(DanceMusicContext context,
        UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager,
        ISearchServiceManager searchService, IDanceStatsManager danceStatsManager,
        IConfiguration configuration, IFileProvider fileProvider, ILogger logger = null) :
        base(context, userManager, roleManager, searchService, danceStatsManager, configuration, fileProvider, logger)
    {
    }
}
