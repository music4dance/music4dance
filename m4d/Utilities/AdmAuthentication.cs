using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Web;
using m4dModels;

namespace m4d.Utilities
{
    [DataContract]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class AccessToken
    {
        [DataMember]
        public string access_token { get; set; }
        [DataMember]
        public string token_type { get; set; }
        [DataMember]
        public int expires_in { get; set; }

        public virtual TimeSpan ExpiresIn => TimeSpan.FromSeconds(expires_in - 60);
    }

    public abstract class CoreAuthentication
    {
        protected abstract string Client { get; }
        public string ClientId => _clientId ?? (_clientId = Environment.GetEnvironmentVariable(Client + "-client-id"));
        public string ClientSecret => _clientSecret ?? (_clientSecret = Environment.GetEnvironmentVariable(Client + "-client-secret"));

        private string _clientId;
        private string _clientSecret;
    }

    public class EnvAuthentication : CoreAuthentication
    {
        public EnvAuthentication(string client)
        {
            Client = client;
        }

        protected override string Client { get; }
    }

    public abstract class AdmAuthentication : CoreAuthentication, IDisposable
    {
        protected abstract string RequestFormat { get; }
        protected virtual string RequestExtra => string.Empty;
        protected abstract string RequestUrl { get; }

        protected Timer AccessTokenRenewer { get; set; }

        public AccessToken GetAccessToken()
        {
            lock (this)
            {
                if (Token != null) 
                    return Token;

                Token = CreateToken();
                if (AccessTokenRenewer == null)
                    AccessTokenRenewer = new Timer(OnTokenExpiredCallback, this, Token.ExpiresIn, Token.ExpiresIn);

                return Token;
            }
        }

        public string GetAccessString()
        {
            var token = GetAccessToken();
            return "Bearer " + token.access_token;
        }

        protected void OnTokenExpiredCallback(object stateInfo)
        {
            Token = null;
        }

        private string _request;
        protected AccessToken Token { get; set; }

        private AccessToken CreateToken()
        {
            //Prepare OAuth request 
            var webRequest = WebRequest.Create(RequestUrl);
            webRequest.ContentType = "application/x-www-form-urlencoded";
            webRequest.Method = "POST";
            var request = _request ?? (_request = string.Format(RequestFormat, Uri.EscapeDataString(ClientId), Uri.EscapeDataString(ClientSecret))) + RequestExtra;
            var bytes = Encoding.ASCII.GetBytes(request);
            webRequest.ContentLength = bytes.Length;
            using (var outputStream = webRequest.GetRequestStream())
            {
                outputStream.Write(bytes, 0, bytes.Length);
            }
            try
            {
                using (var webResponse = webRequest.GetResponse())
                {
                    var serializer = new DataContractJsonSerializer(typeof (AccessToken));
                    //Get deserialized object from JSON stream
                    return (AccessToken)serializer.ReadObject(webResponse.GetResponseStream());
                }
            }
            catch (Exception e)
            {
                Trace.WriteLine(e.Message);
                throw;
            }
        }

        public void Dispose()
        {
            AccessTokenRenewer.Dispose();
        }

        public static string GetServiceAuthorization(ServiceType serviceType, IPrincipal principal = null)
        {
            AdmAuthentication auth = null;

            if (principal != null)
            {
                var userName = principal.Identity.Name;
                if (s_users.TryGetValue(userName, out auth))
                {
                    return auth.GetAccessString();
                }

                if (serviceType == ServiceType.Spotify)
                {
                    auth = SpotUserAuthentication.TryCreate(principal);
                    if (auth != null)
                    {
                        s_users[userName] = auth;
                        return auth.GetAccessString();
                    }
                }
            }

            switch (serviceType)
            {
                case ServiceType.XBox:
                    auth = s_xbox ?? (s_xbox = new XboxAuthentication());
                    break;
                case ServiceType.Spotify:
                    auth = s_spotify ?? (s_spotify = new SpotAuthentication());
                    break;
            }

            return auth?.GetAccessString();
        }

        private static AdmAuthentication s_xbox;
        private static AdmAuthentication s_spotify;

        private static readonly Dictionary<string, AdmAuthentication> s_users = new Dictionary<string, AdmAuthentication>();
    }
}