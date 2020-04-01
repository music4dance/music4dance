using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Net;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Principal;
using System.Text;
using System.Threading;
using m4dModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;

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
        protected CoreAuthentication(IConfiguration configuration)
        {
            ClientId = configuration["Authentication:{Client}:ClientId"];
            ClientSecret = configuration["Authentication:{Client}:ClientSecret"];
        }

        protected abstract string Client { get; }
        public string ClientId { get; }
        public string ClientSecret { get; }
    }

    public abstract class AdmAuthentication : CoreAuthentication, IDisposable
    {
        protected AdmAuthentication(IConfiguration configuration) : base(configuration) { }

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

            var svcCredentials = Convert.ToBase64String(Encoding.ASCII.GetBytes(ClientId + ":" + ClientSecret));
            webRequest.Headers.Add("Authorization", "Basic " + svcCredentials);

            var request = _request ?? (_request = string.Format(RequestFormat, Uri.EscapeDataString(ClientId), Uri.EscapeDataString(ClientSecret))) + RequestExtra;
            var bytes = Encoding.ASCII.GetBytes(request);
            webRequest.ContentLength = bytes.Length;
            using (var outputStream = webRequest.GetRequestStream())
            {
                outputStream.Write(bytes, 0, bytes.Length);
            }
            try
            {
                using var webResponse = webRequest.GetResponse();
                var serializer = new DataContractJsonSerializer(typeof (AccessToken));
                //Get deserialized object from JSON stream
                return (AccessToken)serializer.ReadObject(webResponse.GetResponseStream());
            }
            catch (Exception e)
            {
                Trace.WriteLine(e.Message);
                throw;
            }
        }

        protected virtual string GetServiceId(IPrincipal principal)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            AccessTokenRenewer.Dispose();
        }

        public static string GetServiceAuthorization(IConfiguration configuration, ServiceType serviceType, IPrincipal principal = null, AuthenticateResult authResult = null)
        {
            return SetupService(configuration, serviceType, principal, authResult)?.GetAccessString();
        }

        private static AdmAuthentication SetupService(IConfiguration configuration, ServiceType serviceType, IPrincipal principal = null, AuthenticateResult authResult = null)
        {
            AdmAuthentication auth = null;

            if (principal != null)
            {
                var userName = principal.Identity.Name;
                if (s_users.TryGetValue(userName, out auth))
                {
                    return auth;
                }

                if (authResult != null)
                {
                    auth = TryCreate(configuration, serviceType, authResult);
                    if (auth != null)
                    {
                        s_users[userName] = auth;
                        return auth;
                    }
                }

            }

            switch (serviceType)
            {
                case ServiceType.Spotify:
                    auth = s_spotify ??= new SpotAuthentication(configuration);
                    break;
            }

            return auth;
        }

        public static AdmAuthentication TryCreate(IConfiguration configuration, ServiceType serviceType, AuthenticateResult authResult)
        {
            var accessToken = authResult.Properties.GetTokenValue("access_token");
            var now = DateTime.Now;
            var expiresAt = authResult.Properties.GetTokenValue("expires_at");
            var expiresIn = DateTime.Parse(expiresAt) - now;

            var token = new AccessToken
            {
                access_token = accessToken,
                expires_in = (int)expiresIn.TotalSeconds
            };

            var refreshToken = authResult.Properties.GetTokenValue("refresh_token");

            if (refreshToken == null)
                return null;

            AdmAuthentication auth = null;
            if  (serviceType == ServiceType.Spotify)
            {
                auth = new SpotUserAuthentication(configuration)
                {
                    RefreshToken = refreshToken,
                };
            }
            else
            {
                return null;
            }

            // TODO: Figure out if there is a way to get the expiresPeriod programmatically
            var expirePeriod = new TimeSpan(0, 59, 0);
            auth.Token = token;
            auth.AccessTokenRenewer = new Timer(auth.OnTokenExpiredCallback, auth, token.ExpiresIn, expirePeriod);

            return auth;
        }

        protected string RefreshToken;


        private static AdmAuthentication s_spotify;

        private static readonly Dictionary<string, AdmAuthentication> s_users = new Dictionary<string, AdmAuthentication>();
    }
}