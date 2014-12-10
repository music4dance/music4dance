using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

            string searchEnc = System.Uri.EscapeDataString(artist + " " + title);
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
                string[] parts = pi.Split(new char[] { '=' });

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

            MusicService amazon = new AmazonService(
                ServiceType.Amazon,
                'A',
                "Amazon",
                "amazon_store",
                "Available on Amazon",
                "http://www.amazon.com/gp/product/{0}/ref=as_li_ss_tl?ie=UTF8&camp=1789&creative=390957&creativeASIN={0}&linkCode=as2&tag=music4dance-20",
                null
            );
            s_idMap.Add(ServiceType.Amazon, amazon);
            s_cidMap.Add('A', amazon);

            MusicService itunes = new ITunesService(
                ServiceType.ITunes,
                'I',
                "ITunes",
                "itunes_store",
                "Buy it on ITunes",
                "http://itunes.apple.com/album/id{1}?i={0}&uo=4&at=11lwtf",
                "https://itunes.apple.com/search?term={0}&media=music&entity=song&limit=200"
            );
            s_idMap.Add(ServiceType.ITunes, itunes);
            s_cidMap.Add('I', itunes);

            MusicService xbox = new XboxService(
                ServiceType.XBox,
                'X',
                "XBox",
                "xbox_store",
                "Play it on Xbox Music",
                "http://music.xbox.com/Track/{0}?partnerID=Music4Dance?action=play",
                "https://music.xboxlive.com/1/content/music/search?q={0}&filters=tracks"
            );
            s_idMap.Add(ServiceType.XBox, xbox);
            s_cidMap.Add('X', xbox);

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

        private static Dictionary<ServiceType, MusicService> s_idMap;
        private static Dictionary<char, MusicService> s_cidMap;
        
        #endregion

        #region PurchaseType
        private static char[] s_purchaseTypes = new char[] { '#', 'A', 'S' };
        private static string[] s_purchaseTypesEx = new string[] { "None", "Album", "Song" }; 
        #endregion
    }
}
