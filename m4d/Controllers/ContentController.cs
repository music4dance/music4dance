using AutoMapper;

using Azure.Search.Documents;

using m4d.Services;
using m4d.Services.ServiceHealth;
using m4d.Utilities;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.FileProviders;
using Microsoft.FeatureManagement;

namespace m4d.Controllers;

public class ContentController(
    DanceMusicContext context, UserManager<ApplicationUser> userManager,
    ISearchServiceManager searchService, IDanceStatsManager danceStatsManager,
    IConfiguration configuration, IFileProvider fileProvider, IBackgroundTaskQueue backroundTaskQueue,
    IFeatureManagerSnapshot featureManager, ILogger logger, LinkGenerator linkGenerator, IMapper mapper,
    ServiceHealthManager serviceHealth) : DanceMusicController(context, userManager, searchService, danceStatsManager, configuration,
        fileProvider, backroundTaskQueue, featureManager, logger)
{
    protected SongFilter Filter { get; set; }
    protected readonly LinkGenerator LinkGenerator = linkGenerator;
    protected readonly IMapper Mapper = mapper;
    protected readonly ServiceHealthManager ServiceHealth = serviceHealth;

    public override async Task OnActionExecutionAsync(
    ActionExecutingContext context, ActionExecutionDelegate next)
    {
        ViewBag.SongFilter = Filter = GetFilterFromContext(context);
        await base.OnActionExecutionAsync(context, next);
    }

    protected CruftFilter DefaultCruftFilter()
    {
        return User.IsInRole(DanceMusicCoreService.DiagRole) ||
            User.IsInRole(DanceMusicCoreService.PremiumRole) ||
            User.IsInRole(DanceMusicCoreService.TrialRole)
                ? CruftFilter.AllCruft
                : CruftFilter.NoCruft;
    }

    #region General Utilities

    protected async Task<SearchOptions> AzureParmsFromFilter(
        SongFilter filter, int? pageSize = null)
    {
        return SongIndex.AzureParmsFromFilter(
            await UserMapper.DeanonymizeFilter(filter, UserManager), pageSize);
    }

    protected bool IsPremium()
    {
        return User.IsInRole(DanceMusicCoreService.PremiumRole) ||
            User.IsInRole(DanceMusicCoreService.TrialRole) ||
            User.IsInRole(DanceMusicCoreService.DiagRole);
    }

    protected ActionResult HandleRedirect(RedirectException redirect)
    {
        var model = redirect.Model;
        if (redirect.View == "Login" && model is SongFilter filter)
        {
            return Redirect(
                $"/Identity/Account/Login/?ReturnUrl=/song/advancedsearchform?filter={filter}");
        }

        if (redirect.View == "RequiresPremium")
        {
            Filter.Level = null;
            var redirectUrl =
                LinkGenerator.GetUriByAction(
                    HttpContext, "AdvancedSearchForm", "Song",
                    new { Filter });
            model = new PremiumRedirect
            {
                FeatureType = "search",
                FeatureName = "bonus content",
                InfoUrl = "https://music4dance.blog/?page_id=8217",
                RedirectUrl = redirectUrl
            };
        }

        return View(redirect.View, model);
    }

    #endregion

    #region Service Health Checks

    /// <summary>
    /// Check if the database service is available
    /// </summary>
    protected bool IsDatabaseAvailable()
    {
        return ServiceHealth.IsServiceHealthy("Database");
    }

    /// <summary>
    /// Check if the search service is available
    /// </summary>
    protected bool IsSearchAvailable()
    {
        return ServiceHealth.IsServiceHealthy("SearchService");
    }

    /// <summary>
    /// Check if a specific OAuth provider is available
    /// </summary>
    protected bool IsAuthProviderAvailable(string provider)
    {
        return ServiceHealth.IsServiceHealthy($"{provider}OAuth");
    }

    /// <summary>
    /// Set ViewData for all service availability statuses
    /// </summary>
    protected void SetAllServiceStatuses()
    {
        ViewData["DatabaseAvailable"] = IsDatabaseAvailable();
        ViewData["SearchAvailable"] = IsSearchAvailable();
        ViewData["GoogleAvailable"] = IsAuthProviderAvailable("Google");
        ViewData["FacebookAvailable"] = IsAuthProviderAvailable("Facebook");
        ViewData["SpotifyAvailable"] = IsAuthProviderAvailable("Spotify");
    }

    #endregion
}
