using m4d.Services;
using m4dModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.FileProviders;

namespace m4d.Controllers;

public class ContentController : DanceMusicController
{
    public ContentController(
        DanceMusicContext context, UserManager<ApplicationUser> userManager,
        ISearchServiceManager searchService, IDanceStatsManager danceStatsManager,
        IConfiguration configuration, IFileProvider fileProvider, IBackgroundTaskQueue backroundTaskQueue,
        ILogger logger) :
        base(context, userManager, searchService, danceStatsManager, configuration, fileProvider, backroundTaskQueue, logger)
    {
    }
}
