using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using m4dModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace m4d.Controllers
{
    public class SearchesController : DanceMusicController
    {
        public SearchesController(DanceMusicContext context,
            UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager,
            ISearchServiceManager searchService, IDanceStatsManager danceStatsManager,
            IConfiguration configuration) :
            base(context, userManager, roleManager, searchService, danceStatsManager, configuration)
        {
            HelpPage = "song-list";
        }

        // GET: Searches
        public async Task<IActionResult> Index(string user, string sort = null,
            SongFilter filter = null,
            bool showDetails = false)
        {
            if (string.IsNullOrWhiteSpace(user) || user.Equals(
                UserQuery.IdentityUser,
                StringComparison.OrdinalIgnoreCase))
            {
                user = null;
            }

            user ??= UserName;
            IQueryable<Search> searches = Database.Searches.Include(s => s.ApplicationUser);
            if (user != null)
            {
                var appUser = await Database.FindUser(user);
                if (appUser != null)
                {
                    searches = searches.Where(s => s.ApplicationUserId == appUser.Id);
                }
            }

            searches =
                (string.Equals(sort, "recent")
                    ? searches.OrderByDescending(s => s.Modified)
                    : searches.OrderByDescending(s => s.Count)).Take(100);
            ViewBag.Sort = sort;
            ViewBag.ShowDetails = showDetails;
            ViewBag.SongFilter = filter;
            var list = searches.ToList();
            return View(list);
        }

        // GET: Searches/Details/5
        public IActionResult Details(long? id)
        {
            if (id == null)
            {
                return StatusCode((int)HttpStatusCode.BadRequest);
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
