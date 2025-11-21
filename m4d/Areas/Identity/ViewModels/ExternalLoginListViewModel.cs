using Microsoft.AspNetCore.Authentication;

namespace m4d.Areas.Identity.ViewModels;

public class ExternalLoginListViewModel
{
    public IList<AuthenticationScheme> ExternalLogins { get; set; }
    public string ReturnUrl { get; set; }
}
