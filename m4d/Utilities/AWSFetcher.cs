using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;
using System.ServiceModel.Dispatcher;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;
using m4d.AWSReference;
using m4dModels;
//using AWSReference.com.amazonaws.ecs;

namespace m4d.Utilities
{
    public class AmazonHeader : MessageHeader
    {
        private readonly string _value;

        public AmazonHeader(string name, string value)
        {
            Name = name;
            _value = value;
        }

        public override string Name { get; }

        public override string Namespace => "http://security.amazonaws.com/doc/2007-01-01/";

        protected override void OnWriteHeaderContents(XmlDictionaryWriter xmlDictionaryWriter, MessageVersion messageVersion)
        {
            xmlDictionaryWriter.WriteString(_value);
        }
    }
 

    public class AmazonSigningMessageInspector : IClientMessageInspector
    {
        private readonly string _accessKeyId;
        private readonly string _secretKey;

        public AmazonSigningMessageInspector(string accessKeyId, string secretKey)
        {
            _accessKeyId = accessKeyId;
            _secretKey = secretKey;
        }

        public object BeforeSendRequest(ref Message request, IClientChannel channel)
        {
            // prepare the data to sign
            var operation = Regex.Match(request.Headers.Action, "[^/]+$").ToString();

            var now = DateTime.UtcNow;
            var timestamp = now.ToString("yyyy-MM-ddTHH:mm:ssZ");
            var signMe = operation + timestamp;
            var bytesToSign = Encoding.UTF8.GetBytes(signMe);

            // sign the data
            var secretKeyBytes = Encoding.UTF8.GetBytes(_secretKey);
            HMAC hmacSha256 = new HMACSHA256(secretKeyBytes);
            var hashBytes = hmacSha256.ComputeHash(bytesToSign);
            var signature = Convert.ToBase64String(hashBytes);

            // add the signature information to the request headers
            request.Headers.Add(new AmazonHeader("AWSAccessKeyId", _accessKeyId));
            request.Headers.Add(new AmazonHeader("Timestamp", timestamp));
            request.Headers.Add(new AmazonHeader("Signature", signature));

            return null;
        }

        public void AfterReceiveReply(ref Message reply, object correlationState)
        {
        }

    }
 

    public class AmazonSigningEndpointBehavior : IEndpointBehavior
    {
        private readonly string _accessKeyId;
        private readonly string _secretKey;

        public AmazonSigningEndpointBehavior(string accessKeyId, string secretKey)
        {
            _accessKeyId = accessKeyId;
            _secretKey = secretKey;
        }

        #region IEndpointBehavior Members
        public void ApplyClientBehavior(ServiceEndpoint serviceEndpoint, ClientRuntime clientRuntime)
        {
            clientRuntime.MessageInspectors.Add(new AmazonSigningMessageInspector(_accessKeyId, _secretKey));
        }

        public void ApplyDispatchBehavior(ServiceEndpoint serviceEndpoint, EndpointDispatcher endpointDispatcher)
        {
        }

        public void Validate(ServiceEndpoint serviceEndpoint)
        {
        }

        public void AddBindingParameters(ServiceEndpoint serviceEndpoint, BindingParameterCollection bindingParameters)
        {
        }
        #endregion
    }


    [SuppressMessage("Microsoft.Design", "CA1001:TypesThatOwnDisposableFieldsShouldBeDisposable")]
    public class AWSFetcher : CoreAuthentication, IDisposable
    {
        protected override string Client => "amazon";
        private const string AssociateTag = "ms4dc-20";
        private const string EndPointAddress = "https://webservices.amazon.com/onca/soap?Service=AWSECommerceService";
        //"https://webservices.amazon.fr/onca/soap?Service=AWSECommerceService"

        readonly AWSECommerceServicePortTypeClient _client;

        public AWSFetcher()
        {
            // create a WCF Amazon ECS client
            var binding = new BasicHttpBinding(BasicHttpSecurityMode.Transport) {MaxReceivedMessageSize = int.MaxValue};
            _client = new AWSECommerceServicePortTypeClient(binding, new EndpointAddress(EndPointAddress));

            // add authentication to the ECS client
            _client.ChannelFactory.Endpoint.Behaviors.Add(new AmazonSigningEndpointBehavior(ClientId, ClientSecret));
        }

        public IList<ServiceTrack> FetchTracks(string title, string artist)
        {
            return DoFetchTracks(null, false, title, artist);
        }

        public IList<ServiceTrack> FetchTracks(Song song, bool clean = false)
        {
            return DoFetchTracks(song, clean);
        }

        public ServiceTrack LookupTrack(string asin)
        {
            if (asin.StartsWith("D:")) asin = asin.Substring(2);
            var response = DoLookupTrack(asin);

            if (response == null)
            {
                Trace.WriteLine(asin + ": Invalid Search");
                return null;
            }

            if (response.Items[0].Request.Errors != null)
            {
                Trace.WriteLine(asin + ":" + response.Items[0].Request.Errors[0].Message);
                return null;
            }

            if (response.Items[0].Item.Length != 0)
            {
                return BuildServiceTrack(response.Items[0].Item[0]);
            }

            Trace.WriteLine(asin + ": No Tracks Returned");
            return null;
        }

        private IList<ServiceTrack> DoFetchTracks(Song song, bool clean = false, string title = null, string artist = null)
        {
            var tracks = new List<ServiceTrack>();

            try {

                if (song != null)
                {
                    if (title == null)
                    {
                        title = song.Title;
                    }
                    if (artist == null)
                    {
                        artist = song.Artist;
                    }

                    if (clean)
                    {
                        title = song.CleanTitle;
                        artist = song.CleanArtist;
                    }
                }


                var response = FindTrack(title, artist);

                if (response == null)
                {
                    Trace.WriteLine(title + ": Invalid Search");
                    return tracks;
                }

                if (response.Items[0].Request.Errors != null)
                {
                    Trace.WriteLine(title + ":" + response.Items[0].Request.Errors[0].Message);
                    return tracks;
                }

                foreach (var item in response.Items[0].Item)
                {
                    var track = BuildServiceTrack(item);

                    if (song == null || song.TitleArtistMatch(track.Name, track.Artist))
                    {
                        tracks.Add(track);

                        // If we have an exact match break...
                        if (song?.FindAlbum(track.Album) != null)
                        {
                            break;
                        }
                    }
                }

                return tracks;
            }
            catch (Exception e)
            {
                Trace.WriteLine(e.Message);
                return tracks;
            }
        }

        private ServiceTrack BuildServiceTrack(Item item)
        {
            string artist = null;
            var title = item.ItemAttributes.Title;

            if (item.ItemAttributes.Creator != null && item.ItemAttributes.Creator.Length > 0)
            {
                artist = item.ItemAttributes.Creator[0].Value;
            }

            int trackNum;
            int? ntrackNum = null;

            if (int.TryParse(item.ItemAttributes.TrackSequence, out trackNum))
            {
                ntrackNum = trackNum;
            }

            int? duration = null;
            if (item.ItemAttributes.RunningTime != null && 
                string.Equals(item.ItemAttributes.RunningTime.Units,"seconds",StringComparison.InvariantCultureIgnoreCase))
            {
                duration = (int) decimal.Round(item.ItemAttributes.RunningTime.Value);
            }

            string collectionId = null;
            string albumTitle = null;
            if (item.RelatedItems != null && item.RelatedItems.Length > 0 &&
                item.RelatedItems[0].RelatedItem != null && item.RelatedItems[0].RelatedItem.Length > 0)
            {
                collectionId = item.RelatedItems[0].RelatedItem[0].Item.ASIN;
                if (item.RelatedItems[0].RelatedItem[0].Item.ItemAttributes != null)
                {
                    albumTitle = item.RelatedItems[0].RelatedItem[0].Item.ItemAttributes.Title;
                }
            }

            var genre = item.ItemAttributes.Genre;

            var gidx = -1;
            if (genre != null) gidx = genre.IndexOf("-music", StringComparison.OrdinalIgnoreCase);

            if (gidx != -1)
            {
                genre = genre?.Remove(gidx);
            }
            var track = new ServiceTrack 
            {
                Service = ServiceType.Amazon,
                TrackId = "D:" + item.ASIN,
                Name = title,
                Artist = artist,
                TrackNumber = ntrackNum,
                Duration = duration,
                ReleaseDate = item.ItemAttributes.ReleaseDate,
                Genres = new [] { genre },
                CollectionId = "D:" + collectionId,
                Album = albumTitle
            };

            return track;
        }

        private ItemSearchResponse FindTrack(string title, string artist)
        {
            var log = $"FindTrack - {DateTime.Now} - Title: '{title}' Artist:'{artist}'";
            Trace.WriteLine(log);

            var request = new ItemSearchRequest
            {
                SearchIndex = "DigitalMusic",
                Title = title,
                Keywords = string.IsNullOrEmpty(artist) ? title : artist,
                RelationshipType = new[] {"Tracks"},
                ResponseGroup = new[] {"ItemAttributes", "RelatedItems"}
            };

            var itemSearch = new ItemSearch
            {
                AssociateTag = AssociateTag,
                AWSAccessKeyId = ClientId,
                Request = new[] {request}
            };

            ItemSearchResponse r = null;
            // TODO: Amazon is throttling in a big way, may have to give up on them or figure out a way to 
            //  handle them in the background.  Is there a way to figure out that they're in full throttling
            //  mode and stop using them for a while?
            //while (r == null)
            //{
                try
                {
                    r = _client.ItemSearch(itemSearch);
                }
                catch (ServerTooBusyException e)
                {
                    Trace.WriteLine("FindTrack: " + e.Message);
                    Thread.Sleep(5000);
                }
            //}
            return r;
        }

        private ItemLookupResponse DoLookupTrack(string asin)
        {
            var request = new ItemLookupRequest
            {
                ItemId = new[] {asin},
                RelationshipType = new[] {"Tracks"},
                ResponseGroup = new[] {"ItemAttributes", "RelatedItems"}
            };

            var itemLookup = new ItemLookup
            {
                AssociateTag = AssociateTag,
                AWSAccessKeyId = ClientId,
                Request = new[] {request}
            };

            ItemLookupResponse r = null;
            while (r == null)
            {
                try
                {
                    r = _client.ItemLookup(itemLookup);
                }
                catch (ServerTooBusyException e)
                {
                    Trace.WriteLine("LookupTrack: " + e.Message);
                    Thread.Sleep(5000);
                }
            }
            return r;
        }

        public void Dispose()
        {
            _client.Close();
        }
    }
}

namespace m4d.AWSReference
{
    public partial class ImageSet
    {
        public static implicit operator ImageSet[](ImageSet i)
        {
            return new[] { i };
        }

        public static implicit operator ImageSet(ImageSet[] i)
        {
            if (i != null && i.Length >= 1)
            {
                return i[0];
            }
            return new ImageSet();
        }
    }

}