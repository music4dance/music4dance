using m4d.Services;
using m4d.Utilities;
using m4d.ViewModels;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.FeatureManagement;

namespace m4d.APIControllers;

[ApiController]
[ValidateAntiForgeryToken]
[Route("api/[controller]")]
public class SuggestionController(
    DanceMusicContext context, UserManager<ApplicationUser> userManager,
    ISearchServiceManager searchService, IDanceStatsManager danceStatsManager,
    IConfiguration configuration, ILogger<SuggestionController> logger) : DanceMusicApiController(context, userManager, searchService, danceStatsManager, configuration, logger)
{
    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id)
    {
        var suggestions = await SongIndex.AzureSuggestions(id);
        if (suggestions != null)
        {
            return Ok(suggestions);
        }

        return NotFound();
    }

    /// <summary>
    /// Type-ahead for the artist index search box at /song/artists. Answers from the in-memory
    /// <see cref="ArtistIndex"/> snapshot rather than an Azure suggester, so suggestions are exactly
    /// the entries that page can show - see architecture/individual-artists.md §9.4.
    /// </summary>
    /// <remarks>
    /// The query is a query-string parameter, not a path segment as on <see cref="Get"/>: artist
    /// names contain slashes and hashes, and those don't survive a path segment intact.
    /// </remarks>
    [HttpGet("artist")]
    public async Task<IActionResult> Artist(
        [FromServices] ArtistIndexCache artistIndexCache,
        [FromServices] IFeatureManagerSnapshot featureManager,
        string q, bool all = false)
    {
        if (!await featureManager.IsEnabledAsync(FeatureFlags.ArtistIndex))
        {
            return NotFound();
        }

        var query = (q ?? string.Empty).Trim();
        if (query.Length is < MinimumSuggestionQuery or > MaximumSuggestionQuery)
        {
            return JsonCamelCase(ArtistSuggestions.Empty(query));
        }

        // Never waits on a build: an unbuilt snapshot means no suggestions, not a stalled keystroke.
        var index = artistIndexCache.Current(Database);

        // JsonCamelCase, not Ok: the default serializer here keeps property names as written, and
        // the client reads camelCase like it does everywhere else.
        return JsonCamelCase(index == null
            ? ArtistSuggestions.Empty(query)
            : new ArtistSuggestions(query, index.Search(query, all ? 1 : 2, SuggestionCount)));
    }

    // One character would match most of the catalog and sort it for nothing.
    private const int MinimumSuggestionQuery = 2;
    private const int MaximumSuggestionQuery = 100;
    private const int SuggestionCount = 10;
}
