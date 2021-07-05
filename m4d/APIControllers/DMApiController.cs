using m4d.Utilities;
using m4dModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace m4d.APIControllers
{
    // ReSharper disable once InconsistentNaming
    public class DanceMusicApiController : ControllerBase
    {
        protected readonly DanceMusicService Database;
        private readonly IConfiguration _configuration;

        private MusicServiceManager _musicServiceManager;

        public DanceMusicApiController(DanceMusicContext context,
            // ReSharper disable once UnusedParameter.Local
            UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager,
            ISearchServiceManager searchService, IDanceStatsManager danceStatsManager,
            IConfiguration configuration)
        {
            Database =
                new DanceMusicService(context, userManager, searchService, danceStatsManager);
            DanceStatsManager = danceStatsManager;
            _configuration = configuration;
        }

        protected MusicServiceManager MusicServiceManager =>
            _musicServiceManager ??= new MusicServiceManager(_configuration);

        protected DanceMusicContext Context => Database.Context;

        protected UserManager<ApplicationUser> UserManager => Database.UserManager;

        public IDanceStatsManager DanceStatsManager { get; }

        protected IActionResult JsonCamelCase(object json)
        {
            return new JsonResult(json, JsonHelpers.CamelCaseSerializer);
        }
    }
}
