using System;
using m4d.ViewModels;
using m4dModels;
//using m4dModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using PurchaseKind = m4d.ViewModels.PurchaseKind;
using PurchaseModel = m4d.ViewModels.PurchaseModel;

// For more information on enabling MVC for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace m4d.Controllers
{
    public class HomeController : DanceMusicController
    {
        public HomeController(DanceMusicContext context, UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, ISearchServiceManager searchService, IDanceStatsManager danceStatsManager) : 
            base (context, userManager, roleManager, searchService, danceStatsManager)
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
        public IActionResult Purchase(decimal amount, PurchaseKind kind)
        {
            ThemeName = BlogTheme;
            HelpPage = "subscriptions";

            string user = null;
            string email = null;

            if (User.Identity.IsAuthenticated)
            {
                user = User.Identity.Name;
                //CORETODO: email = UserManager<>.GetEmail(User.Identity.GetUserId());
            }

            var purchase = new PurchaseModel
            {
                Key = Environment.GetEnvironmentVariable("STRIPE_PK"),
                Kind = kind,
                Amount = amount,
                User = user,
                Email = email
            };
            return View(purchase);
        }

        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //[AllowAnonymous]
        //public ActionResult ConfirmPurchase(string stripeToken, string stripeEmail, PurchaseKind kind, decimal amount)
        //{
        //    ThemeName = BlogTheme;
        //    HelpPage = "subscriptions";

        //    // Do the stripy things
        //    var userName = User.Identity.IsAuthenticated ? User.Identity.Name : null;
        //    var conf = new ShortGuid(Guid.NewGuid()).ToString();

        //    var purchase = new PurchaseModel
        //    {
        //        Kind = kind,
        //        Amount = amount,
        //        User = userName,
        //        Confirmation = conf
        //    };

        //    try
        //    {
        //        // TODO: Can this be done once per session?
        //        StripeConfiguration.SetApiKey(Environment.GetEnvironmentVariable("STRIPE_SK"));

        //        var metaData = new Dictionary<string, string> { { "confirmation-code", conf } };
        //        if (userName != null)
        //        {
        //            metaData.Add("user-id", userName);
        //        }

        //        ApplicationUser user = null;
        //        if (kind == PurchaseKind.Purchase && userName != null)
        //        {
        //            user = Database.UserManager.FindById(User.Identity.GetUserId());
        //        }

        //        var options = new ChargeCreateOptions
        //        {
        //            Amount = purchase.Pennies,
        //            Currency = "usd",
        //            Description = purchase.Description,
        //            SourceId = stripeToken,
        //            Metadata = metaData,
        //            ReceiptEmail = stripeEmail
        //        };

        //        var service = new ChargeService();
        //        var charge = service.Create(options);
        //        if (charge.Paid)
        //        {
        //            if (user != null && kind == PurchaseKind.Purchase)
        //            {
        //                DateTime? start = DateTime.Now;
        //                if (user.SubscriptionEnd != null && user.SubscriptionEnd < start)
        //                {
        //                    start = user.SubscriptionEnd;
        //                }
        //                user.SubscriptionStart = start;
        //                user.SubscriptionEnd = start.Value.AddYears(1);
        //                user.SubscriptionLevel = SubscriptionLevel.Silver;
        //                Database.SaveChanges();

        //                UserManager.AddToRoles(user.Id, DanceMusicService.PremiumRole);
        //                UserManager.Update(user);

        //                Request.GetOwinContext().Authentication.SignOut(DefaultAuthenticationTypes.ExternalCookie);
        //                var identity = UserManager.CreateIdentity(user, DefaultAuthenticationTypes.ApplicationCookie);
        //                Request.GetOwinContext().Authentication
        //                    .SignIn(new AuthenticationProperties { IsPersistent = true }, identity);
        //            }

        //            return View("ConfirmPurchase", purchase);
        //        }

        //        purchase.Error = new PurchaseError
        //        {
        //            ErrorType = "internal_error"
        //        };
        //    }
        //    catch (StripeException e)
        //    {
        //        purchase.Error = new PurchaseError
        //        {
        //            ErrorType = e.StripeError.ErrorType,
        //            ErrorCode = e.StripeError.Code,
        //            ErrorMessage = e.StripeError.Message
        //        };
        //    }
        //    return View("PurchaseError", purchase);
        //}

        [AllowAnonymous]
        public IActionResult IntentionalError(string message)
        {
            throw new Exception(message ?? "This is an error");
        }
    }
}
