using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
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
        // XA = Groove Album
        // XS = Groove Song
        // MS = AMG Song
        [DataMember]
        public Dictionary<string, string> Purchase { get; set; }

        [DataMember]
        public string PurchaseInfo
        {
            get => SerializePurchaseInfo();
            set => SetPurchaseInfo(value);
        }

        [DataMember]
        public IEnumerable<PurchaseLink> PurchaseLinks
        {
            // TODO: Think this through some more - we're basically
            //  faking a r/w property that is redundant with PurchaseInfo
            //  in order to get more easily consumable information
            //  into json
            get => GetPurchaseLinks();
            // ReSharper disable once ValueParameterNotUsed
            set {  }
        }

        public bool HasPurchaseInfo => Purchase != null && Purchase.Count > 0;

        public bool IsRealAlbum
        {
            get
            {
                return !string.IsNullOrWhiteSpace(Name) && Track.HasValue && !s_wordPattern.Split(Name.ToLower()).Any(w => BallroomWords.Contains(w));
            }
        }

        public TrackNumber TrackNumber => new TrackNumber(Track??0);
        public AlbumTrack AlbumTrack => new AlbumTrack(Name, TrackNumber);

        public string FormattedTrack => Track.HasValue ? TrackNumber.Format("F") : string.Empty;

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

            var info = new List<string>(Purchase.Count);
            foreach (var p in Purchase)
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
            var pi = GetPurchaseInfo(true);
            return pi == null ? null : string.Join(";", pi);
        }

        #endregion

        #region Purchase Manipulation
        public string GetPurchaseTags()
        {
            var sb = new StringBuilder();
            var added = new HashSet<char>();

            if (Purchase == null) return sb.Length == 0 ? null : sb.ToString();

            foreach (var t in Purchase.Keys)
            {
                var c = t[0];
                if (added.Contains(c)) continue;

                added.Add(c);
                sb.Append(c);
            }

            return sb.Length == 0 ? null : sb.ToString();
        }
        public void SetPurchaseInfo(PurchaseType pt, ServiceType ms, string value)
        {
            if (Purchase == null)
            {
                Purchase = new Dictionary<string, string>();
            }

            Purchase[BuildPurchaseKey(pt,ms)] = value;
        }

        public static string BuildPurchaseInfo(PurchaseType pt, ServiceType ms, string value)
        {
            return $"{BuildPurchaseKey(pt, ms)}={value}";
        }

        public static string BuildPurchaseInfo(ServiceType ms, string collection, string track)
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

            return sb.Length > 0 ? sb.ToString() : null;
        }

        public static string BuildPurchaseKey(PurchaseType purchaseType, ServiceType serviceType)
        {
            if (purchaseType == PurchaseType.None)
                throw new ArgumentOutOfRangeException(nameof(purchaseType));

            if (serviceType == ServiceType.None)
                throw new ArgumentOutOfRangeException(nameof(serviceType));

            return MusicService.GetService(serviceType).BuildPurchaseKey(purchaseType);
        }
        public void SetPurchaseInfo(string purchase)
        {
            if (string.IsNullOrWhiteSpace(purchase))
            {
                return;
            }

            var values = purchase.Split(';');

            foreach (var value in values)
            {
                if (MusicService.TryParsePurchaseInfo(value, out var pt, out var ms, out var pi))
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

        public IList<string> GetExtendedPurchaseIds(PurchaseType pt)
        {
            return (from service in MusicService.GetSearchableServices() let id = GetPurchaseIdentifier(service.Id, pt) where id != null select $"{service.CID}:{PurchaseRegion.ParseIdAndRegionInfo(id,out _)}").ToList();
        }

        public PurchaseLink GetPurchaseLink(ServiceType ms, PurchaseType pt, string region=null)
        {
            // Short-circuit if there is no purchase info for this album
            if (Purchase == null)
                return null;
            
            var service = MusicService.GetService(ms);
            var albumKey = service.BuildPurchaseKey(PurchaseType.Album);
            var songKey = service.BuildPurchaseKey(PurchaseType.Song);

            Purchase.TryGetValue(albumKey, out var albumInfo);
            Purchase.TryGetValue(songKey, out var songInfo);

            var link =  service.GetPurchaseLink(pt, albumInfo, songInfo);
            return link != null && !string.IsNullOrWhiteSpace(region) && link.AvailableMarkets != null && !link.AvailableMarkets.Contains(region)
                ? null
                : link;
        }

        public string GetPurchaseIdentifier(ServiceType ms, PurchaseType pt, bool includeRegion = true)
        {
            // Short-circuit if there is no purchase info for this album
            if (Purchase == null)
                return null;

            if (!Purchase.TryGetValue(MusicService.GetService(ms).BuildPurchaseKey(pt), out var value))
            {
                return null;
            }

            return includeRegion ? value : PurchaseRegion.ParseIdAndRegionInfo(value, out _);
        }

        public bool PurchaseDiff(Song song, AlbumDetails old)
        {
            var modified = false;

            // First delete all of the keys that are in old but not in new
            if (old.Purchase != null)
            {
                // ReSharper disable once LoopCanBeConvertedToQuery
                foreach (var key in old.Purchase.Keys)
                {
                    if (Purchase != null && !Purchase.ContainsKey(key))
                    {
                        modified |= ChangeProperty(song, Index, Song.PurchaseField, key, old.Purchase[key], null);
                    }
                }
            }
            if (Purchase == null) return modified;

            // Now add all of the keys that are in new but either don't exist or are different in old
            foreach (var key in Purchase.Keys)
            {
                if (old.Purchase == null || !old.Purchase.ContainsKey(key))
                {
                    // Add
                    modified |= ChangeProperty(song, Index, Song.PurchaseField, key, null, Purchase[key]);
                }
                else if (old.Purchase != null && old.Purchase.ContainsKey(key) && !string.Equals(Purchase[key], old.Purchase[key]))
                {
                    // Change
                    modified |= ChangeProperty(song, Index, Song.PurchaseField, key, old.Purchase[key], Purchase[key]);
                }
            }

            return modified;
        }

        public void PurchaseAdd(Song song, AlbumDetails old)
        {
            // Now add all of the keys that are in new but either don't exist or are different in old
            if (Purchase == null) return;

            foreach (var key in Purchase.Keys.Where(key => old.Purchase == null || !old.Purchase.ContainsKey(key)))
            {
                ChangeProperty(song, Index, Song.PurchaseField, key, null, Purchase[key]);
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
                var name = Song.CleanAlbum(a.Name,artist);
                if (dict.TryGetValue(name, out var l))
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
                var name = Song.CleanAlbum(a.Name, artist);
                if (!dict.TryGetValue(name, out var l)) continue;

                dict.Remove(name);
                merge.AddRange(MergeList(l));
            }

            if (preserveIndices) return merge;

            foreach (var album in merge.OrderBy(x => x.Name))
            {
                Trace.WriteLineIf(TraceLevels.General.TraceVerbose,$"{album.Name}:{album.Track}");
            }

            for (var i = 0; i < merge.Count; i++)
            {
                merge[i].Index = i;
            }

            return merge;
        }

        private static AlbumDetails MergeTrackList(IList<AlbumDetails> l)
        {
            if (l.Count == 1) return l[0];

            var max = l.Max(t => t.Track);

            Trace.WriteLineIf(TraceLevels.General.TraceVerbose,string.Join(" / ",l.Select(t => t.Name)));
            var m = l[0];
            for (var i = 1; i < l.Count; i++)
            {
                m.Merge(l[i]);
            }

            m.Track = max;

            return m;
        }

        private static IEnumerable<AlbumDetails> MergeList(IList<AlbumDetails> albums)
        {
            var dict = new Dictionary<int, List<AlbumDetails>>();

            foreach (var a in albums)
            {
                var t = a.TrackNumber.Track ?? 0;
                if (!dict.TryGetValue(t, out var l))
                {
                    l = new List<AlbumDetails>();
                    dict.Add(t, l);
                }

                l.Add(a);
            }

            // If we have tracks that don't have a number + tracks that do,
            //  add the numberless tracks to the batch that already has the
            //  most members
            if (dict.ContainsKey(0) && dict.Count > 1)
            {
                var temp = dict[0];
                dict.Remove(0);
                var max = dict.Values.Max(t => t.Count);
                var l = dict.Values.First(t => t.Count == max);
                l.AddRange(temp);
            }

            return dict.Values.Select(MergeTrackList).ToList();
        }

        private void Merge(AlbumDetails album)
        {
            if (!string.IsNullOrWhiteSpace(album.Name) && album.Name.Length < Name.Length)
            {
                Name = album.Name;
            }

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
                foreach (var key in Purchase.Keys)
                {
                    album.Purchase.Remove(key);
                }
                foreach (var p in album.Purchase)
                {
                    Purchase[p.Key] = p.Value;
                }
            }
        }
        
        #endregion

        #region Property Utilities
        public bool ModifyInfo(Song song, AlbumDetails old)
        {
            var modified = false;

            // This indicates a deleted album
            if (string.IsNullOrWhiteSpace(Name))
            {
                old.Remove(song);
                modified = true;
            }
            else
            {
                modified |= ChangeProperty(song, old.Index, Song.AlbumField, null, old.Name, Name);
                modified |= ChangeProperty(song, old.Index, Song.TrackField, null, old.Track, Track);
                modified |= ChangeProperty(song, old.Index, Song.PublisherField, null, old.Publisher, Publisher);

                modified |= PurchaseDiff(song, old);
            }

            return modified;
        }

        public void Remove(Song song)
        {
            ChangeProperty(song, Index, Song.AlbumField, null, Name, null);
            if (Track.HasValue)
                ChangeProperty(song, Index, Song.TrackField, null, Track, null);
            
            if (!string.IsNullOrWhiteSpace(Publisher))
                ChangeProperty(song, Index, Song.PublisherField, null, Publisher, null);
        }

        // Additive update
        public bool UpdateInfo(Song song, AlbumDetails old)
        {
            var modified = false;

            modified |= UpdateProperty(song, old.Index, Song.AlbumField, null, old.Name, Name);
            modified |= UpdateProperty(song, old.Index, Song.TrackField, null, old.Track, Track);
            modified |= UpdateProperty(song, old.Index, Song.PublisherField, null, old.Publisher, Publisher);

            PurchaseAdd(song, old);

            return modified;
        }

        public void CreateProperties(Song song)
        {
            if (string.IsNullOrWhiteSpace(Name))
            {
                throw new FieldAccessException(@"Name");
            }

            AddProperty(song, Index, Song.AlbumField, null, Name);
            AddProperty(song, Index, Song.TrackField, null, Track);
            AddProperty(song, Index, Song.PublisherField, null, Publisher);
            if (Purchase == null) return;

            foreach (var purchase in Purchase)
            {
                AddProperty(song, Index, Song.PurchaseField, purchase.Key, purchase.Value);
            }
        }

        public static void AddProperty(Song song, int idx, string name, string qual, object value)
        {
            if (value == null)
                return;

            var fullName = SongProperty.FormatName(name, idx, qual);

            var op = song.SongProperties.FirstOrDefault(p => p.Name == fullName);

            if (op == null || Equals(op.Value, value))
            {
                song.CreateProperty(fullName, value);
            }
        }


        public static bool ChangeProperty(Song song, int idx, string name, string qual, object oldValue, object newValue)
        {
            if (Equals(oldValue, newValue)) return false;

            song.CreateProperty(SongProperty.FormatName(name, idx, qual), newValue);

            return true;
        }

        public static bool UpdateProperty(Song song, int idx, string name, string qual, object oldValue, object newValue)
        {
            if (oldValue != null || newValue == null) return false;

            song.CreateProperty(SongProperty.FormatName(name, idx, qual), newValue);

            return true;
        }

        static readonly Regex s_wordPattern = new Regex(@"\W");
        static readonly HashSet<string> BallroomWords = new HashSet<string> { "ballroom", "latin", "ultimate", "standard", "dancing", "competition", "classics", "dance" };

        #endregion
    }
}
