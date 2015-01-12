using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;

namespace m4d.Utilities
{
    [DataContract]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class SpotAccessToken : AccessToken
    {
        [DataMember]
        public int expires_in { get; set; }

        public override TimeSpan ExpiresIn
        {
            get { return TimeSpan.FromSeconds(expires_in - 60); }
        }

    }

    public class SpotAuthentication : AdmAuthentication
    {
        protected override string ClientId {get { return "***REMOVED***"; }}
        protected override string ClientSecret { get { return "***REMOVED***"; } }

        protected override string RequestFormat { get { return "grant_type=client_credentials&client_id={0}&client_secret={1}"; } }
        protected override string RequestUrl { get { return "https://accounts.spotify.com/api/token"; } }
        protected override Type AccessTokenType { get { return typeof(SpotAccessToken); } }
    }

}