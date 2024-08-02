using m4d.Services;
using m4dModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using Microsoft.FeatureManagement;

namespace m4d.Controllers;

public class SearchController : DanceMusicController
{
    public SearchController(
        DanceMusicContext context, UserManager<ApplicationUser> userManager,
        ISearchServiceManager searchService, IDanceStatsManager danceStatsManager,
        IConfiguration configuration, IFileProvider fileProvider, IBackgroundTaskQueue backroundTaskQueue,
        IFeatureManager featureManager, ILogger<ActivityLogController> logger) :
        base(context, userManager, searchService, danceStatsManager, configuration,
            fileProvider, backroundTaskQueue, featureManager, logger)
    {
        //HelpPage = "song-list";
    }

    // GET: Search
    public IActionResult Index(string search)
    {
        return Vue3("music4dance search results", "music4dance search results", "search", search, danceEnvironment: true);
    }
}
