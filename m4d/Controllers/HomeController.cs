using System.Collections.Generic;
using System.Threading.Tasks;
using m4d.ViewModels;
using m4dModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Newtonsoft.Json;

// For more information on enabling MVC for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

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
            UseVue = true;
            return View(new HomeModel(SiteMapInfo.GetCategories(fileProvider)));
        }

        public IActionResult FAQ()
        {
            UseVue = true;
            return View();
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
            return View();
        }

        public IActionResult TermsOfService()
        {
            return View();
        }

        public IActionResult PrivacyPolicy()
        {
            return View();
        }

        public IActionResult Counter(int? numerator = null, decimal? tempo = null)
        {
            if (numerator.HasValue && numerator != 0)
            {
                ViewBag.paramNumerator = numerator.Value;
            }

            if (tempo.HasValue)
            {
                ViewBag.paramTempo = tempo.Value;
            }

            HelpPage = "tempo-counter";
            UseVue = true;
            BuildEnvironment(danceEnvironment: true);
            return View("TempoCounter");
        }

        public IActionResult Tempi(List<string> styles, List<string> types,
            List<string> organizations, List<string> meters)
        {
            ViewBag.Styles = ConvertParameter(styles);
            ViewBag.Types = ConvertParameter(types);
            ViewBag.Organizations = ConvertParameter(organizations);
            ViewBag.Meters = ConvertParameter(meters);

            HelpPage = "dance-tempi";
            UseVue = true;
            BuildEnvironment(danceEnvironment: true);
            return View("TempoList");
        }

        private static string ConvertParameter(List<string> parameter)
        {
            return parameter != null && parameter.Count > 0
                ? JsonConvert.SerializeObject(parameter, CamelCaseSerializerSettings)
                : null;
        }

        public IActionResult CounterHelp()
        {
            return RedirectPermanent("https://music4dance.blog/music4dance-help/tempo-counter/");
        }

        public async Task<IActionResult> Contribute()
        {
            HelpPage = "subscriptions";

            var user = await UserManager.GetUserAsync(User);
            return View(GetContributeModel(user));
        }
        private ContributeModel GetContributeModel(ApplicationUser user)
        {
            return new ContributeModel
            {
                CommerceEnabled = IsCommerceEnabled(),
                IsAuthenticated = User.Identity.IsAuthenticated,
                FraudDetected = IsFraudDetected(user),
            };
        }


    }
}
