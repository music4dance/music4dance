using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using m4dModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Owl.reCAPTCHA;
using Owl.reCAPTCHA.v2;

namespace m4d.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class RegisterModel : PageModel
    {
        private readonly IEmailSender _emailSender;
        private readonly ILogger<RegisterModel> _logger;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IreCAPTCHASiteVerifyV2 _siteVerify;
        private readonly UserManager<ApplicationUser> _userManager;

        public RegisterModel(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ILogger<RegisterModel> logger,
            IEmailSender emailSender,
            IreCAPTCHASiteVerifyV2 siteVerify,
            IConfiguration configuration)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _logger = logger;
            _emailSender = emailSender;
            var captcha = configuration["Configuration:Registration:CaptchaEnabled"];
            if (!string.IsNullOrEmpty(captcha) && bool.TryParse(captcha, out var enabled) &&
                enabled)
            {
                _siteVerify = siteVerify;
            }
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public string ReturnUrl { get; set; }

        public IList<AuthenticationScheme> ExternalLogins { get; set; }

        public bool UseCaptcha => _siteVerify != null;

        public static string BuildConfirmMessage(string callbackUrl) =>
            @"<h4>Please confirm your email account by clicking <a href=" + callbackUrl +
            @">here</a><h4><p></p>" +
            @"<h4>Once you've confirmed your email and signed in, please explore these features that you now have access to:</h4>" +
            @"<ul>" +
            @"<li><a href='https://music4dance.blog/music4dance-help/dance-tags/'>Help us categorize songs by dance style</a></li>" +
            @"<li><a href='https://music4dance.blog/music4dance-help/tag-editing/'>Tag songs</a></li>" +
            @"<li><a href='https://music4dance.blog/music4dance-help/advanced-search/'>Search on songs you've tagged</a></li>" +
            @"<li><a href='https://music4dance.blog/are-there-songs-that-you-never-want-to-dance-to-again/'>Like and unlike songs</a></li>" +
            @"<li><a href='https://music4dance.blog/are-there-songs-that-you-never-want-to-dance-to-again/'>Hide songs you've 'unliked'</a></li>" +
            @"<li><a href='https://music4dance.blog/music4dance-help/saved-searches/'>Save your searches</a></li>" +
            @"</ul>" +
            @"<h4>Please consider purchasing a premium subscription or donating to the site by visiting our <a href='https://www.music4dance.net/home/contribute'>contribute page</a>.</h4>";

        public async Task OnGetAsync(string returnUrl = null)
        {
            ReturnUrl = returnUrl;
            ExternalLogins =
                (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");
            ExternalLogins =
                (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            if (UseCaptcha)
            {
                var response = await _siteVerify.Verify(
                    new reCAPTCHASiteVerifyRequest
                    {
                        Response = Input.RecaptchaToken,
                        RemoteIp = HttpContext.Connection.RemoteIpAddress.ToString()
                    });

                if (!response.Success)
                {
                    ModelState.AddModelError(
                        string.Empty, @"Please check the ""I'm not a Robot'"" box");
                }
            }

            if (ModelState.IsValid)
            {
                var user = new ApplicationUser
                    { UserName = Input.UserName, Email = Input.Email, Privacy = 255 };
                var result = await _userManager.CreateAsync(user, Input.Password);
                if (result.Succeeded)
                {
                    _logger.LogInformation("User created a new account with password.");

                    var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                    code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
                    var callbackUrl = Url.Page(
                        "/Account/ConfirmEmail",
                        null,
                        new
                        {
                            area = "Identity", userId = user.Id,
                            code
                        },
                        Request.Scheme);

                    await _emailSender.SendEmailAsync(
                        Input.Email, "Confirm your email", BuildConfirmMessage(callbackUrl));

                    if (_userManager.Options.SignIn.RequireConfirmedAccount)
                    {
                        return RedirectToPage(
                            "RegisterConfirmation",
                            new { email = Input.Email, DisplayConfirmAccountLink = false });
                    }

                    await _signInManager.SignInAsync(user, false);
                    return LocalRedirect(returnUrl);
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            // If we got this far, something failed, redisplay form
            return Page();
        }

        public class InputModel
        {
            [Required]
            [Display(Name = "User Name")]
            public string UserName { get; set; }

            [Required]
            [EmailAddress]
            [Display(Name = "Email")]
            public string Email { get; set; }

            [Display(Name = "Confirm Email")]
            [Compare("Email", ErrorMessage = "The email and confirmation email do not match.")]
            public string ConfirmEmail { get; set; }

            [Required]
            [StringLength(
                100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.",
                MinimumLength = 6)]
            [DataType(DataType.Password)]
            [Display(Name = "Password")]
            public string Password { get; set; }

            [DataType(DataType.Password)]
            [Display(Name = "Confirm password")]
            [Compare(
                "Password", ErrorMessage = "The password and confirmation password do not match.")]
            public string ConfirmPassword { get; set; }

            public string RecaptchaToken { get; set; }
        }
    }
}
