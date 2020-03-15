using System;
using System.Threading.Tasks;
using m4dModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace m4d.APIControllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UpdateRatingsController : DanceMusicApiController
    {
        public UpdateRatingsController(DanceMusicContext context, UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, ISearchServiceManager searchService, IDanceStatsManager danceStatsManager) :
            base(context, userManager, roleManager, searchService, danceStatsManager)
        {
        }

        [HttpPost("{id}")]

        public async Task<IActionResult> Update(Guid id, [FromBody] JTags tags)
        {
            var uts = tags.ToUserTags();

            var user = await Database.UserManager.GetUserAsync(User);

            var changed = Database.EditTags(user, id, uts);

            return Ok(new{changed=changed?1:0});
        }
    }
}