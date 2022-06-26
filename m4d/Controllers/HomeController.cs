using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using m4d.ViewModels;
using m4dModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;

namespace m4d.Controllers
{
    public class HomeController : CommerceController
    {
        public HomeController(DanceMusicContext context, UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager, ISearchServiceManager searchService,
            IDanceStatsManager danceStatsManager, IConfiguration configuration) :
            base(context, userManager, roleManager, searchService, danceStatsManager, configuration)
        {
        }

        public IActionResult Index([FromServices] IFileProvider fileProvider)
        {
            return Vue(
                "Home Page",
                "music4dance.net is an online music catalog, tool and educational resource to help Ballroom, Salsa, Swing, Tango and other dancers find fun and exciting music.",
                "home",
                new HomeModel(SiteMapInfo.GetCategories(fileProvider)),
                script: "_fbLike");
        }

        public IActionResult FAQ()
        {
            return Vue("FAQ", "Frequently Asked Questions about Music4Dance and the answers.", "faq", danceEnvironment: true);
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
            return Vue(
                "About music4Dance", "About the music4dance project an it's creator", "about");
        }

        public IActionResult Resume()
        {
            return Vue("Resume", "David W. Gray's Resume", "resume");
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
            return Vue(
                "Reading List",
                "Some fun and interesting reading related to dance in one way or another",
                "reading-list");
        }

        public IActionResult Counter(int? numerator = null, decimal? tempo = null)
        {
            return Vue(
                "Counter",
                "A web application to measure the tempo of a song and match it with styles of dance.",
                "tempo-counter", new TempoCounterModel { Numerator = numerator, Tempo = tempo },
                danceEnvironment:true);
        }

        public IActionResult Tempi(List<string> styles, List<string> types,
            List<string> organizations, List<string> meters)
        {

            return Vue(
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
                danceEnvironment:true);
        }

        private static List<string> ConvertParameter(List<string> parameter)
        {
            return parameter is { Count: > 0 } ? parameter : null;
        }

        public IActionResult CounterHelp()
        {
            return RedirectPermanent("https://music4dance.blog/music4dance-help/tempo-counter/");
        }

        public async Task<IActionResult> Contribute()
        {
            HelpPage = "subscriptions";

            var user = await UserManager.GetUserAsync(User);
            return View(await GetContributeModel(user));
        }
        private async Task<ContributeModel> GetContributeModel(ApplicationUser user)
        {
            if (User.Identity == null)
            {
                throw new Exception("Expected identity when in contribution page.");
            }

            return user == null
                ? new ContributeModel
                {
                    CommerceEnabled = IsCommerceEnabled(),
                }
                : new ContributeModel
            {
                CommerceEnabled = IsCommerceEnabled(),
                IsAuthenticated = User.Identity.IsAuthenticated,
                CurrentPremium  = await Database.UserManager.IsInRoleAsync(user, DanceMusicCoreService.PremiumRole),
                FraudDetected = IsFraudDetected(user),
            };
        }


    }
}
