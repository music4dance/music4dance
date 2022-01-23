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
using System.Diagnostics;
using System.Threading.Tasks;

namespace m4d.Controllers
{
    [Authorize]
    public class PaymentController : CommerceController
    {
        public PaymentController(DanceMusicContext context, UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager, ISearchServiceManager searchService,
            IDanceStatsManager danceStatsManager, IConfiguration configuration) :
            base(context, userManager, roleManager, searchService, danceStatsManager, configuration)
        {
            StripeConfiguration.ApiKey = configuration["Authentication:Stripe:SecretKey"];
        }

        [HttpPost]
        public async Task<IActionResult> CreateCheckoutSession(decimal amount, PurchaseKind kind)
        {
            HelpPage = "subscriptions";

            var user = await UserManager.GetUserAsync(User);

            if (IsFraudDetected(user) || !IsCommerceEnabled())
            {
                return Redirect("/Home/Contribute");
            }

            var lineItems = new List<SessionLineItemOptions>();
            var donationAmount = amount;
            if (kind == PurchaseKind.Purchase || amount > AnnualSubscription)
            {
                lineItems.Add(new SessionLineItemOptions
                {
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        UnitAmount = (int) (AnnualSubscription * 100),
                        Currency = "usd",
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = "Subscription",
                            Description = "music4dance premium -  1 year"
                        }
                    },
                    Quantity = 1,
                });

                if (amount > AnnualSubscription)
                {
                    donationAmount -= AnnualSubscription;
                    kind = PurchaseKind.Purchase;
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

            //var customerService = new CustomerService();
            //var customer = customerService.Get(session.CustomerId);

            var user = await UserManager.GetUserAsync(User);
            if (user != null && session.PaymentStatus == "paid")
            {
                Trace.WriteLine(session.ToJson());

                var amount = ((decimal)session.AmountTotal) / 100;

                // TODO: Not sure why LineItems don't come through

                var kindString = amount < AnnualSubscription ? "Donation" : "Purchase";
                if (session.Metadata != null)
                {
                    session.Metadata.TryGetValue("kind", out kindString);
                }

                var kind = kindString.Equals("Purchase", StringComparison.OrdinalIgnoreCase) ? PurchaseKind.Purchase : PurchaseKind.Donation;
                if (kind == PurchaseKind.Purchase)
                {
                    DateTime? start = DateTime.Now;
                    if (user.SubscriptionEnd != null && user.SubscriptionEnd > start)
                    {
                        start = user.SubscriptionEnd;
                    }

                    user.SubscriptionStart ??= start;
                    user.SubscriptionEnd = start.Value.AddYears(1);
                    user.SubscriptionLevel = SubscriptionLevel.Silver;
                    user.LifetimePurchased += amount;

                    await UserManager.AddToRoleAsync(user, DanceMusicCoreService.PremiumRole);

                    await signInManager.RefreshSignInAsync(user);
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
            else
            {
                return View("Cancel");
            }
        }

        public async Task<IActionResult> Cancel(string session_id)
        {
            var sessionService = new SessionService();
            var session = sessionService.Get(session_id);

            var user = await UserManager.GetUserAsync(User);
            if (user != null)
            {
                user.FailedCardAttempts += 1;
                await UserManager.UpdateAsync(user);
            }

            Trace.WriteLine(session.ToJson());
            Database.Context.ActivityLog.Add(new ActivityLog("FailedPurchase", user, session.ToJson()));
            await Database.SaveChanges();


            return RedirectToAction("Contribute", "Home");
        }
    }
}
