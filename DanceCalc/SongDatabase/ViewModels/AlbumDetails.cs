using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SongDatabase.ViewModels
{
    public enum MusicService { None, Amazon, ITunes, XBox, AMG };
    public enum PurchaseType { None, Album, Song };

    public class AlbumDetails
    {
        private static char[] s_services = new char[] { '#', 'A', 'I', 'X', 'M' };
        private static char[] s_purchaseTypes = new char[] { '#', 'A', 'S' };

        private static string[] s_servicesEx = new string[] { "None", "Amazon", "ITunes", "XBox", "American Music Group" };
        private static string[] s_purchaseTypesEx = new string[] { "None", "Song", "Album" };


        public AlbumDetails()
        {

        }

        public AlbumDetails(AlbumDetails a)
        {
            Name = a.Name;
            Publisher = a.Publisher;
            Track = a.Track;

            if (a.Purchase != null)
                Purchase = new Dictionary<string, string>(a.Purchase);
            else
                Purchase = new Dictionary<string, string>();
        }

        public string Name { get; set; }
        public string Publisher { get; set; }
        [Range(1, 999)]
        public int? Track { get; set; }
        // Semi-colon separated purchase info of the form XX=YYYYYY (XX is service/type and YYYYY is id)
        // IA = Itunes Album
        // IS = Itunes Song
        // AA = Amazon Album
        // AS = Amazon Song
        // XA = Xbox Album
        // XS = Xbox Song
        // MS = AMG Song
        public Dictionary<string, string> Purchase { get; set; }

        /// <summary>
        /// Formatted purchase info
        /// </summary>
        /// <returns></returns>
        public IList<string> GetPurcahseInfo(bool compact = false)
        {
            if (Purchase == null || Purchase.Count == 0)
            {
                return null;
            }

            List<string> info = new List<string>(Purchase.Count);
            foreach (KeyValuePair<string, string> p in Purchase)
            {
                if (compact)
                {
                    info.Add(p.Key + "=" + p.Value);
                }
                else
                {
                    info.Add(ExpandPurcahseType(p.Key) + "=" + p.Value);
                }
            }

            return info;
        }

        public string SerializePurchaseInfo()
        {
            IList<string> pi = GetPurcahseInfo(true);
            if (pi == null)
            {
                return null;
            }
            else
            {
                return string.Join(";", pi);
            }
        }

        public void SetPurchaseInfo(PurchaseType pt, MusicService ms, string value)
        {
            if (Purchase == null)
            {
                Purchase = new Dictionary<string, string>();
            }

            if (pt == PurchaseType.None)
                throw new ArgumentOutOfRangeException("PurchaseType");

            if (ms == MusicService.None)
                throw new ArgumentOutOfRangeException("MusicService");

            StringBuilder sb = new StringBuilder(3);
            sb.Append(s_services[(int)ms]);
            sb.Append(s_purchaseTypes[(int)pt]);

            Purchase.Add(sb.ToString(), value);
        }

        public void SetPurchaseInfo(string purchase)
        {
            PurchaseType pt;
            MusicService ms;
            string pi;

            if (AlbumDetails.TryParsePurchaseInfo(purchase, out pt, out ms, out pi))
            {
                SetPurchaseInfo(pt, ms, pi);
            }
        }

        public bool HasPurchaseInfo
        {
            get
            {
                return Purchase != null && Purchase.Count > 0;
            }
        }

        static public bool TryParsePurchaseInfo(string pi, out PurchaseType pt, out MusicService ms, out string id)
        {
            bool success = false;

            string[] parts = pi.Split(new char[] { '=' });

            pt = PurchaseType.None;
            ms = MusicService.None;
            id = null;

            if (parts.Length == 2 && TryParsePurchaseType(parts[0], out pt, out ms))
            {
                id = parts[1];
                success = true;
            }

            return success;
        }

        static public bool TryParsePurchaseType(string abbrv, out PurchaseType pt, out MusicService ms)
        {
            if (abbrv == null)
            {
                throw new ArgumentNullException("abbrv");
            }

            ms = MusicService.None;
            pt = PurchaseType.None;

            if (abbrv.Length != 2)
            {
                return false;
            }

            switch (abbrv[0])
            {
                case 'I':
                    ms = MusicService.ITunes;
                    break;
                case 'A':
                    ms = MusicService.Amazon;
                    break;
                case 'X':
                    ms = MusicService.XBox;
                    break;
                case 'M':
                    ms = MusicService.AMG;
                    break;
            }

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

        static public string ExpandPurcahseType(string abbrv)
        {
            PurchaseType pt;
            MusicService ms;

            if (!TryParsePurchaseType(abbrv, out pt, out ms))
            {
                throw new ArgumentOutOfRangeException("abbrv");
            }

            string service = s_servicesEx[(int)ms];
            string type = s_purchaseTypesEx[(int)pt];

            return service + " " + type;
        }
    }
}
