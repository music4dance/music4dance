using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
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
        public MusicServiceController(DanceMusicContext context,
            UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager,
            ISearchServiceManager searchService, IDanceStatsManager danceStatsManager,
            IConfiguration configuration) :
            base(context, userManager, roleManager, searchService, danceStatsManager, configuration)
        {
        }

        [HttpGet("{id}")]
        public IActionResult Get(Guid id, string service = null, string title = null
            , string artist = null, string album = null)
        {
            var song = Database.FindSong(id);
            if (song != null && artist == null && title == null)
            {
                artist = song.Artist;
                title = song.Title;
            }

            var key = $"{id}|{service ?? "A"}|{artist ?? ""}|{title ?? ""}";


            if (!s_cache.TryGetValue(key, out var tracks))
                tracks = InternalGetServiceTracks(song, service, title, artist, album);

            if (tracks == null || tracks.Count == 0) return NotFound();

            s_cache[key] = tracks;

            return JsonCamelCase(tracks);
        }

        private IList<ServiceTrack> InternalGetServiceTracks(Song song,
            string serviceId, string title, string artist, string album)
        {
            IList<ServiceTrack> tracks = null;

            var service = string.IsNullOrWhiteSpace(serviceId)
                ? null
                : MusicService.GetService(serviceId[0]);

            try
            {
                tracks = MusicServiceManager.FindMusicServiceSong(
                    song, service, title, artist, album);
            }
            catch (WebException e)
            {
                Trace.WriteLineIf(TraceLevels.General.TraceError,
                    $"GetServiceTracks Failed: {e.Message}");

                if (e.Message.Contains("Unauthorized"))
                    Trace.WriteLineIf(TraceLevels.General.TraceError,
                        "!!!!!AUTHORIZATION FAILED!!!!!");
            }

            return tracks;
        }

        // ReSharper disable once InconsistentNaming
        private static readonly Dictionary<string, IList<ServiceTrack>> s_cache = new();
    }
}