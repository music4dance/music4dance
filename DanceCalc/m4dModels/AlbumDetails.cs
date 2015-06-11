using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Text.RegularExpressions;

namespace m4dModels
{
    // ReSharper disable once InconsistentNaming

    [DataContract]
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

            Purchase = a.Purchase != null ? new Dictionary<string, string>(a.Purchase) : new Dictionary<string, string>();
        }
        #endregion

        #region Properties
        [DataMember]
        public string Name { get; set; }
        [DataMember]
        public string Publisher { get; set; }
        [DataMember]
        [Range(1, 999999999)]
        public int? Track { get; set; }
        // This is the serialization and default ordering index
        //  Actual order will be affected by repeated application
        //  of the MakePrimary attribute
        [DataMember]
        public int Index { get; set; }

        // Semi-colon separated purchase info of the form XX=YYYYYY (XX is service/type and YYYYY is id)
        // IA = Itunes Album
        // IS = Itunes Song
        // AA = Amazon Album
        // AS = Amazon Song
        // SA = Spotify Album
        // SS = Spotify Song
        // XA = Xbox Album
        // XS = Xbox Song
        // MS = AMG Song
        [DataMember]
        public Dictionary<string, string> Purchase { get; set; }

        [DataMember]
        public string PurchaseInfo
        {
            get { return SerializePurchaseInfo(); }
            set { SetPurchaseInfo(value); }

        }

        [DataMember]
        public IEnumerable<PurchaseLink> PurchaseLinks
        {
            // TODO: Think this through some more - we're basically
            //  faking a r/w property that is redundant with PurchaseInfo
            //  in order to get more easily consumable information
            //  into json
            get { return GetPurchaseLinks();}
            // ReSharper disable once ValueParameterNotUsed
            set {  }
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

        public AlbumTrack AlbumTrack
        {
            get
            {
                return new AlbumTrack(Name, new TrackNumber(Track??0));
            }
        }

        public string FormattedTrack 
        {
            get { return Track.HasValue ? new TrackNumber(Track.Value).Format("F") : string.Empty; }
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

            Purchase[BuildPurchaseKey(pt,ms)] = value;
        }

        static public string BuildPurchaseInfo(PurchaseType pt, ServiceType ms, string value)
        {
            return string.Format("{0}={1}", BuildPurchaseKey(pt, ms), value);
        }

        static public string BuildPurchaseInfo(ServiceType ms, string collection, string track, string[] availableMarkets=null)
        {
            var sb = new StringBuilder();

            if (collection != null)
            {
                sb.Append(BuildPurchaseInfo(PurchaseType.Album, ms, collection));
            }

            if (track != null)
            {
                if (sb.Length != 0)
                {
                    sb.Append(";");
                }
                sb.Append(BuildPurchaseInfo(PurchaseType.Song, ms, track));
            }

            var ret = sb.Length > 0 ? sb.ToString() : null;

            return (ret == null || availableMarkets == null) ? ret : MusicService.FormatRegionInfo(ret, availableMarkets);
        }

        public static string BuildPurchaseKey(PurchaseType purchaseType, ServiceType serviceType)
        {
            if (purchaseType == PurchaseType.None)
                throw new ArgumentOutOfRangeException("purchaseType");

            if (serviceType == ServiceType.None)
                throw new ArgumentOutOfRangeException("serviceType");

            return MusicService.GetService(serviceType).BuildPurchaseKey(purchaseType);
        }
        public void SetPurchaseInfo(string purchase)
        {
            if (string.IsNullOrWhiteSpace(purchase))
            {
                return;
            }

            string[] values = purchase.Split(';');

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

        public PurchaseLink GetPurchaseLink(ServiceType ms, string region=null)
        {
            return GetPurchaseLink(ms, PurchaseType.Song, region) ?? GetPurchaseLink(ms, PurchaseType.Album, region);
        }

        public IList<PurchaseLink> GetPurchaseLinks()
        {
            return MusicService.GetServices().Select(service => GetPurchaseLink(service.Id)).Where(link => link != null).ToList();
        }

        public PurchaseLink GetPurchaseLink(ServiceType ms, PurchaseType pt, string region=null)
        {
            // Short-circuit if there is no purchase info for this ablum
            if (Purchase == null)
                return null;
            
            var service = MusicService.GetService(ms);
            var albumKey = service.BuildPurchaseKey(PurchaseType.Album);
            var songKey = service.BuildPurchaseKey(PurchaseType.Song);
            string albumInfo;
            string songInfo;

            Purchase.TryGetValue(albumKey, out albumInfo);
            Purchase.TryGetValue(songKey, out songInfo);

            var link =  service.GetPurchaseLink(pt, albumInfo, songInfo);
            return link != null && !string.IsNullOrWhiteSpace(region) && link.AvailableMarkets != null && !link.AvailableMarkets.Contains(region)
                ? null
                : link;
        }

        public string GetPurchaseIdentifier(ServiceType ms, PurchaseType pt)
        {
            // Short-circuit if there is no purchase info for this ablum
            if (Purchase == null)
                return null;

            string value;
            return Purchase.TryGetValue(MusicService.GetService(ms).BuildPurchaseKey(pt), out value) ? value : null;
            
        }

        public bool PurchaseDiff(DanceMusicService dms, Song song, AlbumDetails old, SongLog log)
        {
            bool modified = false;

            // First delete all of the keys that are in old but not in new
            if (old.Purchase != null)
            {
                // ReSharper disable once LoopCanBeConvertedToQuery
                foreach (string key in old.Purchase.Keys)
                {
                    if (Purchase != null && !Purchase.ContainsKey(key))
                    {
                        modified |= ChangeProperty(dms, song, Index, SongBase.PurchaseField, key, old.Purchase[key], null, log);
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
                        modified |= ChangeProperty(dms, song, Index, SongBase.PurchaseField, key, null, Purchase[key], log);
                    }
                    else if (old.Purchase != null && old.Purchase.ContainsKey(key) && !string.Equals(Purchase[key], old.Purchase[key]))
                    {
                        // Change
                        modified |= ChangeProperty(dms, song, Index, SongBase.PurchaseField, key, old.Purchase[key], Purchase[key], log);
                    }
                }
            }

            return modified;
        }

        public void PurchaseAdd(DanceMusicService dms, Song song, AlbumDetails old, SongLog log)
        {
            // Now add all of the keys that are in new but either don't exist or are different in old
            if (Purchase != null)
            {
                foreach (string key in Purchase.Keys)
                {
                    if (old.Purchase == null || !old.Purchase.ContainsKey(key))
                    {
                        // Add
                        ChangeProperty(dms, song, Index, SongBase.PurchaseField, key, null, Purchase[key], log);
                    }
                }
            }
        }

        #endregion

        #region Merging
        public static IList<AlbumDetails> MergeAlbums(IList<AlbumDetails> albums, string artist, bool preserveIndices)
        {
            var dict = new Dictionary<string, List<AlbumDetails>>();

            var duplicate = false;

            foreach (var a in albums)
            {
                var name = SongBase.CleanAlbum(a.Name,artist);
                List<AlbumDetails> l;
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

            var merge = new List<AlbumDetails>();
            foreach (var a in albums)
            {
                var name = SongBase.CleanAlbum(a.Name, artist);
                List<AlbumDetails> l;
                if (!dict.TryGetValue(name, out l)) continue;

                dict.Remove(name);
                merge.Add(MergeList(l));
            }

            if (preserveIndices) return merge;

            foreach (var album in merge.OrderBy(x => x.Name))
            {
                Trace.WriteLine(string.Format("{0}:{1}",album.Name,album.Track));
            }

            for (var i = 0; i < merge.Count(); i++)
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
            if (string.IsNullOrWhiteSpace(Publisher))
            {
                Publisher = album.Publisher;
            }
            if (!Track.HasValue)
            {
                Track = album.Track;
            }

            if (!HasPurchaseInfo)
            {
                Purchase = album.Purchase;
            }
            else if (album.HasPurchaseInfo)
            {
                // Case where both albums have purchase info: Merge the dictionaries keeping all unique entries
                foreach (string key in Purchase.Keys)
                {
                    album.Purchase.Remove(key);
                }
                foreach (KeyValuePair<string, string> p in album.Purchase)
                {
                    Purchase[p.Key] = p.Value;
                }
            }
        }
        
        #endregion

        #region Property Utilities
        public bool ModifyInfo(DanceMusicService dms, Song song, AlbumDetails old, SongLog log)
        {
            bool modified = false;

            // This indicates a deleted album
            if (string.IsNullOrWhiteSpace(Name))
            {
                old.Remove(dms, song, log);
                modified = true;
            }
            else
            {
                modified |= ChangeProperty(dms, song, old.Index, SongBase.AlbumField, null, old.Name, Name, log);
                modified |= ChangeProperty(dms, song, old.Index, SongBase.TrackField, null, old.Track, Track, log);
                modified |= ChangeProperty(dms, song, old.Index, SongBase.PublisherField, null, old.Publisher, Publisher, log);

                modified |= PurchaseDiff(dms, song, old, log);
            }

            return modified;
        }

        public void Remove(DanceMusicService dms, Song song, SongLog log)
        {
            ChangeProperty(dms, song, Index, SongBase.AlbumField, null, Name, null, log);
            if (Track.HasValue)
                ChangeProperty(dms, song, Index, SongBase.TrackField, null, Track, null, log);
            
            if (!string.IsNullOrWhiteSpace(Publisher))
                ChangeProperty(dms, song, Index, SongBase.PublisherField, null, Publisher, null, log);
        }

        // Additive update
        public bool UpdateInfo(DanceMusicService dms, Song song, AlbumDetails old, SongLog log)
        {
            bool modified = false;

            modified |= UpdateProperty(dms, song, old.Index, SongBase.AlbumField, null, old.Name, Name, log);
            modified |= UpdateProperty(dms, song, old.Index, SongBase.TrackField, null, old.Track, Track, log);
            modified |= UpdateProperty(dms, song, old.Index, SongBase.PublisherField, null, old.Publisher, Publisher, log);

            PurchaseAdd(dms, song, old, log);

            return modified;
        }

        public void CreateProperties(DanceMusicService dms, Song song, SongLog log = null)
        {
            if (string.IsNullOrWhiteSpace(Name))
            {
                throw new FieldAccessException(@"Name");
            }

            AddProperty(dms, song, Index, SongBase.AlbumField, null, Name, log);
            AddProperty(dms, song, Index, SongBase.TrackField, null, Track, log);
            AddProperty(dms, song, Index, SongBase.PublisherField, null, Publisher, log);
            if (Purchase != null)
            {
                foreach (KeyValuePair<string, string> purchase in Purchase)
                {
                    AddProperty(dms, song, Index, SongBase.PurchaseField, purchase.Key, purchase.Value, log);
                }
            }
        }

        public static void AddProperty(DanceMusicService dms, Song song, int idx, string name, string qual, object value, SongLog log = null)
        {
            if (value == null)
                return;

            string fullName = SongProperty.FormatName(name, idx, qual);

            SongProperty op = song.SongProperties.FirstOrDefault(p => p.Name == fullName);

            if (op == null || Equals(op.Value, value))
            {
                dms.CreateSongProperty(song, fullName, value, log);
            }
        }


        public static bool ChangeProperty(DanceMusicService dms, Song song, int idx, string name, string qual, object oldValue, object newValue, SongLog log = null)
        {
            bool modified = false;

            if (!Equals(oldValue, newValue))
            {
                string fullName = SongProperty.FormatName(name, idx, qual);

                dms.CreateSongProperty(song, fullName, newValue, log);

                modified = true;
            }

            return modified;
        }

        public static bool UpdateProperty(DanceMusicService dms, Song song, int idx, string name, string qual, object oldValue, object newValue, SongLog log = null)
        {
            bool modified = false;

            if (oldValue == null && newValue != null)
            {
                string fullName = SongProperty.FormatName(name, idx, qual);

                dms.CreateSongProperty(song, fullName, newValue, log);

                modified = true;
            }

            return modified;
        }

        static readonly Regex s_wordPattern = new Regex(@"\W");
        static readonly HashSet<string> s_ballroomWords = new HashSet<string>() { "ballroom", "latin", "ultimate", "standard", "dancing", "competition", "classics", "dance" };

        #endregion
    }
}
