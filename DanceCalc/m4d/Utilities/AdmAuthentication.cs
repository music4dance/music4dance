using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Web;

namespace m4d.Utilities
{
    [DataContract]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class AdmAccessToken
    {
        [DataMember]
        public string access_token { get; set; }
        [DataMember]
        public string token_type { get; set; }
        [DataMember]
        public string expires_in { get; set; }
        [DataMember]
        public string scope { get; set; }
    }

    public class AdmAuthentication : IDisposable
    {
        private const string DatamarketAccessUri = "https://datamarket.accesscontrol.windows.net/v2/OAuth2-13";
        private readonly string _clientId;
        private readonly string _request;
        private AdmAccessToken _token;
        private readonly Timer _accessTokenRenewer;

        //Access token expires every 10 minutes. Renew it every 9 minutes only.
        private const int RefreshTokenDuration = 9;

        public AdmAuthentication(string clientId, string clientSecret)
        {
            _clientId = clientId;
            //If clientid or client secret has special characters, encode before sending request
            _request = string.Format("grant_type=client_credentials&client_id={0}&client_secret={1}&scope=http://music.xboxlive.com", HttpUtility.UrlEncode(clientId), HttpUtility.UrlEncode(clientSecret));
            _token = HttpPost(DatamarketAccessUri, _request);
            //renew the token every specfied minutes
            _accessTokenRenewer = new Timer(OnTokenExpiredCallback, this, TimeSpan.FromMinutes(RefreshTokenDuration), TimeSpan.FromMilliseconds(-1));
        }

        public AdmAccessToken GetAccessToken()
        {
            return _token;
        }


        private void RenewAccessToken()
        {
            var newAccessToken = HttpPost(DatamarketAccessUri, _request);
            //swap the new token with old one
            //Note: the swap is thread unsafe
            _token = newAccessToken;
            Console.WriteLine(@"Renewed token for user: {0} is: {1}", this._clientId, this._token.access_token);
        }

        private void OnTokenExpiredCallback(object stateInfo)
        {
            try
            {
                RenewAccessToken();
            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Format("Failed renewing access token. Details: {0}", ex.Message));
            }
            finally
            {
                try
                {
                    _accessTokenRenewer.Change(TimeSpan.FromMinutes(RefreshTokenDuration), TimeSpan.FromMilliseconds(-1));
                }
                catch (Exception ex)
                {
                    Console.WriteLine(string.Format("Failed to reschedule the timer to renew access token. Details: {0}", ex.Message));
                }
            }
        }


        private static AdmAccessToken HttpPost(string datamarketAccessUri, string requestDetails)
        {
            //Prepare OAuth request 
            var webRequest = WebRequest.Create(datamarketAccessUri);
            webRequest.ContentType = "application/x-www-form-urlencoded";
            webRequest.Method = "POST";
            var bytes = Encoding.ASCII.GetBytes(requestDetails);
            webRequest.ContentLength = bytes.Length;
            using (var outputStream = webRequest.GetRequestStream())
            {
                outputStream.Write(bytes, 0, bytes.Length);
            }
            using (var webResponse = webRequest.GetResponse())
            {
                var serializer = new DataContractJsonSerializer(typeof(AdmAccessToken));
                //Get deserialized object from JSON stream
                return (AdmAccessToken)serializer.ReadObject(webResponse.GetResponseStream());
            }
        }

        public void Dispose()
        {
            _accessTokenRenewer.Dispose();
            //GC.SuppressFinalize(this);
        }
    }

}