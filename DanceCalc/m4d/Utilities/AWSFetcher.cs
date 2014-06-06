using m4dModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

//using AWSReference.com.amazonaws.ecs;
using m4d.AWSReference;
using System.Diagnostics;
using System.Threading.Tasks;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.ServiceModel.Channels;
using System.ServiceModel.Dispatcher;
using System.Xml;
using System.Text.RegularExpressions;
using System.Text;
using System.Security.Cryptography;

namespace m4d.Utilities
{
    public class AmazonHeader : MessageHeader
    {

        private readonly string name;
        private readonly string value;

        public AmazonHeader(string name, string value)
        {
            this.name = name;
            this.value = value;
        }

        public override string Name
        {
            get { return name; }
        }

        public override string Namespace
        {
            get { return "http://security.amazonaws.com/doc/2007-01-01/"; }
        }

        protected override void OnWriteHeaderContents(XmlDictionaryWriter xmlDictionaryWriter, MessageVersion messageVersion)
        {
            xmlDictionaryWriter.WriteString(value);
        }
    }
 

    public class AmazonSigningMessageInspector : IClientMessageInspector
    {
        private readonly string accessKeyId;
        private readonly string secretKey;

        public AmazonSigningMessageInspector(string accessKeyId, string secretKey)
        {
            this.accessKeyId = accessKeyId;
            this.secretKey = secretKey;
        }

        public object BeforeSendRequest(ref Message request, IClientChannel channel)
        {
            // prepare the data to sign
            string operation = Regex.Match(request.Headers.Action, "[^/]+$").ToString();

            DateTime now = DateTime.UtcNow;
            string timestamp = now.ToString("yyyy-MM-ddTHH:mm:ssZ");
            string signMe = operation + timestamp;
            byte[] bytesToSign = Encoding.UTF8.GetBytes(signMe);

            // sign the data
            byte[] secretKeyBytes = Encoding.UTF8.GetBytes(secretKey);
            HMAC hmacSha256 = new HMACSHA256(secretKeyBytes);
            byte[] hashBytes = hmacSha256.ComputeHash(bytesToSign);
            string signature = Convert.ToBase64String(hashBytes);

            // add the signature information to the request headers
            request.Headers.Add(new AmazonHeader("AWSAccessKeyId", accessKeyId));
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
        private readonly string accessKeyId;
        private readonly string secretKey;

        public AmazonSigningEndpointBehavior(string accessKeyId, string secretKey)
        {
            this.accessKeyId = accessKeyId;
            this.secretKey = secretKey;
        }

        #region IEndpointBehavior Members
        public void ApplyClientBehavior(ServiceEndpoint serviceEndpoint, ClientRuntime clientRuntime)
        {
            clientRuntime.MessageInspectors.Add(new AmazonSigningMessageInspector(accessKeyId, secretKey));
        }

        public void ApplyDispatchBehavior(ServiceEndpoint serviceEndpoint, EndpointDispatcher endpointDispatcher)
        {
            return;
        }

        public void Validate(ServiceEndpoint serviceEndpoint)
        {
            return;
        }

        public void AddBindingParameters(ServiceEndpoint serviceEndpoint, BindingParameterCollection bindingParameters)
        {
            return;
        }
        #endregion
    }


    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1001:TypesThatOwnDisposableFieldsShouldBeDisposable")]
    public class AWSFetcher : IDisposable
    {
        private const string accessKeyId = "***REMOVED***";
        private const string secretKeyId = "***REMOVED***";
        private const string associateTag = "music4dance-20";
        private const string endPointAddress = "https://webservices.amazon.com/onca/soap?Service=AWSECommerceService";
        //"https://webservices.amazon.fr/onca/soap?Service=AWSECommerceService"

        BasicHttpBinding _binding;
        AWSECommerceServicePortTypeClient _client;

        public AWSFetcher()
        {
            // create a WCF Amazon ECS client
            _binding = new BasicHttpBinding(BasicHttpSecurityMode.Transport);
            _binding.MaxReceivedMessageSize = int.MaxValue;
            _client = new AWSECommerceServicePortTypeClient(_binding, new EndpointAddress(endPointAddress));

            // add authentication to the ECS client
            _client.ChannelFactory.Endpoint.Behaviors.Add(new AmazonSigningEndpointBehavior(accessKeyId, secretKeyId));

        }

        public IList<ServiceTrack> FetchTracks(string title, string artist)
        {
            return DoFetchTracks(null, false, title, artist);
        }

        public IList<ServiceTrack> FetchTracks(SongDetails song, bool clean = false)
        {
            return DoFetchTracks(song, clean);
        }

        private IList<ServiceTrack> DoFetchTracks(SongDetails song, bool clean = false, string title = null, string artist = null)
        {
            List<ServiceTrack> tracks = new List<ServiceTrack>();

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


                ItemSearchResponse response = FindTrack(title, artist);

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
                    artist = null;
                    title = item.ItemAttributes.Title;

                    if (item.ItemAttributes.Creator != null && item.ItemAttributes.Creator.Length > 0)
                    {
                        artist = item.ItemAttributes.Creator[0].Value;
                    }

                    if (song == null || song.TitleArtistMatch(title, artist))
                    {
                        int trackNum = 0;
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

                        string trackId = item.ASIN;
                        ServiceTrack track = new ServiceTrack 
                        {
                            TrackId = "D:" + item.ASIN,
                            Name = title,
                            Artist = artist,
                            TrackNumber = ntrackNum,
                            Duration = duration,
                            ReleaseDate = item.ItemAttributes.ReleaseDate,
                            CollectionId = "D:" + collectionId,
                            Album = albumTitle
                        };

                        tracks.Add(track);

                        // If we have an exact match break...
                        if (song.FindAlbum(track.Album) != null)
                        {
                            break;
                        }
                    }
                }

                //FindAlbums(tracks);

                return tracks;
            }
            catch (Exception e)
            {
                Trace.WriteLine(e.Message);
                return tracks;
            }
        }


        private ItemSearchResponse FindTrack(string title, string artist)
        {
            string log = string.Format("FindTrack - {0} - Title: '{1}' Artist:'{2}'", DateTime.Now, title, artist);
            Trace.WriteLine(log);

            ItemSearchRequest request = null;
            ItemSearch itemSearch = null;

            request = new ItemSearchRequest();
            request.SearchIndex = "DigitalMusic";
            request.Title = title;
            request.Keywords = artist;
            request.RelationshipType = new string[] { "Tracks" };
            request.ResponseGroup = new string[] { "ItemAttributes", "RelatedItems" };

            itemSearch = new ItemSearch();
            itemSearch.AssociateTag = associateTag;
            itemSearch.AWSAccessKeyId = accessKeyId;
            itemSearch.Request = new ItemSearchRequest[] { request };

            ItemSearchResponse r = null;
            while (r == null)
            {
                try
                {
                    r = _client.ItemSearch(itemSearch);
                }
                catch (System.ServiceModel.ServerTooBusyException e)
                {
                    Trace.WriteLine("FindTrack: " + e.Message);
                    System.Threading.Thread.Sleep(5000);
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
            return new ImageSet[] { i };
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