using m4dModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;

namespace m4d.Controllers;

public class SearchController : DanceMusicController
{
    public SearchController(DanceMusicContext context,
        UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager,
        ISearchServiceManager searchService, IDanceStatsManager danceStatsManager,
        IConfiguration configuration, IFileProvider fileProvider) :
        base(context, userManager, roleManager, searchService, danceStatsManager, configuration, fileProvider)
    {
        //HelpPage = "song-list";
    }

    // GET: Search
    public IActionResult Index(string search)
    {
        return Vue("music4dance search results", "music4dance search results", "search", search);
    }
}
