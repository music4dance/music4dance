using AutoMapper;
using Azure.Search.Documents;
using m4d.Services;
using m4d.Utilities;
using m4dModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.FileProviders;
using Microsoft.FeatureManagement;

namespace m4d.Controllers;

public class ContentController : DanceMusicController
{
    protected SongFilter Filter { get; set; }
    protected readonly LinkGenerator LinkGenerator;
    protected readonly IMapper Mapper;

    public ContentController(
        DanceMusicContext context, UserManager<ApplicationUser> userManager,
        ISearchServiceManager searchService, IDanceStatsManager danceStatsManager,
        IConfiguration configuration, IFileProvider fileProvider, IBackgroundTaskQueue backroundTaskQueue,
        IFeatureManager featureManager, ILogger logger, LinkGenerator linkGenerator, IMapper mapper) :
        base(context, userManager, searchService, danceStatsManager, configuration,
            fileProvider, backroundTaskQueue, featureManager, logger)
    {
        LinkGenerator = linkGenerator;
        Mapper = mapper;
    }

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
}
