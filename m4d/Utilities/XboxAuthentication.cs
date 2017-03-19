using System;

namespace m4d.Utilities
{
    public class XboxAuthentication : AdmAuthentication
    {
        protected override string Client => "ms";
        protected override string RequestFormat => "grant_type=client_credentials&client_id={0}&client_secret={1}" + $"&scope={Scope}";
        protected override string RequestUrl => "https://login.live.com/accesstoken.srf";

        private static string Scope => Uri.EscapeDataString("app.music.xboxlive.com");
    }
}