using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace m4d.APIControllers;

[ApiController]
[ValidateAntiForgeryToken]
[Route("api/[controller]")]
public class DanceEnvironmentController : DanceMusicApiController
{
    private readonly DanceStatsInstance _statistics;

    public DanceEnvironmentController(
        DanceMusicContext context, UserManager<ApplicationUser> userManager,
        ISearchServiceManager searchService, IDanceStatsManager danceStatsManager,
        IConfiguration configuration, ILogger<DanceEnvironmentController> logger) :
        base(context, userManager, searchService, danceStatsManager, configuration, logger)
    {
        _statistics = DanceStatsManager.Instance;
    }

    [HttpGet]
    [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Any, NoStore = false)]
    public IActionResult Get()
    {
        var environment = new DanceEnvironment(_statistics);
        return JsonCamelCase(environment);
    }

    [HttpGet("{id}")]
    [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Any, NoStore = false)]
    public IActionResult Get(string id)
    {
        var sparse = new DanceStatsSparse(_statistics.FromId(id));
        return JsonCamelCase(sparse);
    }
}
