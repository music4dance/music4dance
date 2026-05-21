using m4d.Services;
using m4d.Services.ServiceHealth;
using m4d.ViewModels;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using Microsoft.FeatureManagement;

using System.Net;

namespace m4d.Controllers;

public class UsersController(
    DanceMusicContext context, UserManager<ApplicationUser> userManager,
    ISearchServiceManager searchService, IDanceStatsManager danceStatsManager,
    IConfiguration configuration, IFileProvider fileProvider, IBackgroundTaskQueue backroundTaskQueue,
    IFeatureManagerSnapshot featureManager, ILogger<ActivityLogController> logger,
    ServiceHealthManager serviceHealth) : DanceMusicController(context, userManager, searchService, danceStatsManager, configuration,
        fileProvider, backroundTaskQueue, featureManager, logger, serviceHealth)
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
        var isAuthenticated = User.Identity?.IsAuthenticated == true;
        // For anonymous (private) users always expose the GUID, not the real username,
        // even if the profile was looked up by username.
        // For public users, always use the canonical username so a GUID URL param
        // doesn't make the client treat a public user as anonymous.
        var profileUserName = user.Privacy == 0 && !user.IsPseudo ? user.Id : user.UserName;

        // Unauthenticated visitors bypass the cache to prevent leaking cached counts.
        if (!isAuthenticated || !s_userCache.TryGetValue(id, out var profile))
        {
            if (isAuthenticated)
            {
                var songIndex = Database.SongIndex;
                var favoriteCountTask = songIndex.UserSongCount(userName, true);
                var blockedCountTask = songIndex.UserSongCount(userName, false);
                var editCountTask = songIndex.UserSongCount(userName, null);
                await Task.WhenAll(favoriteCountTask, blockedCountTask, editCountTask);

                profile = new UserProfile
                {
                    UserName = profileUserName,
                    IsPseudo = user.IsPseudo,
                    SpotifyId = user.SpotifyId,
                    FavoriteCount = favoriteCountTask.Result,
                    BlockedCount = blockedCountTask.Result,
                    EditCount = editCountTask.Result,
                };
                s_userCache[id] = profile;
            }
            else
            {
                // Unauthenticated visitors don't see song lists, so skip the Azure Search calls.
                profile = new UserProfile
                {
                    UserName = profileUserName,
                    IsPseudo = user.IsPseudo,
                    SpotifyId = user.SpotifyId,
                };
            }
        }

        return Vue3($"Info for {profile.UserName}",
            $"Favorites and song lists for {profile.UserName}",
            "user-info",
            profile, "account-management");
    }

    public static void ClearCache()
    {
        s_userCache.Clear();
    }
}
