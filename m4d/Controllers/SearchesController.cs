using System;
using System.Linq;
using System.Net;
using m4dModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace m4d.Controllers
{
    public class SearchesController : DanceMusicController
    {
        public SearchesController(DanceMusicContext context, UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, ISearchServiceManager searchService, IDanceStatsManager danceStatsManager) :
            base(context, userManager, roleManager, searchService, danceStatsManager)
        {
            HelpPage = "song-list";
        }

        public override string DefaultTheme => MusicTheme;

        // GET: Searches
        public IActionResult Index(string user, string sort=null, SongFilter filter=null, bool showDetails=false)
        {
            if (string.IsNullOrWhiteSpace(user) || user.Equals(UserQuery.AnonymousUser, StringComparison.OrdinalIgnoreCase))
            {
                user = null;
            }

            var appUser = Database.FindUser(user??User.Identity.Name);
            IQueryable<Search> searches = Database.Searches.Include(s => s.ApplicationUser);
            if (appUser != null)
                searches = searches.Where(s => s.ApplicationUserId == appUser.Id);

            searches = (string.Equals(sort, "recent") ? searches.OrderByDescending(s => s.Modified) : searches.OrderByDescending(s => s.Count)).Take(100);
            ViewBag.Sort = sort;
            ViewBag.ShowDetails = showDetails;
            ViewBag.SongFilter = filter;
            return View(searches.ToList());
        }

        // GET: Searches/Details/5
        public IActionResult Details(long? id)
        {
            if (id == null)
            {
                return StatusCode((int) HttpStatusCode.BadRequest);
            }
            var search = Database.Searches.Find(id);
            if (search == null)
            {
                return StatusCode((int)HttpStatusCode.NotFound);
            }
            return View(search);
        }
    }
}
