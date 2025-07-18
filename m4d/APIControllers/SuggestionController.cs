﻿using m4dModels;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace m4d.APIControllers;

[ApiController]
[ValidateAntiForgeryToken]
[Route("api/[controller]")]
public class SuggestionController(
    DanceMusicContext context, UserManager<ApplicationUser> userManager,
    ISearchServiceManager searchService, IDanceStatsManager danceStatsManager,
    IConfiguration configuration, ILogger<SuggestionController> logger) : DanceMusicApiController(context, userManager, searchService, danceStatsManager, configuration, logger)
{
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
