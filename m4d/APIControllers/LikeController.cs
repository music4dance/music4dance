using System;
using System.Web;
using System.Web.Http;
using m4dModels;
using Microsoft.AspNet.Identity;

namespace m4d.APIControllers
{
    public class LikeModel
    {
        public string Dance { get; set; }
        public bool? Like { get; set; }
    }

    public class LikeController : DMApiController
    {
        [HttpGet]
        public IHttpActionResult Get(Guid id, string dance=null)
        {
            var user = Database.FindUser(HttpContext.Current.User.Identity.GetUserName());

            var like = Database.GetLike(user, id, string.IsNullOrWhiteSpace(dance) || dance == "null" ? null : dance);

            return Ok(new { like = like });
        }

        [HttpPut]
        public IHttpActionResult Put(Guid id, [FromBody] LikeModel model)
        {
            var user = Database.FindUser(HttpContext.Current.User.Identity.GetUserName());

            var changed = Database.EditLike(user, id, model.Like, string.IsNullOrWhiteSpace(model.Dance) || model.Dance == "null" ? null : model.Dance);

            return Ok(new { changed = changed ? 1 : 0 });
        }

        //[HttpPut]
        //public IHttpActionResult Put(Guid id, bool? like, string dance = null)
        //{
        //    var user = Database.FindUser(HttpContext.Current.User.Identity.GetUserName());

        //    var changed = Database.EditLike(user, id, like, string.IsNullOrWhiteSpace(dance) || dance == "null" ? null : dance);

        //    return Ok(new { changed = changed ? 1 : 0 });
        //}
    }
}