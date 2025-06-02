using System;
using System.Net.Http.Headers;
using System.Text;

namespace RecomputeJob
{
    public class TokenAuthenticationHeaderValue : AuthenticationHeaderValue
    {
        public TokenAuthenticationHeaderValue(string token)
            : base("Token", Convert.ToBase64String(Encoding.UTF8.GetBytes(token)))
        { }
    }
}
