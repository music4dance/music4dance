using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Web.Http;
using DanceLibrary;
using m4dModels;

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
