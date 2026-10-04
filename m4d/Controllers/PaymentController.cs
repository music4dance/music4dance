using m4d.Services;
using m4d.Services.ServiceHealth;
using m4d.Utilities;
using m4d.ViewModels;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
        ILogger<PaymentController> logger, IreCAPTCHASiteVerifyV2 siteVerify,
        ServiceHealthManager serviceHealth) :
        base(context, userManager, searchService, danceStatsManager, configuration, fileProvider, backroundTaskQueue, featureManager, logger, serviceHealth)
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
            // Records who started the checkout; Success only credits this account.
            ClientReferenceId = user?.Id,
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
        var host = StripeReturnHost(Request.Host, Environment.GetEnvironmentVariable("WEBSITE_HOSTNAME"));
        return Url.ActionLink(action, "payment", new { session_id = "{CHECKOUT_SESSION_ID}" }, "https", host)
            .Replace("%7B", "{").Replace("%7D", "}");
    }

    internal const string DefaultReturnHost = "www.music4dance.net";

    /// <summary>
    /// The host Stripe sends the buyer back to. The request's Host header is only trusted when it's
    /// one of ours (music4dance.net or a subdomain, this App Service's own *.azurewebsites.net name
    /// from WEBSITE_HOSTNAME, or localhost); anything else gets the production host, so a forged
    /// Host header can't send someone to another site after they pay.
    /// </summary>
    internal static string StripeReturnHost(HostString requestHost, string appServiceHost)
    {
        var name = requestHost.Host;
        if (string.IsNullOrEmpty(name))
        {
            return DefaultReturnHost;
        }

        var allowed =
            name.Equals("music4dance.net", StringComparison.OrdinalIgnoreCase) ||
            name.EndsWith(".music4dance.net", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("127.0.0.1", StringComparison.Ordinal) ||
            (!string.IsNullOrEmpty(appServiceHost) &&
                name.Equals(appServiceHost, StringComparison.OrdinalIgnoreCase));

        return allowed ? requestHost.ToString() : DefaultReturnHost;
    }

    internal enum SessionAccess
    {
        Allowed,
        SignInRequired,
        Denied
    }

    /// <summary>
    /// Decides whether the current visitor may complete a checkout session. A session started by a
    /// signed-in user (ClientReferenceId set) may only be completed by that user, so a sign-in is
    /// required if nobody is signed in. A session started anonymously (an anonymous donation) may
    /// only be completed anonymously, since it never credits an account.
    /// </summary>
    internal static SessionAccess CheckSessionAccess(string clientReferenceId, string userId)
    {
        if (string.IsNullOrEmpty(clientReferenceId))
        {
            return userId == null ? SessionAccess.Allowed : SessionAccess.Denied;
        }

        if (userId == null)
        {
            return SessionAccess.SignInRequired;
        }

        return string.Equals(clientReferenceId, userId, StringComparison.Ordinal)
            ? SessionAccess.Allowed
            : SessionAccess.Denied;
    }

    /// <summary>
    /// Records that a checkout session has been credited. Returns null if it was already recorded
    /// (a reload, a second tab or a replayed success URL), including when a concurrent request
    /// recorded it first: the session id is the table's primary key, so the database decides.
    /// </summary>
    internal static async Task<CheckoutSession> TryClaimSession(
        DanceMusicContext context, string sessionId, string userId)
    {
        if (await context.CheckoutSessions.AnyAsync(c => c.SessionId == sessionId))
        {
            return null;
        }

        var claim = new CheckoutSession
        {
            SessionId = sessionId,
            ApplicationUserId = userId,
            Processed = DateTimeOffset.Now
        };
        var entry = context.CheckoutSessions.Add(claim);
        try
        {
            _ = await context.SaveChangesAsync();
            return claim;
        }
        catch (DbUpdateException)
        {
            entry.State = EntityState.Detached;
            return null;
        }
    }

    /// <summary>
    /// Forgets a claimed session after crediting it failed, so a reload can retry.
    /// </summary>
    internal static async Task ReleaseSession(DanceMusicContext context, CheckoutSession claim)
    {
        _ = context.CheckoutSessions.Remove(claim);
        _ = await context.SaveChangesAsync();
    }

    public async Task<IActionResult> Success([FromServices] SignInManager<ApplicationUser> signInManager, string session_id)
    {
        HelpPage = "subscriptions";
        ViewBag.HideAds = true;
        ViewBag.NoWarnings = true;

        var sessionService = new SessionService();
        var session = sessionService.Get(session_id);

        var user = await UserManager.GetUserAsync(User);
        switch (CheckSessionAccess(session.ClientReferenceId, user?.Id))
        {
            case SessionAccess.SignInRequired:
                return Challenge();
            case SessionAccess.Denied:
                Logger.LogWarning(
                    "Checkout session {SessionId} opened by user {UserId}, but it belongs to {Owner}",
                    session_id, user?.Id, session.ClientReferenceId ?? "an anonymous checkout");
                return Forbid();
        }

        if (session.PaymentStatus == "paid")
        {
            Logger.LogInformation(session.ToJson());

            // A session that's already recorded is likely a reload or the back button - just
            //  re-show the success page without crediting anything again
            var claim = await TryClaimSession(Database.Context, session.Id, user?.Id);
            var duplicate = claim == null;
            if (duplicate)
            {
                Logger.LogInformation("Duplicate session_id: {SessionId}", session.Id);
            }

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
                    try
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

                            _ = await UserManager.AddToRoleAsync(user, DanceMusicCoreService.PremiumRole);

                            await signInManager.RefreshSignInAsync(user);
                        }
                        else
                        {
                            user.LifetimePurchased += amount;
                        }
                    }
                    catch (Exception e)
                    {
                        Logger.LogError(e, "Crediting checkout session {SessionId} to user {UserId} failed",
                            session.Id, user.Id);
                        await ReleaseSession(Database.Context, claim);
                        throw;
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
                _ = Database.Context.ActivityLog.Add(new ActivityLog("Purchase", user, purchase));
                _ = await Database.SaveChanges();
            }

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
            _ = await UserManager.UpdateAsync(user);
        }

        Logger.LogInformation(session.ToJson());
        if (await FeatureManager.IsEnabledAsync(FeatureFlags.ActivityLogging))
        {
            _ = Database.Context.ActivityLog.Add(new ActivityLog("FailedPurchase", user, session.ToJson()));
            _ = await Database.SaveChanges();
        }

        return RedirectToAction("Contribute", "Home");
    }
}
