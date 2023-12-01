using m4d.ViewModels;
using m4dModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Stripe;
using Stripe.Checkout;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using m4d.APIControllers;
using Microsoft.Extensions.FileProviders;
using m4d.Utilities;

namespace m4d.Controllers
{
    [Authorize]
    public class PaymentController : CommerceController
    {
        public PaymentController(DanceMusicContext context, UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager, ISearchServiceManager searchService,
            IDanceStatsManager danceStatsManager, IConfiguration configuration, IFileProvider fileProvider, ILogger<MusicServiceController> logger) :
            base(context, userManager, roleManager, searchService, danceStatsManager, configuration, fileProvider, logger)
        {
            var test = GlobalState.UseTestKeys ? "Test" : "";
            StripeConfiguration.ApiKey = configuration[$"Authentication:Stripe{test}:SecretKey"];
        }

        [HttpPost]
        public async Task<IActionResult> CreateCheckoutSession(decimal amount, PurchaseKind kind)
        {
            HelpPage = "subscriptions";

            var user = await UserManager.GetUserAsync(User);
            if (user == null)
            {
                var message = "CreateCheckoutSession called by anonymous user";
                Logger.LogError(message);
                throw new Exception(message);
            }

            if (IsFraudDetected(user) || !IsCommerceEnabled())
            {
                return Redirect("/Home/Contribute");
            }

            var lineItems = new List<SessionLineItemOptions>();
            var donationAmount = amount;
            if (kind == PurchaseKind.Purchase || amount > AnnualSubscription)
            {
                var level = SubscriptionLevelDescription.FindSubscriptionLevel(amount);
                if (level != null)
                {
                    lineItems.Add(new SessionLineItemOptions
                    {
                        PriceData = new SessionLineItemPriceDataOptions
                        {
                            UnitAmount = (int)(level.Price * 100),
                            Currency = "usd",
                            ProductData = new SessionLineItemPriceDataProductDataOptions
                            {
                                Name = $"{level.Name} Subscription",
                                Description = "music4dance premium -  1 year"
                            }
                        },
                        Quantity = 1,
                    });

                    donationAmount -= level.Price;
                }
            }

            if (donationAmount > 0)
            {
                lineItems.Add(new SessionLineItemOptions
                {
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        UnitAmount = (int)(donationAmount * 100),
                        Currency = "usd",
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = "Donation"
                        }
                    },
                    Quantity = 1,
                });
            }


            var options = new SessionCreateOptions
            {
                CustomerEmail = user.Email,
                Metadata = new Dictionary<string, string>
                {
                    { "kind", kind==PurchaseKind.Purchase ? "Purchase" : "Donation" }
                },
                LineItems = lineItems,
                Mode = "payment",
                SuccessUrl = CreateStripeUrl("success"),
                CancelUrl = CreateStripeUrl("cancel")
            };

            var service = new SessionService();
            var session = service.Create(options);

            Response.Headers.Add("Location", session.Url);
            return new StatusCodeResult(303);
        }

        private string CreateStripeUrl(string action)
        {
            // TODO: Whitelist host
            return Url.ActionLink(action, "payment", new { session_id = "{CHECKOUT_SESSION_ID}" }, "https", Request.Host.ToString())
                .Replace("%7B", "{").Replace("%7D", "}");
        }

        public async Task<IActionResult> Success([FromServices] SignInManager<ApplicationUser> signInManager, string session_id)
        {
            HelpPage = "subscriptions";

            var sessionService = new SessionService();
            var session = sessionService.Get(session_id);

            var user = await UserManager.GetUserAsync(User);
            if (user != null && session.PaymentStatus == "paid")
            {
                Logger.LogInformation(session.ToJson());

                var amount = ((decimal)(session.AmountTotal ?? 0)) / 100;

                // TODO: Not sure why LineItems don't come through

                //var kindString = amount < AnnualSubscription ? "Donation" : "Purchase";
                //if (session.Metadata != null)
                //{
                //    session.Metadata.TryGetValue("kind", out kindString);
                //}

                //var kind = kindString.Equals("Purchase", StringComparison.OrdinalIgnoreCase) ? PurchaseKind.Purchase : PurchaseKind.Donation;

                var kind = amount < AnnualSubscription ? PurchaseKind.Donation : PurchaseKind.Purchase;
                if (kind == PurchaseKind.Purchase)
                {
                    DateTime? start = DateTime.Now;
                    if (user.SubscriptionEnd != null && user.SubscriptionEnd > start)
                    {
                        start = user.SubscriptionEnd;
                    }

                    user.SubscriptionStart ??= start;
                    user.SubscriptionEnd = start.Value.AddYears(1);
                    user.SubscriptionLevel = SubscriptionLevelDescription.FindSubscriptionLevel(amount).Level;
                    user.LifetimePurchased += amount;

                    await UserManager.AddToRoleAsync(user, DanceMusicCoreService.PremiumRole);

                    await signInManager.RefreshSignInAsync(user);
                }
                else
                {
                    user.LifetimePurchased += amount;
                }


                var purchase = new PurchaseModel
                {
                    Kind = kind,
                    Amount = amount,
                    User = user.UserName,
                    Confirmation = session.Id
                };

                Database.Context.ActivityLog.Add(new ActivityLog("Purchase", user, purchase));
                await Database.SaveChanges();

                return View(purchase);
            }

            return View("Cancel");
        }

        public async Task<IActionResult> Cancel(string session_id)
        {
            var sessionService = new SessionService();
            var session = await sessionService.GetAsync(session_id);

            var user = await UserManager.GetUserAsync(User);
            if (user != null)
            {
                user.FailedCardAttempts += 1;
                await UserManager.UpdateAsync(user);
            }

            Logger.LogInformation(session.ToJson());
            Database.Context.ActivityLog.Add(new ActivityLog("FailedPurchase", user, session.ToJson()));
            await Database.SaveChanges();


            return RedirectToAction("Contribute", "Home");
        }
    }
}
