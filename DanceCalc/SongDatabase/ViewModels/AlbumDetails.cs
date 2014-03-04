using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using SongDatabase.Models;

namespace SongDatabase.ViewModels
{
    public enum MusicService { None, Amazon, ITunes, XBox, AMG };
    public enum PurchaseType { None, Album, Song };

    public class AlbumDetails
    {
        private static char[] s_services = new char[] { '#', 'A', 'I', 'X', 'M' };
        private static char[] s_purchaseTypes = new char[] { '#', 'A', 'S' };

        private static string[] s_servicesEx = new string[] { "None", "Amazon", "ITunes", "XBox", "American Music Group" };
        private static string[] s_purchaseTypesEx = new string[] { "None", "Album", "Song" };


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
        // This is the serialization and default ordering index
        //  Actual order will be affected by repeated application
        //  of the MakePrimary attribute
        public int Index { get; set; }

        // Semi-colon separated purchase info of the form XX=YYYYYY (XX is service/type and YYYYY is id)
        // IA = Itunes Album
        // IS = Itunes Song
        // AA = Amazon Album
        // AS = Amazon Song
        // XA = Xbox Album
        // XS = Xbox Song
        // MS = AMG Song
        public Dictionary<string, string> Purchase { get; set; }

        public string PurchaseInfo
        {
            get { return SerializePurchaseInfo(); }
            set { SetPurchaseInfo(value); }

        }
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

            pt = PurchaseType.None;
            ms = MusicService.None;
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
        public bool ModifyInfo(DanceMusicContext dmc, Song song, int idx, AlbumDetails old, SongLog log)
        {
            bool modified = true;

            // This indicates a deleted album
            if (string.IsNullOrWhiteSpace(Name))
            {
                ChangeProperty(dmc, song, idx, DanceMusicContext.AlbumField, null, old.Name, null, log);
                if (old.Track.HasValue)
                    ChangeProperty(dmc, song, idx, DanceMusicContext.TrackField, null, old.Track, null, log);
                if (!string.IsNullOrWhiteSpace(old.Publisher))
                    ChangeProperty(dmc, song, idx, DanceMusicContext.PublisherField, null, old.Publisher, null, log);

                modified = true;
            }
            else
            {
                modified |= ChangeProperty(dmc, song, idx, DanceMusicContext.AlbumField, null, old.Name, Name, log);
                modified |= ChangeProperty(dmc, song, idx, DanceMusicContext.TrackField, null, old.Track, Track, log);
                modified |= ChangeProperty(dmc, song, idx, DanceMusicContext.PublisherField, null, old.Publisher, Publisher, log);

                PurchaseDiff(dmc, song, old, log);
            }

            return modified;
        }

        public void CreateProperties(DanceMusicContext dmc, Song song, int idx, SongLog log = null)
        {
            if (string.IsNullOrWhiteSpace(Name))
            {
                throw new ArgumentOutOfRangeException("album");
            }

            AddProperty(dmc, song, idx, DanceMusicContext.AlbumField, null, Name, log);
            AddProperty(dmc, song, idx, DanceMusicContext.TrackField, null, Track, log);
            AddProperty(dmc, song, idx, DanceMusicContext.PublisherField, null, Publisher, log);
            if (Purchase != null)
            {
                foreach (KeyValuePair<string, string> purchase in Purchase)
                {
                    AddProperty(dmc, song, idx, DanceMusicContext.PurchaseField, purchase.Key, purchase.Value, log);
                }
            }
        }

        public static void AddProperty(DanceMusicContext dmc, Song old, int idx, string name, string qual, object value, SongLog log = null)
        {
            if (value == null)
                return;

            string fullName = SongProperty.FormatName(name, idx, qual);

            SongProperty np = dmc.SongProperties.Create();
            np.Song = old;
            np.Name = fullName;
            np.Value = DanceMusicContext.SerializeValue(value);

            dmc.SongProperties.Add(np);
            if (log != null)
            {
                dmc.LogPropertyUpdate(np, log);
            }
        }

        public void PurchaseDiff(DanceMusicContext dmc, Song song, AlbumDetails old, Models.SongLog log)
        {
            Dictionary<string, string> add = new Dictionary<string, string>();
            //HashSet<string> rem = new HashSet<string>();

            // First delete all of the keys that are in old but not in new
            if (old.Purchase != null)
            {
                foreach (string key in old.Purchase.Keys)
                {
                    if (Purchase != null && !Purchase.ContainsKey(key))
                    {
                        ChangeProperty(dmc, song, this.Index, DanceMusicContext.PurchaseField, key, Purchase[key], null, log);
                    }
                }
            }

            // Now add all of the keys that are in new but either don't exist or are different in old
            if (Purchase != null)
            {
                foreach (string key in Purchase.Keys)
                {
                    if (old.Purchase == null || !old.Purchase.ContainsKey(key))
                    {
                        // Add
                        ChangeProperty(dmc, song, this.Index, DanceMusicContext.PurchaseField, key, null, Purchase[key], log);
                    }
                    else if (old.Purchase != null && old.Purchase.ContainsKey(key) && !string.Equals(Purchase[key],old.Purchase[key]))
                    {
                        // Change
                        ChangeProperty(dmc, song, this.Index, DanceMusicContext.PurchaseField, key, old.Purchase[key], Purchase[key], log);
                    }
                }
            }

        }

        public static bool ChangeProperty(DanceMusicContext dmc, Song song, int idx, string name, string qual, object oldValue, object newValue, SongLog log = null)
        {
            bool modified = false;

            if (!object.Equals(oldValue, newValue))
            {
                string fullName = SongProperty.FormatName(name, idx, qual);

                SongProperty np = dmc.SongProperties.Create();
                np.Song = song;
                np.Name = fullName;
                np.Value = DanceMusicContext.SerializeValue(newValue);

                dmc.SongProperties.Add(np);
                if (log != null)
                {
                    dmc.LogPropertyUpdate(np, log, DanceMusicContext.SerializeValue(oldValue));
                }


                modified = true;
            }

            return modified;
        }

    }
}
