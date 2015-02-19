using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Text;

namespace m4dModels
{
    public class MusicService
    {
        #region Properties
        public ServiceType Id { get; private set; }
        public char CID { get; private set; }
        public string Name { get; private set; }
        public string Target { get; private set; }
        public string Description { get; private set; }

        // This is pretty kludgy but until I implement
        // a second service that requires a key I don't
        // want to spend time generalizing
        public virtual bool RequiresKey
        {
            get { return false;}
        }

        public virtual bool IsSearchable { get {return true;}}
        public virtual bool ShowInProfile { get { return true; }}
        protected string AssociateLink { get; set; }
        protected string SearchRequest { get; set; }
        protected string TrackRequest { get; set; }
        #endregion

        #region Methods
        public PurchaseLink GetPurchaseLink(PurchaseType pt, string album, string song)
        {
            PurchaseLink ret = null;
            string[] regions = null;
            string id = ParseRegionInfo(song, out regions);
            string link = BuildPurchaseLink(pt, album, id);
            if (link != null)
            {

                ret = new PurchaseLink
                {
                    Link = link,
                    Target = Target,
                    Logo = Name + "-logo.png",
                    Charm = Name + "-charm.png",
                    AltText = Description,
                    AvailableMarkets = regions
                };
            }

            return ret;
        }
        public string BuildPurchaseKey(PurchaseType pt)
        {
            StringBuilder sb = new StringBuilder(3);
            sb.Append(CID);
            sb.Append(s_purchaseTypes[(int)pt]);
            return sb.ToString();
        }

        #endregion

        #region Overrides
        protected virtual string BuildPurchaseLink(PurchaseType pt, string album, string song)
        {
            string info = (pt == PurchaseType.Song) ? song : album;

            if (info != null)
                return string.Format(AssociateLink, info);
            else
                return null;
        }
        public virtual string BuildSearchRequest(string artist, string title)
        {
            return BuildRequest(SearchRequest,(artist ?? string.Empty) + " " + (title ?? string.Empty));
        }

        public string BuildTrackRequest(string id)
        {
            return BuildRequest(TrackRequest, id);
        }

        private string BuildRequest(string request, string value)
        {
            return request == null ? null : string.Format(request, Uri.EscapeDataString(value));
        }

        public virtual string PreprocessResponse(string response)
        {
            return response;
        }

        public virtual IList<ServiceTrack> ParseSearchResults(dynamic results)
        {
            throw new NotImplementedException();
        }

        public virtual ServiceTrack ParseTrackResults(dynamic results)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region Constructors

        protected MusicService(
            ServiceType id, char cid,
            string name, string target, string description, string link, string searchRequest, string trackRequest=null)
        {
            Id = id;
            CID = cid;
            Name = name;
            Target = target;
            Description = description;
            AssociateLink = link;
            SearchRequest = searchRequest;
            TrackRequest = trackRequest;
        }
        
        #endregion

        #region Static Helpers
        public static string ExpandPurchaseType(string abbrv)
        {
            PurchaseType pt;
            ServiceType ms;

            if (!TryParsePurchaseType(abbrv, out pt, out ms))
            {
                throw new ArgumentOutOfRangeException("abbrv");
            }

            string service = s_idMap[ms].Name;
            string type = s_purchaseTypesEx[(int)pt];

            return service + " " + type;
        }
        static public bool TryParsePurchaseInfo(string pi, out PurchaseType pt, out ServiceType ms, out string id)
        {
            bool success = false;

            pt = PurchaseType.None;
            ms = ServiceType.None;
            id = null;

            if (!string.IsNullOrWhiteSpace(pi))
            {
                string[] parts = pi.Split('=');

                if (parts.Length == 2 && TryParsePurchaseType(parts[0], out pt, out ms))
                {
                    id = parts[1];
                    success = true;
                }
            }

            return success;
        }
        static public bool TryParsePurchaseType(string abbrv, out PurchaseType pt, out ServiceType ms)
        {
            if (abbrv == null)
            {
                throw new ArgumentNullException("abbrv");
            }

            ms = ServiceType.None;
            pt = PurchaseType.None;

            if (abbrv.Length != 2)
            {
                return false;
            }

            MusicService service = s_cidMap[abbrv[0]];
            if (service == null)
            {
                throw new ArgumentOutOfRangeException("abbrv");
            }
            ms = service.Id;

            switch (abbrv[1])
            {
                case 'S':
                    pt = PurchaseType.Song;
                    break;
                case 'A':
                    pt = PurchaseType.Album;
                    break;
            }

            return true;
        }

        static public string ParseRegionInfo(string value, out string[] regions)
        {
            regions = null;

            if (value == null || !value.EndsWith("]")) return value;

            var fields = value.Split('[');

            if (fields.Length == 2)
            {
                regions = fields[1].Substring(0,fields[1].Length-1).Split(',');
            }

            return fields[0];
        }

        public static string FormatRegionInfo(string id, string[] regions)
        {
            if (regions == null) return id;

            var sb = new StringBuilder(id);
            sb.Append(id + "[");
            var sep = string.Empty;
            foreach (var r in regions)
            {
                sb.Append(sep);
                sb.Append(r);
                sep = ",";
            }
            sb.Append("]");

            return sb.ToString();
        }

        #endregion

        #region Services
        public static IEnumerable<MusicService> GetServices()
        {
            return s_idMap.Values;
        }

        public static IEnumerable<MusicService> GetSearchableServices()
        {
            return s_idMap.Values.Where(s => s.IsSearchable);
        }

        public static IEnumerable<MusicService> GetProfileServices()
        {
            return s_idMap.Values.Where(s => s.ShowInProfile);            
        }

        public static MusicService GetService(ServiceType id)
        {
            return s_idMap[id];
        }

        public static MusicService GetService(char cid)
        {
            return s_cidMap.GetValueOrDefault(char.ToUpper(cid));
        }

        public static MusicService GetService(string type)
        {
            if (string.IsNullOrEmpty(type))
            {
                throw new ArgumentNullException("type");
            }

            return GetService(type[0]);
        }
        static MusicService()
        {
            s_idMap = new Dictionary<ServiceType, MusicService>();
            s_cidMap = new Dictionary<char, MusicService>();

            AddService(new AmazonService());
            AddService(new ITunesService());
            AddService(new SpotifyService());
            AddService(new XboxService());
            AddService(new MusicServiceStub(ServiceType.Emusic, 'E', "EMusic"));
            AddService(new MusicServiceStub(ServiceType.Pandora, 'P', "Pandora"));
            AddService(new MusicServiceStub(ServiceType.AMG,'M',"American Music Group",false));
        }

        private static void AddService(MusicService service)
        {
            s_idMap.Add(service.Id, service);
            s_cidMap.Add(service.CID, service);            
        }
        private static readonly Dictionary<ServiceType, MusicService> s_idMap;
        private static readonly Dictionary<char, MusicService> s_cidMap;
        
        #endregion

        #region PurchaseType
        private static readonly char[] s_purchaseTypes = { '#', 'A', 'S' };
        private static readonly string[] s_purchaseTypesEx = { "None", "Album", "Song" }; 
        #endregion
    }
}
