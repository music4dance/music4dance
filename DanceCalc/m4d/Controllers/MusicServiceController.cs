using m4d.Context;
using m4dModels;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace m4d.Controllers
{
    public class MusicServiceController : DMApiController
    {

        public IHttpActionResult GetServiceTracks(Guid id, string service, string title=null, string artist=null, string album=null)
        {
            SongDetails song = Database.FindSongDetails(id);
            if (song != null && artist == null && title == null)
            {
                artist = song.Artist;
                title = song.Title;
            }

            string key = string.Format("{0}|{1}|{2}|{3}", id, service, artist, title);

            IList<ServiceTrack> tracks = null;

            if (!s_cache.TryGetValue(key,out tracks))
            {
                MusicService ms = MusicService.GetService(service[0]);
                tracks = InternalGetServiceTracks(song,ms,false,title,artist,album);

                if (tracks == null || tracks.Count == 0)
                {
                    artist = SongBase.CleanString(artist);
                    title = SongBase.CleanString(title);

                    tracks = InternalGetServiceTracks(song, ms, true, title, artist, album);
                }
            }

            if (tracks == null || tracks.Count == 0)
            {
                return NotFound();
            }
            else
            {
                s_cache[key] = tracks;
            }

            return Ok(tracks);
        }

        // TODO:  Pretty sure we can pull the 'clean' parameter from this and descendents
        private IList<ServiceTrack> InternalGetServiceTracks(SongDetails song, MusicService service, bool clean, string title, string artist, string album)
        {
            Guid songId = Guid.Empty;

            IList<ServiceTrack> tracks = null;

            try
            {
                tracks = Context.FindMusicServiceSong(song, service, clean, title, artist, album);
            }
            catch (WebException e)
            {
                Trace.WriteLine(string.Format("GetServiceTracks Failed: {0}",e.Message));
            }

            return tracks;
        }

        private static Dictionary<string,IList<ServiceTrack>> s_cache = new Dictionary<string,IList<ServiceTrack>>();
    }
}
