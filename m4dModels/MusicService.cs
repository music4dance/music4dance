using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace m4dModels
{
    public class MusicService
    {
        #region Properties
        public ServiceType Id { get; }
        public char CID { get; }
        public string Name { get; }
        public string Target { get; }
        public string Description { get; }

        public string User => $"batch-{CID.ToString().ToLower()}";

        // This is pretty kludgy but until I implement
        // a second service that requires a key I don't
        // want to spend time generalizing
        public virtual bool RequiresKey => false;

        public virtual bool IsSearchable => true;
        public virtual bool ShowInProfile => true;
        public virtual bool HasRegions => false;
        protected string AssociateLink { get; set; }
        protected string SearchRequest { get; set; }
        protected string TrackRequest { get; set; }
        #endregion

        #region Methods
        public PurchaseLink GetPurchaseLink(PurchaseType pt, string album, string song)
        {
            PurchaseLink ret = null;
            var id = PurchaseRegion.ParseIdAndRegionInfo(song, out var regions);
            var link = BuildPurchaseLink(pt, album, id);

            if (link != null)
            {
                ret = new PurchaseLink
                {
                    ServiceType = Id,
                    Link = link,
                    AlbumId = album,
                    SongId = id,
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
            var sb = new StringBuilder(3);
            sb.Append(CID);
            sb.Append(PurchaseTypes[(int)pt]);
            return sb.ToString();
        }

        #endregion

        #region Overrides
        protected virtual string BuildPurchaseLink(PurchaseType pt, string album, string song)
        {
            if (string.IsNullOrWhiteSpace(AssociateLink)) return null;

            var info = (pt == PurchaseType.Song) ? song : album;

            return info != null ? string.Format(AssociateLink, info) : null;
        }

        public virtual string BuildSearchRequest(string artist, string title)
        {
            return BuildRequest(SearchRequest,(artist ?? string.Empty) + " " + (title ?? string.Empty));
        }

        public virtual string BuildLookupRequest(string url)
        {
            return url;
        }

        public virtual string GetNextRequest(dynamic last)
        {
            return null;
        }

        public virtual string NormalizeId(string id)
        {
            return id;
        }

        public virtual string BuildPlayListLink(PlayList playlist, string user, string email)
        {
            return null;
        }

        public string BuildTrackRequest(string id, string region=null)
        {
            var ret = BuildRequest(TrackRequest, id);
            if (!string.IsNullOrWhiteSpace(region))
            {
                ret += "?market=" + region;
            }
            return ret;
        }

        private string BuildRequest(string request, string value)
        {
            return request == null ? null : string.Format(request, Uri.EscapeDataString(value));
        }

        public virtual string PreprocessResponse(string response)
        {
            return response;
        }

        public virtual IList<ServiceTrack> ParseSearchResults(dynamic results, Func<string, dynamic> getResult)
        {
            throw new NotImplementedException();
        }

        public virtual ServiceTrack ParseTrackResults(dynamic results, Func<string, dynamic> getResult)
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
                throw new ArgumentOutOfRangeException(nameof(abbrv));
            }

            var service = IdMap[ms].Name;
            var type = PurchaseTypesEx[(int)pt];

            return service + " " + type;
        }
        public static bool TryParsePurchaseInfo(string pi, out PurchaseType pt, out ServiceType ms, out string id)
        {
            pt = PurchaseType.None;
            ms = ServiceType.None;
            id = null;

            if (string.IsNullOrWhiteSpace(pi)) return false;

            var parts = pi.Split('=');

            if (parts.Length != 2 || !TryParsePurchaseType(parts[0], out pt, out ms)) return false;

            id = parts[1];

            return true;
        }
        public static bool TryParsePurchaseType(string abbrv, out PurchaseType pt, out ServiceType ms)
        {
            if (abbrv == null)
            {
                throw new ArgumentNullException(nameof(abbrv));
            }

            ms = ServiceType.None;
            pt = PurchaseType.None;

            if (abbrv.Length != 2)
            {
                return false;
            }

            var service = CidMap[abbrv[0]];
            if (service == null)
            {
                throw new ArgumentOutOfRangeException(nameof(abbrv));
            }
            ms = service.Id;

            // ReSharper disable once SwitchStatementMissingSomeCases
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


        public static string FormatPurchaseFilter(string pf, string separator = ", ")
        {
            if (string.IsNullOrWhiteSpace(pf))
                return null;

            var services = (from c in pf where CidMap.ContainsKey(c) select CidMap[c] into service select service.Name).ToList();

            return services.Count == 0 ? null : string.Join(separator, services);
        }
        #endregion

        #region Services
        public static IEnumerable<MusicService> GetServices()
        {
            return IdMap.Values;
        }

        public static IEnumerable<MusicService> GetSearchableServices()
        {
            return IdMap.Values.Where(s => s.IsSearchable);
        }

        public static IEnumerable<MusicService> GetProfileServices()
        {
            return IdMap.Values.Where(s => s.ShowInProfile);
        }


        public static MusicService GetService(ServiceType id)
        {
            return IdMap[id];
        }

        public static MusicService GetService(char cid)
        {
            return CidMap.GetValueOrDefault(char.ToUpper(cid));
        }

        public static MusicService GetService(string type)
        {
            if (string.IsNullOrEmpty(type))
            {
                throw new ArgumentNullException(nameof(type));
            }

            return GetService(type[0]);
        }

        static MusicService()
        {
            IdMap = new Dictionary<ServiceType, MusicService>();
            CidMap = new Dictionary<char, MusicService>();

            AddService(new AmazonService());
            AddService(new ITunesService());
            AddService(new SpotifyService());
            AddService(new MusicServiceStub(ServiceType.Emusic, 'E', "EMusic"));
            AddService(new MusicServiceStub(ServiceType.Pandora, 'P', "Pandora"));
            AddService(new MusicServiceStub(ServiceType.AMG,'M',"American Music Group",false));
        }

        private static void AddService(MusicService service)
        {
            IdMap.Add(service.Id, service);
            CidMap.Add(service.CID, service);
        }
        private static readonly Dictionary<ServiceType, MusicService> IdMap;
        private static readonly Dictionary<char, MusicService> CidMap;
        
        #endregion

        #region PurchaseType
        private static readonly char[] PurchaseTypes = { '#', 'A', 'S' };
        private static readonly string[] PurchaseTypesEx = { "None", "Album", "Song" }; 
        #endregion
    }
}
