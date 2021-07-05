using m4dModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;

namespace m4d.Controllers
{
    public class ContentController : DanceMusicController
    {
        public ContentController(DanceMusicContext context,
            UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager,
            ISearchServiceManager searchService, IDanceStatsManager danceStatsManager,
            IConfiguration configuration) :
            base(context, userManager, roleManager, searchService, danceStatsManager, configuration)
        {
        }
    }
}
