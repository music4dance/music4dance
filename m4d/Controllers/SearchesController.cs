using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Authentication;
using System.Threading.Tasks;
using m4dModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

namespace m4d.Controllers
{
    [Authorize]
    public class SearchesController : DanceMusicController
    {
        public SearchesController(DanceMusicContext context,
            UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager,
            ISearchServiceManager searchService, IDanceStatsManager danceStatsManager,
            IConfiguration configuration) :
            base(context, userManager, roleManager, searchService, danceStatsManager, configuration)
        {
            HelpPage = "saved-searches";
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

            Authenticate(user);

            IQueryable<Search> searches = Database.Searches.Include(s => s.ApplicationUser);
            if (user != null && user != "all")
            {
                var appUser = await Database.FindUser(user);
                if (appUser != null)
                {
                    searches = searches.Where(s => s.ApplicationUserId == appUser.Id);
                }
            }

            var model =
                (string.Equals(sort, "recent")
                    ? searches.OrderByDescending(s => s.Modified)
                    : searches.OrderByDescending(s => s.Count)).Take(250).ToList();


            if (user != null && user != "all")
            {
                await SetSpotify(searches, user);
            }

            ViewBag.Sort = sort;
            ViewBag.ShowDetails = showDetails;
            ViewBag.SongFilter = filter;
            ViewBag.User = user;
            return View(model);
        }

        private async Task SetSpotify(IEnumerable<Search> searches, string userName)
        {
            var exports = await MapSpotify(userName);
            foreach (var search in searches)
            {
                var filter = search.Filter.Normalize(userName).ToString();
                if (exports.TryGetValue(filter, out var export))
                {
                    search.Spotify = export.Id;
                }
            }
        }

        private async Task<Dictionary<string, SpotifyCreate>> MapSpotify(string userName)
        {
            var map = new Dictionary<string, SpotifyCreate>();
            foreach (var export in await GetSpotify(userName))
            {
                if (export?.Info == null)
                {
                    continue;
                }

                var filter = new SongFilter(export.Info.Filter).Normalize(userName).ToString();
                if (!map.ContainsKey(filter))
                {
                    map[filter] = export;
                }
            }
            return map;
        }

        // TODONEXT: why is this coming up empty for Arne?

        private async Task<List<SpotifyCreate>> GetSpotify(string userName)
        {
            if (string.IsNullOrEmpty(userName))
            {
                return new List<SpotifyCreate>();
            }

            var user = await UserManager.FindByNameAsync(userName);
            if (user == null)
            {
                return new List<SpotifyCreate>();
            }

            var userId = user.Id;

            return  Database.ActivityLog.Where(l => l.ApplicationUserId == userId).OrderByDescending(e => e.Date)
                .Select(ex => JsonConvert.DeserializeObject<SpotifyCreate>(ex.Details)).ToList();
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

        // GET: Searches/Delete/5
        [Authorize]
        public async Task<ActionResult> Delete(long id, string sort, bool showDetails = false, string user = null)
        {
            var search = await Find(id);
            if (search == null)
            {
                return new NotFoundResult();
            }

            ViewBag.Sort = sort;
            ViewBag.ShowDetails = showDetails;
            ViewBag.User = user;

            user ??= search.ApplicationUser.UserName;
            Authenticate(user);

            return View(search);
        }

        // POST: PlayLists/Delete/5
        [HttpPost]
        [ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<ActionResult> DeleteConfirmed(long id, string sort, bool showDetails = false, string user = null)
        {
            var search = await Find(id);

            if (search != null)
            {
                Authenticate(search.ApplicationUser.UserName);
                Database.Searches.Remove(search);
                await Database.SaveChanges();
                return RedirectToAction("Index", new {sort, showDetails, user});
            }

            ViewBag.errorMessage = $"Search ${id} not found.";
            return View("Error");
        }

        private async Task<Search> Find(long id)
        {
            return await Database.Searches.Where(s => s.Id == id).Include("ApplicationUser")
                .FirstOrDefaultAsync();
        }

        private void Authenticate(string user)
        {
            if (!User.IsInRole("dbAdmin") && !string.Equals(
                    user, UserName, StringComparison.OrdinalIgnoreCase))
            {
                throw new AuthenticationException();
            }
        }
    }
}
