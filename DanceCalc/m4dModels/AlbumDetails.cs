using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace m4dModels
{
    public enum ServiceType { None, Amazon, ITunes, XBox, AMG, Max };
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

        public bool IsRealAlbum
        {
            get
            {
                return !string.IsNullOrWhiteSpace(Name) && !s_wordPattern.Split(Name.ToLower()).Any(w => s_ballroomWords.Contains(w));
            }
        }

        #endregion

        #region Purchase Serialization
        /// <summary>
        /// Formatted purchase info
        /// </summary>
        /// <returns></returns>
        public IList<string> GetPurchaseInfo(bool compact = false)
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
                    info.Add(MusicService.ExpandPurchaseType(p.Key) + "=" + p.Value);
                }
            }

            return info;
        }

        public string SerializePurchaseInfo()
        {
            IList<string> pi = GetPurchaseInfo(true);
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
        public void SetPurchaseInfo(PurchaseType pt, ServiceType ms, string value)
        {
            if (Purchase == null)
            {
                Purchase = new Dictionary<string, string>();
            }

            if (pt == PurchaseType.None)
                throw new ArgumentOutOfRangeException("PurchaseType");

            if (ms == ServiceType.None)
                throw new ArgumentOutOfRangeException("ServiceType");

            MusicService service = MusicService.GetService(ms);
            Purchase[service.BuildPurchaseKey(pt)] =  value;
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
                ServiceType ms;
                string pi;

                if (MusicService.TryParsePurchaseInfo(value, out pt, out ms, out pi))
                {
                    SetPurchaseInfo(pt, ms, pi);
                }
            }
        }

        public PurchaseLink GetPurchaseLink(ServiceType ms)
        {
            PurchaseLink l = GetPurchaseLink(ms, PurchaseType.Song);
            if (l == null)
            {
                l = GetPurchaseLink(ms, PurchaseType.Album);
            }
            return l;
        }

        public PurchaseLink GetPurchaseLink(ServiceType ms, PurchaseType pt)
        {
            // Short-circuit if there is no purchase info for this ablum
            if (Purchase == null)
                return null;

            MusicService service = MusicService.GetService(ms);
            string albumKey = service.BuildPurchaseKey(PurchaseType.Album);
            string songKey = service.BuildPurchaseKey(PurchaseType.Song);
            string albumInfo = null;
            string songInfo = null;

            Purchase.TryGetValue(albumKey, out albumInfo);
            Purchase.TryGetValue(songKey, out songInfo);

            return service.GetPurchaseLink(pt, albumInfo, songInfo);
        }

        public void PurchaseDiff(ISongPropertyFactory spf, Song song, AlbumDetails old, SongLog log)
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
        
        #endregion

        public static IList<AlbumDetails> MergeAlbums(IList<AlbumDetails> albums)
        {
            Dictionary<string, List<AlbumDetails>> dict = new Dictionary<string, List<AlbumDetails>>();

            bool duplicate = false;

            foreach (AlbumDetails a in albums)
            {
                string name = Song.CreateNormalForm(a.Name);
                List<AlbumDetails> l = null;
                if (dict.TryGetValue(name, out l))
                {
                    duplicate = true;
                }
                else
                {
                    l = new List<AlbumDetails>();
                    dict.Add(name, l);
                }

                l.Add(a);
            }

            // Short circuit out of here if there was nothing to merge
            if (!duplicate)
            {
                return albums;
            }

            List<AlbumDetails> merge = new List<AlbumDetails>();
            foreach (AlbumDetails a in albums)
            {
                string name = Song.CreateNormalForm(a.Name);
                List<AlbumDetails> l = null;
                if (dict.TryGetValue(name, out l))
                {
                    dict.Remove(name);
                    merge.Add(MergeList(l));
                }
            }

            for (int i = 0; i < merge.Count(); i++ )
            {
                merge[i].Index = i;
            }

            return merge;
        }

        private static AlbumDetails MergeList(IList<AlbumDetails> l)
        {
            AlbumDetails m = l[0];
            for (int i = 1; i < l.Count; i++)
            {
                m.Merge(l[i]);
            }

            return m;
        }
        private void Merge(AlbumDetails album)
        {
            if (string.IsNullOrWhiteSpace(this.Publisher))
            {
                this.Publisher = album.Publisher;
            }
            if (!this.Track.HasValue)
            {
                this.Track = album.Track;
            }

            if (!this.HasPurchaseInfo)
            {
                this.Purchase = album.Purchase;
            }
            else if (album.HasPurchaseInfo)
            {
                // Case where both albums have purchase info: Merge the dictionaries keeping all unique entries
                foreach (string key in this.Purchase.Keys)
                {
                    album.Purchase.Remove(key);
                }
                foreach (KeyValuePair<string, string> p in album.Purchase)
                {
                    this.Purchase[p.Key] = p.Value;
                }
            }
        }

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

        public static void AddProperty(ISongPropertyFactory spf, Song song, int idx, string name, string qual, object value, SongLog log = null)
        {
            if (value == null)
                return;

            string fullName = SongProperty.FormatName(name, idx, qual);

            SongProperty op = song.SongProperties.FirstOrDefault(p => p.Name == fullName);

            if (op == null || string.Equals(op.Value, value))
            {
                SongProperty np = spf.CreateSongProperty(song, fullName, value);
                song.SongProperties.Add(np);

                if (log != null)
                {
                    log.UpdateData(np.Name, np.Value);
                }
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

        static Regex s_wordPattern = new Regex(@"\W");
        static HashSet<string> s_ballroomWords = new HashSet<string>() { "ballroom", "latin", "ultimate", "standard", "dancing", "competition", "classics", "dance" };

        #endregion
    }
}
