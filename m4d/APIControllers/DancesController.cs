﻿using System.Net;

using m4d.ViewModels;

using m4dModels;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace m4d.APIControllers;

[ApiController]
[ValidateAntiForgeryToken]
[Route("api/[controller]")]
public class DancesController(
    DanceMusicContext context, UserManager<ApplicationUser> userManager,
    ISearchServiceManager searchService, IDanceStatsManager danceStatsManager,
    IConfiguration configuration, ILogger<DancesController> logger) : DanceMusicApiController(context, userManager, searchService, danceStatsManager, configuration, logger)
{
    [HttpGet]
    public IActionResult GetDances(bool details = false)
    {
        // This should eventually take a filter (or multiple filter) parameter
        var dances = Dance.DanceLibrary.NonPerformanceDanceTypes;
        if (details)
        {
            return Ok(dances);
        }

        var jsonDances = DanceJson.Convert(dances);

        return JsonCamelCase(jsonDances);
    }

    [HttpGet("{id}")]
    public IActionResult GetDance(string id)
    {
        var o = Dance.DanceLibrary.DanceFromId(id);
        if (o != null)
        {
            return JsonCamelCase(new DanceJson(o));
        }

        return NotFound();
    }

    [Authorize]
    [HttpPatch("{id}")]
    public async Task<IActionResult> Patch(string id, [FromBody] DanceCore dance)
    {
        if (User.Identity == null || !User.Identity.IsAuthenticated ||
            !User.IsInRole("dbAdmin"))
        {
            return StatusCode((int)HttpStatusCode.Forbidden);
        }

        if (id != dance.Id)
        {
            return StatusCode((int)HttpStatusCode.BadRequest);
        }

        return StatusCode(
            (int)(await Database.EditDance(dance) == null
                ? HttpStatusCode.NotFound
                : HttpStatusCode.OK));
    }
}
