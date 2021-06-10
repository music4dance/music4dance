using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;

namespace m4d.Areas.Identity
{
    public class UsernameValidator<TUser> : IUserValidator<TUser>
        where TUser : IdentityUser
    {
        public Task<IdentityResult> ValidateAsync(UserManager<TUser> manager, TUser user)
        {
            if (user.UserName.Any(x => _exclude.Contains(x)))
            {
                return Task.FromResult(
                    IdentityResult.Failed(
                        new IdentityError
                        {
                            Code = "InvalidCharactersUsername",
                            Description = "Username can not contain '@', ':', ';', ',' or '|'"
                        }));
            }

            return Task.FromResult(IdentityResult.Success);
        }

        private readonly HashSet<char> _exclude = ":;,|@".ToHashSet();
    }
}
