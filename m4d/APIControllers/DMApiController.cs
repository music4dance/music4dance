using m4d.Services;
using m4d.Services.ServiceHealth;
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
    IConfiguration configuration, ILogger logger = null,
    IBackgroundTaskQueue backgroundTaskQueue = null,
    ServiceHealthManager serviceHealth = null) : ControllerBase
{
    protected DanceMusicService Database { get; } =
            new DanceMusicService(context, userManager, searchService, danceStatsManager);
    protected IConfiguration Configuration = configuration;
    protected ILogger Logger { get; } = logger;
    protected IBackgroundTaskQueue TaskQueue { get; } = backgroundTaskQueue;
    protected ServiceHealthManager ServiceHealth { get; } = serviceHealth;

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

    #region Search Service Error Handling

    /// <summary>
    /// Determines if an InvalidOperationException is related to Azure Search service availability or credential issues.
    /// </summary>
    protected static bool IsSearchServiceError(InvalidOperationException ex)
    {
        return ex.Message.Contains("Azure Search service is unavailable") ||
               ex.Message.Contains("Client registration requires a TokenCredential");
    }

    /// <summary>
    /// Handles search service errors by marking the service unavailable and logging the error.
    /// </summary>
    protected void HandleSearchServiceError(InvalidOperationException ex)
    {
        Logger.LogError(ex, "Search operation failed due to unavailable or misconfigured Azure Search service");
        ServiceHealth.MarkUnavailable("SearchService", $"Client error: {ex.Message}");
    }

    #endregion
}
