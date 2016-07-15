using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;

namespace m4dModels
{
    // You can add profile data for the user by adding more properties to your ApplicationUser class, please visit http://go.microsoft.com/fwlink/?LinkID=317594 to learn more.
    public sealed class ApplicationUser : IdentityUser
    {
        #region Properties

        // Date that the member signed up
        public DateTime StartDate { get; set; }
        public DateTime LastActive { get; set; }

        // Two character country code based on ISO 3166-1 alpha-2 country codes.
        public string Region { get; set; }
        // Privacy level 0-255 (inital states are only 0 & 255)
        public byte Privacy { get; set; }
        // Conctact bitfield
        public ContactStatus CanContact { get; set; }
        // Services in order of preference?
        // Or actively use, interested, not interested
        // Or just actively use?
        // Single character service Ids (add in P=Pandora, G=Google Play, E=Emusic for this purpose)
        public string ServicePreference { get; set; }

        public int? RowCountDefault { get; set; }
        public string ColumnDefaults { get; set; }

        public string RegionName => CountryCodes.TranslateCode(Region);

        public string PrivacyDescription => string.Format(PrivacyMessage, (Privacy == 0) ? "Don't" : "Do");

        public IEnumerable<string> ContactDescription
        {
            get
            {
                var ret = new List<string>();
                var i = 0;
                byte mask = 1;
                while (mask != (byte)ContactStatus.Max)
                {
                    if ((mask & (byte)CanContact) == mask)
                    {
                        ret.Add(ContactStrings[i]);
                    }
                    mask = (byte)(mask << 1);
                    i += 1;
                }
                return ret;
            }
        }

        public IEnumerable<string> ServicePreferenceDescription
        {
            get
            {
                var ret = new List<string>();

                if (!string.IsNullOrEmpty(ServicePreference))
                {
                    ret.AddRange(ServicePreference.Select(MusicService.GetService).Where(s => s != null).Select(s => s.Name));
                }
                return ret;
            }
        }

        public static List<KeyValuePair<byte, string>> ContactOptions
        {
            get
            {
                var ret = new List<KeyValuePair<byte,string>>();
                var i = 0;
                byte mask = 1;
                while (mask != (byte)ContactStatus.Max)
                {
                    ret.Add(new KeyValuePair<byte, string>(mask,ContactStrings[i]));
                    mask = (byte)(mask << 1);
                    i += 1;
                }
                return ret;
            }
        }

        public List<byte> ContactSelection
        {
            get
            {
                var ret = new List<byte>();
                byte mask = 1;
                while (mask != (byte)ContactStatus.Max)
                {
                    if ((mask & (byte)CanContact) == mask)
                    {
                        ret.Add(mask);
                    }
                    mask = (byte)(mask << 1);
                }
                return ret;                
            }
        }
        public static string[] ContactStrings { get; } = {
            @"I would be interested in participating in email or surveys to help improve music4dance",
            @"I am interested in occassional promotional emails from music4dance partners",
            @"I am interested in occassional promotional emails from music4dance"
        };

        private const string PrivacyMessage = "{0} allow other members to see my profile and tags.";

        #endregion

        public ApplicationUser()
        {
            StartDate = DateTime.Now;
        }

        public ApplicationUser(string userName)
        {
            StartDate = DateTime.MinValue;
            UserName = userName;
        }

        public bool IsPlaceholder => StartDate == DateTime.MinValue;

        public async Task<ClaimsIdentity> GenerateUserIdentityAsync(UserManager<ApplicationUser> manager)
        {
            // Note the authenticationType must match the one defined in CookieAuthenticationOptions.AuthenticationType
            var userIdentity = await manager.CreateIdentityAsync(this, DefaultAuthenticationTypes.ApplicationCookie);
            // Add custom user claims here
            return userIdentity;
        }

        public string GetRoles(IDbSet<IdentityRole> roles, string separator=", ")
        {
            // TODO: Can we do this w/o sending in roleMap?

            var sb = new StringBuilder();
            var sp = string.Empty;
            // ReSharper disable once LoopCanBePartlyConvertedToQuery
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
                sb.Append($"{sp}{name}|{key}");
                sp = "|";
            }
            return sb.ToString();
        }

        public static string SerializeProviders(IEnumerable<UserLoginInfo> logins)
        {
            var sb = new StringBuilder();
            var sp = string.Empty;
            foreach (var provider in logins)
            {
                var name = provider.LoginProvider;
                var key = provider.ProviderKey;
                sb.Append($"{sp}{name}|{key}");
                sp = "|";
            }
            return sb.ToString();            
        }

        public override string ToString()
        {
            return $"{UserName}{(IsPlaceholder ? "(P)" : string.Empty)}";
        }
    }
}