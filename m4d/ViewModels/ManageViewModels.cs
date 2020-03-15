using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System;
using Microsoft.AspNetCore.Mvc.Rendering;
using m4dModels;
using Microsoft.AspNetCore.Identity;

namespace m4d.ViewModels
{
    public class IndexViewModel
    {
        public string Name { get; set; }
        public bool HasPassword { get; set; }
        public IList<UserLoginInfo> Logins { get; set; }
        public string PhoneNumber { get; set; }
        public bool TwoFactor { get; set; }
        public bool BrowserRemembered { get; set; }
        public DateTime MemberSince { get; set; }
        public SubscriptionLevel SubscriptionLevel { get; set; }
        public DateTime? SubscriptionStart { get; set; }
        public DateTime? SubscriptionEnd { get; set; }

        // These are user readable strings/enumerable strings
        public string Region { get; set; }
        public string Privacy { get; set; }
        public IEnumerable<string> CanContact { get; set; }
        public IEnumerable<string> ServicePreference { get; set; }

    }

    // CORETODO: Figure out how manage logins works in core
    //public class ManageLoginsViewModel
    //{
    //    public IList<UserLoginInfo> CurrentLogins { get; set; }
    //    public IList<AuthenticationDescription> OtherLogins { get; set; }
    //}

    public class FactorViewModel
    {
        public string Purpose { get; set; }
    }

    public class SetPasswordViewModel
    {
        [Required]
        [StringLength(100, ErrorMessage = @"The {0} must be at least {2} characters long.", MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = @"New password")]
        public string NewPassword { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = @"Confirm new password")]
        [System.ComponentModel.DataAnnotations.Compare("NewPassword", ErrorMessage = @"The new password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; }
    }

    public class ChangePasswordViewModel
    {
        [Required]
        [DataType(DataType.Password)]
        [Display(Name = @"Current password")]
        public string OldPassword { get; set; }

        [Required]
        [StringLength(100, ErrorMessage = @"The {0} must be at least {2} characters long.", MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = @"New password")]
        public string NewPassword { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = @"Confirm new password")]
        [System.ComponentModel.DataAnnotations.Compare("NewPassword", ErrorMessage = @"The new password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; }
    }

    public class AddPhoneNumberViewModel
    {
        [Required]
        [Phone]
        [Display(Name = @"Phone Number")]
        public string Number { get; set; }
    }

    public class VerifyPhoneNumberViewModel
    {
        [Required]
        [Display(Name = @"Code")]
        public string Code { get; set; }

        [Required]
        [Phone]
        [Display(Name = @"Phone Number")]
        public string PhoneNumber { get; set; }
    }

    public class ConfigureTwoFactorViewModel
    {
        public string SelectedProvider { get; set; }
        public ICollection<SelectListItem> Providers { get; set; }
    }

    public class ProfileViewModel
    {
        [Key]
        public string Name { get; set; }

        public string Region { get; set; }

        public IEnumerable<SelectListItem> RegionItems { get; set; }

        public bool PublicProfile { get; set; }
        public List<KeyValuePair<byte,string>> ContactOptions { get; set; }
        public List<byte> ContactSelection { get; set; }
        public List<KeyValuePair<char,string>> ServiceOptions { get; set; }
        public List<char> ServiceSelection { get; set; }
    }

    public class DeleteUserViewModel
    {
        public string Id { get; set; }
        public string UserName { get; set; }
    }
}