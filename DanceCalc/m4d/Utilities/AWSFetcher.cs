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


    public class AWSFetcher
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
        public IList<ServiceTrack> FetchTracks(SongDetails song, bool clean = false)
        {
            List<ServiceTrack> tracks = new List<ServiceTrack>();

            try
            {
                string title = song.Title;
                string artist = song.Artist;

                if (clean)
                {
                    title = song.CleanTitle;
                    artist = song.CleanArtist;
                }

                ItemSearchResponse response = FindTrack(title, artist);


                if (response == null)
                {
                    Trace.WriteLine(song.Title + ": Invalid Search");
                    return tracks;
                }

                if (response.Items[0].Request.Errors != null)
                {
                    Trace.WriteLine(song.Title + ":" + response.Items[0].Request.Errors[0].Message);
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

                    if (song.TitleArtistMatch(title, artist))
                    {
                        // TODO: Figure out how better to deal with Amazon throttling
                        System.Threading.Thread.Sleep(1000);

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

                        string trackId = item.ASIN;
                        ServiceTrack track = new ServiceTrack 
                        {
                            TrackId = "D:" + item.ASIN,
                            Name = title,
                            Artist = artist,
                            TrackNumber = ntrackNum,
                            Duration = duration,
                            ReleaseDate = item.ItemAttributes.ReleaseDate
                        };

                        ItemLookupResponse albumRef = FindAlbumId(trackId);
                        if (albumRef.Items[0].Request.Errors != null)
                        {
                            Trace.WriteLine(song.Title + ":" + response.Items[0].Request.Errors[0].Message);
                            return tracks;
                        }

                        string collectionId = null;
                        if (albumRef.Items[0].Item != null && albumRef.Items[0].Item.Length > 0 &&
                            albumRef.Items[0].Item[0].RelatedItems != null && albumRef.Items[0].Item[0].RelatedItems.Length > 0)
                        {
                            collectionId = albumRef.Items[0].Item[0].RelatedItems[0].RelatedItem[0].Item.ASIN;
                            track.CollectionId = "D:" + collectionId;
                        }

                        if (track.CollectionId != null)
                        {
                            ItemLookupResponse albumInfo = FindAlbumInfo(collectionId);
                            if (string.Equals(albumInfo.Items[0].Request.IsValid,"true",StringComparison.InvariantCultureIgnoreCase) &&
                                albumInfo.Items[0].Item != null && albumInfo.Items[0].Item.Length > 0)
                            {
                                track.Album = albumInfo.Items[0].Item[0].ItemAttributes.Title;
                            }
                        }
                        tracks.Add(track);

                        // If we have an exact match break...
                        if (song.FindAlbum(track.Album) != null)
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

            request.ResponseGroup = new string[] { "ItemAttributes" };

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

        private ItemLookupResponse FindAlbumId(string trackId)
        {
            string log = string.Format("FindAlbID - {0} - TrackId: '{1}'", DateTime.Now, trackId);
            Trace.WriteLine(log);

            ItemLookupRequest request = null;
            ItemLookup itemLookup = null;

            request = new ItemLookupRequest();
            request.IncludeReviewsSummary = "false";
            request.ItemId = new string[] {trackId};
            request.RelationshipType = new string[] { "Tracks" };
            request.ResponseGroup = new string[] { "RelatedItems" };

            itemLookup = new ItemLookup();
            itemLookup.AssociateTag = associateTag;
            itemLookup.AWSAccessKeyId = accessKeyId;
            itemLookup.Request = new ItemLookupRequest[] { request };

            ItemLookupResponse r = null;
            while (r == null)
            {
                try
                {
                    r = _client.ItemLookup(itemLookup);
                }
                catch (System.ServiceModel.ServerTooBusyException e)
                {
                    Trace.WriteLine("FindAlbumId: " + e.Message);
                    System.Threading.Thread.Sleep(5000);
                }
            }

            return r;
        }

        private ItemLookupResponse FindAlbumInfo(string albumId)
        {
            string log = string.Format("FindAlINF - {0} - AlbumId: '{1}'", DateTime.Now, albumId);
            Trace.WriteLine(log);

            ItemLookupRequest request = null;
            ItemLookup itemLookup = null;

            request = new ItemLookupRequest();
            request.IncludeReviewsSummary = "false";
            request.ItemId = new string[] { albumId };
            request.ResponseGroup = new string[] { "Small" };

            itemLookup = new ItemLookup();
            itemLookup.AssociateTag = associateTag;
            itemLookup.AWSAccessKeyId = accessKeyId;
            itemLookup.Request = new ItemLookupRequest[] { request };

            ItemLookupResponse r = null;
            while (r == null)
            {
                try
                {
                    r = _client.ItemLookup(itemLookup);
                }
                catch (System.ServiceModel.ServerTooBusyException e)
                {
                    Trace.WriteLine("FindAlbumInfo: " + e.Message);
                    System.Threading.Thread.Sleep(5000);
                }
            }

            return r;
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