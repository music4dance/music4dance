using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;

namespace m4d.APIControllers;

/// <summary>
/// Hands an antiforgery request token to pages that don't carry one. Anonymous pages leave the
/// token out of their HTML so they set no cookie and can be cached, and fetch one here the first
/// time they need it (ClientApp/src/helpers/AntiforgeryToken.ts). Generating the token also sets
/// the antiforgery cookie, exactly as rendering it into a page does.
///
/// This doesn't weaken CSRF protection: another site can make a browser send this GET, but the
/// same-origin policy stops it from reading the token, and the cookie alone validates nothing.
/// That holds only while the endpoint:
/// - returns JSON, never script (a script include would expose the token to any site);
/// - has no CORS policy that lets another origin read it;
/// - is never cached (no-store, and excluded from any CDN), or everyone would share one pair;
/// - does nothing besides issuing the token.
/// See architecture/plans/bot-load-and-public-page-rendering.md (A3).
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AntiforgeryController(IAntiforgery antiforgery) : ControllerBase
{
    [HttpGet("token")]
    [Produces("application/json")]
    public IActionResult Token()
    {
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        Response.Headers.CacheControl = "no-store";

        // Lower-case on purpose: API JSON keeps C# property casing (DefaultContractResolver),
        // and the client reads response.data.token.
        return Ok(new { token = tokens.RequestToken });
    }
}
