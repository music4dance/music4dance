using System.Collections.Generic;

namespace m4dModels
{
    public class UserInfo
    {
        public ApplicationUser User;
        public List<string> Roles;
        public List<string> Logins;

        public bool IsPseudo => User.IsPseudo;

        public bool IsConfirmed => User.EmailConfirmed && !IsPseudo;
    }
}