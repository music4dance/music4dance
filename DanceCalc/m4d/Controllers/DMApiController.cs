using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Http;

using m4dModels;
using m4d.Context;

namespace m4d.Controllers
{
    public class DMApiController : ApiController
    {
        public DMApiController()
            : base()
        {
            Database = new DanceMusicService(new DanceMusicContext());
        }
        protected DanceMusicService Database { get; private set; }

        protected DanceMusicContext Context { get { return Database.Context as DanceMusicContext; } }

        protected override void Dispose(bool disposing)
        {
            Database.Dispose();
            base.Dispose(disposing);
        }
    }
}