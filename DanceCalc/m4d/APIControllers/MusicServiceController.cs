using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Web.Http;
using m4dModels;

namespace m4d.APIControllers
{
    public class MusicServiceController : DMApiController
    {
        public IHttpActionResult Get(Guid id, string service, string title = null, string artist = null, string album = null, string region=null)
        {
            var song = Database.FindSongDetails(id);
            if (song != null && artist == null && title == null)
            {
                artist = song.Artist;
                title = song.Title;
            }

            var key = $"{id}|{service}|{artist}|{title}";

            IList<ServiceTrack> tracks;

            if (!s_cache.TryGetValue(key,out tracks))
            {
                var ms = MusicService.GetService(service[0]);
                tracks = InternalGetServiceTracks(song,ms,false,title,artist,album, region);

                if (tracks == null || tracks.Count == 0)
                {
                    artist = SongBase.CleanString(artist);
                    title = SongBase.CleanString(title);

                    tracks = InternalGetServiceTracks(song, ms, true, title, artist, album, region);
                }
            }

            if (tracks == null || tracks.Count == 0)
            {
                return NotFound();
            }

            s_cache[key] = tracks;

            return Ok(tracks);
        }

        // TODO:  Pretty sure we can pull the 'clean' parameter from this and descendents
        private IList<ServiceTrack> InternalGetServiceTracks(SongDetails song, MusicService service, bool clean, string title, string artist, string album, string region)
        {
            IList<ServiceTrack> tracks = null;

            try
            {
                tracks = Context.FindMusicServiceSong(song, service, clean, title, artist, album, region);
            }
            catch (WebException e)
            {
                Trace.WriteLineIf(TraceLevels.General.TraceError, $"GetServiceTracks Failed: {e.Message}");
                if (e.Message.Contains("Unauthorized"))
                {
                    Trace.WriteLineIf(TraceLevels.General.TraceError, "!!!!!AUTHORIZATION FAILED!!!!!");
                }
            }

            return tracks;
        }

        private static readonly Dictionary<string,IList<ServiceTrack>> s_cache = new Dictionary<string,IList<ServiceTrack>>();
    }
}
