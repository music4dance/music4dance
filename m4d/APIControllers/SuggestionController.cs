using System.Web.Http;

namespace m4d.APIControllers
{
    public class SuggestionController : DMApiController
    {
        public IHttpActionResult Get(string id)
        {
            var suggestions = Database.AzureSuggestions(id);
            if (suggestions != null)
            {
                return Ok(suggestions);
            }
            return NotFound();
        }
    }
}
