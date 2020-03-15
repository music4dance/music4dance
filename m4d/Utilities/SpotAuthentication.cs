using System.Linq;
using System.Security.Claims;
using System.Security.Principal;
using System.Web;

namespace m4d.Utilities
{
    public class SpotAuthentication : AdmAuthentication
    {
        protected override string Client => "spot";

        protected override string RequestFormat => "grant_type=client_credentials";
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
        protected override string RequestExtra => "&refresh_token=" + HttpUtility.UrlEncode(RefreshToken);
        protected override string RequestFormat => "grant_type=refresh_token";
    }
}