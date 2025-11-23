using m4d.Utilities;

using m4dModels.Utilities;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace m4d.APIControllers;

// ReSharper disable once InconsistentNaming
public class DanceMusicApiController(DanceMusicContext context,
    // ReSharper disable once UnusedParameter.Local
    UserManager<ApplicationUser> userManager,
    ISearchServiceManager searchService, IDanceStatsManager danceStatsManager,
    IConfiguration configuration, ILogger logger = null) : ControllerBase
{
    protected DanceMusicService Database { get; } =
            new DanceMusicService(context, userManager, searchService, danceStatsManager);
    protected IConfiguration Configuration = configuration;
    protected ILogger Logger { get; } = logger;

    private MusicServiceManager _musicServiceManager;

    protected MusicServiceManager MusicServiceManager =>
        _musicServiceManager ??= new MusicServiceManager(Configuration);

    protected DanceMusicContext Context => Database.Context;
    protected SongIndex SongIndex => Database.SongIndex;

    protected UserManager<ApplicationUser> UserManager => Database.UserManager;

    protected IDanceStatsManager DanceStatsManager { get; } = danceStatsManager;

    protected IActionResult JsonCamelCase(object json)
    {
        return new JsonResult(json, JsonHelpers.CamelCaseSerializer);
    }
}
