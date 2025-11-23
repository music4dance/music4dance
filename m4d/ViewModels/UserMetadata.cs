using Microsoft.AspNetCore.Identity;

namespace m4d.ViewModels;

public class UserMetadata
{
    public static async Task<UserMetadata> Create(string userName, UserManager<ApplicationUser> userManager)
    {
        var user = userName == null ? null : await userManager.FindByNameAsync(userName);
        var roles = user != null ? await userManager.GetRolesAsync(user) : null;
        return new UserMetadata(user, roles);
    }

    private UserMetadata(ApplicationUser user, IList<string> roles)
    {
        User = user;
        if (user == null) return;

        Roles = roles ?? Enumerable.Empty<string>();
        Expiration = FormatDate(user.SubscriptionEnd);
        Started = FormatDate(user.StartDate);
        Level = user.SubscriptionLevel.ToString();
        HitCount = user.HitCount;
    }

    public ApplicationUser User { get; }
    public string Expiration { get; }
    public string Started { get; }
    public string Level { get; }
    public IEnumerable<string> Roles { get; }
    public int HitCount { get; }

    private string FormatDate(DateTime? date)
    {
        return date?.ToString();
    }
}
