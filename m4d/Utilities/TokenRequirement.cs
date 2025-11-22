using System.Text;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace m4d.Utilities;

//https://stackoverflow.com/questions/31464359/how-do-you-create-a-custom-authorizeattribute-in-asp-net-core
public class TokenRequirement : AuthorizationHandler<TokenRequirement>,
    IAuthorizationRequirement
{
    public TokenRequirement([FromServices] IConfiguration configuration)
    {
        if (configuration != null)
        {
            SetSecurityToken(configuration);
        }
    }

    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context,
        TokenRequirement requirement)
    {
        var authFilterCtx =
            (Microsoft.AspNetCore.Mvc.Filters.AuthorizationFilterContext)context.Resource;
        var request = authFilterCtx.HttpContext.Request;

        if (Authorize(request))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }

    public static bool Authorize(HttpRequest request, IConfiguration configuration = null)
    {
        if (configuration != null)
        {
            SetSecurityToken(configuration);
        }

        var auth = request.Headers["Authorization"];
        return Authorize(auth.ToString());
    }

    private static bool Authorize(string authenticationHeader)
    {
        var parts =
            authenticationHeader?.Split([' '], StringSplitOptions.RemoveEmptyEntries);

        if (parts?.Length != 2)
        {
            return false;
        }

        var token = Encoding.UTF8.GetString(Convert.FromBase64String(parts[1]));
        return parts[0] == "Token" && token == s_securityToken;
    }

    private static void SetSecurityToken(IConfiguration configuration)
    {
        s_securityToken ??= configuration["Authentication:RecomputeJob:Key"];
    }

    private static string s_securityToken;
}
