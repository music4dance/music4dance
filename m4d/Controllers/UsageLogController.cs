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
    public BotFilter BotFilter { get; set; }
}

public class PageSummary
{
    public string Page { get; set; }
    public int UniqueUsers { get; set; }
    public DateTimeOffset MinDate { get; set; }
    public DateTimeOffset MaxDate { get; set; }
    public int Hits { get; set; }
}

public enum PageType
{
    All,
    Songs,
    Other
}

public enum UserHitFilter
{
    All,
    SingleHit,
    MultiHit
}

public enum BotFilter
{
    All,
    ExcludeBots,
    BotsOnly
}

public class PageUsageModel
{
    public DateTime LastUpdate { get; set; }
    public List<PageSummary> Summaries { get; set; }
    public bool UseBaseUrl { get; set; }
    public UserHitFilter UserHitFilter { get; set; }
    public PageType PageType { get; set; }
    public BotFilter BotFilter { get; set; }
}

public class UsageLogController : DanceMusicController
{
    // Cached model for default Index view (static for cross-request caching)
    private static UsageModel s_model;

    // Bot detection patterns - centralized to avoid duplication
    private static readonly string[] BotPatterns = ["bot", "spider", "crawler", "slurp", "Mediapartners"];

    /// <summary>
    /// Generates SQL WHERE clause fragment to filter bot traffic based on UserAgent.
    /// </summary>
    /// <param name="filter">The bot filter to apply</param>
    /// <param name="columnPrefix">Optional table alias prefix (e.g., "u." for aliased queries)</param>
    private static string GetBotFilterSql(BotFilter filter, string columnPrefix = "")
    {
        var column = $"{columnPrefix}[UserAgent]";
        return filter switch
        {
            BotFilter.ExcludeBots => string.Join(" ", BotPatterns.Select(p => $"AND {column} NOT LIKE '%{p}%'")),
            BotFilter.BotsOnly => "AND (" + string.Join(" OR ", BotPatterns.Select(p => $"{column} LIKE '%{p}%'")) + ")",
            _ => ""
        };
    }

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
    // Default: Exclude Bots (cached)
    public IActionResult Index(BotFilter botFilter = BotFilter.ExcludeBots)
    {
        // Only cache the default (ExcludeBots) result
        if (botFilter == BotFilter.ExcludeBots && s_model != null)
        {
            return View(s_model);
        }

        // Build bot filter SQL using shared helper
        var botSqlFilter = GetBotFilterSql(botFilter);

        var sql = $"""
            SELECT [UsageId], MAX([UserName]) as UserName, MIN([Date]) as MinDate, MAX([Date]) as MaxDate, COUNT(*) as Hits 
            FROM dbo.UsageLog 
            WHERE 1=1 {botSqlFilter}
            GROUP BY [UsageId] 
            HAVING COUNT(*) > 5 
            ORDER BY Hits DESC
            """;

        var model = new UsageModel
        {
            Summaries = [.. Context.Database.SqlQueryRaw<UsageSummary>(sql)],
            LastUpdate = DateTime.Now,
            BotFilter = botFilter,
        };

        // Cache only the default view
        if (botFilter == BotFilter.ExcludeBots)
        {
            s_model = model;
        }

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


    // GET: UsageLogs/Pages
    // Defaults: Other Pages, Base URL Only, Multi-Hit Users, Exclude Bots
    public IActionResult Pages(
        bool useBaseUrl = true, 
        UserHitFilter userHitFilter = UserHitFilter.MultiHit, 
        PageType pageType = PageType.Other,
        BotFilter botFilter = BotFilter.ExcludeBots)
    {
        // Build the appropriate SQL query based on filters
        // No caching since we have multiple filter combinations
        var model = new PageUsageModel
        {
            LastUpdate = DateTime.Now,
            UseBaseUrl = useBaseUrl,
            UserHitFilter = userHitFilter,
            PageType = pageType,
            BotFilter = botFilter,
        };

        // Build page filter clause (safe - built from constants, not user input)
        var pageFilter = pageType switch
        {
            PageType.Songs => "AND [Page] LIKE '/song/details/%'",
            PageType.Other => "AND [Page] NOT LIKE '/song/details/%'",
            _ => ""
        };

        // Build user hit filter - filter by users' total hit count
        var userHitSqlFilter = userHitFilter switch
        {
            UserHitFilter.SingleHit => "AND u.[UsageId] IN (SELECT [UsageId] FROM dbo.UsageLog GROUP BY [UsageId] HAVING COUNT(*) = 1)",
            UserHitFilter.MultiHit => "AND u.[UsageId] IN (SELECT [UsageId] FROM dbo.UsageLog GROUP BY [UsageId] HAVING COUNT(*) > 1)",
            _ => ""
        };

        // Build bot filter using shared helper (with table alias prefix)
        var botSqlFilter = GetBotFilterSql(botFilter, "u.");


        var minHits = userHitFilter == UserHitFilter.SingleHit ? 1 : 10;

        // Use SqlQueryRaw because we're building dynamic SQL from constants (not user input)
        // SqlQuery with interpolation would try to parameterize the SQL fragments
        if (useBaseUrl)
        {
            var sql = $"""
                SELECT 
                    CASE 
                        WHEN CHARINDEX('?', u.[Page]) > 0
                        THEN SUBSTRING(u.[Page], 1, CHARINDEX('?', u.[Page]) - 1)
                        ELSE u.[Page]
                    END as Page,
                    COUNT(DISTINCT u.[UsageId]) as UniqueUsers,
                    MIN(u.[Date]) as MinDate,
                    MAX(u.[Date]) as MaxDate,
                    COUNT(*) as Hits
                FROM dbo.UsageLog u
                WHERE 1=1 {pageFilter} {userHitSqlFilter} {botSqlFilter}
                GROUP BY 
                    CASE 
                        WHEN CHARINDEX('?', u.[Page]) > 0 
                        THEN SUBSTRING(u.[Page], 1, CHARINDEX('?', u.[Page]) - 1)
                        ELSE u.[Page]
                    END
                HAVING COUNT(*) >= {minHits}
                ORDER BY Hits DESC
                """;
            model.Summaries = [.. Context.Database.SqlQueryRaw<PageSummary>(sql)];
        }
        else
        {
            var sql = $"""
                SELECT 
                    u.[Page],
                    COUNT(DISTINCT u.[UsageId]) as UniqueUsers,
                    MIN(u.[Date]) as MinDate,
                    MAX(u.[Date]) as MaxDate,
                    COUNT(*) as Hits
                FROM dbo.UsageLog u
                WHERE 1=1 {pageFilter} {userHitSqlFilter} {botSqlFilter}
                GROUP BY u.[Page]
                HAVING COUNT(*) >= {minHits}
                ORDER BY Hits DESC
                """;
            model.Summaries = [.. Context.Database.SqlQueryRaw<PageSummary>(sql)];
        }

        return View(model);
    }

    // GET: UsageLogs/PageLog
    public async Task<IActionResult> PageLog(string page, bool exactMatch = true)
    {
        if (string.IsNullOrEmpty(page))
        {
            return NotFound();
        }

        ViewData["page"] = page;
        ViewData["exactMatch"] = exactMatch;

        // If this is a song details page, look up the song info for the header
        await EnrichPageWithSongInfo(page);

        var query = exactMatch
            ? Database.Context.UsageLog.Where(l => l.Page == page)
            : Database.Context.UsageLog.Where(l => l.Page.StartsWith(page));

        return View(query
            .OrderByDescending(l => l.Id)
            .Take(5000));
    }

    /// <summary>
    /// If the page is a song details page, looks up the song and adds title/artist to ViewData.
    /// </summary>
    private async Task EnrichPageWithSongInfo(string page)
    {
        const string songDetailPrefix = "/song/details/";

        if (!page.StartsWith(songDetailPrefix, StringComparison.OrdinalIgnoreCase))
            return;

        // Extract GUID from URL (handle query strings)
        var guidPart = page[songDetailPrefix.Length..];
        var queryIndex = guidPart.IndexOf('?');
        if (queryIndex >= 0)
            guidPart = guidPart[..queryIndex];

        if (Guid.TryParse(guidPart, out var songGuid))
        {
            try
            {
                var song = await SongIndex.FindSong(songGuid);
                if (song != null)
                {
                    ViewData["SongTitle"] = song.Title;
                    ViewData["SongArtist"] = song.Artist;
                    ViewData["SongId"] = song.SongId;
                }
            }
            catch (Azure.RequestFailedException)
            {
                // Song not found in index - leave ViewData empty
            }
        }
    }

}
