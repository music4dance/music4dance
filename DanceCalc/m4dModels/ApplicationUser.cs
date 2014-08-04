using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace m4dModels
{
    // You can add profile data for the user by adding more properties to your ApplicationUser class, please visit http://go.microsoft.com/fwlink/?LinkID=317594 to learn more.
    public class ApplicationUser : IdentityUser
    {
        public async Task<ClaimsIdentity> GenerateUserIdentityAsync(UserManager<ApplicationUser> manager)
        {
            // Note the authenticationType must match the one defined in CookieAuthenticationOptions.AuthenticationType
            var userIdentity = await manager.CreateIdentityAsync(this, DefaultAuthenticationTypes.ApplicationCookie);
            // Add custom user claims here
            return userIdentity;
        }
        public virtual ICollection<ModifiedRecord> Modified { get; set; }

        public string GetRoles(IDictionary<string, IdentityRole> roleMap, string separator=", ")
        {
            // TODO: Can we do this w/o sending in roleMap?

            StringBuilder sb = new StringBuilder();
            string sp = string.Empty;
            foreach (var idRole in Roles)
            {
                Microsoft.AspNet.Identity.EntityFramework.IdentityRole role = roleMap[idRole.RoleId];
                sb.Append(sp + role.Name);
                sp = separator;
            }

            return sb.ToString();
        }

        public string GetProviders()
        {
            StringBuilder sb = new StringBuilder();
            string sp = string.Empty;
            foreach (var provider in Logins)
            {
                string name = provider.LoginProvider;
                string key = provider.ProviderKey;
                sb.Append(string.Format("{0}{1}|{2}",sp,name,key));
                sp = "|";
            }
            return sb.ToString();
        }

    }
}