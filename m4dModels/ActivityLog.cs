using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;

namespace m4dModels
{
    public class ActivityLog
    {
        public ActivityLog()
        {
            Date = DateTimeOffset.Now;
        }

        public ActivityLog(string action, ApplicationUser user, string details) : this()
        {
            Action = action;
            User = user;
            Details = details;
        }

        public ActivityLog(string action, ApplicationUser user, object details) : 
            this(action, user, JsonConvert.SerializeObject(details, SerializerSettings))
        {
        }

        public int Id { get; set; }
        public string ApplicationUserId { get; set; }
        public ApplicationUser User { get; set; }
        public DateTimeOffset Date { get; set; }
        public string Action { get; set; }
        public string Details { get; set; }

        private static readonly JsonSerializerSettings SerializerSettings = new()
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            NullValueHandling = NullValueHandling.Ignore
        };
    }

    public class ProfileChanges
    {
        public ProfileDelta Old { get; set; }
        public ProfileDelta New { get; set; }
    }

    public class ProfileDelta
    {
        public byte? Privacy { get; set; }
        public ContactStatus? CanContact { get; set; }
        public string ServicePreference { get; set; }
        public string Region { get; set; }
    }

    public class SpotifyCreate { 
        public string Id { get; set; }
        public PlaylistCreateInfo Info { get; set; }
    }
}
