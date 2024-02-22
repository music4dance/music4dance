using System.Net;
using m4dModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace m4d.APIControllers;

[ApiController]
[Route("api/[controller]")]
public class ServicePlaylistController : DanceMusicApiController
{
    private static readonly Dictionary<string, GenericPlaylist> s_cache = new();
    public ServicePlaylistController(
        DanceMusicContext context, UserManager<ApplicationUser> userManager,
        ISearchServiceManager searchService, IDanceStatsManager danceStatsManager,
        IConfiguration configuration, ILogger<ServicePlaylistController> logger) :
        base(context, userManager, searchService, danceStatsManager, configuration, logger)
    {
    }

    [Authorize]
    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id)
    {
        try
        {
            return JsonCamelCase(await GetPlaylist(id));
        }
        catch (Exception e)
        {
            Logger.LogError($"PlayListLookup Failed: {e.Message}");
            return BadRequest(e.Message);
        }
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Post(string id, string tags)
    {
        Logger.LogInformation($"Enter Post: Playlist = {id}");
        if (User.Identity is not { IsAuthenticated: true })
        {
            return StatusCode((int)HttpStatusCode.Forbidden);
        }

        GenericPlaylist serviceList;
        try
        {
            serviceList = await GetPlaylist(id);
        }
        catch (Exception e)
        {
            Logger.LogError($"PlayListLookup Failed: {e.Message}");
            return BadRequest(e.Message);
        }

        id = id.Substring(1);

        var localList = await Database.PlayLists.FindAsync(id);
        if (localList != null)
        {
            return Conflict($"Playlist {id} already exists");
        }

        var name = serviceList.OwnerName.Replace(" ", "");
        var email = $"{serviceList.OwnerId}@spotify.com";
        var user = await Database.FindUser(serviceList.OwnerName) 
            ?? await Database.AddPseudoUser(name, email);

        if (user == null)
        {
            return UnprocessableEntity($"Unable to create PseudoUser ${name}, ${email}");
        }

        await Database.AddPlaylist(id, PlayListType.SongsFromSpotify, name, tags);
        return Ok(id);
    }

    private async Task<GenericPlaylist> GetPlaylist(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Length < 2)
        {
            throw new Exception("Invalid Id");
        }

        if (s_cache.TryGetValue(id, out var playlist))
        {
            return playlist;
        }

        var service = MusicService.GetService(id[0]);
        var trackId = service.NormalizeId(id.Substring(1));

        playlist = await MusicServiceManager.LookupPlaylistWithAudioData(
            service, service.BuildPlayListLink(trackId));

        s_cache[id] = playlist;
        return playlist;
    }
}
