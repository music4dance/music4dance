using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using m4dModels;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace m4d.APIControllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SearchController : DanceMusicApiController
    {
        private readonly ISearchServiceManager _searchServiceManager;
        private readonly IServer _server;

        public SearchController(DanceMusicContext context, UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager, ISearchServiceManager searchService,
            IDanceStatsManager danceStatsManager, IConfiguration configuration, IServer server, ILogger<SearchController> logger) :
            base(context, userManager, roleManager, searchService, danceStatsManager, configuration, logger)
        {
            _searchServiceManager = new SearchServiceManager(configuration);
            _server = server;
        }

        [HttpGet]
        public async Task<IActionResult> Get(string search)
        {
            Logger.LogInformation($"Enter Search: {search}, User = {User.Identity?.Name}");

            if (string.IsNullOrWhiteSpace(search))
            {
                return StatusCode((int)HttpStatusCode.BadRequest);
            }

            var pages = await Search(search);

            return JsonCamelCase(pages);
        }

        private async Task<List<PageSearch>> Search(string search)
        {
            var parameters = new SearchOptions
            {
                QueryType = SearchQueryType.Simple,
                Size = int.MaxValue,
            };
            parameters.Select.AddRange(new[] { "Url", "Title", "Description" });

            var client = _searchServiceManager.GetInfo("freep").AdminClient;
            var response = await client.SearchAsync<PageSearch>(search, parameters);

            return response.Value.GetResults().Select(r => r.Document)
                .Select(p => p.GetDecoded()).ToList();
        }

        //private string GetBaseAddress()
        //{
        //    var features = _server.Features;
        //    var addresses = features.Get<IServerAddressesFeature>();
        //    var address = addresses?.Addresses.FirstOrDefault();
        //    return address ?? "https://www.music4dance.net/"
        //}
    }
}
