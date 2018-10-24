using System;
using System.Diagnostics;
using System.Linq;
using System.Security.Claims;
using System.Security.Principal;
using System.Threading;
using System.Web;

namespace m4d.Utilities
{
    public class SpotAuthentication : AdmAuthentication
    {
        protected override string Client => "spot";

        protected override string RequestFormat => "grant_type=client_credentials&client_id={0}&client_secret={1}";
        protected override string RequestUrl => "https://accounts.spotify.com/api/token";

        protected override string GetServiceId(IPrincipal principal)
        {
            if (!(principal is ClaimsPrincipal claimsPrincipal)) return null;

            var idClaim = claimsPrincipal.Claims.FirstOrDefault(c => c.Type == "urn:spotify:id");

            return idClaim?.Value;
        }

    }

    public class SpotUserAuthentication : SpotAuthentication
    {
        public static SpotUserAuthentication TryCreate(IPrincipal principal)
        {
            if (!(principal is ClaimsPrincipal claimsPrincipal))
                return null;

            var token = new AccessToken();
            string refreshToken = null;
            var start = DateTime.Now;
            foreach (var claim in claimsPrincipal.Claims)
            {
                Trace.WriteLine($"{claim.Issuer}: {claim.Type}: {claim.Value}");
                switch (claim.Type)
                {
                    case "urn:spotify:access_token":
                        token.access_token = claim.Value;
                        break;
                    case "urn:spotify:refresh_token":
                        refreshToken = claim.Value;
                        break;
                    case "urn:spotify:expires_in":
                        token.expires_in = (int)(TimeSpan.Parse(claim.Value).TotalMilliseconds / 1000);
                        break;
                    case "urn:spotify:start_time":
                        start = DateTime.Parse(claim.Value);
                        break;
                }
            }
            
            if (refreshToken == null)
                return null;

            var auth = new SpotUserAuthentication
            {
                _refreshToken = refreshToken, 
            };

            // TODO: Handle the case when this codepath gets called well after the intial access token is isssued
            var delta = DateTime.Now - start;
            if (delta < token.ExpiresIn)
            {
                auth.Token = token;
                auth.AccessTokenRenewer = new Timer(auth.OnTokenExpiredCallback, auth, token.ExpiresIn - delta, token.ExpiresIn);
            }

            return auth;
        }

        protected override string RequestExtra => "&refreshToken=" + HttpUtility.UrlEncode(_refreshToken);
        protected override string RequestFormat => "grant_type=refresh_token&refresh_token={0}&client_secret={1}";

        private string _refreshToken;
    }
}