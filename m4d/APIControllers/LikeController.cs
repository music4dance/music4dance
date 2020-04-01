using System;
using System.Threading.Tasks;
using m4dModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace m4d.APIControllers
{
    public class LikeModel
    {
        public string Dance { get; set; }
        public bool? Like { get; set; }
    }

    [ApiController]
    [Route("api/[controller]")]

    public class LikeController : DanceMusicApiController
    {
        public LikeController(DanceMusicContext context, UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, ISearchServiceManager searchService, IDanceStatsManager danceStatsManager, IConfiguration configuration) :
            base(context, userManager, roleManager, searchService, danceStatsManager, configuration)
        {
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(Guid id, string dance=null)
        {
            var user = await Database.UserManager.GetUserAsync(User);

            var like = Database.GetLike(user, id, string.IsNullOrWhiteSpace(dance) || dance == "null" ? null : dance);

            return Ok(new { like = like });
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Put(Guid id, [FromBody] LikeModel like)
        {
            var user = await Database.UserManager.GetUserAsync(User);

            var changed = Database.EditLike(user, id, like.Like, string.IsNullOrWhiteSpace(like.Dance) || like.Dance == "null" ? null : like.Dance);

            return Ok(new { changed = changed ? 1 : 0 });
        }
    }
}