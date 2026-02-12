using m4d.Services;
using m4d.Services.ServiceHealth;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

using System.Net;

namespace m4d.APIControllers;

[ApiController]
[ValidateAntiForgeryToken]
[Route("api/[controller]")]
public class MusicServiceController(
    DanceMusicContext context, UserManager<ApplicationUser> userManager,
    ISearchServiceManager searchService, IDanceStatsManager danceStatsManager,
    IConfiguration configuration, ILogger<MusicServiceController> logger) : DanceMusicApiController(context, userManager, searchService, danceStatsManager, configuration, logger)
{
    // ReSharper disable once InconsistentNaming
    private static readonly Dictionary<string, IList<ServiceTrack>> s_cache = [];

    [HttpGet]
    public async Task<IActionResult> Get(string service = null, string title = null
        , string artist = null, string album = null)
    {
        return await Get(Guid.Empty, service, title, artist, album);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(Guid id, string service = null, string title = null
        , string artist = null, string album = null)
    {
        Song song = null;

        // Only try search if service is healthy
        if (id != Guid.Empty && ServiceHealth.IsServiceHealthy("SearchService"))
        {
            try
            {
                song = await SongIndex.FindSong(id);
            }
            catch (InvalidOperationException ex) when (IsSearchServiceError(ex))
            {
                Logger.LogWarning(ex, "Search service unavailable, continuing with service lookup only");
                ServiceHealth.MarkUnavailable("SearchService", $"Client error: {ex.Message}");
            }
        }

        if (song != null && artist == null && title == null)
        {
            artist = song.Artist;
            title = song.Title;
        }

        var key = $"{id}|{service ?? "A"}|{artist ?? ""}|{title ?? ""}";

        if (!s_cache.TryGetValue(key, out var tracks))
        {
            tracks = await InternalGetServiceTracks(service, song, title, artist, album);
        }

        if (tracks == null || tracks.Count == 0)
        {
            return NotFound();
        }

        s_cache[key] = tracks;

        return JsonCamelCase(tracks);
    }

    private async Task<IList<ServiceTrack>> InternalGetServiceTracks(string serviceId,
        Song song, string title, string artist, string album)
    {
        IList<ServiceTrack> tracks = null;

        var service = string.IsNullOrWhiteSpace(serviceId)
            ? null
            : MusicService.GetService(serviceId[0]);

        try
        {
            tracks = await MusicServiceManager.FindMusicServiceSong(
                service, song, title, artist, album);
        }
        catch (WebException e)
        {
            Logger.LogError($"GetServiceTracks Failed: {e.Message}");

            if (e.Message.Contains("Unauthorized"))
            {
                Logger.LogError("!!!!!AUTHORIZATION FAILED!!!!!");
            }
        }

        return tracks;
    }
}
