using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace m4dModels
{
    // You can add profile data for the user by adding more properties to your ApplicationUser class, please visit http://go.microsoft.com/fwlink/?LinkID=317594 to learn more.
    public sealed class ApplicationUser : IdentityUser
    {
        public DateTime StartDate { get; set; }

        public ApplicationUser()
        {
            StartDate = DateTime.Now;
        }

        public ApplicationUser(string userName)
        {
            StartDate = DateTime.MinValue;
            UserName = userName;
        }

        public bool IsPlaceholder
        {
            get
            {
                return StartDate == DateTime.MinValue;
            }
        }
        public async Task<ClaimsIdentity> GenerateUserIdentityAsync(UserManager<ApplicationUser> manager)
        {
            // Note the authenticationType must match the one defined in CookieAuthenticationOptions.AuthenticationType
            var userIdentity = await manager.CreateIdentityAsync(this, DefaultAuthenticationTypes.ApplicationCookie);
            // Add custom user claims here
            return userIdentity;
        }
        public ICollection<ModifiedRecord> Modified { get; set; }

        public string GetRoles(IDbSet<IdentityRole> roles, string separator=", ")
        {
            // TODO: Can we do this w/o sending in roleMap?

            var sb = new StringBuilder();
            var sp = string.Empty;
            foreach (var idRole in Roles)
            {
                var role = roles.Find(idRole.RoleId);
                sb.Append(sp + role.Name);
                sp = separator;
            }

            return sb.ToString();
        }

        public string GetProviders()
        {
            var sb = new StringBuilder();
            var sp = string.Empty;
            foreach (var provider in Logins)
            {
                var name = provider.LoginProvider;
                var key = provider.ProviderKey;
                sb.Append(string.Format("{0}{1}|{2}",sp,name,key));
                sp = "|";
            }
            return sb.ToString();
        }

        public override string ToString()
        {
            return string.Format("{0}{1}", UserName, IsPlaceholder ? "(P)" : string.Empty);
        }
    }
}