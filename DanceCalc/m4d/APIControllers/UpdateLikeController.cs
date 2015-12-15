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
        public IHttpActionResult Update(Guid id, bool? like)
        {
            var user = Database.FindUser(HttpContext.Current.User.Identity.GetUserName());
            Database.EditLike(user, id, like);

            return Ok(new { changed = 1 });
        }

        //public IHttpActionResult Update(Guid id, [FromBody] bool? like)
        //{
        //    var user = Database.FindUser(HttpContext.Current.User.Identity.GetUserName());
        //    Database.EditLike(user, id, like);

        //    return Ok(new { changed = 1 });
        //}
    }
}