using System;
using System.Net.Http;
using System.Text;
using System.Web;
using System.Web.Mvc;

namespace m4d.Utilities
{
    public class TokenAuthorizeAttribute : AuthorizeAttribute
    {
        protected override bool AuthorizeCore(HttpContextBase httpContext)
        {
            return Authorize(httpContext.Request);
        }

        public static bool Authorize(HttpRequestBase request)
        {
            return Authorize(request.Headers["Authorization"]);
        }

        public static bool Authorize(HttpRequestMessage request)
        {
            return Authorize(request.Headers.Authorization.ToString());
        }

        public static bool Authorize(string authenticationHeader)
        {
            var parts = authenticationHeader?.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (parts?.Length != 2) return false;

            var token = Encoding.UTF8.GetString(Convert.FromBase64String(parts[1]));
            return parts[0] == "Token" && token == SecurityToken;
        }

        public static string SecurityToken => s_securityToken ?? (s_securityToken = Environment.GetEnvironmentVariable("RECOMPUTEJOB_KEY"));

        private static string s_securityToken;
    }
}