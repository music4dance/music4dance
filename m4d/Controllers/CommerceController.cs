using m4d.APIControllers;
using m4dModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;

namespace m4d.Controllers
{
    public class CommerceController : DanceMusicController
    {
        public const decimal AnnualSubscription = 15.0M;

        public CommerceController(DanceMusicContext context, UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager, ISearchServiceManager searchService,
            IDanceStatsManager danceStatsManager, IConfiguration configuration,
            IFileProvider fileProvider, ILogger<MusicServiceController> logger) :
            base(context, userManager, roleManager, searchService, danceStatsManager, configuration, fileProvider, logger)
        {
        }

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
}
