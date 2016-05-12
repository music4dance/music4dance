using System;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web.Mvc;
using m4d.AWSReference;
using m4dModels;

namespace m4d.Controllers
{
    public class SearchesController : DMController
    {
        public SearchesController()
        {
            HelpPage = "song-list";
        }

        public override string DefaultTheme => MusicTheme;

        // GET: Searches
        public ActionResult Index(string user, string sort=null, SongFilter filter=null, bool showDetails=false)
        {
            if (string.IsNullOrWhiteSpace(user) || user.Equals(UserQuery.AnonymousUser, StringComparison.OrdinalIgnoreCase))
            {
                user = null;
            }

            var appUser = Database.FindUser(user??User.Identity.Name);
            var searches = Database.Searches.Include(s => s.ApplicationUser);
            if (appUser != null)
                searches = searches.Where(s => s.ApplicationUserId == appUser.Id);

            searches = (string.Equals(sort, "recent") ? searches.OrderByDescending(s => s.Modified) : searches.OrderByDescending(s => s.Count)).Take(100);
            ViewBag.Sort = sort;
            ViewBag.ShowDetails = showDetails;
            ViewBag.SongFilter = filter;
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
