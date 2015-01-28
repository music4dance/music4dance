using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.Web;
using System.Web.Http;
using System.Web.Mvc;
using m4dModels;
using Microsoft.AspNet.Identity;

namespace m4d.APIControllers
{
    [DataContract]
    public class JTag
    {
        [DataMember]
        public string Id { get; set; }
        [DataMember]
        public string Tags { get; set; }
    }
    [DataContract]
    public class JTags
    {
        [DataMember]
        public IEnumerable<JTag> Tags { get; set; }
    };

    public class UpdateRatingsController : DMApiController
    {
        public IHttpActionResult Update(Guid id, [FromBody] JTags tags)
        {
            var uts = tags.Tags.Select(jtag => new UserTag {Id = jtag.Id ?? string.Empty, Tags = new TagList(jtag.Tags)}).ToList();

            var user = Database.FindUser(HttpContext.Current.User.Identity.GetUserName());
            Database.EditTags(user, id, uts);

            return Ok(new{changed=1});
        }
    }
}