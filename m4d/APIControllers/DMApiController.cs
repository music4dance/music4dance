using Microsoft.AspNet.Identity.Owin;

using System.Web;
using System.Web.Http;

using m4dModels;
using m4d.Context;
using m4d.Controllers;
using m4d.Utilities;

namespace m4d.APIControllers
{
    // ReSharper disable once InconsistentNaming
    public class DMApiController : ApiController
    {
        protected DanceMusicService Database => _database ??
                                                (_database =
                                                    new DanceMusicService(HttpContext.Current.GetOwinContext().Get<DanceMusicContext>(),
                                                        HttpContext.Current.GetOwinContext().GetUserManager<ApplicationUserManager>()));

        private DanceMusicService _database;
        protected MusicServiceManager MusicServiceManager => _musicServiceManager ?? (_musicServiceManager = new MusicServiceManager());

        private MusicServiceManager _musicServiceManager;

        protected DanceMusicContext Context => Database.Context as DanceMusicContext;

        protected void DetachDatabase()
        {
            _database = null;
        }
    }
}