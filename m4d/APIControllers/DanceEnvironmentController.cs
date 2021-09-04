using m4dModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace m4d.APIControllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DanceEnvironmentController : DanceMusicApiController
    {
        private readonly DanceStatsInstance _statistics;

        public DanceEnvironmentController(DanceMusicContext context,
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
            var environment = new DanceEnvironment(_statistics);
            return JsonCamelCase(environment);
        }

        [HttpGet("{id}")]
        [ResponseCache(Duration = 600, Location = ResponseCacheLocation.Any, NoStore = false)]
        public IActionResult Get(string id)
        {
            var sparse = new DanceStatsSparse(_statistics.FromId(id));
            return JsonCamelCase(sparse);
        }
    }
}
