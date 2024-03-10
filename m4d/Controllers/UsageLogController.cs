using m4d.Services;
using m4dModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using Microsoft.FeatureManagement;
using Microsoft.VisualStudio.Web.CodeGenerators.Mvc.View;

namespace m4d.Controllers;

public class UsageLogController : DanceMusicController
{
    public UsageLogController(
        DanceMusicContext context, UserManager<ApplicationUser> userManager,
        ISearchServiceManager searchService, IDanceStatsManager danceStatsManager,
        IConfiguration configuration, IFileProvider fileProvider, IBackgroundTaskQueue backroundTaskQueue,
        IFeatureManager featureManager, ILogger<ActivityLogController> logger) :
        base(context, userManager, searchService, danceStatsManager, configuration,
            fileProvider, backroundTaskQueue, featureManager, logger)
    {
        HelpPage = "song-list";
    }

    // GET: UsageLogs
    public IActionResult Index(string user = null, string usageId = null, int days = 0)
    {
        IQueryable<UsageLog> log = Database.Context.UsageLog;

        if (!string.IsNullOrEmpty(user))
        {
            log = log.Where(l => l.UserName == user);
            ViewData["user"] = user;
        }

        if (!string.IsNullOrEmpty(usageId))
        {
            log = log.Where(l => l.UsageId == usageId);
            ViewData["usageId"] = usageId;
        }

        if (days > 0)
        {
            log = log.Where(l => l.Date < DateTimeOffset.Now.AddDays(-days));
            ViewData["days"] = days;
        }

        return View(log.OrderByDescending(l => l.Id).Take(1000));
    }
}
