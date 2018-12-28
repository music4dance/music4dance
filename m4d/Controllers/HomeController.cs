using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using m4dModels;
using Microsoft.AspNet.Identity;
using Stripe;

namespace m4d.Controllers
{
    public class HomeController : DMController
    {
        public override string DefaultTheme => MusicTheme;

        [AllowAnonymous]
        public ActionResult Index()
        {
            return View();
        }

        [AllowAnonymous]
        public ActionResult FAQ()
        {
            ThemeName = BlogTheme;
            return View();
        }

        [AllowAnonymous]
        public ActionResult Credits()
        {
            ThemeName = BlogTheme;
            return View();
        }

        [AllowAnonymous]
        public ActionResult Dances()
        {
            return RedirectPermanent("/dances");
        }

        [AllowAnonymous]
        public ActionResult SiteMap()
        {
            ThemeName = BlogTheme;
            var data = DanceStatsManager.GetFlatDanceStats(Database).OrderBy(sc => sc.DanceName);
            return View(data);
        }

        [AllowAnonymous]
        public ActionResult Tempi(bool detailed = false, int style = 0, int meter = 1, int type = 0, int org = 0)
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
        public ActionResult About()
        {
            ThemeName = BlogTheme;
            return View();
        }

        [AllowAnonymous]
        public ActionResult TermsOfService()
        {
            ThemeName = BlogTheme;
            return View();
        }

        [AllowAnonymous]
        public ActionResult PrivacyPolicy()
        {
            ThemeName = BlogTheme;
            return View();
        }


        [AllowAnonymous]
        public ActionResult Contact()
        {
            ThemeName = BlogTheme;
            return View();
        }

        [AllowAnonymous]
        public ActionResult Counter(bool showMPM=true, bool showBPM=false, bool showEpsilon=true, int? numerator=null, decimal? tempo= null)
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
        public ActionResult CounterHelp()
        {
            return RedirectPermanent("/blog/music4dance-help/tempo-counter/");
        }

        [AllowAnonymous]
        public ActionResult Contribute()
        {
            return View();
        }

        // TODONEXT:
        //  Wire up purchase to ad-free experience
        //  More verification of error handling
        //  Verify that anonymous donations still work
        //  Turn off full address verification?
        //  Fill out the other contributions with links
        [AllowAnonymous]
        public ActionResult Purchase(decimal amount, PurchaseKind kind)
        {
            string user = null;
            string email = null;

            if (User.Identity.IsAuthenticated)
            {
                user = User.Identity.Name;
                email = UserManager.GetEmail(User.Identity.GetUserId());
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AllowAnonymous]
        public ActionResult ConfirmPurchase(string stripeToken, PurchaseKind kind, decimal amount)
        {
            // Do the stripy things
            var user = User.Identity.IsAuthenticated ? User.Identity.Name : null;
            var conf = new ShortGuid(Guid.NewGuid()).ToString();

            var purchase = new PurchaseModel
            {
                Kind = kind,
                Amount = amount,
                User = user,
                Confirmation = conf
            };

            try
            {
                // TODO: Can this be done once per session?
                StripeConfiguration.SetApiKey(Environment.GetEnvironmentVariable("STRIPE_SK"));

                var metaData = new Dictionary<string, string> {{"confirmation-code", conf}};
                if (user != null)
                {
                    metaData.Add("user-id", user);
                }

                var options = new ChargeCreateOptions
                {
                    Amount = purchase.Pennies,
                    Currency = "usd",
                    Description = purchase.Description,
                    SourceId = stripeToken,
                    Metadata = metaData
                };

                var service = new ChargeService();
                var charge = service.Create(options);

                return View("ConfirmPurchase", purchase);
            }
            catch (StripeException e)
            {
                purchase.Error = new PurchaseError
                {
                    ErrorType = e.StripeError.ErrorType,
                    ErrorCode = e.StripeError.Code,
                    ErrorMessage = e.StripeError.Message
                };

                return View("PurchaseError", purchase);
            }
        }
    }
}