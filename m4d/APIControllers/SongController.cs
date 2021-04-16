using System;
using System.Diagnostics;
using System.Net;
using AutoMapper;
using m4dModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace m4d.APIControllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SongController : DanceMusicApiController
    {
        public SongController(DanceMusicContext context, UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager, ISearchServiceManager searchService,
            IDanceStatsManager danceStatsManager, IConfiguration configuration) :
            base(context, userManager, roleManager, searchService, danceStatsManager, configuration)
        {
        }

        [Authorize]
        [HttpPatch("{id}")]
        public IActionResult Patch([FromServices]IMapper mapper, Guid id, [FromBody] SongHistory history)
        {
            Trace.WriteLine($"Enter Patch: SongId = {id}, User = {User.Identity.Name}");
            if (!User.Identity.IsAuthenticated)
            {
                return StatusCode((int)HttpStatusCode.Forbidden);
            }

            if (id != history.Id)
            {
                return StatusCode((int) HttpStatusCode.NotFound);
            }

            return Database.AppendHistory(history, mapper)
                ? Ok()
                : StatusCode((int) HttpStatusCode.BadRequest);
        }

        [Authorize]
        [HttpPut("{id}")]
        public IActionResult Put([FromServices] IMapper mapper, Guid id, [FromBody] SongHistory history)
        {
            Trace.WriteLine($"Enter Patch: SongId = {id}, User = {User.Identity.Name}");
            if (!User.Identity.IsAuthenticated)
            {
                return StatusCode((int)HttpStatusCode.Forbidden);
            }

            if (id != history.Id)
            {
                return StatusCode((int)HttpStatusCode.NotFound);
            }

            return Database.AdminEditSong(history, mapper)
                ? Ok()
                : StatusCode((int)HttpStatusCode.BadRequest);
        }

    }
}
