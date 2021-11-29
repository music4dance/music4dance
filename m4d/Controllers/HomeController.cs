using System;
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
using Stripe;

// For more information on enabling MVC for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace m4d.Controllers
{
    public class HomeController : DanceMusicController
    {
        public HomeController(DanceMusicContext context, UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager, ISearchServiceManager searchService,
            IDanceStatsManager danceStatsManager, IConfiguration configuration) :
            base(context, userManager, roleManager, searchService, danceStatsManager, configuration)
        {
        }

        [AllowAnonymous]
        public IActionResult Index([FromServices]IFileProvider fileProvider)
        {
            UseVue = true;
            return View(new HomeModel(SiteMapInfo.GetCategories(fileProvider)));
        }

        [AllowAnonymous]
        public IActionResult FAQ()
        {
            UseVue = true;
            return View();
        }

        [AllowAnonymous]
        public IActionResult Info()
        {
            return View();
        }

        [AllowAnonymous]
        public IActionResult Credits()
        {
            return View();
        }

        [AllowAnonymous]
        public IActionResult Dances()
        {
            return RedirectPermanent("/dances");
        }

        [AllowAnonymous]
        public IActionResult SiteMap([FromServices]IFileProvider fileProvider)
        {
            return View(SiteMapInfo.GetCategories(fileProvider));
        }

        [AllowAnonymous]
        public IActionResult About()
        {
            return View();
        }

        [AllowAnonymous]
        public IActionResult TermsOfService()
        {
            return View();
        }

        [AllowAnonymous]
        public IActionResult PrivacyPolicy()
        {
            return View();
        }

        [AllowAnonymous]
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
            return View("TempoCounter");
        }

        [AllowAnonymous]
        public IActionResult Tempi(List<string> styles, List<string> types,
            List<string> organizations, List<string> meters)
        {
            ViewBag.Styles = ConvertParameter(styles);
            ViewBag.Types = ConvertParameter(types);
            ViewBag.Organizations = ConvertParameter(organizations);
            ViewBag.Meters = ConvertParameter(meters);

            HelpPage = "dance-tempi";
            UseVue = true;
            return View("TempoList");
        }

        private static string ConvertParameter(List<string> parameter)
        {
            return parameter != null && parameter.Count > 0
                ? JsonConvert.SerializeObject(parameter, CamelCaseSerializerSettings)
                : null;
        }

        [AllowAnonymous]
        public IActionResult CounterHelp()
        {
            return RedirectPermanent("https://music4dance.blog/music4dance-help/tempo-counter/");
        }

        [AllowAnonymous]
        public IActionResult Contribute()
        {
            HelpPage = "subscriptions";
            return View();
        }

        [AllowAnonymous]
        public async Task<IActionResult> Purchase([FromServices]IConfiguration configuration,
            decimal amount, PurchaseKind kind)
        {
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
        public async Task<ActionResult> ConfirmPurchase(
            [FromServices]SignInManager<ApplicationUser> signInManager,
            [FromServices]IConfiguration configuration, string stripeToken, string stripeEmail,
            PurchaseKind kind, decimal amount)
        {
            HelpPage = "subscriptions";

            // Do the stripy things
            var userName = Identity.IsAuthenticated ? UserName : null;
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
                    user = await UserManager.GetUserAsync(User);
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
                var charge = await service.CreateAsync(options);
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
                        await Database.SaveChanges();

                        await UserManager.AddToRoleAsync(user, DanceMusicCoreService.PremiumRole);

                        await signInManager.RefreshSignInAsync(user);
                    }

                    Database.Context.ActivityLog.Add(new ActivityLog("Purchase", user, purchase));
                    await Database.SaveChanges();
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
                    ErrorType = e.StripeError.Type,
                    ErrorCode = e.StripeError.Code,
                    ErrorMessage = e.StripeError.Message
                };
            }

            Database.Context.ActivityLog.Add(new ActivityLog("Purchase", null, purchase));
            await Database.SaveChanges();

            return View("PurchaseError", purchase);
        }

        [AllowAnonymous]
        public IActionResult IntentionalError(string message)
        {
            throw new Exception(message ?? "This is an error");
        }
    }
}
