using m4d.ViewModels;
using m4dModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace m4d.APIControllers
{


    [ApiController]
    [Route("api/[controller]")]
    public class DancesController : DanceMusicApiController
    {
        public DancesController(DanceMusicContext context, UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, ISearchServiceManager searchService, IDanceStatsManager danceStatsManager, IConfiguration configuration) :
            base(context, userManager, roleManager, searchService, danceStatsManager, configuration)
        {
        }

        [HttpGet]
        public IActionResult GetDances(bool details=false)
        {
            // This should eventually take a filter (or multiple filter) parameter
            var dances = Dance.DanceLibrary.NonPerformanceDanceTypes;
            if (details)
            {
                return Ok(dances);
            }
            var jsonDances = DanceJson.Convert(dances);

            return JsonCamelCase(jsonDances);
        }

        [HttpGet("{id}")]

        public IActionResult GetDance(string id)
        {
            var o = Dance.DanceLibrary.DanceFromId(id);
            if (o != null)
            {
                return JsonCamelCase(new DanceJson(o));
            }
            return NotFound();
        }
    }
}
