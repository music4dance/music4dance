using m4d.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace m4d.ViewModels
{
    public enum MusicService { None, Amazon, ITunes, XBox, AMG, Max };
    public enum PurchaseType { None, Album, Song, Max };

    public class AlbumDetails
    {
        #region Construction
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
        #endregion

        #region Properties
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
        public bool HasPurchaseInfo
        {
            get
            {
                return Purchase != null && Purchase.Count > 0;
            }
        }
        #endregion

        #region Purchase Serialization
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
                    info.Add(ExpandPurchaseType(p.Key) + "=" + p.Value);
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
        
        #endregion

        #region Purchase Manipulation
        public string GetPurchaseTags()
        {
            StringBuilder sb = new StringBuilder();
            HashSet<char> added = new HashSet<char>();

            if (Purchase != null)
            {
                foreach (string t in Purchase.Keys)
                {
                    char c = t[0];
                    if (!added.Contains(c))
                    {
                        added.Add(c);
                        sb.Append(c);
                    }
                }
            }

            if (sb.Length == 0)
                return null;
            else
                return sb.ToString();
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


            Purchase.Add(BuildPurchaseKey(ms, pt), value);
        }

        public void SetPurchaseInfo(string purchase)
        {
            if (string.IsNullOrWhiteSpace(purchase))
            {
                return;
            }

            string[] values = purchase.Split(new char[] { ';' });

            foreach (string value in values)
            {
                PurchaseType pt;
                MusicService ms;
                string pi;

                if (AlbumDetails.TryParsePurchaseInfo(value, out pt, out ms, out pi))
                {
                    SetPurchaseInfo(pt, ms, pi);
                }
            }
        }

        public PurchaseLink GetPurchaseLink(MusicService ms)
        {
            PurchaseLink l = GetPurchaseLink(ms, PurchaseType.Song);
            if (l == null)
            {
                l = GetPurchaseLink(ms, PurchaseType.Album);
            }
            return l;
        }

        public PurchaseLink GetPurchaseLink(MusicService ms, PurchaseType pt)
        {
            PurchaseLink ret = null;
            string info = null;
            string key = BuildPurchaseKey(ms, pt);
            if (Purchase != null && Purchase.TryGetValue(key, out info))
            {
                string extra = String.Empty;

                // Thanks Apple - special case itunes w/ song + album
                if (pt == PurchaseType.Song && ms == MusicService.ITunes)
                {
                    Purchase.TryGetValue("IA", out extra);
                }
                else if (ms == MusicService.XBox)
                {
                    if (info.StartsWith("music."))
                    {
                        info = info.Substring(6);
                    }
                }

                ret = new PurchaseLink
                {
                    Link = string.Format(s_serviceLink[(int)ms], info, extra),
                    Target = s_serviceTarget[(int)ms],
                    Logo = s_servicesEx[(int)ms] + "-logo.png",
                    Charm = s_servicesEx[(int)ms] + "-charm.png",
                    AltText = s_servicesAlt[(int)ms]
                };
            }
            return ret;
        }

        public void PurchaseDiff(ISongPropertyFactory spf, Song song, AlbumDetails old, Models.SongLog log)
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
                        ChangeProperty(spf, song, this.Index, Song.PurchaseField, key, Purchase[key], null, log);
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
                        ChangeProperty(spf, song, this.Index, Song.PurchaseField, key, null, Purchase[key], log);
                    }
                    else if (old.Purchase != null && old.Purchase.ContainsKey(key) && !string.Equals(Purchase[key], old.Purchase[key]))
                    {
                        // Change
                        ChangeProperty(spf, song, this.Index, Song.PurchaseField, key, old.Purchase[key], Purchase[key], log);
                    }
                }
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

        static public string ServiceString(MusicService ms)
        {
            return s_servicesEx[(int)ms];
        }
        static public string ExpandPurchaseType(string abbrv)
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
        private static string BuildPurchaseKey(MusicService ms, PurchaseType pt)
        {
            StringBuilder sb = new StringBuilder(3);
            sb.Append(s_services[(int)ms]);
            sb.Append(s_purchaseTypes[(int)pt]);
            return sb.ToString();
        }
        
        #endregion

        #region Property Utilities
        public bool ModifyInfo(ISongPropertyFactory spf, Song song, AlbumDetails old, SongLog log)
        {
            bool modified = true;

            // This indicates a deleted album
            if (string.IsNullOrWhiteSpace(Name))
            {
                ChangeProperty(spf, song, old.Index, Song.AlbumField, null, old.Name, null, log);
                if (old.Track.HasValue)
                    ChangeProperty(spf, song, old.Index, Song.TrackField, null, old.Track, null, log);
                if (!string.IsNullOrWhiteSpace(old.Publisher))
                    ChangeProperty(spf, song, old.Index, Song.PublisherField, null, old.Publisher, null, log);

                modified = true;
            }
            else
            {
                modified |= ChangeProperty(spf, song, old.Index, Song.AlbumField, null, old.Name, Name, log);
                modified |= ChangeProperty(spf, song, old.Index, Song.TrackField, null, old.Track, Track, log);
                modified |= ChangeProperty(spf, song, old.Index, Song.PublisherField, null, old.Publisher, Publisher, log);

                PurchaseDiff(spf, song, old, log);
            }

            return modified;
        }
        public void CreateProperties(ISongPropertyFactory spf, Song song, SongLog log = null)
        {
            if (string.IsNullOrWhiteSpace(Name))
            {
                throw new ArgumentOutOfRangeException("album");
            }

            AddProperty(spf, song, Index, Song.AlbumField, null, Name, log);
            AddProperty(spf, song, Index, Song.TrackField, null, Track, log);
            AddProperty(spf, song, Index, Song.PublisherField, null, Publisher, log);
            if (Purchase != null)
            {
                foreach (KeyValuePair<string, string> purchase in Purchase)
                {
                    AddProperty(spf, song, Index, Song.PurchaseField, purchase.Key, purchase.Value, log);
                }
            }
        }

        public static void AddProperty(ISongPropertyFactory spf, Song old, int idx, string name, string qual, object value, SongLog log = null)
        {
            if (value == null)
                return;

            string fullName = SongProperty.FormatName(name, idx, qual);

            SongProperty np = spf.CreateSongProperty(old, fullName, value);

            if (log != null)
            {
                log.UpdateData(np.Name, np.Value);
            }
        }


        public static bool ChangeProperty(ISongPropertyFactory spf, Song song, int idx, string name, string qual, object oldValue, object newValue, SongLog log = null)
        {
            bool modified = false;

            if (!object.Equals(oldValue, newValue))
            {
                string fullName = SongProperty.FormatName(name, idx, qual);

                SongProperty np = spf.CreateSongProperty(song, fullName, newValue);

                if (log != null)
                {
                    log.UpdateData(np.Name, np.Value, LogBase.SerializeValue(oldValue));
                }


                modified = true;
            }

            return modified;
        }
        #endregion

        #region Purchase Statics
        private static char[] s_services = new char[] { '#', 'A', 'I', 'X', 'M' };
        private static char[] s_purchaseTypes = new char[] { '#', 'A', 'S' };

        private static string[] s_servicesEx = new string[] { "None", "Amazon", "ITunes", "Xbox", "American Music Group" };
        private static string[] s_serviceLink = new string[] { 
            "Error", 
            "http://www.amazon.com/gp/product/{0}/ref=as_li_ss_tl?ie=UTF8&camp=1789&creative=390957&creativeASIN={0}&linkCode=as2&tag=thegraycom-20", 
            "http://itunes.apple.com/album/id{1}?i={0}&uo=4&at=11lwtf",
            "http://music.xbox.com/Track/{0}?partnerID=Music4Dance?action=play"};
        private static string[] s_serviceTarget = new string[] { "_blank", "amazon_store", "itunes_store", "xbox_music", "_blank" };
        private static string[] s_servicesAlt = new string[] { "None", "Available on Amazon", "Play it on ITunes", "Play it on Xbox Music", "Catalogged by American Music Group" };
        private static string[] s_purchaseTypesEx = new string[] { "None", "Album", "Song" };
        #endregion
    }
}
