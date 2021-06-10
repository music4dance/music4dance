using m4dModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace m4d.APIControllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DancesStatisticsController : DanceMusicApiController
    {
        private readonly DanceStatsInstance _statistics;

        public DancesStatisticsController(DanceMusicContext context,
            UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager,
            ISearchServiceManager searchService, IDanceStatsManager danceStatsManager,
            IConfiguration configuration) :
            base(context, userManager, roleManager, searchService, danceStatsManager, configuration)
        {
            _statistics = DanceStatsManager.Instance;
        }

        [HttpGet]
        public IActionResult Get()
        {
            return JsonCamelCase(_statistics);
        }

        [HttpGet("{id}")]
        public IActionResult Get(string id)
        {
            return JsonCamelCase(_statistics.FromId(id));
        }
    }
}
