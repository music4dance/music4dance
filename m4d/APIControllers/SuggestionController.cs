using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

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
        // Validated explicitly (rather than via [ValidateAntiForgeryToken]) so a failure can be
        // logged with enough detail to diagnose it - this endpoint fires on every keystroke in
        // SuggestionEntry.vue and is the single largest contributor to the recurring, unexplained
        // 400s tracked by Http4xxTracker (the built-in attribute fails silently, no log at any
        // level). See architecture/client-side-usage-logging.md §10.1 and
        // architecture/distributed-attack-mitigation.md's triage log.
        try
        {
            await _antiforgery.ValidateRequestAsync(HttpContext);
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
                id?.Length ?? 0, SanitizeForLog(id), hasAntiforgeryCookie, hasHeaderToken,
                User?.Identity?.IsAuthenticated == true);

            return BadRequest("Antiforgery validation failed");
        }

        var suggestions = await SongIndex.AzureSuggestions(id);
        if (suggestions != null)
        {
            return Ok(suggestions);
        }

        return NotFound();
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
