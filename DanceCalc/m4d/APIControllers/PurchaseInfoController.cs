using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Http;
using m4dModels;

namespace m4d.APIControllers
{
    public class PurchaseInfoController : DMApiController
    {
        public IHttpActionResult GetPurchaseInfo(string id)
        {
            Guid songId = Guid.Empty;
            string serviceType = "AIX";

            if (id.Length > 0)
            {
                serviceType = new string(id[0], 1);
                Guid.TryParse(id.Substring(1), out songId);
            }

            SongDetails song = Database.FindSongDetails(songId);
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
    }
}
