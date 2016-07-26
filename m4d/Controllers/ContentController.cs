using System.Collections.Generic;
using m4dModels;

namespace m4d.Controllers
{
    public class ContentController : DMController
    {
        protected void SetupLikes(IEnumerable<Song> songs, string danceId)
        {
            var userName = HttpContext.User.Identity.Name;
            var likes = Database.UserLikes(songs, userName);
            if (likes != null)
            {
                ViewBag.Likes = likes;
            }

            if (danceId == null) return;

            var danceLikes = Database.UserDanceLikes(songs, danceId, userName);
            if (danceLikes != null)
            {
                ViewBag.DanceLikes = danceLikes;
                ViewBag.DanceId = danceId;
            }
        }
    }
}
