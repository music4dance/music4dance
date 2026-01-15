using AutoMapper;

using m4d.ViewModels;

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

        // Check if search service is available
        if (!ServiceHealth.IsServiceHealthy("SearchService"))
        {
            Logger.LogWarning("ServiceTrack requested but SearchService is unavailable");
            return StatusCode(503, new { error = "Search service temporarily unavailable" });
        }

        // Find a song associate with the service id
        Song song;
        try
        {
            song = await SongIndex.GetSongFromService(service, id);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Azure Search service is unavailable") ||
                                                   ex.Message.Contains("Client registration requires a TokenCredential"))
        {
            Logger.LogError(ex, "Search service unavailable in ServiceTrackController");
            ServiceHealth.MarkUnavailable("SearchService", $"Client error: {ex.Message}");
            return StatusCode(503, new { error = "Search service temporarily unavailable" });
        }

        var created = false;

        if (song == null && !localOnly)
        {
            song = await MusicServiceManager.CreateSong(Database, user, id, service);
            if (song != null)
            {
                try
                {
                    created = await SongIndex.FindSong(song.SongId) == null;
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("Azure Search service is unavailable") ||
                                                           ex.Message.Contains("Client registration requires a TokenCredential"))
                {
                    Logger.LogWarning(ex, "Search service unavailable when checking if song exists, assuming created");
                    ServiceHealth.MarkUnavailable("SearchService", $"Client error: {ex.Message}");
                    created = true;
                }
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
