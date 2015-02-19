using System.Security.Claims;
using System.Security.Principal;

namespace m4d.Utilities
{
    public static class GenericPrincipalExtensions
    {
        public static string Region(this IPrincipal user)
        {
            if (!user.Identity.IsAuthenticated) return DefaultRegion;

            var claimsIdentity = user.Identity as ClaimsIdentity;
            if (claimsIdentity == null || claimsIdentity.Claims == null) return DefaultRegion;

            foreach (var claim in claimsIdentity.Claims)
            {
                if (claim.Type == "Region")
                    return claim.Value;
            }
            return DefaultRegion;
        }

        private const string DefaultRegion = "US";
    }
}