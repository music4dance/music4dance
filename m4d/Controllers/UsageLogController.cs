using m4d.Services;

using m4dModels;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.FeatureManagement;

namespace m4d.Controllers;

public class UsageModel
{
    public DateTime LastUpdate { get; set; }
    public List<UsageSummary> Summaries { get; set; }
}
public class UsageLogController : DanceMusicController
{
    private static UsageModel s_model;

    public UsageLogController(
        DanceMusicContext context, UserManager<ApplicationUser> userManager,
        ISearchServiceManager searchService, IDanceStatsManager danceStatsManager,
        IConfiguration configuration, IFileProvider fileProvider, IBackgroundTaskQueue backroundTaskQueue,
        IFeatureManagerSnapshot featureManager, ILogger<ActivityLogController> logger) :
        base(context, userManager, searchService, danceStatsManager, configuration,
            fileProvider, backroundTaskQueue, featureManager, logger)
    {
        HelpPage = "song-list";
        context.Database.SetCommandTimeout(1000);
    }

    // GET: UsageLogs
    public IActionResult Index()
    {
        var model = s_model ?? new UsageModel
        {
            Summaries = [.. Context.Database.SqlQuery<UsageSummary>(
                    $"""
                        SELECT [UsageId], MAX([UserName]) as UserName, MIN([Date]) as MinDate, MAX([Date]) as MaxDate, COUNT(*) as Hits FROM dbo.UsageLog GROUP BY [UsageId] HAVING COUNT(*) > 5 ORDER BY Hits DESC
                    """)],
            LastUpdate = DateTime.Now,
        };
        s_model = model;

        return View(model);
    }

    // GET: UsageLogs/DayLog
    public IActionResult DayLog(int days = 0)
    {
        IQueryable<UsageLog> log = Database.Context.UsageLog;

        if (days > 0)
        {
            log = log.Where(l => l.Date < DateTimeOffset.Now.AddDays(-days));
        }
        ViewData["days"] = days;

        return View(log.OrderByDescending(l => l.Id).Take(5000));
    }

    // GET: UsageLogs/UserLog
    public IActionResult UserLog(string user)
    {
        if (user == null)
        {
            return NotFound();
        }

        ViewData["user"] = user;

        return View(Database.Context.UsageLog
            .Where(l => l.UserName == user)
            .OrderByDescending(l => l.Id)
            .Take(5000));
    }

    // GET: UsageLogs/IdLog
    public IActionResult IdLog(string usageId)
    {
        if (usageId == null)
        {
            return NotFound();
        }

        ViewData["usageId"] = usageId;

        return View(Database.Context.UsageLog
            .Where(l => l.UsageId == usageId)
            .OrderByDescending(l => l.Id)
            .Take(5000));
    }

    // GET: UsageLogs/ClearCache
    public IActionResult ClearCache()
    {
        s_model = null;
        return RedirectToAction("Index");
    }

}
