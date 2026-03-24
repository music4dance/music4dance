// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using m4d.Security;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.WebUtilities;

namespace m4d.Areas.Identity.Pages.Account;

public abstract class LoginModelBase : PageModel
{
    private readonly IUrlHelperFactory _urlHelperFactory;
    private readonly ILogger _logger;
    private readonly AuthenticationTracker _authTracker;
    // Changed from '\u001a' to '~' to avoid corrupt marker byte issues
    private readonly string _subStr = "~";

    protected LoginModelBase(IUrlHelperFactory urlHelperFactory, ILogger logger, AuthenticationTracker authTracker)
    {
        _urlHelperFactory = urlHelperFactory;
        _logger = logger;
        _authTracker = authTracker;
    }

    /// <summary>
    /// Cleans the return URL by replacing the substitute character and validating it's local
    /// </summary>
    /// <param name="returnUrl">The return URL to clean and validate</param>
    /// <returns>A cleaned and validated local URL, or home page if invalid</returns>
    protected string CleanUrl(string returnUrl)
    {
        returnUrl ??= Url.Content("~/");
        returnUrl = returnUrl.Replace(_subStr, "-");

        if (!IsLocalUrl(returnUrl)) return Url.Content("~/");

        // Never redirect back to auth action pages — they should never be a final destination
        string[] authPaths = ["/identity/account/login", "/identity/account/register", "/identity/account/logout"];
        if (authPaths.Any(p => returnUrl.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
        {
            // Use the framework query parser to safely extract a nested returnUrl.
            // This handles both ?returnUrl= and &returnUrl=, and won't throw on malformed encoding.
            var qIndex = returnUrl.IndexOf('?');
            if (qIndex >= 0)
            {
                var query = QueryHelpers.ParseQuery(returnUrl[qIndex..]);
                if (query.TryGetValue("returnUrl", out var nested))
                {
                    var nestedUrl = nested.ToString();
                    if (!authPaths.Any(p => nestedUrl.StartsWith(p, StringComparison.OrdinalIgnoreCase)) && IsLocalUrl(nestedUrl))
                        return nestedUrl;
                }
            }
            return Url.Content("~/");
        }

        return returnUrl;
    }

    /// <summary>
    /// Validates that the URL is local using the same logic as LocalRedirectResultExecutor
    /// </summary>
    /// <param name="url">The URL to validate</param>
    /// <returns>True if the URL is local, false otherwise</returns>
    protected bool IsLocalUrl(string url)
    {
        var urlHelper = _urlHelperFactory.GetUrlHelper(PageContext);
        var isLocal = urlHelper.IsLocalUrl(url);

        if (!isLocal)
        {
            _logger.LogWarning("Non-local return URL detected: {ReturnUrl}", url);

            // Track this suspicious activity
            var clientIp = HttpContext?.Connection?.RemoteIpAddress?.ToString() ?? "unknown";
            _authTracker?.RecordSuspiciousActivity(clientIp, "Non-local returnUrl");
        }

        return isLocal;
    }
}
