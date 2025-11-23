using Azure.Search.Documents;
using Azure.Search.Documents.Models;

using m4dModels;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Azure;

using System.Net;

namespace m4d.APIControllers;

[ApiController]
[ValidateAntiForgeryToken]
[Route("api/[controller]")]
public class SearchController(
    DanceMusicContext context, UserManager<ApplicationUser> userManager,
    ISearchServiceManager searchService, IDanceStatsManager danceStatsManager,
    IConfiguration configuration, ILogger<SearchController> logger,
    IAzureClientFactory<SearchClient> searchFactory) : DanceMusicApiController(context, userManager, searchService, danceStatsManager, configuration, logger)
{
    private readonly SearchClient _client = searchFactory.CreateClient("PageIndex");

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
        parameters.Select.AddRange(["Url", "Title", "Description"]);

        var response = await _client.SearchAsync<PageSearch>(search, parameters);

        return [.. response.Value.GetResults().Select(r => r.Document).Select(p => p.GetDecoded())];
    }
}
