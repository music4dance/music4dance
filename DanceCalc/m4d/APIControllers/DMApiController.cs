using Microsoft.AspNet.Identity.Owin;

using System.Web;
using System.Web.Http;

using m4dModels;
using m4d.Context;

namespace m4d.APIControllers
{
    public class DMApiController : ApiController
    {
        public DMApiController()
            : base()
        {
        }
        protected DanceMusicService Database
        {
            get
            {
                if (_database == null)
                {
                    _database = new DanceMusicService(HttpContext.Current.GetOwinContext().Get<DanceMusicContext>(), HttpContext.Current.GetOwinContext().GetUserManager<ApplicationUserManager>());
                }
                return _database;
            }
        }
        private DanceMusicService _database = null;

        protected DanceMusicContext Context { get { return Database.Context as DanceMusicContext; } }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
        }
    }
}