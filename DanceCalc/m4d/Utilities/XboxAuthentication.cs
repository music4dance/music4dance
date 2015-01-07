using System;

namespace m4d.Utilities
{
    public class XboxAuthentication : AdmAuthentication
    {
        protected override string ClientId { get { return "music4dance"; } }
        protected override string ClientSecret { get { return "iGvYm97JA+qYV1K2lvh8sAnL8Pebp5cN2KjvGnOD4gI="; } }
        protected override string RequestFormat { get { return "grant_type=client_credentials&client_id={0}&client_secret={1}&scope=http://music.xboxlive.com";} }
        protected override string RequestUrl { get { return "https://datamarket.accesscontrol.windows.net/v2/OAuth2-13"; } }
        protected override Type AccessTokenType { get { return typeof (XboxAccessToken); } }  
    }
}