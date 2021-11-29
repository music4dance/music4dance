using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Identity;

namespace m4dModels
{
    public enum SubscriptionLevel
    {
        None = 0,
        Trial = 1,
        Silver = 2,
        Gold = 3,
        Platinum = 4
    }

    // You can add profile data for the user by adding more properties to your ApplicationUser class, please visit http://go.microsoft.com/fwlink/?LinkID=317594 to learn more.
    public sealed class ApplicationUser : IdentityUser
    {
        public ApplicationUser()
        {
            StartDate = DateTime.Now;
        }

        public ApplicationUser(string userName, bool pseudo = false) : this()
        {
            UserName = userName;
            if (pseudo)
            {
                Email = $"{userName}@music4dance.net";
            }
        }

        public ApplicationUser(string userName, string email) : this()
        {
            UserName = userName;
            Email = email;
        }

        public static ApplicationUser AdminUser => new ApplicationUser("admin", true);

        public bool IsPlaceholder => StartDate == DateTime.MinValue;

        public bool IsPseudo => IsM4d || IsSpotify;

        public bool IsM4d =>
            Email.EndsWith("@music4dance.net", StringComparison.OrdinalIgnoreCase);

        public bool IsSpotify =>
            Email.EndsWith("@spotify.com", StringComparison.OrdinalIgnoreCase);

        public string EmailAlias
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Email) || !Email.Contains("@"))
                {
                    return null;
                }

                return Email.Substring(0, Email.IndexOf('@'));
            }
        }

        public bool IsConfirmed => EmailConfirmed && !IsPseudo;

        public string DecoratedName => BuildDecoratedName(UserName, IsPseudo);

        public static string BuildDecoratedName(string userName, bool isPseudo)
        {
            return isPseudo ? $"{userName}|P" : userName;
        }

        public override string ToString()
        {
            return $"{UserName}{(IsPlaceholder ? "(P)" : string.Empty)}";
        }

        #region Properties

        // Date that the member signed up
        public DateTime StartDate { get; set; }
        public DateTime LastActive { get; set; }

        // Two character country code based on ISO 3166-1 alpha-2 country codes.
        public string Region { get; set; }

        // Privacy level 0-255 (initial states are only 0 & 255)
        public byte Privacy { get; set; }

        // Contact bitfield
        public ContactStatus CanContact { get; set; }

        // Services in order of preference?
        // Or actively use, interested, not interested
        // Or just actively use?
        // Single character service Ids (add in P=Pandora, G=Google Play, E=Emusic for this purpose)
        public string ServicePreference { get; set; }

        public int? RowCountDefault { get; set; }
        public string ColumnDefaults { get; set; }

        public DateTime? SubscriptionStart { get; set; }
        public DateTime? SubscriptionEnd { get; set; }

        public SubscriptionLevel SubscriptionLevel { get; set; }

        public List<Search> Searches { get; set; }
        public List<ActivityLog> ActivityLog { get; set; }

        // Everything below here are computed properties

        public string SpotifyId => IsSpotify ? EmailAlias : null;

        public string RegionName => CountryCodes.TranslateCode(Region);

        public string PrivacyDescription =>
            string.Format(PrivacyMessage, Privacy == 0 ? "Don't" : "Do");

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
                    ret.AddRange(
                        ServicePreference.Select(MusicService.GetService)
                            .Where(s => s != null).Select(s => s.Name));
                }

                return ret;
            }
        }

        public static List<KeyValuePair<byte, string>> ContactOptions
        {
            get
            {
                var ret = new List<KeyValuePair<byte, string>>();
                var i = 0;
                byte mask = 1;
                while (mask != (byte)ContactStatus.Max)
                {
                    ret.Add(new KeyValuePair<byte, string>(mask, ContactStrings[i]));
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

        public static string[] ContactStrings { get; } =
        {
            @"I would be interested in participating in email or surveys to help improve music4dance",
            @"I am interested in occasional promotional emails from music4dance partners",
            @"I am interested in occasional promotional emails from music4dance"
        };

        private const string PrivacyMessage = "{0} allow other members to see my profile.";

        #endregion
    }
}
