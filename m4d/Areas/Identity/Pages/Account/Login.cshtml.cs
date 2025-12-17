// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using m4d.Services.ServiceHealth;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

using System.ComponentModel.DataAnnotations;

namespace m4d.Areas.Identity.Pages.Account;

public class LoginModel : LoginModelBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ILogger<LoginModel> _logger;
    private readonly IEmailSender _emailSender;
    private readonly ServiceHealthManager _serviceHealth;

    public LoginModel(SignInManager<ApplicationUser> signInManager,
        ILogger<LoginModel> logger,
        UserManager<ApplicationUser> userManager,
        IEmailSender emailSender,
        IUrlHelperFactory urlHelperFactory,
        ServiceHealthManager serviceHealth)
        : base(urlHelperFactory, logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _emailSender = emailSender;
        _logger = logger;
        _serviceHealth = serviceHealth;
    }

    /// <summary>
    ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
    ///     directly from your code. This API may change or be removed in future releases.
    /// </summary>
    [BindProperty]
    public InputModel Input { get; set; }

    /// <summary>
    ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
    ///     directly from your code. This API may change or be removed in future releases.
    /// </summary>
    public IList<AuthenticationScheme> ExternalLogins { get; set; }

    /// <summary>
    ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
    ///     directly from your code. This API may change or be removed in future releases.
    /// </summary>
    public string ReturnUrl { get; set; }

    public string Provider { get; set; }

    /// <summary>
    ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
    ///     directly from your code. This API may change or be removed in future releases.
    /// </summary>
    [TempData]
    public string ErrorMessage { get; set; }

    /// <summary>
    ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
    ///     directly from your code. This API may change or be removed in future releases.
    /// </summary>
    public class InputModel
    {
        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [Required]
        [Display(Name = "User Name or Email")]
        public string UserName { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [Display(Name = "Remember me?")]
        public bool RememberMe { get; set; }
    }

    public async Task OnGetAsync(string returnUrl = null, string provider = null)
    {
        if (!string.IsNullOrEmpty(ErrorMessage))
        {
            ModelState.AddModelError(string.Empty, ErrorMessage);
        }

        ReturnUrl = CleanUrl(returnUrl);

        // Clear the existing external cookie to ensure a clean login process
        await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

        ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

        // Set ViewData for OAuth provider availability
        ViewData["GoogleAvailable"] = _serviceHealth.IsServiceHealthy("GoogleOAuth");
        ViewData["FacebookAvailable"] = _serviceHealth.IsServiceHealthy("FacebookOAuth");
        ViewData["SpotifyAvailable"] = _serviceHealth.IsServiceHealthy("SpotifyOAuth");

        Provider = provider;
    }

    public async Task<IActionResult> OnPostAsync(string returnUrl = null)
    {
        returnUrl = CleanUrl(returnUrl);

        ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

        if (ModelState.IsValid)
        {
            // If the input is a valid user's email, use that
            var user = await _userManager.FindByEmailAsync(Input.UserName);
            if (user == null)
            {
                user = await _userManager.FindByNameAsync(Input.UserName);
            }

            if (user != null)
            {
                Input.UserName = user.UserName;

                var result = await _signInManager.PasswordSignInAsync(
                    Input.UserName, Input.Password, Input.RememberMe, lockoutOnFailure: true);
                if (result.Succeeded)
                {
                    _logger.LogInformation($"User {user.UserName} logged in.");
                    user.LastActive = System.DateTime.Now;
                    var lastLoginResult = await _userManager.UpdateAsync(user);
                    if (!lastLoginResult.Succeeded)
                    {
                        _logger.LogWarning(
                            $"Failed to set last loging for user {user.UserName}");
                    }

                    return LocalRedirect(returnUrl);
                }

                if (result.RequiresTwoFactor)
                {
                    return RedirectToPage(
                        "./LoginWith2fa",
                        new { ReturnUrl = returnUrl, RememberMe = Input.RememberMe });
                }

                if (result.IsLockedOut)
                {
                    _logger.LogWarning("User account locked out.");
                    return RedirectToPage("./Lockout");
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                    return Page();
                }
            }
        }

        // If we got this far, something failed, redisplay form
        return Page();
    }
}
