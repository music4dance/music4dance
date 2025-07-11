﻿using m4d.Services;
using m4d.Utilities;
using m4d.ViewModels;

using m4dModels;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using Microsoft.FeatureManagement;

using Owl.reCAPTCHA;
using Owl.reCAPTCHA.v2;

using Stripe;
using Stripe.Checkout;

namespace m4d.Controllers;

public class PaymentController : CommerceController
{
    private readonly IreCAPTCHASiteVerifyV2 _siteVerify;
    private async Task<bool> UseCaptcha() =>
        await FeatureManager.IsEnabledAsync(FeatureFlags.Captcha);

    public PaymentController(
        DanceMusicContext context, UserManager<ApplicationUser> userManager,
        ISearchServiceManager searchService, IDanceStatsManager danceStatsManager,
        IConfiguration configuration, IFileProvider fileProvider,
        IBackgroundTaskQueue backroundTaskQueue, IFeatureManagerSnapshot featureManager,
        ILogger<PaymentController> logger, IreCAPTCHASiteVerifyV2 siteVerify) :
        base(context, userManager, searchService, danceStatsManager, configuration, fileProvider, backroundTaskQueue, featureManager, logger)
    {
        var test = GlobalState.UseTestKeys ? "Test" : "";
        StripeConfiguration.ApiKey = configuration[$"Authentication:Stripe{test}:SecretKey"];
        _siteVerify = siteVerify;
    }

    [HttpPost]
    public async Task<IActionResult> CreateCheckoutSession(decimal amount, PurchaseKind kind, string recaptchaToken = null)
    {
        HelpPage = "subscriptions";
        ViewBag.HideAds = true;
        ViewBag.NoWarnings = true;

        var user = await UserManager.GetUserAsync(User);
        if (user == null)
        {
            if (kind == PurchaseKind.Purchase)
            {
                var message = "CreateCheckoutSession purchase called by anonymous user";
                Logger.LogError(message);
                throw new Exception(message);
            }

            // Otherwise, this is an anonymous donation so need to verify recaptcha
            if (await UseCaptcha())
            {
                var response = await _siteVerify.Verify(
                    new reCAPTCHASiteVerifyRequest
                    {
                        Response = recaptchaToken,
                        RemoteIp = HttpContext.Connection.RemoteIpAddress.ToString()
                    });

                if (!response.Success)
                {
                    return RedirectToAction("Contribute", "Home", new { recaptchaFailed = true });
                }
            }
        }
        else if (IsFraudDetected(user) || !IsCommerceEnabled())
        {
            return Redirect("/Home/Contribute");
        }

        var lineItems = new List<SessionLineItemOptions>();
        var donationAmount = amount;
        if (kind == PurchaseKind.Purchase || (amount > AnnualSubscription && user != null))
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
            CustomerEmail = user?.Email,
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

        Response.Headers.Append("Location", session.Url);
        return new StatusCodeResult(303);
    }

    private string CreateStripeUrl(string action)
    {
        // TODO: Whitelist host
        return Url.ActionLink(action, "payment", new { session_id = "{CHECKOUT_SESSION_ID}" }, "https", Request.Host.ToString())
            .Replace("%7B", "{").Replace("%7D", "}");
    }

    private static readonly HashSet<string> _completedSessions = [];

    public async Task<IActionResult> Success([FromServices] SignInManager<ApplicationUser> signInManager, string session_id)
    {
        HelpPage = "subscriptions";
        ViewBag.HideAds = true;
        ViewBag.NoWarnings = true;

        // This is likely due to a reload/back button - just re-show the success page
        //  without doing anything else
        var duplicate = _completedSessions.Contains(session_id);
        if (duplicate)
        {
            Logger.LogInformation($"Duplicate session_id: {session_id}");
        }

        var sessionService = new SessionService();
        var session = sessionService.Get(session_id);

        var user = await UserManager.GetUserAsync(User);
        if (session.PaymentStatus == "paid")
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

            var kind = amount < AnnualSubscription || user == null ? PurchaseKind.Donation : PurchaseKind.Purchase;
            string email = null;
            if (user != null)
            {
                if (!duplicate)
                {
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
                }
            }
            else
            {
                email = session.CustomerDetails?.Email;
            }

            var purchase = new PurchaseModel
            {
                Kind = kind,
                Amount = amount,
                User = user?.UserName ?? "Anonymous Donation",
                Email = email ?? user?.Email,
                Confirmation = session.Id
            };

            if (!duplicate && await FeatureManager.IsEnabledAsync(FeatureFlags.ActivityLogging))
            {
                Database.Context.ActivityLog.Add(new ActivityLog("Purchase", user, purchase));
                await Database.SaveChanges();
            }

            _completedSessions.Add(session_id);
            return View(purchase);
        }

        return View("Cancel");
    }

    public async Task<IActionResult> Cancel(string session_id)
    {
        ViewBag.HideAds = true;

        var sessionService = new SessionService();
        var session = await sessionService.GetAsync(session_id);

        var user = await UserManager.GetUserAsync(User);
        if (user != null)
        {
            user.FailedCardAttempts += 1;
            await UserManager.UpdateAsync(user);
        }

        Logger.LogInformation(session.ToJson());
        if (await FeatureManager.IsEnabledAsync(FeatureFlags.ActivityLogging))
        {
            Database.Context.ActivityLog.Add(new ActivityLog("FailedPurchase", user, session.ToJson()));
            await Database.SaveChanges();
        }        

        return RedirectToAction("Contribute", "Home");
    }
}
