using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Http;
using m4dModels;
using Microsoft.Ajax.Utilities;
using Microsoft.AspNet.Identity;

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

            var user = Database.FindUser(HttpContext.Current.User.Identity.GetUserName());
            string region = "US";
            if (user != null && !string.IsNullOrWhiteSpace(user.Region))
            {
                region = user.Region;
            }
            ICollection<PurchaseLink> links = song.GetPurchaseLinks(serviceType,region);
            PurchaseLink link = links.First();

            if (link == null) 
            {
                return NotFound();
            }

            return Ok(link);
        }
    }
}
