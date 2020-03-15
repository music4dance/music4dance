using System.Threading.Tasks;
using m4dModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace m4d.APIControllers
{
    [ApiController]
    [Route("api/[controller]")]

    public class ServiceTrackController : DanceMusicApiController
    {
        public ServiceTrackController(DanceMusicContext context, UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, ISearchServiceManager searchService, IDanceStatsManager danceStatsManager) :
            base(context, userManager, roleManager, searchService, danceStatsManager)
        {
        }

        // GET api/<controller>
        [HttpGet("{id}")]
        public async Task<IActionResult> Get(string id)
        {
            if (string.IsNullOrWhiteSpace(id) || id.Length < 2)
            {
                return BadRequest("Invalid Id");
            }

            var service = MusicService.GetService(id[0]);
            id = service.NormalizeId(id.Substring(1));

            var user = await Database.UserManager.GetUserAsync(User);

            // Find a song associate with the service id
            var song = Database.GetSongFromService(service, id, user.UserName);

            if (song != null)
            {
                return Ok(new {song.SongId, song.Title, song.Artist});
            }

            // Otherwise, get the track info based on the idea
            var track = MusicServiceManager.GetMusicServiceTrack(id, service);
            if (track != null) return Ok(track);

            // If that fails, the ID is bad.

            return NotFound();
        }
    }
}