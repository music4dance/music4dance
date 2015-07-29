using System;
using System.Web.Http;

namespace m4d.APIControllers
{
    public class TagSuggestionController : DMApiController
    {
        public IHttpActionResult GetTagSuggestions(Guid? user = null, char? targetType = null, string tagType = null,
            int count = int.MaxValue, bool normalized = false)
        {
            bool test = false;
            try
            {
                var tags = Database.GetTagSuggestions(user, targetType, tagType, count, normalized);
                if (test)
                    return NotFound();
                else
                    return Ok(tags);
            }
            catch (Exception)
            {
                return NotFound();
            }                        
        }
    }
}
