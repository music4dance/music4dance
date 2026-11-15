using m4d.Services;
using m4d.Services.ServiceHealth;
using m4d.ViewModels;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using Microsoft.FeatureManagement;

using System.Net;

namespace m4d.Controllers;

/// <summary>
/// The review queue of songs that share a Spotify, iTunes or ISRC id, and the background tasks
/// that seed it and merge it in bulk. See architecture/songs/shared-id-review.md.
/// </summary>
[Authorize(Roles = "dbAdmin")]
public class SharedIdsController(
    DanceMusicContext context, UserManager<ApplicationUser> userManager,
    ISearchServiceManager searchService, IDanceStatsManager danceStatsManager,
    IConfiguration configuration, IFileProvider fileProvider, IBackgroundTaskQueue backgroundTaskQueue,
    IFeatureManagerSnapshot featureManager, ILogger<SharedIdsController> logger,
    ServiceHealthManager serviceHealth, SharedIdPreviewStore previews)
    : DanceMusicController(context, userManager, searchService, danceStatsManager, configuration,
        fileProvider, backgroundTaskQueue, featureManager, logger, serviceHealth)
{
    //
    // GET: /SharedIds
    // A page of the queue, sorted by the last merge-all preview when there is one, with the
    // clusters left for later at the end
    public async Task<ActionResult> Index(int page = 1, string message = null)
    {
        if (ServiceHealth?.IsServiceHealthy("SearchService") == false)
        {
            return ReturnError(HttpStatusCode.ServiceUnavailable, "Search service temporarily unavailable. Please try again later.");
        }

        var user = await Database.FindUser(UserName);
        var queue = await Database.SharedIds.LoadPage(user, page, previews.Ranker(), previews.IsDeferred);
        return Vue3("Shared Song Ids", "Admin: songs that share a service id", "shared-id-review",
            SharedIdReviewModel.Build(queue, previews.Preview, message));
    }

    //
    // POST: /SharedIds/Seed
    // Queues every pair of songs that already share an id
    [HttpPost]
    [ValidateAntiForgeryToken]
    public ActionResult Seed() =>
        RunAdminTask("SeedSharedIds", async dms => $"Added={await dms.SharedIds.Seed(AdminMonitor.UpdateTask)}");

    //
    // POST: /SharedIds/MergeMatching
    // Merges every pending cluster whose songs also have the same title and artist
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<ActionResult> MergeMatching()
    {
        var user = await Database.FindUser(UserName);
        return RunAdminTask("MergeMatchingSharedIds", async dms =>
        {
            var merged = await dms.SharedIds.MergeMatching(user, AdminMonitor.UpdateTask);
            await DanceStatsManager.ClearCache(dms, false);
            return $"Merged={merged}";
        });
    }

    //
    // POST: /SharedIds/MergeFeaturing
    // Merges every pending cluster whose titles and artists differ only by feat., moving the
    // featured artists into the merged song's individual artists
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<ActionResult> MergeFeaturing()
    {
        var user = await Database.FindUser(UserName);
        return RunAdminTask("MergeFeaturingSharedIds", async dms =>
        {
            var merged = await dms.SharedIds.MergeFeaturing(user, AdminMonitor.UpdateTask);
            await DanceStatsManager.ClearCache(dms, false);
            return $"Merged={merged}";
        });
    }

    //
    // POST: /SharedIds/PreviewMergeMatching
    // Works out what MergeMatching and MergeFeaturing would merge and skip, and the names each
    // merge would land on, without merging anything. Also sorts the review page.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<ActionResult> PreviewMergeMatching()
    {
        var user = await Database.FindUser(UserName);
        return RunAdminTask("PreviewMergeMatchingSharedIds", async dms =>
        {
            var plans = await dms.SharedIds.PlanMergeMatching(AdminMonitor.UpdateTask, user);
            previews.Set(plans, DateTimeOffset.UtcNow);
            return $"WouldMerge={plans.Count(p => p.WillMerge)}, " +
                $"WouldMergeFeat={plans.Count(p => p.WillMergeFeaturing)}, " +
                $"Skipped={plans.Count(p => !p.WillMerge && !p.WillMergeFeaturing)} (see /SharedIds/Preview)";
        });
    }

    //
    // GET: /SharedIds/Preview
    // The result of the last PreviewMergeMatching
    public ActionResult Preview() => View(previews.Preview);
}
