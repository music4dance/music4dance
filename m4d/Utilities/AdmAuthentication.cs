using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Net;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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
            ClientId = configuration[$"Authentication:{Client}:ClientId"];
            ClientSecret = configuration[$"Authentication:{Client}:ClientSecret"];
        }

        protected abstract string Client { get; }
        public string ClientId { get; }
        public string ClientSecret { get; }
    }

    public abstract class AdmAuthentication : CoreAuthentication, IDisposable
    {
        protected AdmAuthentication(IConfiguration configuration) : base(configuration)
        {
        }

        protected abstract string RequestFormat { get; }
        protected virtual string RequestExtra => string.Empty;
        protected abstract string RequestUrl { get; }

        protected Timer AccessTokenRenewer { get; set; }

        public async Task<AccessToken> GetAccessToken()
        {
            if (Token != null)
            {
                return Token;
            }

            Token = await CreateToken();
            if (AccessTokenRenewer == null)
            {
                AccessTokenRenewer = new Timer(
                    OnTokenExpiredCallback, this, Token.ExpiresIn,
                    Token.ExpiresIn);
            }

            return Token;
        }

        public async Task<string> GetAccessString()
        {
            var token = await GetAccessToken();
            return "Bearer " + token.access_token;
        }

        protected void OnTokenExpiredCallback(object stateInfo)
        {
            Token = null;
        }

        private string _request;
        protected AccessToken Token { get; set; }

        private async Task<AccessToken> CreateToken()
        {
            //Prepare OAuth request 
            using var webRequest = new HttpRequestMessage(HttpMethod.Post, RequestUrl);
            //webRequest.Headers.Add("ContentType", "application/x-www-form-urlencoded");

            var svcCredentials =
                Convert.ToBase64String(Encoding.ASCII.GetBytes(ClientId + ":" + ClientSecret));
            webRequest.Headers.Add("Authorization", "Basic " + svcCredentials);

            var request = _request ?? (_request = string.Format(
                RequestFormat,
                Uri.EscapeDataString(ClientId), Uri.EscapeDataString(ClientSecret))) + RequestExtra;
            webRequest.Content = new StringContent(request, Encoding.UTF8, "application/x-www-form-urlencoded");
            var bytes = Encoding.ASCII.GetBytes(request);

            try
            {
                using var webResponse = await HttpClientHelper.Client.SendAsync(webRequest);
                var serializer = new DataContractJsonSerializer(typeof(AccessToken));
                //Get deserialized object from JSON stream
                return (AccessToken)serializer.ReadObject(await webResponse.Content.ReadAsStreamAsync());
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

        public static bool HasAccess(IConfiguration configuration,
            ServiceType serviceType, IPrincipal principal = null,
            AuthenticateResult authResult = null)
        {
            var service = SetupService(configuration, serviceType, principal, authResult);
            if (service == null)
            {
                return false;
            }
            return service as SpotUserAuthentication != null;
        }

        public static async Task<string> GetServiceAuthorization(IConfiguration configuration,
            ServiceType serviceType, IPrincipal principal = null,
            AuthenticateResult authResult = null)
        {
            var service = SetupService(configuration, serviceType, principal, authResult);
            return service == null ? null : await service.GetAccessString();
        }

        private static AdmAuthentication SetupService(IConfiguration configuration,
            ServiceType serviceType, IPrincipal principal = null,
            AuthenticateResult authResult = null)
        {
            AdmAuthentication auth = null;

            if (principal != null && principal.Identity.IsAuthenticated &&
                !string.IsNullOrWhiteSpace(principal.Identity.Name))
            {
                var userName = principal.Identity.Name;
                if (s_users.TryGetValue(userName, out auth))
                {
                    return auth;
                }

                if (authResult?.Properties != null)
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

        public static AdmAuthentication TryCreate(IConfiguration configuration,
            ServiceType serviceType, AuthenticateResult authResult)
        {
            var accessToken = authResult.Properties.GetTokenValue("access_token");
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                return null;
            }

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
            {
                return null;
            }

            AdmAuthentication auth = null;
            if (serviceType == ServiceType.Spotify)
            {
                auth = new SpotUserAuthentication(configuration)
                {
                    RefreshToken = refreshToken
                };
            }
            else
            {
                return null;
            }

            // TODO: Figure out if there is a way to get the expiresPeriod programmatically
            var expirePeriod = new TimeSpan(0, 59, 0);
            auth.Token = token;
            if (expiresIn.TotalSeconds < 0)
            {
                expiresIn = new TimeSpan(0);
                auth.Token = null;
            }

            auth.AccessTokenRenewer =
                new Timer(auth.OnTokenExpiredCallback, auth, expiresIn, expirePeriod);

            return auth;
        }

        protected string RefreshToken;


        private static AdmAuthentication s_spotify;

        private static readonly Dictionary<string, AdmAuthentication> s_users = new();
    }
}
