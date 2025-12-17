using AutoMapper;

using m4d.Services;
using m4d.Services.ServiceHealth;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.FeatureManagement;

using System.Security.Authentication;

namespace m4d.Controllers;

[Authorize]
public class SearchesController : ContentController
{
    public SearchesController(
        DanceMusicContext context, UserManager<ApplicationUser> userManager,
        ISearchServiceManager searchService, IDanceStatsManager danceStatsManager,
        IConfiguration configuration, IFileProvider fileProvider, IBackgroundTaskQueue backroundTaskQueue,
        IFeatureManagerSnapshot featureManager, ILogger<SearchesController> logger, LinkGenerator linkGenerator, IMapper mapper,
        ServiceHealthManager serviceHealth) :
        base(context, userManager, searchService, danceStatsManager, configuration,
            fileProvider, backroundTaskQueue, featureManager, logger, linkGenerator, mapper, serviceHealth)
    {
        HelpPage = "saved-searches";
    }

    // GET: Searches
    public async Task<IActionResult> Index(string user, string sort = null,
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
        if (user is not null and not "all")
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


        if (user is not null and not "all")
        {
            await SetSpotify(searches, user);
        }

        ViewBag.Sort = sort;
        ViewBag.ShowDetails = showDetails;
        ViewBag.SongFilter = Filter;
        ViewBag.User = user;
        return View(model);
    }

    private async Task SetSpotify(IEnumerable<Search> searches, string userName)
    {
        foreach (var search in searches)
        {
            search.Spotify = await SpotifyFromFilter(search.Filter, userName);
        }
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

        user ??= search.ApplicationUser?.UserName;
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
            Authenticate(search.ApplicationUser?.UserName);
            _ = Database.Searches.Remove(search);
            _ = await Database.SaveChanges();
            return RedirectToAction("Index", new { sort, showDetails, user });
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
