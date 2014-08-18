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
    public class MusicServiceController : ApiController
    {
        private DanceMusicContext _db = new DanceMusicContext();
        public IHttpActionResult GetServiceTracks(Guid id, string service, bool clean=false, string title=null, string artist=null)
        {
            Guid songId = Guid.Empty;
            SongDetails song = _db.FindSongDetails(id);

            if (song != null && artist == null && title == null)
            {
                artist = clean ? song.CleanArtist : song.Artist;
                title = clean ? song.CleanTitle : song.Title;
            }

            string key = string.Format("{0}|{1}|{2}|{3}|{4}", id, service, clean, artist, title);

            IList<ServiceTrack> tracks = null;

            if (!s_cache.TryGetValue(key,out tracks))
            {
                try
                {
                    tracks = _db.FindMusicServiceSong(song, MusicService.GetService(service[0]), clean, title, artist);
                    s_cache[key] = tracks;
                }
                catch (WebException e)
                {
                    Trace.WriteLine(string.Format("GetServiceTracks Failed: {0}",e.Message));
                }
            }

            if (tracks == null || tracks.Count == 0)
            {
                return NotFound();
            }

            return Ok(tracks);
        }
        protected override void Dispose(bool disposing)
        {
            _db.Dispose();
            base.Dispose(disposing);
        }

        private static Dictionary<string,IList<ServiceTrack>> s_cache = new Dictionary<string,IList<ServiceTrack>>();
    }
}
