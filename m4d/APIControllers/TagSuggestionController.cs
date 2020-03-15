using System;
using m4dModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace m4d.APIControllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TagSuggestionController : DanceMusicApiController
    {
        public TagSuggestionController(DanceMusicContext context, UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, ISearchServiceManager searchService, IDanceStatsManager danceStatsManager) :
            base(context, userManager, roleManager, searchService, danceStatsManager)
        {
        }


        [HttpGet]

        public IActionResult GetTagSuggestions(string user = null, char? targetType = null, int count = int.MaxValue, bool normalized = false)
        {
            return GetTagSuggestions(null, user, targetType, count, normalized);
        }


        [HttpGet("{id}")]

        public IActionResult GetTagSuggestions(string id, string user = null, char? targetType = null, int count = int.MaxValue, bool normalized = false)
        {
            //var test = false;
            try
            {
                var tags = Database.GetTagSuggestions(user, targetType, id, count, normalized);
                //if (test)
                //    return NotFound();
                //else
                    return JsonCamelCase(tags);
            }
            catch (Exception)
            {
                return NotFound();
            }
        }
    }
}
