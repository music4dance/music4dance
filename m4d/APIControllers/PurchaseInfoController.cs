using System;
using System.Collections.Generic;
using System.Web;
using System.Web.Http;
using m4d.Utilities;
using m4dModels;
using Microsoft.AspNet.Identity;

namespace m4d.APIControllers
{
    public class PurchaseInfoController : DMApiController
    {
        // id must be a single character service type id
        public IHttpActionResult Get(string id, string songs, bool fullLink=true)
        {
            var userAgent = Request.Headers.UserAgent;
            if (SpiderManager.CheckAnySpiders(userAgent.ToString()))
            {
                return NotFound();
            }

            var user = Database.FindUser(HttpContext.Current.User.Identity.GetUserName());
            var region = "US";
            if (!string.IsNullOrWhiteSpace(user?.Region))
            {
                region = user.Region;
            }

            if (id.Length != 1) return NotFound();

            var type = MusicService.GetService(id);
            if (type == null) return NotFound();

            var links = Database.GetPurchaseLinks(type.Id, Array.ConvertAll(songs.Split(','), s => new Guid(s)), region);

            if (links.Count == 0) 
            {
                return NotFound();
            }

            if (fullLink)
            {
                return Ok(DanceMusicService.ReducePurchaseLinks(links, region));
            }
            return Ok(DanceMusicService.PurchaseLinksToInfo(links, region));
        }
    }
}
