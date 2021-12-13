using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Threading.Tasks;
using m4dModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace m4d.APIControllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MusicServiceController : DanceMusicApiController
    {
        // ReSharper disable once InconsistentNaming
        private static readonly Dictionary<string, IList<ServiceTrack>> s_cache = new();

        public MusicServiceController(DanceMusicContext context,
            UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager,
            ISearchServiceManager searchService, IDanceStatsManager danceStatsManager,
            IConfiguration configuration) :
            base(context, userManager, roleManager, searchService, danceStatsManager, configuration)
        {
        }

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
            var song = id == Guid.Empty ? null : await Database.FindSong(id);
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
                Trace.WriteLineIf(
                    TraceLevels.General.TraceError,
                    $"GetServiceTracks Failed: {e.Message}");

                if (e.Message.Contains("Unauthorized"))
                {
                    Trace.WriteLineIf(
                        TraceLevels.General.TraceError,
                        "!!!!!AUTHORIZATION FAILED!!!!!");
                }
            }

            return tracks;
        }
    }
}
