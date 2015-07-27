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
        // id must be a single character service type id
        public IHttpActionResult Get(string id, string songs, bool fullLink=true)
        {
            var user = Database.FindUser(HttpContext.Current.User.Identity.GetUserName());
            string region = "US";
            if (user != null && !string.IsNullOrWhiteSpace(user.Region))
            {
                region = user.Region;
            }

            if (id.Length != 1) return NotFound();

            MusicService type = MusicService.GetService(id);
            if (type == null) return NotFound();

            ICollection<ICollection<PurchaseLink>> links = Database.GetPurchaseLinks(type.Id, Array.ConvertAll(songs.Split(','), s => new Guid(s)), region);

            if (links.Count == 0) 
            {
                return NotFound();
            }

            if (fullLink)
            {
                return Ok(DanceMusicService.ReducePurchaseLinks(links,region));
            }
            else
            {
                return Ok(DanceMusicService.PurchaseLinksToInfo(links, region));
            }
        }
    }
}
