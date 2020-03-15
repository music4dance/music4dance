using System.Collections.Generic;

namespace m4dModels
{
    public class UserInfo
    {
        public ApplicationUser User;
        public List<string> Roles;
        public List<string> Logins;

        public bool IsPseudo => User.Email.EndsWith("@music4dance.net") ||
                                User.Email.EndsWith("@spotify.com") ||
                                User.Email.EndsWith("@thegray.com");

        public bool IsConfirmed => User.EmailConfirmed && !IsPseudo;
    }
}