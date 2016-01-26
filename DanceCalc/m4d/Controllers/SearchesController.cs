using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web.Mvc;
using m4d.AWSReference;

namespace m4d.Controllers
{
    public class SearchesController : DMController
    {
        public SearchesController()
        {
            HelpPage = "song-list";
        }

        public override string DefaultTheme => MusicTheme;

        // TODONEXT: Add in sort direction to view, add link to replay the search
        // Serialize stored searches (probably in their own section).
        // Don't forget to update user serialization to enable new fields (date, etc.)
        // GET: Searches
        public ActionResult Index(string user, string sort=null, bool showDetails=false)
        {
            var appUser = Database.FindUser(user);
            var searches = Database.Searches.Include(s => s.ApplicationUser);
            if (appUser != null)
                searches = searches.Where(s => s.ApplicationUserId == appUser.Id);

            searches = (string.Equals(sort, "recent") ? searches.OrderByDescending(s => s.Modified) : searches.OrderByDescending(s => s.Count)).Take(100);
            ViewBag.Sort = sort;
            ViewBag.ShowDetails = showDetails;
            return View(searches.ToList());
        }

        // GET: Searches/Details/5
        public ActionResult Details(long? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            var search = Database.Searches.Find(id);
            if (search == null)
            {
                return HttpNotFound();
            }
            return View(search);
        }
    }
}
