using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
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

        [HttpGet]
        [SuppressMessage("ReSharper", "PossibleMultipleEnumeration")]
        public async Task<IActionResult> Search([FromServices]IMapper mapper, string title,
            string artist)
        {
            Trace.WriteLine(
                $"Enter Search: Title = {title}, Artist={artist}, User = {User.Identity?.Name}");
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(artist))
            {
                return StatusCode((int)HttpStatusCode.BadRequest);
            }

            var songs = await Database.SongsFromTitleArtist(title, artist);
            return songs == null || !songs.Any()
                ? StatusCode((int)HttpStatusCode.NotFound)
                : JsonCamelCase(songs.Select(s => s.GetHistory(mapper)));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get([FromServices]IMapper mapper, Guid id)
        {
            Trace.WriteLine($"Enter Patch: SongId = {id}, User = {User.Identity?.Name}");

            var song = await Database.FindSong(id);
            return song == null
                ? StatusCode((int)HttpStatusCode.NotFound)
                : JsonCamelCase(song.GetHistory(mapper));
        }

        [Authorize]
        [HttpPatch("{id}")]
        public async Task<IActionResult> Patch([FromServices]IMapper mapper, Guid id,
            [FromBody]SongHistory history)
        {
            Trace.WriteLine($"Enter Patch: SongId = {id}, User = {User.Identity?.Name}");
            if (User.Identity == null || !User.Identity.IsAuthenticated)
            {
                return StatusCode((int)HttpStatusCode.Forbidden);
            }

            if (id != history.Id)
            {
                return StatusCode((int)HttpStatusCode.NotFound);
            }

            return await Database.AppendHistory(history, mapper)
                ? Ok()
                : StatusCode((int)HttpStatusCode.BadRequest);
        }

        [Authorize]
        [HttpPut("{id}")]
        public async Task<IActionResult> Put([FromServices]IMapper mapper, Guid id,
            [FromBody]SongHistory history)
        {
            Trace.WriteLine($"Enter Patch: SongId = {id}, User = {User.Identity?.Name}");
            if (User.Identity == null || !User.Identity.IsAuthenticated)
            {
                return StatusCode((int)HttpStatusCode.Forbidden);
            }

            if (id != history.Id)
            {
                return StatusCode((int)HttpStatusCode.NotFound);
            }

            return await Database.AdminEditSong(history, mapper)
                ? Ok()
                : StatusCode((int)HttpStatusCode.BadRequest);
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> Post([FromServices]IMapper mapper,
            [FromBody]SongHistory history)
        {
            Trace.WriteLine($"Enter Post: User = {User.Identity?.Name}");
            if (User.Identity == null || !User.Identity.IsAuthenticated)
            {
                return StatusCode((int)HttpStatusCode.Forbidden);
            }

            return await Database.CreateSong(
                history.Properties.Select(mapper.Map<SongProperty>).ToList())
                ? Ok()
                : StatusCode((int)HttpStatusCode.BadRequest);
        }
    }
}
