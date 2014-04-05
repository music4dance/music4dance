using PagedList;
using System.Linq;
using System.Web.Mvc;

using m4d.Context;

namespace music4dance.Controllers
{
    public class SongPropertyController : Controller
    {
        private DanceMusicContext _db = new DanceMusicContext();

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
            var songProperties = from s in _db.SongProperties select s;

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

        protected override void Dispose(bool disposing)
        {
            _db.Dispose();
            base.Dispose(disposing);
        }
    }
}
