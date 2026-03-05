// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using m4d.Security;
using m4d.Services.ServiceHealth;
using m4d.Utilities;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.FeatureManagement;

using Owl.reCAPTCHA;
using Owl.reCAPTCHA.v2;

using System.ComponentModel.DataAnnotations;

namespace m4d.Areas.Identity.Pages.Account;

public class LoginModel : LoginModelBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ILogger<LoginModel> _logger;
    private readonly IEmailSender _emailSender;
    private readonly ServiceHealthManager _serviceHealth;
    private readonly AuthenticationTracker _authTracker;
    private readonly IreCAPTCHASiteVerifyV2 _siteVerify;
    private readonly IFeatureManagerSnapshot _featureManager;

    public LoginModel(SignInManager<ApplicationUser> signInManager,
        ILogger<LoginModel> logger,
        UserManager<ApplicationUser> userManager,
        IEmailSender emailSender,
        IUrlHelperFactory urlHelperFactory,
        ServiceHealthManager serviceHealth,
        AuthenticationTracker authTracker,
        IreCAPTCHASiteVerifyV2 siteVerify,
        IFeatureManagerSnapshot featureManager)
        : base(urlHelperFactory, logger, authTracker)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _emailSender = emailSender;
        _logger = logger;
        _serviceHealth = serviceHealth;
        _authTracker = authTracker;
        _siteVerify = siteVerify;
        _featureManager = featureManager;
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

    public bool ShowCaptcha { get; set; }

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

        public string RecaptchaToken { get; set; }
    }

    public async Task<bool> UseCaptcha() =>
        await _featureManager.IsEnabledAsync(FeatureFlags.Captcha);

    public async Task OnGetAsync(string returnUrl = null, string provider = null)
    {
        if (!string.IsNullOrEmpty(ErrorMessage))
        {
            ModelState.AddModelError(string.Empty, ErrorMessage);
        }

        ReturnUrl = CleanUrl(returnUrl);

        // Check if CAPTCHA should be shown due to security conditions
        if (GlobalState.RequireCaptcha && await UseCaptcha())
        {
            ShowCaptcha = true;
        }

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

        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        // Check if CAPTCHA is required (GlobalState or FeatureFlag)
        var captchaRequired = (GlobalState.RequireCaptcha || ShowCaptcha) && await UseCaptcha();

        if (captchaRequired)
        {
            // Validate reCAPTCHA
            var response = await _siteVerify.Verify(
                new reCAPTCHASiteVerifyRequest
                {
                    Response = Input.RecaptchaToken,
                    RemoteIp = clientIp
                });

            if (!response.Success)
            {
                ModelState.AddModelError(string.Empty, "Please complete the CAPTCHA verification.");
                ShowCaptcha = true;
                return Page();
            }
        }

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

                    // Track successful login
                    _authTracker.RecordAttempt(user.UserName, clientIp, success: true);

                    user.LastActive = System.DateTime.Now;
                    var lastLoginResult = await _userManager.UpdateAsync(user);
                    if (!lastLoginResult.Succeeded)
                    {
                        _logger.LogWarning(
                            $"Failed to set last login for user {user.UserName}");
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
                    _authTracker.RecordAttempt(user.UserName, clientIp, success: false, failureReason: "LockedOut");
                    return RedirectToPage("./Lockout");
                }
                else
                {
                    // Failed login attempt
                    _authTracker.RecordAttempt(user.UserName, clientIp, success: false, failureReason: "InvalidPassword");
                    ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                    ShowCaptcha = await UseCaptcha(); // Show CAPTCHA on next attempt
                    return Page();
                }
            }
            else
            {
                // User not found
                _authTracker.RecordAttempt(Input.UserName, clientIp, success: false, failureReason: "UserNotFound");
                ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                ShowCaptcha = await UseCaptcha(); // Show CAPTCHA on next attempt
                return Page();
            }
        }

        // If we got this far, something failed, redisplay form
        return Page();
    }
}
