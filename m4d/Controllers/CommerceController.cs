using m4d.Services;

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.FileProviders;
using Microsoft.FeatureManagement;

namespace m4d.Controllers;


public class CommerceController(
    DanceMusicContext context, UserManager<ApplicationUser> userManager,
    ISearchServiceManager searchService, IDanceStatsManager danceStatsManager,
    IConfiguration configuration, IFileProvider fileProvider, IBackgroundTaskQueue backroundTaskQueue,
    IFeatureManagerSnapshot featureManager, ILogger logger) : DanceMusicController(context, userManager, searchService, danceStatsManager, configuration,
        fileProvider, backroundTaskQueue, featureManager, logger)
{
    public static decimal AnnualSubscription = SubscriptionLevelDescription.SubscriptionLevels.Last().Price;

    protected bool IsFraudDetected(ApplicationUser user)
    {
        return user != null && user.FailedCardAttempts >= GetCardFailLimit();
    }

    protected int GetCardFailLimit()
    {
        var ecString = Configuration["Configuration:Commerce:FailLimit"];

        if (!int.TryParse(ecString, out var failLimit))
        {
            failLimit = 5;
        }
        return failLimit;
    }

    protected bool IsCommerceEnabled()
    {
        var ecString = Configuration["Configuration:Commerce:Enabled"];
        if (!bool.TryParse(ecString, out var enableCommerce))
        {
            enableCommerce = true;
        }
        return enableCommerce;
    }
}
