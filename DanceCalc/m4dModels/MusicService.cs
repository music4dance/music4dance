using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace m4dModels
{
    public class MusicService
    {
        #region Properties
        public ServiceType ID { get; private set; }
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
        
        #endregion

        #region Methods
        public PurchaseLink GetPurchaseLink(PurchaseType pt, string album, string song)
        {
            PurchaseLink ret = null;
            string link = BuildPurchaseLink(pt, album, song);
            if (link != null)
            {

                ret = new PurchaseLink
                {
                    Link = link,
                    Target = Target,
                    Logo = Name + "-logo.png",
                    Charm = Name + "-charm.png",
                    AltText = Description
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
                return string.Format(_associateLink, info);
            else
                return null;
        }
        public virtual string BuildSearchRequest(string artist, string title)
        {
            if (_request == null)
            {
                return null;
            }

            artist = artist ?? string.Empty;
            title = title ?? string.Empty;

            string searchEnc = Uri.EscapeDataString(artist + " " + title);
            string req = string.Format(_request, searchEnc);
            return req;
        }

        public virtual string PreprocessSearchResponse(string response)
        {
            return response;
        }

        public virtual IList<ServiceTrack> ParseSearchResults(dynamic results)
        {
            throw new NotImplementedException();
        }

        protected string _associateLink;
        protected string _request;
        #endregion

        #region Constructors
        private MusicService() { }

        protected MusicService(
            ServiceType id, char cid,
            string name, string target, string description, string link, string request)
        {
            ID = id;
            CID = cid;
            Name = name;
            Target = target;
            Description = description;
            _associateLink = link;
            _request = request;
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
            ms = service.ID;

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
        
        #endregion

        #region Services
        public static IEnumerable<MusicService> GetServices()
        {
            return s_idMap.Values;
        }

        public static IEnumerable<MusicService> GetSearchableServices()
        {
            return s_idMap.Values.Where(s => s.CID != 'M');
        }

        public static MusicService GetService(ServiceType id)
        {
            return s_idMap[id];
        }

        public static MusicService GetService(char cid)
        {
            if (s_cidMap.ContainsKey(cid))
            {
                return s_cidMap[cid];
            }
            else
            {
                return null;
            }
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

            MusicService amazon = new AmazonService();
            s_idMap.Add(ServiceType.Amazon, amazon);
            s_cidMap.Add(amazon.CID, amazon);

            MusicService itunes = new ITunesService();
            s_idMap.Add(ServiceType.ITunes, itunes);
            s_cidMap.Add(itunes.CID, itunes);

            MusicService xbox = new XboxService();
            s_idMap.Add(ServiceType.XBox, xbox);
            s_cidMap.Add(xbox.CID, xbox);

            MusicService amg = new MusicService(
                ServiceType.AMG,
                'M',
                "American Music Group",
                null,
                null,
                null,
                null
            );
            s_idMap.Add(ServiceType.AMG, amg);
            s_cidMap.Add('M', amg);
        }

        private static readonly Dictionary<ServiceType, MusicService> s_idMap;
        private static readonly Dictionary<char, MusicService> s_cidMap;
        
        #endregion

        #region PurchaseType
        private static readonly char[] s_purchaseTypes = new char[] { '#', 'A', 'S' };
        private static readonly string[] s_purchaseTypesEx = new string[] { "None", "Album", "Song" }; 
        #endregion
    }
}
