using System.Diagnostics.CodeAnalysis;
using System.Net;
using AutoMapper;
using m4d.Utilities;
using m4dModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace m4d.APIControllers;

[ApiController]
[Route("api/[controller]")]
[ValidateAntiForgeryToken]
public class SongController : DanceMusicApiController
{
    public SongController(
        DanceMusicContext context, UserManager<ApplicationUser> userManager,
        ISearchServiceManager searchService, IDanceStatsManager danceStatsManager,
        IConfiguration configuration, ILogger<SongController> logger) :
        base(context, userManager, searchService, danceStatsManager, configuration, logger)
    {
    }

    [HttpGet]
    [SuppressMessage("ReSharper", "PossibleMultipleEnumeration")]
    public async Task<IActionResult> Get([FromServices] IMapper mapper,
        string search = null, string title = null, string artist = null, string filter = null)
    {
        Logger.LogInformation(
            $"Enter Search: Search = { search }, Title = {title}, Artist={artist}, Filter = {filter}, User = {User.Identity?.Name}");

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
            var songFilter = new SongFilter(filter);
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

    [HttpGet("{id}")]
    public async Task<IActionResult> Get([FromServices]IMapper mapper, Guid id)
    {
        Logger.LogInformation($"Enter Patch: SongId = {id}, User = {User.Identity?.Name}");

        var song = await SongIndex.FindSong(id);
        return song == null
            ? StatusCode((int)HttpStatusCode.NotFound)
            : JsonCamelCase(
                await UserMapper.AnonymizeHistory(song.GetHistory(mapper), UserManager));
    }

    [Authorize]
    [HttpPatch("{id}")]
    public async Task<IActionResult> Patch([FromServices]IMapper mapper, Guid id,
        [FromBody]SongHistory history)
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
    public async Task<IActionResult> Put([FromServices]IMapper mapper, Guid id,
        [FromBody]SongHistory history)
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
    public async Task<IActionResult> Post([FromServices]IMapper mapper,
        [FromBody]SongHistory history)
    {
        Logger.LogInformation($"Enter Post: User = {User.Identity?.Name}");
        if (User.Identity == null || !User.Identity.IsAuthenticated)
        {
            return StatusCode((int)HttpStatusCode.Forbidden);
        }

        var appUser = await UserManager.FindByNameAsync(User.Identity.Name);

        return await SongIndex.CreateOrMergeSong(
            history.Properties.Select(mapper.Map<SongProperty>).ToList(), appUser)
            ? Ok()
            : StatusCode((int)HttpStatusCode.BadRequest);
    }
}
