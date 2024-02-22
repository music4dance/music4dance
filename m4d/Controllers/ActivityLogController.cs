﻿using m4d.Services;
using m4dModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;

namespace m4d.Controllers;

public class ActivityLogController : DanceMusicController
{
    public ActivityLogController(
        DanceMusicContext context, UserManager<ApplicationUser> userManager,
        ISearchServiceManager searchService, IDanceStatsManager danceStatsManager,
        IConfiguration configuration, IFileProvider fileProvider, IBackgroundTaskQueue backroundTaskQueue,
        ILogger<ActivityLogController> logger) :
        base(context, userManager, searchService, danceStatsManager, configuration, fileProvider, backroundTaskQueue, logger)
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
