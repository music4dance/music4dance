using AutoMapper;

using m4d.ViewModels;

using m4dModels;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace m4d.APIControllers;

[ApiController]
[ValidateAntiForgeryToken]
[Route("api/[controller]")]
public class ServiceTrackController(
    DanceMusicContext context, UserManager<ApplicationUser> userManager,
    ISearchServiceManager searchService, IDanceStatsManager danceStatsManager,
    IConfiguration configuration, ILogger<ServiceTrackController> logger, IMapper mapper) : DanceMusicApiController(context, userManager, searchService, danceStatsManager, configuration, logger)
{

    // GET api/<controller>
    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id, bool localOnly = false)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Length < 2)
        {
            return BadRequest("Invalid Id");
        }

        var service = MusicService.GetService(id[0]);
        id = service.NormalizeId(id[1..]);

        var user = await Database.UserManager.GetUserAsync(User);
        // Find a song associate with the service id
        var song = await SongIndex.GetSongFromService(service, id);
        var created = false;

        if (song == null && !localOnly)
        {
            song = await MusicServiceManager.CreateSong(Database, user, id, service);
            if (song != null)
            {
                created = await SongIndex.FindSong(song.SongId) == null;
            }
        }

        if (song != null)
        {
            return JsonCamelCase(
                new SongDetailsModel
                {
                    Created = created,
                    Title = song.Title,
                    SongHistory = song.GetHistory(mapper)
                });
        }

        // If that fails, the ID is bad.

        return NotFound();
    }
}
