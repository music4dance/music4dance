using m4dModels;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace m4d.APIControllers;

[ApiController]
[ValidateAntiForgeryToken]
[Route("api/[controller]")]
public class ServiceUserController(
    DanceMusicContext context, UserManager<ApplicationUser> userManager,
    ISearchServiceManager searchService, IDanceStatsManager danceStatsManager,
    IConfiguration configuration, ILogger<ServiceUserController> logger) : DanceMusicApiController(context, userManager, searchService, danceStatsManager, configuration, logger)
{
    private static readonly Dictionary<string, ServiceUser> s_cache = [];

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Length < 2)
        {
            return BadRequest("Invalid Id");
        }

        if (s_cache.TryGetValue(id, out var serviceUser))
        {
            return JsonCamelCase(serviceUser);
        }

        var service = MusicService.GetService(id[0]);
        var userId = id[1..];

        try
        {
            serviceUser = await MusicServiceManager.LookupServiceUser(
                service, userId);

            await FillLocalPlaylists(serviceUser.Playlists);

            s_cache[id] = serviceUser;
            return JsonCamelCase(serviceUser);
        }
        catch (Exception e)
        {
            Logger.LogError($"serviceUserLookup Failed: {e.Message}");
            return BadRequest(e.Message);
        }
    }

    private async Task FillLocalPlaylists(IList<SimplePlaylist> playlists)
    {
        // For spotify, we use the bare spotify id for our id (which we should probably fix at some point).
        var ids = playlists.Select(x => x.Id);
        var list = await Database.PlayLists.Where(
                p => p.Type == PlayListType.SongsFromSpotify && ids.Contains(p.Id)).Select(p => p.Id).ToListAsync();
        var m4d = list.ToHashSet();
        foreach (var playlist in playlists.Where(p => m4d.Contains(p.Id)))
        {
            playlist.Music4danceId = playlist.Id;
        }
    }
}
