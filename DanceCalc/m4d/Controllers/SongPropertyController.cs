using System.Linq;
using System.Web.Mvc;
using PagedList;

namespace m4d.Controllers
{
    public class SongPropertyController : DMController
    {
        public override string DefaultTheme
        {
            get
            {
                return MusicTheme;
            }
        }

        //
        // GET: /SongProperty/

        [AllowAnonymous]
        public ActionResult Index(string dances, long? start, int? count, int? page)
        {
            // Set up the viewbag
            ViewBag.Start = start;
            ViewBag.Count = count;

            // Now setup the view
            // Start with all of the songs in the database
            var songProperties = from s in Database.SongProperties select s;

            if (start != null)
            {
                songProperties = songProperties.Where(p => p.Id > start.Value);
            }

            if (count != null)
            {
                songProperties = songProperties.Where(p => p.Id < start.Value + count.Value);
            }

            songProperties = songProperties.OrderBy(p => p.Id);

            int pageSize = 50;
            int pageNumber = (page ?? 1);

            return View(songProperties.ToPagedList(pageNumber, pageSize));
        }
    }
}
