using System.Linq;
using m4dModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace m4d.APIControllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TagController : DanceMusicApiController
    {
        private readonly DanceStatsInstance _statistics;

        public TagController(DanceMusicContext context,
            UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager,
            ISearchServiceManager searchService, IDanceStatsManager danceStatsManager,
            IConfiguration configuration) :
            base(context, userManager, roleManager, searchService, danceStatsManager, configuration)
        {
            _statistics = DanceStatsManager.Instance;
        }

        [HttpGet]
        [ResponseCache(Duration = 600, Location = ResponseCacheLocation.Any, NoStore = false)]
        public IActionResult Get()
        {
            return JsonCamelCase(
                _statistics.TagGroups.Where(g => g.Category != "Dance" && g.PrimaryId == null)
                    .Select(g => new { g.Key, g.Count }));
        }
    }
}
