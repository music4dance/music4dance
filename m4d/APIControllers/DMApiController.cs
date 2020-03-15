using m4d.Utilities;
using m4dModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace m4d.APIControllers
{
    // ReSharper disable once InconsistentNaming
    public class DanceMusicApiController : ControllerBase
    {
        public DanceMusicApiController(DanceMusicContext context, UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, ISearchServiceManager searchService, IDanceStatsManager danceStatsManager)
        {
            Database = new DanceMusicService(context, userManager, searchService, danceStatsManager);
            DanceStatsManager = danceStatsManager;
        }

        protected readonly DanceMusicService Database;
        protected MusicServiceManager MusicServiceManager => _musicServiceManager ??= new MusicServiceManager();

        private MusicServiceManager _musicServiceManager;

        protected DanceMusicContext Context => Database.Context as DanceMusicContext;

        protected UserManager<ApplicationUser> UserManager => Database.UserManager;

        public IDanceStatsManager DanceStatsManager { get; }

        protected IActionResult JsonCamelCase(object json)
        {
            return new JsonResult(json, CamelCaseSerializer);
        }

        private static readonly DefaultContractResolver ContractResolver = new DefaultContractResolver
        {
            NamingStrategy = new CamelCaseNamingStrategy()
        };

        private static readonly JsonSerializerSettings CamelCaseSerializer = new JsonSerializerSettings
        {
            ContractResolver = ContractResolver
        };
    }
}