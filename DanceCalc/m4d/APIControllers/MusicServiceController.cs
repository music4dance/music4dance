using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Net;
using System.Web.Http;
using m4dModels;

namespace m4d.APIControllers
{
    public class MusicServiceController : DMApiController
    {
        // TODONEXT:When doing manual add of track information, make sure we get the US info (pull the stuff from SpotifyUpdate and put it here somehow?
        public IHttpActionResult GetServiceTracks(Guid id, string service, string title = null, string artist = null, string album = null, string region=null)
        {
            var song = Database.FindSongDetails(id);
            if (song != null && artist == null && title == null)
            {
                artist = song.Artist;
                title = song.Title;
            }

            var key = string.Format("{0}|{1}|{2}|{3}", id, service, artist, title);

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
                Trace.WriteLine(string.Format("GetServiceTracks Failed: {0}",e.Message));
            }

            return tracks;
        }

        private static readonly Dictionary<string,IList<ServiceTrack>> s_cache = new Dictionary<string,IList<ServiceTrack>>();
    }
}
