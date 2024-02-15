using m4dModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace m4d.APIControllers;

[ApiController]
[Route("api/[controller]")]
public class SuggestionController : DanceMusicApiController
{
    public SuggestionController(DanceMusicContext context,
        UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager,
        ISearchServiceManager searchService, IDanceStatsManager danceStatsManager,
        IConfiguration configuration) :
        base(context, userManager, roleManager, searchService, danceStatsManager, configuration)
    {
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id)
    {
        var suggestions = await SongIndex.AzureSuggestions(id);
        if (suggestions != null)
        {
            return Ok(suggestions);
        }

        return NotFound();
    }
}
