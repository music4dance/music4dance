using m4d.Services;
using m4d.ViewModels;

using m4dModels;

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using Microsoft.FeatureManagement;

namespace m4d.Controllers;

public class HomeController(
    DanceMusicContext context, UserManager<ApplicationUser> userManager,
    ISearchServiceManager searchService, IDanceStatsManager danceStatsManager,
    IConfiguration configuration, IFileProvider fileProvider, IBackgroundTaskQueue backroundTaskQueue,
    IFeatureManagerSnapshot featureManager, ILogger<HomeController> logger) : CommerceController(context, userManager, searchService, danceStatsManager, configuration,
        fileProvider, backroundTaskQueue, featureManager, logger)
{
    public IActionResult Index([FromServices] IFileProvider fileProvider)
    {
        return Vue3(
            "Home Page",
            "music4dance.net is an online music catalog, tool and educational resource to help Ballroom, Salsa, Swing, Tango and other dancers find fun and exciting music.",
            "home",
            new HomeModel(SiteMapInfo.GetCategories(fileProvider)),
            script: "_fbLike",
            danceEnvironment: true);
    }

    public IActionResult FAQ()
    {
        return Vue3("FAQ", "Frequently Asked Questions about Music4Dance and the answers.", "faq", danceEnvironment: true);
    }

    public IActionResult Info()
    {
        return View();
    }

    public IActionResult Credits()
    {
        return View();
    }

    public IActionResult Dances()
    {
        return RedirectPermanent("/dances");
    }

    public IActionResult SiteMap([FromServices] IFileProvider fileProvider)
    {
        return View(SiteMapInfo.GetCategories(fileProvider));
    }

    public IActionResult About()
    {
        return Vue3(
            "About music4Dance", "About the music4dance project an it's creator", "about");
    }

    public IActionResult Resume()
    {
        return Vue3("Resume", "David W. Gray's Resume", "resume");
    }

    public IActionResult SpotifyExplorer()
    {
        return Vue3("Spotify Explorer", "Tools to look at spotify users and playlists with a music4dance lense", "spotify-explorer", danceEnvironment: true);
    }

    public IActionResult TermsOfService()
    {
        return View();
    }

    public IActionResult PrivacyPolicy()
    {
        return View();
    }

    public IActionResult ReadingList()
    {
        ViewBag.HideAds = true;
        return Vue3(
            "Reading List",
            "Some fun and interesting reading related to dance in one way or another",
            "reading-list");
    }

    public IActionResult TechnicalBlog()
    {
        return Vue3(
            "Techincal Blog",
            "Some articles about technical issues I've solved, often involving the music4dance site",
            "tech-blog");
    }

    public IActionResult Counter(int? numerator = null, decimal? tempo = null, string count = "beats")
    {
        return Vue3(
            "Counter",
            "A web application to measure the tempo of a song and match it with styles of dance.",
            "tempo-counter",
            new TempoCounterModel { Numerator = numerator, Tempo = tempo, Count = count },
            "tempo-counter",
            danceEnvironment: true);
    }

    public IActionResult Tempi(List<string> styles, List<string> types,
        List<string> organizations, List<string> meters)
    {
        return Vue3(
            "Tempos",
            "A web application to show the relationship between different dance tempos.",
            "tempo-list",
            new TempoListModel
            {
                Styles = ConvertParameter(styles),
                Types = ConvertParameter(types),
                Organizations = ConvertParameter(organizations),
                Meters = ConvertParameter(meters),
            },
            "dance-tempi",
            danceEnvironment: true);
    }

    private static List<string> ConvertParameter(List<string> parameter)
    {
        return parameter is { Count: > 0 } ? parameter : null;
    }

    public IActionResult CounterHelp()
    {
        return RedirectPermanent("https://music4dance.blog/music4dance-help/tempo-counter/");
    }

    public async Task<IActionResult> Contribute(bool recaptchaFailed = false)
    {
        HelpPage = "subscriptions";
        ViewBag.HideAds = true;
        ViewBag.NoWarnings = true;

        var user = await UserManager.GetUserAsync(User);
        return View(await GetContributeModel(user, recaptchaFailed));
    }

    public IActionResult Error()
    {
        var error = HttpContext.Features.Get<IExceptionHandlerPathFeature>();
        return View(
            "HttpError", new ErrorModel
            {
                HttpStatusCode = 500,
                Message = "Something went very wrong",
                Exception = error?.Error
            });
    }

    private async Task<ContributeModel> GetContributeModel(ApplicationUser user, bool recaptchaFailed = false)
    {
        if (User.Identity == null)
        {
            throw new Exception("Expected identity when in contribution page.");
        }

        return user == null
            ? new ContributeModel
            {
                CommerceEnabled = IsCommerceEnabled(),
                RecaptchaFailed = recaptchaFailed
            }
            : new ContributeModel
            {
                CommerceEnabled = IsCommerceEnabled(),
                IsAuthenticated = User.Identity.IsAuthenticated,
                CurrentPremium = await Database.UserManager.IsInRoleAsync(user, DanceMusicCoreService.PremiumRole),
                PremiumExpiration = user.SubscriptionEnd,
                FraudDetected = IsFraudDetected(user),
            };
    }


}
