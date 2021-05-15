using m4d.Utilities;
using m4dModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace m4d.APIControllers
{
    // ReSharper disable once InconsistentNaming
    public class DanceMusicApiController : ControllerBase
    {
        public DanceMusicApiController(DanceMusicContext context, UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, ISearchServiceManager searchService, IDanceStatsManager danceStatsManager, IConfiguration configuration)
        {
            Database = new DanceMusicService(context, userManager, searchService, danceStatsManager);
            DanceStatsManager = danceStatsManager;
            _configuration = configuration;
        }

        protected readonly DanceMusicService Database;
        protected MusicServiceManager MusicServiceManager => _musicServiceManager ??= new MusicServiceManager(_configuration);

        private MusicServiceManager _musicServiceManager;
        private IConfiguration _configuration;

        protected DanceMusicContext Context => Database.Context;

        protected UserManager<ApplicationUser> UserManager => Database.UserManager;

        public IDanceStatsManager DanceStatsManager { get; }

        protected IActionResult JsonCamelCase(object json)
        {
            return new JsonResult(json, JsonHelpers.CamelCaseSerializer);
        }
    }
}