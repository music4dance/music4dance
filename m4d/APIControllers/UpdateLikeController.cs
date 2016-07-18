using System;
using System.Web;
using System.Web.Http;
using m4dModels;
using Microsoft.AspNet.Identity;

namespace m4d.APIControllers
{
    public class UpdateLikeController : DMApiController
    {
        [HttpGet]
        public IHttpActionResult Update(Guid id, bool? like, string dance=null)
        {
            var user = Database.FindUser(HttpContext.Current.User.Identity.GetUserName());

            var changed = Database.EditLike(user, id, like, string.IsNullOrWhiteSpace(dance) || dance == "null" ? null : dance);

            return Ok(new { changed = changed ? 1 : 0 });
        }
    }
}