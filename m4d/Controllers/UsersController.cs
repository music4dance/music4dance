using System.Net;

using m4d.Services;
using m4d.ViewModels;

using m4dModels;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using Microsoft.FeatureManagement;

namespace m4d.Controllers;

public class UsersController(
    DanceMusicContext context, UserManager<ApplicationUser> userManager,
    ISearchServiceManager searchService, IDanceStatsManager danceStatsManager,
    IConfiguration configuration, IFileProvider fileProvider, IBackgroundTaskQueue backroundTaskQueue,
    IFeatureManagerSnapshot featureManager, ILogger<ActivityLogController> logger) : DanceMusicController(context, userManager, searchService, danceStatsManager, configuration,
        fileProvider, backroundTaskQueue, featureManager, logger)
{
    private static readonly Dictionary<string, UserProfile> s_userCache = [];

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

        return Vue3($"Info for {id}",
            $"Favorites and song lists for {id}",
            "user-info",
            profile, "account-management");
    }

    public static void ClearCache()
    {
        s_userCache.Clear();
    }
}
