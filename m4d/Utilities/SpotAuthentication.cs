using System.Security.Claims;
using System.Security.Principal;
using System.Web;

namespace m4d.Utilities;

public class SpotAuthentication(IConfiguration configuration) : AdmAuthentication(configuration)
{
    protected override string Client => "spotify";

    protected override string RequestBody => "grant_type=client_credentials";
    protected override string RequestUrl => "https://accounts.spotify.com/api/token";

    protected override string GetServiceId(IPrincipal principal)
    {
        if (principal is not ClaimsPrincipal claimsPrincipal)
        {
            return null;
        }

        var idClaim = claimsPrincipal.Claims.FirstOrDefault(c => c.Type == "urn:spotify:id");

        return idClaim?.Value;
    }
}

public class SpotUserAuthentication(IConfiguration configuration) : SpotAuthentication(configuration)
{
    protected override string RequestExtra => string.IsNullOrWhiteSpace(RefreshToken)
        ? string.Empty
        : $"&refresh_token={HttpUtility.UrlEncode(RefreshToken)}";

    protected override string RequestBody => "grant_type=refresh_token";
}
