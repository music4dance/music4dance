using m4dModels;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace m4d.APIControllers;

[ApiController]
[Route("api/[controller]")]
public class TagController : DanceMusicApiController
{
    private readonly DanceStatsInstance _statistics;

    public TagController(
        DanceMusicContext context, UserManager<ApplicationUser> userManager,
        ISearchServiceManager searchService, IDanceStatsManager danceStatsManager,
        IConfiguration configuration, ILogger<TagController> logger) :
        base(context, userManager, searchService, danceStatsManager, configuration, logger)
    {
        _statistics = DanceStatsManager.Instance;
    }

    [HttpGet]
    [ResponseCache(Duration = 600, Location = ResponseCacheLocation.Any, NoStore = false)]
    public IActionResult Get()
    {
        return JsonCamelCase(
            _statistics.TagGroups.Where(g => g.Category != "Dance" && g.PrimaryId == null)
                .Select(g => new { g.Key, g.Count }));
    }
}
