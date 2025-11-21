// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Routing;

namespace m4d.Areas.Identity.Pages.Account;

public abstract class LoginModelBase : PageModel
{
    private readonly IUrlHelperFactory _urlHelperFactory;
    private readonly ILogger _logger;
    // Changed from '\u001a' to '~' to avoid corrupt marker byte issues
    private readonly string _subStr = "~";

    protected LoginModelBase(IUrlHelperFactory urlHelperFactory, ILogger logger)
    {
        _urlHelperFactory = urlHelperFactory;
        _logger = logger;
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
        
        return IsLocalUrl(returnUrl) ? returnUrl : Url.Content("~/");
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
        }
        
        return isLocal;
    }
}
