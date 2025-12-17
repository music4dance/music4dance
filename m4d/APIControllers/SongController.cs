using AutoMapper;

using m4d.Services.ServiceHealth;
using m4d.Utilities;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

using System.Net;

namespace m4d.APIControllers;

[ApiController]
[ValidateAntiForgeryToken]
[Route("api/[controller]")]
public class SongController(
    DanceMusicContext context, UserManager<ApplicationUser> userManager,
    ISearchServiceManager searchService, IDanceStatsManager danceStatsManager,
    IConfiguration configuration, ILogger<SongController> logger,
    ServiceHealthManager serviceHealth) : DanceMusicApiController(context, userManager, searchService, danceStatsManager, configuration, logger)
{
    [HttpGet]
    public async Task<IActionResult> Get([FromServices] IMapper mapper,
        string search = null, string title = null, string artist = null, string filter = null)
    {
        Logger.LogInformation(
            $"Enter Search: Search = {search}, Title = {title}, Artist={artist}, Filter = {filter}, User = {User.Identity?.Name}");

        // Check if search service is available
        if (!serviceHealth.IsServiceHealthy("SearchService"))
        {
            Logger.LogWarning("API song search requested but SearchService is unavailable");
            return StatusCode((int)HttpStatusCode.ServiceUnavailable, new
            {
                error = "Search service temporarily unavailable",
                message = "Please try again in a few minutes"
            });
        }

        try
        {
            IEnumerable<Song> songs;
            if (!string.IsNullOrWhiteSpace(search))
            {
                songs = await SongIndex.SimpleSearch(search);
            }
            else if (!string.IsNullOrWhiteSpace(title) && !string.IsNullOrWhiteSpace(artist))
            {
                songs = await SongIndex.SongsFromTitleArtist(title, artist);
            }
            else if (!string.IsNullOrWhiteSpace(filter))
            {
                var songFilter = Database.SearchService.GetSongFilter(filter);
                var results = await SongIndex.Search(songFilter);
                songs = results.Songs;
            }
            else
            {
                return StatusCode((int)HttpStatusCode.BadRequest);
            }

            if (songs == null || !songs.Any())
            {
                return StatusCode((int)HttpStatusCode.NotFound);
            }

            var anonymized = new List<SongHistory>();
            foreach (var song in songs)
            {
                anonymized.Add(await UserMapper.AnonymizeHistory(song.GetHistory(mapper), UserManager));
            }

            return JsonCamelCase(anonymized);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Azure Search service is unavailable"))
        {
            Logger.LogError(ex, "API song search failed due to unavailable Azure Search service");
            return StatusCode((int)HttpStatusCode.ServiceUnavailable, new
            {
                error = "Search service temporarily unavailable",
                message = "Please try again in a few minutes"
            });
        }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get([FromServices] IMapper mapper, Guid id)
    {
        Logger.LogInformation($"Enter Get by ID: SongId = {id}, User = {User.Identity?.Name}");

        // Check if search service is available
        if (!serviceHealth.IsServiceHealthy("SearchService"))
        {
            Logger.LogWarning("API song get by ID requested but SearchService is unavailable");
            return StatusCode((int)HttpStatusCode.ServiceUnavailable, new
            {
                error = "Search service temporarily unavailable",
                message = "Please try again in a few minutes"
            });
        }

        try
        {
            var song = await SongIndex.FindSong(id);
            return song == null
                ? StatusCode((int)HttpStatusCode.NotFound)
                : JsonCamelCase(
                    await UserMapper.AnonymizeHistory(song.GetHistory(mapper), UserManager));
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Azure Search service is unavailable"))
        {
            Logger.LogError(ex, "API song get by ID failed due to unavailable Azure Search service");
            return StatusCode((int)HttpStatusCode.ServiceUnavailable, new
            {
                error = "Search service temporarily unavailable",
                message = "Please try again in a few minutes"
            });
        }
    }

    [Authorize]
    [HttpPatch("{id}")]
    public async Task<IActionResult> Patch([FromServices] IMapper mapper, Guid id,
        [FromBody] SongHistory history)
    {
        Logger.LogInformation($"Enter Patch: SongId = {id}, User = {User.Identity?.Name}");
        if (User.Identity == null || !User.Identity.IsAuthenticated)
        {
            return StatusCode((int)HttpStatusCode.Forbidden);
        }

        if (id != history.Id)
        {
            return StatusCode((int)HttpStatusCode.NotFound);
        }

        // TODO: Consider returning the history (or the tail) to the client and then
        //  updating the client - this will smooth out the situation where a tag gets
        //  transformed by the server
        return await SongIndex.AppendHistory(
            await UserMapper.DeanonymizeHistory(history, UserManager), mapper)
            ? Ok()
            : StatusCode((int)HttpStatusCode.BadRequest);
    }

    [Authorize]
    [HttpPut("{id}")]
    public async Task<IActionResult> Put([FromServices] IMapper mapper, Guid id,
        [FromBody] SongHistory history)
    {
        Logger.LogInformation($"Enter Patch: SongId = {id}, User = {User.Identity?.Name}");
        if (User.Identity == null || !User.Identity.IsAuthenticated)
        {
            return StatusCode((int)HttpStatusCode.Forbidden);
        }

        if (id != history.Id)
        {
            return StatusCode((int)HttpStatusCode.NotFound);
        }

        return await SongIndex.AdminEditSong(
            await UserMapper.DeanonymizeHistory(history, UserManager), mapper)
            ? Ok()
            : StatusCode((int)HttpStatusCode.BadRequest);
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Post([FromServices] IMapper mapper,
        [FromBody] SongHistory history)
    {
        Logger.LogInformation($"Enter Post: User = {User.Identity?.Name}");
        if (User.Identity == null || !User.Identity.IsAuthenticated)
        {
            return StatusCode((int)HttpStatusCode.Forbidden);
        }

        var appUser = await UserManager.FindByNameAsync(User.Identity.Name);

        return await SongIndex.CreateOrMergeSong(
            [.. history.Properties.Select(mapper.Map<SongProperty>)], appUser)
            ? Ok()
            : StatusCode((int)HttpStatusCode.BadRequest);
    }
}
