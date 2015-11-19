using System.Web;
using System.Web.Http;
using m4dModels;
using Microsoft.AspNet.Identity;
using SpotifyWebAPI;

namespace m4d.APIControllers
{
    public class ServiceTrackController : DMApiController
    {
        // GET api/<controller>
        public IHttpActionResult Get(string id)
        {
            if (string.IsNullOrWhiteSpace(id) || id.Length < 2)
            {
                return BadRequest("Invalid Id");
            }

            var service = MusicService.GetService(id[0]);
            id = id.Substring(1);

            // Find a song associate with the serice id
            var song = Database.GetSongFromService(service,id, HttpContext.Current.User.Identity.GetUserName());

            if (song != null)
            {
                return Ok(new {song.SongId, song.Title, song.Artist});
            }

            // Otherwise, get the track info based on the idea
            var track = Context.GetMusicServiceTrack(id, service);
            if (track != null) return Ok(track);

            // If that failes, the ID is bad.

            return NotFound();
        }
    }
}