using System;
using System.Collections.Generic;
using System.Configuration;
using System.Threading.Tasks;
using m4d.ViewModels;
using m4dModels;
using Microsoft.AspNetCore.Authentication;
//using m4dModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Stripe;
using PurchaseKind = m4d.ViewModels.PurchaseKind;
using PurchaseModel = m4d.ViewModels.PurchaseModel;

// For more information on enabling MVC for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace m4d.Controllers
{
    public class HomeController : DanceMusicController
    {
        public HomeController(DanceMusicContext context, UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, ISearchServiceManager searchService, IDanceStatsManager danceStatsManager, IConfiguration configuration) : 
            base (context, userManager, roleManager, searchService, danceStatsManager, configuration)
        {
        }

        public override string DefaultTheme => MusicTheme;

        [AllowAnonymous]
        public IActionResult Index([FromServices] IFileProvider fileProvider)
        {
            return View(SiteMapInfo.GetCategories(fileProvider));
        }

        [AllowAnonymous]
        public IActionResult FAQ()
        {
            ThemeName = BlogTheme;
            return View();
        }

        [AllowAnonymous]
        public IActionResult Credits()
        {
            ThemeName = BlogTheme;
            return View();
        }

        [AllowAnonymous]
        public IActionResult Dances()
        {
            return RedirectPermanent("/dances");
        }

        [AllowAnonymous]
        public IActionResult SiteMap([FromServices] IFileProvider fileProvider)
        {
            return View(SiteMapInfo.GetCategories(fileProvider));
        }

        [AllowAnonymous]
        public IActionResult Tempi(bool detailed = false, int style = 0, int meter = 1, int type = 0, int org = 0)
        {
            ThemeName = ToolTheme;
            ViewBag.paramDetailed = detailed;
            ViewBag.paramStyle = style;
            ViewBag.paramMeter = meter;
            ViewBag.paramType = type;
            ViewBag.paramOrg = org;
            ViewBag.DanceStyles = Dance.DanceLibrary.AllDanceGroups;
            HelpPage = "dance-tempi";
            return View(Dance.DanceLibrary);
        }

        [AllowAnonymous]
        public IActionResult About()
        {
            ThemeName = BlogTheme;
            return View();
        }

        [AllowAnonymous]
        public IActionResult TermsOfService()
        {
            ThemeName = BlogTheme;
            return View();
        }

        [AllowAnonymous]
        public IActionResult PrivacyPolicy()
        {
            ThemeName = BlogTheme;
            return View();
        }


        [AllowAnonymous]
        public IActionResult Contact()
        {
            ThemeName = BlogTheme;
            return View();
        }

        [AllowAnonymous]
        public IActionResult Counter(bool showMPM = true, bool showBPM = false, bool showEpsilon = true,
            int? numerator = null, decimal? tempo = null)
        {
            ThemeName = ToolTheme;
            ViewBag.paramShowMPM = showMPM;
            ViewBag.paramShowBPM = showBPM;
            ViewBag.ShowEpsilon = showEpsilon;

            if (numerator.HasValue && numerator != 0)
            {
                ViewBag.paramNumerator = numerator.Value;
            }

            if (tempo.HasValue)
            {
                ViewBag.paramTempo = tempo.Value;
            }

            HelpPage = "tempo-counter";
            return View();
        }

        [AllowAnonymous]
        public IActionResult CounterHelp()
        {
            return RedirectPermanent("https://music4dance.blog/music4dance-help/tempo-counter/");
        }

        [AllowAnonymous]
        public IActionResult Contribute()
        {
            ThemeName = BlogTheme;
            HelpPage = "subscriptions";
            return View();
        }

        [AllowAnonymous]
        public async Task<IActionResult> Purchase([FromServices] IConfiguration configuration, decimal amount, PurchaseKind kind)
        {
            ThemeName = BlogTheme;
            HelpPage = "subscriptions";

            string userName = null;
            string email = null;

            if (User.Identity.IsAuthenticated)
            {
                var user = await UserManager.GetUserAsync(User);
                userName = user.UserName;
                email = user.Email;
            }

            var purchase = new PurchaseModel
            {
                Key = configuration["Authentication:Stripe:PublicKey"],
                Kind = kind,
                Amount = amount,
                User = userName,
                Email = email
            };
            return View(purchase);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AllowAnonymous]
        public async Task<ActionResult> ConfirmPurchase([FromServices]SignInManager<ApplicationUser> signInManager, [FromServices] IConfiguration configuration, string stripeToken, string stripeEmail, PurchaseKind kind, decimal amount)
        {
            ThemeName = BlogTheme;
            HelpPage = "subscriptions";

            // Do the stripy things
            var userName = User.Identity.IsAuthenticated ? User.Identity.Name : null;
            var conf = new ShortGuid(Guid.NewGuid()).ToString();

            var purchase = new PurchaseModel
            {
                Kind = kind,
                Amount = amount,
                User = userName,
                Confirmation = conf
            };

            try
            {
                // TODO: Can this be done once per session?
                StripeConfiguration.ApiKey = configuration["Authentication:Stripe:SecretKey"];
                

                var metaData = new Dictionary<string, string> { { "confirmation-code", conf } };
                if (userName != null)
                {
                    metaData.Add("user-id", userName);
                }

                ApplicationUser user = null;
                if (kind == PurchaseKind.Purchase && userName != null)
                {
                    user = await UserManager.GetUserAsync(User); ;
                }

                var options = new ChargeCreateOptions
                {
                    Amount = purchase.Pennies,
                    Currency = "usd",
                    Description = purchase.Description,
                    Source = stripeToken,
                    Metadata = metaData,
                    ReceiptEmail = stripeEmail
                };

                var service = new ChargeService();
                var charge = service.Create(options);
                if (charge.Paid)
                {
                    if (user != null && kind == PurchaseKind.Purchase)
                    {
                        DateTime? start = DateTime.Now;
                        if (user.SubscriptionEnd != null && user.SubscriptionEnd < start)
                        {
                            start = user.SubscriptionEnd;
                        }
                        user.SubscriptionStart = start;
                        user.SubscriptionEnd = start.Value.AddYears(1);
                        user.SubscriptionLevel = SubscriptionLevel.Silver;
                        Database.SaveChanges();

                        await UserManager.AddToRoleAsync(user, DanceMusicCoreService.PremiumRole);

                        await signInManager.RefreshSignInAsync(user);
                    }

                    return View("ConfirmPurchase", purchase);
                }

                purchase.Error = new PurchaseError
                {
                    ErrorType = "internal_error"
                };
            }
            catch (StripeException e)
            {
                purchase.Error = new PurchaseError
                {
                    ErrorType = e.StripeError.ErrorType,
                    ErrorCode = e.StripeError.Code,
                    ErrorMessage = e.StripeError.Message
                };
            }
            return View("PurchaseError", purchase);
        }

        [AllowAnonymous]
        public IActionResult IntentionalError(string message)
        {
            throw new Exception(message ?? "This is an error");
        }
    }
}
