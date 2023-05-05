using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using m4dModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

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
        public string scope { get; set; }

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

        protected static readonly ILogger Logger = ApplicationLogging.CreateLogger<AdmAuthentication>();

        public async Task<AccessToken> GetAccessToken()
        {
            if (Token != null)
            {
                return Token;
            }

            Token = await CreateToken();
            AccessTokenRenewer ??= new Timer(
                OnTokenExpiredCallback, this, Token.ExpiresIn,
                Token.ExpiresIn);

            return Token;
        }

        public async Task<string> GetAccessString()
        {
            var token = await GetAccessToken();
            return "Bearer " + token.access_token;
        }

        protected void OnTokenExpiredCallback(object stateInfo)
        {
            Logger.LogInformation("Disposing Access Token");
            var renewer = AccessTokenRenewer;
            AccessTokenRenewer = null;
            Token = null;
            renewer.Dispose();
        }

        private string _request;
        protected AccessToken Token { get; set; }

        private async Task<AccessToken> CreateToken()
        {
            Logger.LogInformation("Creating Access Token");

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

            try
            {
                using var webResponse = await HttpClientHelper.Client.SendAsync(webRequest);
                var serializer = new DataContractJsonSerializer(typeof(AccessToken));
                //Get deserialized object from JSON stream
                var token = (AccessToken)serializer.ReadObject(await webResponse.Content.ReadAsStreamAsync());
                if (string.IsNullOrWhiteSpace(token.access_token))
                {
                    Logger.LogError("Failed to create Token (null token)");
                }
                return token;
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Failed to create Token");
                throw;
            }
        }

        protected virtual string GetServiceId(IPrincipal principal)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            AccessTokenRenewer?.Dispose();
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
            return service is SpotUserAuthentication;
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
            if (authResult == null || authResult.Properties == null)
            {
                return null;
            }
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
                Logger.LogWarning("Failed to fetch a refresh token");
                return null;
            }

            AdmAuthentication auth;
            if (serviceType == ServiceType.Spotify)
            {
                Logger.LogInformation("Creating Spotify User Token");
                auth = new SpotUserAuthentication(configuration)
                {
                    RefreshToken = refreshToken
                };
            }
            else
            {
                return null;
            }

            auth.Token = token;
            if (expiresIn.TotalSeconds < 0)
            {
                Logger.LogWarning("Refresh token expired during create");
                expiresIn = new TimeSpan(0);
                auth.Token = null;
            }

            auth.AccessTokenRenewer =
                new Timer(auth.OnTokenExpiredCallback, auth, expiresIn, Timeout.InfiniteTimeSpan);

            return auth;
        }

        public static void Clear()
        {
            s_spotify = null;
            s_users.Clear();
        }

        protected string RefreshToken;

        private static AdmAuthentication s_spotify;

        private static readonly Dictionary<string, AdmAuthentication> s_users = new();
    }
}
