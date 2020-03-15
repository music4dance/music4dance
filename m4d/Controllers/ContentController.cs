using System.Collections.Generic;
using System.Linq;
using m4dModels;
using Microsoft.AspNetCore.Identity;

namespace m4d.Controllers
{
    public class ContentController : DanceMusicController
    {
        public ContentController(DanceMusicContext context, UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, ISearchServiceManager searchService, IDanceStatsManager danceStatsManager) :
            base(context, userManager, roleManager, searchService, danceStatsManager) { }

        protected void SetupLikes(IEnumerable<Song> songs, string danceId)
        {
            var user = Database.UserManager.GetUserAsync(HttpContext.User).Result;
            var userName =  user?.UserName;
            var list = songs.ToList();
            var likes = Database.UserLikes(list, userName);
            if (likes != null)
            {
                ViewBag.Likes = likes;
            }

            if (danceId == null) return;

            var danceLikes = Database.UserDanceLikes(list, danceId, userName);
            if (danceLikes != null)
            {
                ViewBag.DanceLikes = danceLikes;
                ViewBag.DanceId = danceId;
            }
        }
    }
}
