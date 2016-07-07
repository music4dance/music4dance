using System;
using System.Web;
using System.Web.Http;
using m4dModels;
using Microsoft.AspNet.Identity;

namespace m4d.APIControllers
{
    public class UpdateRatingsController : DMApiController
    {
        public IHttpActionResult Update(Guid id, [FromBody] JTags tags)
        {
            var uts = tags.ToUserTags();

            var user = Database.FindUser(HttpContext.Current.User.Identity.GetUserName());
            if (Database.EditTags(user, id, uts))
            {
                IndexUpdater.Enqueue();
            }

            return Ok(new{changed=1});
        }
    }
}