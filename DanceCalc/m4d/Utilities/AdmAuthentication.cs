using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Web;
using m4dModels;

namespace m4d.Utilities
{
    [DataContract]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public abstract class AccessToken
    {
        [DataMember]
        public string access_token { get; set; }
        [DataMember]
        public string token_type { get; set; }
        public abstract TimeSpan ExpiresIn { get; }
    }

    [DataContract]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class XboxAccessToken : AccessToken
    {
        [DataMember]
        public string expires_in { get; set; }
        [DataMember]
        public string scope { get; set; }

        public override TimeSpan ExpiresIn
        {
            get { return TimeSpan.FromMinutes(9); }
        }
    }

    public abstract class AdmAuthentication : IDisposable
    {
        protected abstract string ClientId { get; }
        protected abstract string ClientSecret { get; }
        protected abstract string RequestFormat { get; }
        protected abstract string RequestUrl { get; }
        protected abstract Type AccessTokenType { get; } 

        protected Timer AccessTokenRenewer { get; set; }

        public AccessToken GetAccessToken()
        {
            lock (this)
            {
                if (_token == null)
                {
                    _token = CreateToken();
                    if (AccessTokenRenewer == null)
                    {
                        AccessTokenRenewer = new Timer(OnTokenExpiredCallback, this, _token.ExpiresIn, TimeSpan.FromMilliseconds(-1));
                    }
                }
                return _token;                
            }
        }

        public string GetAccessString()
        {
            var token = GetAccessToken();
            return "Bearer " + token.access_token;
        }

        private void OnTokenExpiredCallback(object stateInfo)
        {
            _token = null;
        }

        private string _request;
        private AccessToken _token;

        private AccessToken CreateToken()
        {
            //Prepare OAuth request 
            var webRequest = WebRequest.Create(RequestUrl);
            webRequest.ContentType = "application/x-www-form-urlencoded";
            webRequest.Method = "POST";
            string request = _request ?? (_request = string.Format(RequestFormat, HttpUtility.UrlEncode(ClientId), HttpUtility.UrlEncode(ClientSecret)));
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
                    var serializer = new DataContractJsonSerializer(AccessTokenType);
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

        public static string GetServiceAuthorization(ServiceType serviceType)
        {
            AdmAuthentication auth = null;
            switch (serviceType)
            {
                case ServiceType.XBox:
                    auth = s_xbox ?? (s_xbox = new XboxAuthentication());
                    break;
                case ServiceType.Spotify:
                    auth = s_spotify ?? (s_spotify = new SpotAuthentication());
                    break;
            }

            return (auth == null) ? null : auth.GetAccessString();
        }

        private static AdmAuthentication s_xbox = null;
        private static AdmAuthentication s_spotify = null;

    }
}