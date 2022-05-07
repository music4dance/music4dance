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
        private static readonly Dictionary<string, UserProfile> s_userCache = new();

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

            var user = await UserManager.FindByNameAsync(id)
                ?? await UserManager.FindByIdAsync(id);

            if (user == null)
            {
                return StatusCode((int)HttpStatusCode.NotFound);
            }

            var userName = user.UserName;
            if (!s_userCache.TryGetValue(id, out var profile))
            {
                var songIndex = Database.SongIndex;
                profile = new UserProfile
                {
                    UserName = id, // This will be username or id depending on what came in
                    IsPublic = user.Privacy > 0,
                    IsPseudo = user.IsPseudo,
                    SpotifyId = user.SpotifyId,
                    FavoriteCount = await songIndex.UserSongCount(userName, true),
                    BlockedCount = await songIndex.UserSongCount(userName, false),
                    EditCount = await songIndex.UserSongCount(userName, null),
                };
                s_userCache[id] = profile;
            }

            return View(profile);
        }

        public static void ClearCache()
        {
            s_userCache.Clear();
        }
    }
}
