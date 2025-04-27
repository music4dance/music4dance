// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System.ComponentModel.DataAnnotations;

using m4d.Controllers;
using m4d.Utilities;

using m4dModels;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace m4d.Areas.Identity.Pages.Account.Manage;

public class IndexModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;

    public IndexModel(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager)
    {
        _userManager = userManager;
        _signInManager = signInManager;
    }

    /// <summary>
    ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
    ///     directly from your code. This API may change or be removed in future releases.
    /// </summary>
    public string UserName { get; set; }

    public DateTime MemberSince { get; set; }
    public SubscriptionLevel SubscriptionLevel { get; set; }
    public DateTime? SubscriptionStart { get; set; }
    public DateTime? SubscriptionEnd { get; set; }

    public List<KeyValuePair<byte, string>> ContactOptions { get; set; }
    public List<KeyValuePair<char, string>> ServiceOptions { get; set; }

    public IEnumerable<SelectListItem> RegionItems { get; set; }

    /// <summary>
    ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
    ///     directly from your code. This API may change or be removed in future releases.
    /// </summary>
    [TempData]
    public string StatusMessage { get; set; }

    /// <summary>
    ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
    ///     directly from your code. This API may change or be removed in future releases.
    /// </summary>
    [BindProperty]
    public InputModel Input { get; set; }

    private async Task LoadAsync(ApplicationUser user)
    {
        var userName = await _userManager.GetUserNameAsync(user);
        var phoneNumber = await _userManager.GetPhoneNumberAsync(user);

        UserName = userName;

        MemberSince = user.StartDate;
        SubscriptionLevel = user.SubscriptionLevel;
        SubscriptionStart = user.SubscriptionStart;
        SubscriptionEnd = user.SubscriptionEnd;

        ContactOptions = ApplicationUser.ContactOptions;
        ServiceOptions = MusicService.GetProfileServices()
            .Select(s => new KeyValuePair<char, string>(s.CID, s.Name)).ToList();
        RegionItems = CountryCodes.Codes
            .Select(code => new SelectListItem { Text = code.Value, Value = code.Key })
            .OrderBy(cc => cc.Text).ToList();

        Input = new InputModel
        {
            PublicProfile = user.Privacy > 0,
            ContactSelection = user.ContactSelection,
            ServiceSelection = user.ServicePreference == null
                ? []
                : user.ServicePreference.Select(c => c).ToList(),
            Region = string.IsNullOrWhiteSpace(user.Region) ? "US" : user.Region
        };
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
        }

        await LoadAsync(user);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
        }

        if (!ModelState.IsValid)
        {
            await LoadAsync(user);
            return Page();
        }

        var changes = new ProfileChanges
        {
            Old = new ProfileDelta(), New = new ProfileDelta()
        };

        var modified = false;
        var privacy = (byte)(Input.PublicProfile ? 255 : 0);
        if (user.Privacy != privacy)
        {
            changes.Old.Privacy = user.Privacy;
            changes.New.Privacy = user.Privacy = privacy;
            modified = true;
        }

        var canContact = Input.ContactSelection == null
            ? ContactStatus.None
            : (ContactStatus)Input.ContactSelection.Aggregate<byte, byte>(
                0, (current, cnt) => (byte)(current | cnt));
        if (user.CanContact != canContact)
        {
            changes.Old.CanContact = user.CanContact;
            changes.New.CanContact = user.CanContact = canContact;
            modified = true;
        }

        var servicePreference = Input.ServiceSelection == null
            ? string.Empty
            : new string(Input.ServiceSelection.ToArray());
        if (user.ServicePreference != servicePreference)
        {
            changes.Old.ServicePreference = user.ServicePreference;
            changes.New.ServicePreference = user.ServicePreference = servicePreference;
            modified = true;
        }

        if (Input.Region != user.Region)
        {
            changes.Old.Region = user.Region;
            changes.New.Region = user.Region = Input.Region;
            modified = true;
        }

        //var phoneNumber = await _userManager.GetPhoneNumberAsync(user);
        //if (Input.PhoneNumber != phoneNumber)
        //{
        //    var setPhoneResult = await _userManager.SetPhoneNumberAsync(user, Input.PhoneNumber);
        //    if (!setPhoneResult.Succeeded)
        //    {
        //        var userId = await _userManager.GetUserIdAsync(user);
        //        throw new InvalidOperationException($"Unexpected error occurred setting phone number for user with ID '{userId}'.");
        //    }
        //}

        if (modified)
        {
            if (user.ActivityLog == null)
            {
                user.ActivityLog = [];
            }
            user.ActivityLog.Add(new ActivityLog("Profile", user, changes));
            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Unexpected error occurred updating user '{user.UserName}' profile.");
            }

            UserMapper.Clear();
            UsersController.ClearCache();
        }

        await _signInManager.RefreshSignInAsync(user);
        StatusMessage = "Your profile has been updated";
        return RedirectToPage();
    }

    public class InputModel
    {
        public string Region { get; set; }

        [Display(Name = "Share my profile with other members.")]
        public bool PublicProfile { get; set; }

        public List<byte> ContactSelection { get; set; }
        public List<char> ServiceSelection { get; set; }
    }
}
