using m4d.Context;
using m4dModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace m4d.Controllers
{
    public class PurchaseInfoController : ApiController
    {
        private DanceMusicContext _db = new DanceMusicContext();
        public IHttpActionResult GetPurchaseInfo(string id)
        {
            int songId = 0;
            string serviceType = "AIX";

            if (id.Length > 0)
            {
                serviceType = new string(id[0], 1);
                int.TryParse(id.Substring(1), out songId);
            }

            SongDetails song = _db.FindSongDetails(songId);
            if (song == null)
            {
                return NotFound();
            }

            ICollection<PurchaseLink> links = song.GetPurchaseLinks(serviceType);
            PurchaseLink link = links.First();

            if (link == null) 
            {
                return NotFound();
            }

            return Ok(link);
        }
        protected override void Dispose(bool disposing)
        {
            _db.Dispose();
            base.Dispose(disposing);
        }
    }
}
