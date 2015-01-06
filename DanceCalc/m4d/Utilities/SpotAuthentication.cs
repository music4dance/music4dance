using System;
using System.Diagnostics;
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
    public class SpotAccessToken
    {
        [DataMember]
        public string access_token { get; set; }
        [DataMember]
        public string token_type { get; set; }
        [DataMember]
        public int expires_in { get; set; }
    }

    public class SpotAuthentication : IDisposable
    {
        //private readonly string _clientId;
        //private readonly string _clientSecret;
        private readonly string _request;
        private SpotAccessToken _token;
        private readonly Timer _accessTokenRenewer;


        public SpotAuthentication(string clientId, string clientSecret)
        {
            //_clientId = clientId;
            //_clientSecret = clientSecret;
            _request = string.Format("grant_type=client_credentials&client_id={0}&client_secret={1}",clientId,clientSecret);

            _token = CreateToken();

            //renew the token every specfied minutes
            _accessTokenRenewer = new Timer(OnTokenExpiredCallback, this, TimeSpan.FromSeconds(_token.expires_in - 60), TimeSpan.FromMilliseconds(-1));            
        }

        public SpotAccessToken GetAccessToken()
        {
            lock (this)
            {
                return _token ?? (_token = CreateToken());
            }            
        }

        private void OnTokenExpiredCallback(object stateInfo)
        {
            _token = null;
        }

        private SpotAccessToken CreateToken()
        {
            //Prepare OAuth request 
            var webRequest = WebRequest.Create("https://accounts.spotify.com/api/token");
            webRequest.ContentType = "application/x-www-form-urlencoded";
            webRequest.Method = "POST";
            //var key = string.Format("Basic <{0}:{1}>", _clientId, _clientSecret);
            //webRequest.Headers.Add("Authorization", key);                
            var bytes = Encoding.ASCII.GetBytes(_request);
            webRequest.ContentLength = bytes.Length;
            using (var outputStream = webRequest.GetRequestStream())
            {
                outputStream.Write(bytes, 0, bytes.Length);
            }
            try
            {
                using (var webResponse = webRequest.GetResponse())
                {
                    var serializer = new DataContractJsonSerializer(typeof(SpotAccessToken));
                    //Get deserialized object from JSON stream
                    return (SpotAccessToken)serializer.ReadObject(webResponse.GetResponseStream());
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
            _accessTokenRenewer.Dispose();
        }
    }

}