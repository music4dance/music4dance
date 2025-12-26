using m4d.Services;
using m4d.Services.ServiceHealth;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.FeatureManagement;

namespace m4d.Controllers;

public class ActivityLogController : DanceMusicController
{
    public ActivityLogController(
        DanceMusicContext context, UserManager<ApplicationUser> userManager,
        ISearchServiceManager searchService, IDanceStatsManager danceStatsManager,
        IConfiguration configuration, IFileProvider fileProvider, IBackgroundTaskQueue backroundTaskQueue,
        IFeatureManagerSnapshot featureManager, ILogger<ActivityLogController> logger,
        ServiceHealthManager serviceHealth) :
        base(context, userManager, searchService, danceStatsManager, configuration,
            fileProvider, backroundTaskQueue, featureManager, logger, serviceHealth)
    {
        HelpPage = "song-list";
    }

    // GET: ActivityLogs
    public IActionResult Index()
    {
        var list = Database.Context.ActivityLog.OrderByDescending(l => l.Date).Take(500).Include(a => a.User);
        return View(list);
    }
}
