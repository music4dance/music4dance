using System;
using System.Web.Http;

namespace m4d.APIControllers
{
    public class TagSuggestionController : DMApiController
    {
        public IHttpActionResult GetTagSuggestions(Guid? user = null, char? targetType = null, string tagType = null,
            int count = int.MaxValue, bool normalized = false)
        {
            var tags = Database.GetTagSuggestions(user, targetType, tagType, count, normalized);

            return Ok(tags);
        }
    }
}
