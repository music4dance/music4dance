using AutoMapper;

using m4d.Services;
using m4d.Services.ServiceHealth;
using m4d.ViewModels;

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

    private const int SearchesPageSize = 100;

    // GET: Searches
    public async Task<IActionResult> Index(string user, string sort = null,
        bool showDetails = false, bool spotifyOnly = false, int page = 1)
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
            if (appUser == null)
            {
                return NotFound();
            }
            searches = searches.Where(s => s.ApplicationUserId == appUser.Id);
        }

        var ordered = string.Equals(sort, "recent")
            ? searches.OrderByDescending(s => s.Modified)
            : searches.OrderByDescending(s => s.Count);

        var totalCount = await ordered.CountAsync();
        var model = await ordered
            .Skip((page - 1) * SearchesPageSize)
            .Take(SearchesPageSize)
            .ToListAsync();


        if (user is not null and not "all")
        {
            await SetSpotify(model, user);
        }

        if (spotifyOnly && user is not null and not "all")
        {
            model = model.Where(s => !string.IsNullOrWhiteSpace(s.Spotify)).ToList();
        }

        var isAdmin = User.IsInRole("showDiagnostics");
        var searchSummaries = model.Select(item =>
        {
            var t = item.Filter;
            if (!t.IsAzure)
            {
                t.Action = "Advanced";
            }
            string searchPageUrl = null;
            if (item.MostRecentPage.HasValue && item.MostRecentPage.Value > 1)
            {
                var tPage = item.Filter;
                tPage.Page = item.MostRecentPage;
                if (!tPage.IsAzure)
                {
                    tPage.Action = "Advanced";
                }
                searchPageUrl = Url.Action("Index", "Song", new { filter = tPage.ToString() });
            }
            return new SearchSummary
            {
                Id = item.Id,
                UserName = item.ApplicationUser?.UserName ?? "anonymous",
                Query = item.Query,
                Description = t.Description,
                SearchUrl = Url.Action("Index", "Song", new { filter = t.ToString() }),
                SearchPageUrl = searchPageUrl,
                MostRecentPage = item.MostRecentPage,
                Count = item.Count,
                Created = item.Created,
                Modified = item.Modified,
                Spotify = item.Spotify,
                DeleteUrl = Url.Action("Delete", "Searches", new { id = item.Id, user, showDetails, spotifyOnly, sort }),
            };
        }).ToList();

        var viewModel = new SearchesPageModel
        {
            Searches = searchSummaries,
            Page = page,
            TotalPages = (int)Math.Ceiling((double)totalCount / SearchesPageSize),
            Sort = sort,
            ShowDetails = showDetails,
            SpotifyOnly = spotifyOnly,
            User = user,
            IsAdmin = isAdmin,
            CanDeleteAll = user is not null and not "all",
            BasicSearchUrl = Url.Action("Index", "Song", new { user }),
            AdvancedSearchUrl = Url.Action("advancedsearchform", "Song", new { filter = Filter?.ToString() }),
            DeleteAllUrl = user is not null and not "all"
                ? Url.Action("DeleteAll", "Searches", new { sort, showDetails, spotifyOnly })
                : null,
        };

        return Vue3("My Searches", "Search history", "searches", viewModel);
    }

    // GET: Searches/Resume
    public async Task<IActionResult> Resume()
    {
        var appUser = await Database.FindUser(UserName);
        if (appUser == null)
        {
            return RedirectToAction("Index");
        }

        var latest = await Database.Searches
            .Where(s => s.ApplicationUserId == appUser.Id)
            .OrderByDescending(s => s.Modified)
            .FirstOrDefaultAsync();

        if (latest == null)
        {
            return RedirectToAction("Index");
        }

        var filter = latest.Filter;
        if (filter.IsAzure == false)
        {
            filter.Action = "Advanced";
        }
        if (latest.MostRecentPage.HasValue && latest.MostRecentPage.Value > 1)
        {
            filter.Page = latest.MostRecentPage;
        }

        return RedirectToAction("Index", "Song", new { filter });
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
    public async Task<ActionResult> Delete(long id, string sort, bool showDetails = false, bool spotifyOnly = false, string user = null)
    {
        var search = await Find(id);
        if (search == null)
        {
            return new NotFoundResult();
        }

        ViewBag.Sort = sort;
        ViewBag.ShowDetails = showDetails;
        ViewBag.SpotifyOnly = spotifyOnly;
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
    public async Task<ActionResult> DeleteConfirmed(long id, string sort, bool showDetails = false, bool spotifyOnly = false, string user = null)
    {
        var search = await Find(id);

        if (search != null)
        {
            Authenticate(search.ApplicationUser?.UserName);

            // Anonymize rather than hard-delete so the data is preserved for site statistics
            var anon = await Database.Searches
                .FirstOrDefaultAsync(s => s.ApplicationUserId == null && s.Query == search.Query);

            if (anon != null)
            {
                // Merge counts and widen the time range into the existing anonymous row
                anon.Count += search.Count;
                if (search.Modified > anon.Modified)
                    anon.Modified = search.Modified;
                if (search.Created < anon.Created)
                    anon.Created = search.Created;
                Database.Searches.Remove(search);
            }
            else
            {
                // No anonymous entry yet — detach this row from the user
                search.ApplicationUserId = null;
                search.ApplicationUser = null;
            }

            _ = await Database.SaveChanges();
            return RedirectToAction("Index", new { sort, showDetails, spotifyOnly, user });
        }

        ViewBag.errorMessage = $"Search {id} not found.";
        return View("Error");
    }

    // GET: Searches/DeleteAll
    public async Task<IActionResult> DeleteAll(string sort, bool showDetails = false, bool spotifyOnly = false)
    {
        var appUser = await Database.FindUser(UserName);
        if (appUser == null)
        {
            return RedirectToAction("Index");
        }

        var count = await Database.Searches.CountAsync(s => s.ApplicationUserId == appUser.Id);
        ViewBag.Count = count;
        ViewBag.Sort = sort;
        ViewBag.ShowDetails = showDetails;
        ViewBag.SpotifyOnly = spotifyOnly;
        return View();
    }

    // POST: Searches/DeleteAll
    [HttpPost]
    [ActionName("DeleteAll")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAllConfirmed(string sort, bool showDetails = false, bool spotifyOnly = false)
    {
        var appUser = await Database.FindUser(UserName);
        if (appUser != null)
        {
            var userSearches = await Database.Searches
                .Where(s => s.ApplicationUserId == appUser.Id)
                .ToListAsync();

            foreach (var search in userSearches)
            {
                // Look for an existing anonymous entry with the same normalized query
                var anon = await Database.Searches
                    .FirstOrDefaultAsync(s => s.ApplicationUserId == null && s.Query == search.Query);

                if (anon != null)
                {
                    // Merge counts and widen the time range into the existing anonymous row
                    anon.Count += search.Count;
                    if (search.Modified > anon.Modified)
                        anon.Modified = search.Modified;
                    if (search.Created < anon.Created)
                        anon.Created = search.Created;
                    Database.Searches.Remove(search);
                }
                else
                {
                    // No anonymous entry yet — detach this row from the user
                    search.ApplicationUserId = null;
                    search.ApplicationUser = null;
                }
            }

            _ = await Database.SaveChanges();
        }
        return RedirectToAction("Index", new { sort, showDetails, spotifyOnly });
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
