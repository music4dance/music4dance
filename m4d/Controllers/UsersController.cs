using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using m4d.ViewModels;
using m4dModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace m4d.Controllers
{
    public class UsersController : DanceMusicController
    {
        private static readonly Dictionary<string, UserProfile> _userCache =
            new Dictionary<string, UserProfile>();

        public UsersController(DanceMusicContext context,
            UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager,
            ISearchServiceManager searchService, IDanceStatsManager danceStatsManager,
            IConfiguration configuration) :
            base(context, userManager, roleManager, searchService, danceStatsManager, configuration)
        {
            // HelpPage = "song-list";
            UseVue = true;
        }

        // GET: Users/Info/username
        public async Task<IActionResult> Info(string id)
        {
            if (id == null)
            {
                return StatusCode((int)HttpStatusCode.BadRequest);
            }

            var user = await UserManager.FindByNameAsync(id);
            if (user == null)
            {
                return StatusCode((int)HttpStatusCode.NotFound);
            }

            if (!_userCache.TryGetValue(id, out var profile))
            {
                profile = new UserProfile
                {
                    UserName = user.UserName,
                    IsPublic = user.Privacy > 0,
                    IsPseudo = user.IsPseudo,
                    SpotifyId = user.SpotifyId,
                    favoriteCount = await Database.UserSongCount(id, true),
                    blockedCount = await Database.UserSongCount(id, false),
                    editCount = await Database.UserSongCount(id, null),
                };
                _userCache[id] = profile;
            }

            return View(profile);
        }

        public static void ClearCache()
        {
            _userCache.Clear();
        }
    }
}
