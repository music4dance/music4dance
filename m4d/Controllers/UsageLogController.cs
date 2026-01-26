using m4d.Services;
using m4d.Services.ServiceHealth;

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

public class PageSummary
{
    public string Page { get; set; }
    public int UniqueUsers { get; set; }
    public DateTimeOffset MinDate { get; set; }
    public DateTimeOffset MaxDate { get; set; }
    public int Hits { get; set; }
}

public class PageUsageModel
{
    public DateTime LastUpdate { get; set; }
    public List<PageSummary> Summaries { get; set; }
    public bool UseBaseUrl { get; set; }
}

public class UsageLogController : DanceMusicController
{
    private static UsageModel s_model;
    private static PageUsageModel s_pageModelFull;
    private static PageUsageModel s_pageModelBase;

    public UsageLogController(
        DanceMusicContext context, UserManager<ApplicationUser> userManager,
        ISearchServiceManager searchService, IDanceStatsManager danceStatsManager,
        IConfiguration configuration, IFileProvider fileProvider, IBackgroundTaskQueue backroundTaskQueue,
        IFeatureManagerSnapshot featureManager, ILogger<ActivityLogController> logger,
        ServiceHealthManager serviceHealth) :
        base(context, userManager, searchService, danceStatsManager, configuration,
            fileProvider, backroundTaskQueue, featureManager, logger, serviceHealth)
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
        s_pageModelFull = null;
        s_pageModelBase = null;
        return RedirectToAction("Index");
    }

    // GET: UsageLogs/Pages
    public IActionResult Pages(bool useBaseUrl = false)
    {
        var cachedModel = useBaseUrl ? s_pageModelBase : s_pageModelFull;

        if (cachedModel == null)
        {
            cachedModel = useBaseUrl
                ? new PageUsageModel
                {
                    Summaries = [.. Context.Database.SqlQuery<PageSummary>(
                        $"""
                        SELECT 
                            CASE 
                                WHEN CHARINDEX('?', [Page]) > 0 
                                THEN SUBSTRING([Page], 1, CHARINDEX('?', [Page]) - 1)
                                ELSE [Page]
                            END as Page,
                            COUNT(DISTINCT [UsageId]) as UniqueUsers,
                            MIN([Date]) as MinDate,
                            MAX([Date]) as MaxDate,
                            COUNT(*) as Hits
                        FROM dbo.UsageLog 
                        GROUP BY 
                            CASE 
                                WHEN CHARINDEX('?', [Page]) > 0 
                                THEN SUBSTRING([Page], 1, CHARINDEX('?', [Page]) - 1)
                                ELSE [Page]
                            END
                        HAVING COUNT(*) > 10 
                        ORDER BY Hits DESC
                        """)],
                    LastUpdate = DateTime.Now,
                    UseBaseUrl = true,
                }
                : new PageUsageModel
                {
                    Summaries = [.. Context.Database.SqlQuery<PageSummary>(
                        $"""
                        SELECT 
                            [Page],
                            COUNT(DISTINCT [UsageId]) as UniqueUsers,
                            MIN([Date]) as MinDate,
                            MAX([Date]) as MaxDate,
                            COUNT(*) as Hits
                        FROM dbo.UsageLog 
                        GROUP BY [Page]
                        HAVING COUNT(*) > 10 
                        ORDER BY Hits DESC
                        """)],
                    LastUpdate = DateTime.Now,
                    UseBaseUrl = false,
                };

            if (useBaseUrl)
                s_pageModelBase = cachedModel;
            else
                s_pageModelFull = cachedModel;
        }

        return View(cachedModel);
    }

    // GET: UsageLogs/PageLog
    public IActionResult PageLog(string page, bool exactMatch = true)
    {
        if (string.IsNullOrEmpty(page))
        {
            return NotFound();
        }

        ViewData["page"] = page;
        ViewData["exactMatch"] = exactMatch;

        var query = exactMatch
            ? Database.Context.UsageLog.Where(l => l.Page == page)
            : Database.Context.UsageLog.Where(l => l.Page.StartsWith(page));

        return View(query
            .OrderByDescending(l => l.Id)
            .Take(5000));
    }

    // GET: UsageLogs/LowUsage
    public IActionResult LowUsage()
    {
        var model = new UsageModel
        {
            Summaries = [.. Context.Database.SqlQuery<UsageSummary>(
                $"""
                SELECT [UsageId], MAX([UserName]) as UserName, MIN([Date]) as MinDate, MAX([Date]) as MaxDate, COUNT(*) as Hits 
                FROM dbo.UsageLog 
                GROUP BY [UsageId] 
                HAVING COUNT(*) <= 5 
                ORDER BY MaxDate DESC
                """)],
            LastUpdate = DateTime.Now,
        };

        return View(model);
    }

}
