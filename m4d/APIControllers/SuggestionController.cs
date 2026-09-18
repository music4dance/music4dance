using m4d.Services;
using m4d.Utilities;
using m4d.ViewModels;

using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.FeatureManagement;

namespace m4d.APIControllers;

[ApiController]
[Route("api/[controller]")]
public class SuggestionController(
    DanceMusicContext context, UserManager<ApplicationUser> userManager,
    ISearchServiceManager searchService, IDanceStatsManager danceStatsManager,
    IConfiguration configuration, ILogger<SuggestionController> logger,
    IAntiforgery antiforgery) : DanceMusicApiController(context, userManager, searchService, danceStatsManager, configuration, logger)
{
    private readonly IAntiforgery _antiforgery = antiforgery;

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id)
    {
        var rejected = await ValidateAntiforgery(id);
        if (rejected != null)
        {
            return rejected;
        }

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

        var rejected = await ValidateAntiforgery(q);
        if (rejected != null)
        {
            return rejected;
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

    // Validated explicitly (rather than via [ValidateAntiForgeryToken]) so a failure can be
    // logged with enough detail to diagnose it - these endpoints fire on every keystroke in
    // SuggestionEntry.vue and ArtistSuggest.vue, and are the single largest contributor to the
    // recurring, unexplained 400s tracked by Http4xxTracker (the built-in attribute fails
    // silently, no log at any level). See architecture/client-side-usage-logging.md §10.1 and
    // architecture/distributed-attack-mitigation.md's triage log. Returns null when the request
    // is good, the rejection to return otherwise.
    private async Task<IActionResult> ValidateAntiforgery(string lookup)
    {
        try
        {
            await _antiforgery.ValidateRequestAsync(HttpContext);
            return null;
        }
        catch (AntiforgeryValidationException ex)
        {
            var hasAntiforgeryCookie = Request.Cookies.Keys
                .Any(k => k.StartsWith(".AspNetCore.Antiforgery", StringComparison.Ordinal));
            var hasHeaderToken = !string.IsNullOrEmpty(Request.Headers["RequestVerificationToken"]);

            Logger.LogWarning(
                ex,
                "Antiforgery validation failed for suggestion request (IdLength={IdLength}, " +
                "IdSample='{IdSample}'). HasAntiforgeryCookie={HasAntiforgeryCookie}, " +
                "HasHeaderToken={HasHeaderToken}, IsAuthenticated={IsAuthenticated}",
                lookup?.Length ?? 0, SanitizeForLog(lookup), hasAntiforgeryCookie, hasHeaderToken,
                User?.Identity?.IsAuthenticated == true);

            return BadRequest("Antiforgery validation failed");
        }
    }

    // Bounds and strips control characters (newlines in particular, to prevent log injection)
    // from user-controlled route values before they're logged.
    private static string SanitizeForLog(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        var sample = value.Length > 40 ? value[..40] : value;
        var sanitized = new string([.. sample.Where(c => !char.IsControl(c))]);

        return value.Length > 40 ? sanitized + "..." : sanitized;
    }
}
