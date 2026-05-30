using m4d.Services;
using m4d.Services.ServiceHealth;
using m4d.ViewModels;

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

    private const int ActivityLogPageSize = 100;

    // GET: ActivityLogs
    public IActionResult Index(int page = 1)
    {
        var query = Database.Context.ActivityLog
            .OrderByDescending(l => l.Date)
            .Include(a => a.User);
        var totalCount = query.Count();
        var entries = query
            .Skip((page - 1) * ActivityLogPageSize)
            .Take(ActivityLogPageSize)
            .AsEnumerable()
            .Select(item => new ActivityLogEntry
            {
                Id = item.Id,
                Date = item.Date,
                UserName = item.User?.UserName,
                Action = item.Action,
                Details = item.Details,
            })
            .ToList();

        var viewModel = new ActivityLogPageModel
        {
            Entries = entries,
            Page = page,
            TotalPages = (int)Math.Ceiling((double)totalCount / ActivityLogPageSize),
        };

        return Vue3("Activity Log", "Admin: Activity log", "activity-log", viewModel);
    }
}
