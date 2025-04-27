using m4dModels;

using Microsoft.AspNetCore.Identity;

namespace m4d.ViewModels
{
    public class UserExpiration
    {
        public static async Task<UserExpiration> Create(string userName, UserManager<ApplicationUser> userManager)
        {
            var user = userName == null ? null : await userManager.FindByNameAsync(userName);
            var userExpiration = new UserExpiration { User = user };
            if (user != null)
            {
                var roles = await userManager.GetRolesAsync(user);
                var expiration = roles.Contains(DanceMusicCoreService.PremiumRole) ? user.SubscriptionEnd.ToString() : null;
                userExpiration.ExpirationString = expiration;
            }
            return userExpiration;
        }
        public ApplicationUser User { get; set; }
        public string ExpirationString { get; set; }
    }
}
