using DanceLibrary;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace m4dModels
{
    // TODONEXT: Get reloading the DB (or loading from string) to handle tags correctly
    // This is a transitory object (really a ViewModel object) that is used for 
    // viewing and editing a song, it shouldn't ever end up in a database,
    // it's meant to aggregate the information about a song in an easily digestible way
    public class SongDetails : SongBase
    {
        #region Construction
        public SongDetails() 
        {
        }

        public SongDetails(Song song)
        {
            SongId = song.SongId;
            Tempo = song.Tempo;
            Title = song.Title;
            Artist = song.Artist;
            Length = song.Length;
            Created = song.Created;
            Modified = song.Modified;
            TagSummary = song.TagSummary;

            RatingsList.AddRange(song.DanceRatings);
            Properties.AddRange(song.SongProperties);
            ModifiedList.AddRange(song.ModifiedBy);

            BuildAlbumInfo();
        }

        // TODO: I want to be able to create SongDetails as a completely disconnected object
        //  but mapping all of the information from songs.  
        public SongDetails(Guid songId, ICollection<SongProperty> properties)
        {
            Load(songId, properties);
        }

        public SongDetails(string s)
        {
            const string idField = "SongId=";
            SongId = Guid.Empty;
            if (s.StartsWith("SongId"))
            {
                int t = s.IndexOf('\t');
                if (t != -1)
                {
                    string sg = s.Substring(idField.Length, t - idField.Length);
                    s = s.Substring(t + 1);
                    Guid g = Guid.Empty;
                    if (Guid.TryParse(sg, out g))
                    {
                        SongId = g;
                    }
                }
            }

            if (SongId.Equals(Guid.Empty))
            {
                SongId = Guid.NewGuid();
            }

            List<SongProperty> properties = new List<SongProperty>();
            SongProperty.Load(SongId, s, properties);
            Load(SongId, properties);
        }

        public SongDetails(string title, string artist, decimal? tempo, int? length, List<AlbumDetails> albums)
        {
            Title = title;
            Artist = artist;
            Tempo = tempo;
            Length = length;
            _albums = albums;
        }
        private void Load(Guid songId, ICollection<SongProperty> properties)
        {
            SongId = songId;

            LoadProperties(properties);

            Albums = BuildAlbumInfo(properties);
            Properties.AddRange(properties);
        }

        #endregion

        #region Serialization
        /// <summary>
        /// Serialize the song to a single string
        /// </summary>
        /// <param name="actions">Actions to include in the serialization</param>
        /// <returns></returns>

        public static SongDetails CreateFromRow(IList<string> fields, IList<string> cells, int weight=1)
        {
            List<SongProperty> properties = new List<SongProperty>();
            for (int i = 0; i < cells.Count; i++)
            {
                if (fields[i] != null)
                {
                    string cell = cells[i];
                    string baseName = SongProperty.ParseBaseName(fields[i]);
                    string qual = null;
                    cell = cell.Trim();
                    if ((cell.Length > 0) && (cell[0] == '"') && (cell[cell.Length-1] == '"'))
                    {
                        cell = cell.Trim(new char[] {'"'});
                    }
                    switch (SongProperty.ParseBaseName(baseName))
                    {
                        case DanceRatingField:
                            // Any positive delta here will be translated into whatever the creator
                            //  decides is appropriate, just need this property to be appropriately
                            //  parsable as a DRD.
                            {
                                IEnumerable<DanceRatingDelta> ratings = DanceRating.BuildDeltas(cell, weight);
                                foreach (var rating in ratings)
                                {
                                    SongProperty prop = new SongProperty(Guid.Empty, baseName, rating.ToString());
                                    properties.Add(prop);
                                }

                                cell = null;
                            }
                            break;
                        case LengthField:
                            if (!string.IsNullOrWhiteSpace(cell))
                            {
                                try
                                {
                                    SongDuration d = new SongDuration(cell);
                                    decimal l = d.Length;
                                    cell = l.ToString("F0");
                                }
                                catch (ArgumentOutOfRangeException)
                                {
                                    cell = null;
                                }
                            }
                            break;
                        case ArtistField:
                            cell = CleanArtistString(cell);
                            break;
                        case TitleField:
                            cell = CleanText(cell);
                            // Song is not valid without a title
                            if (string.IsNullOrWhiteSpace(cell))
                            {
                                return null;
                            }
                            break;
                        case PurchaseField:
                            qual = SongProperty.ParseQualifier(fields[i]);
                            break;
                    }

                    if (!string.IsNullOrWhiteSpace(cell))
                    {
                        int idx = IsAlbumField(fields[i]) ? 0 : -1;
                        SongProperty prop = new SongProperty(Guid.Empty, baseName, cell, idx, qual);
                        properties.Add(prop);
                    }
                }
            }

            SongDetails song = new SongDetails(Guid.Empty, properties);
            return song;
        }

        public static List<string> BuildHeaderMap(string line, char separator = '\t')
        {
            List<string> map = new List<string>();
            string[] headers = line.ToUpper().Split(new char[] { separator });

            for (int i = 0; i < headers.Length; i++)
            {
                string header = headers[i].Trim().ToUpper();
                string field = null;
                // If this fails, we want to add a null to our list because
                // that indicates a column we don't care about
                s_propertyMap.TryGetValue(header, out field);
                map.Add(field);
            }

            return map;
        }

        private static Dictionary<string, string> s_propertyMap = new Dictionary<string, string>()
        {
            {"DANCE", DanceRatingField},
            {"TITLE", TitleField},
            {"ARTIST", ArtistField},
            {"CONTRIBUTING ARTISTS", ArtistField},
            {"LABEL", PublisherField},
            {"USER", UserField},
            {"TEMPO", TempoField},
            {"BPM", TempoField},
            {"BEATS-PER-MINUTE", TempoField},
            {"LENGTH", LengthField},
            {"TRACK",TrackField},
            {"ALBUM", AlbumField},
            {"#", TrackField},
            {"PUBLISHER", PublisherField},
            {"AMAZONTRACK", SongProperty.FormatName(PurchaseField,null,"AS")}
        };

        public static IList<SongDetails> CreateFromRows(string separator, IList<string> headers, IEnumerable<string> rows, int weight)
        {
            Dictionary<string, SongDetails> songs = new Dictionary<string, SongDetails>();
            bool itc = string.Equals(separator.Trim(), "ITC");
            bool itcd = string.Equals(separator.Trim(), "ITC-");

            foreach (string line in rows)
            {
                List<string> cells = null;
                
                if (itc || itcd)
                {
                    cells = new List<string>();
                    Regex re = null;
                    if (itc) re = new Regex(@"\b*(?<bpm>\d+)(?<title>[^\t]*)\t(?<artist>.*)");
                    else if (itcd) re = new Regex(@"\b*(?<bpm>\d+)(?<title>[^-]*)-(?<artist>.*)");
                    Match m = re.Match(line.Trim());
                    if (m.Success)
                    {
                        cells.Add(m.Groups["bpm"].Value);
                        cells.Add(m.Groups["title"].Value);
                        cells.Add(m.Groups["artist"].Value);
                    }
                    else
                    {
                        continue;
                    }
                }
                else
                {
                    cells = new List<string>(Regex.Split(line, separator));
                }
                

                // Concat back the last field (which seems a typical pattern)
                while (cells.Count > headers.Count)
                {
                    cells[headers.Count - 1] = string.Format("{0}{1}{2}",cells[headers.Count - 1],separator,(cells[headers.Count]));
                    cells.RemoveAt(headers.Count);
                }

                if (cells.Count == headers.Count)
                {
                    SongDetails sd = SongDetails.CreateFromRow(headers, cells, weight);
                    if (sd != null)
                    {
                        string ta = sd.TitleArtistString;
                        if (string.Equals(sd.Title,sd.Artist))
                        {
                            Trace.WriteLine(string.Format("Title and Artist are the same ({0})",sd.Title));
                        }
                        if (!songs.ContainsKey(ta))
                        {
                            songs.Add(ta,sd);
                        }
                    }
                }
            }

            return new List<SongDetails>(songs.Values);
        }

        #endregion

        #region Properties
        public override string Album
        {
            get
            {
                if (Albums != null && Albums.Count > 0)
                {
                    return Albums[0].AlbumTrack;
                }
                else
                {
                    return null;
                }
            }
            set
            {
                throw new NotImplementedException("Album shouldn't be set directly in SongDetails");
            }
        }

        public override string Purchase 
        { 
            get
            {
                return GetPurchaseTags();
            }
            set
            {
                throw new NotImplementedException("Purchase shouldn't be set directly in SongDetails");
            }
        }

        // TAGDELETE:
        //public override string TagSummary
        //{
        //    get
        //    {
        //        return BuildTagSummary();
        //    }
        //    set
        //    {
        //        throw new NotImplementedException("TagSummary shouldn't be set directly in SongDetails");
        //    }
        //}

        //private string BuildTagSummary()
        //{
        //    StringBuilder sb = new StringBuilder();
        //    string separator = String.Empty;
        //    foreach (Tag tag in Tags)
        //    {
        //        sb.Append(separator);
        //        sb.Append(tag.Value);
        //        separator = "|";
        //    }

        //    return sb.ToString();
        //}

        public override ICollection<DanceRating> DanceRatings 
        { 
            get
            {
                return RatingsList;
            }
            set
            {
                throw new NotImplementedException("Shouldn't need to set this explicitly");
            }
        }

        // TAGDELETE:
        //public override ICollection<Tag> Tags
        //{
        //    get
        //    {
        //        return TagList;
        //    }
        //    set
        //    {
        //        throw new NotImplementedException("Shouldn't need to set this explicitly");
        //    }
        //}
        public override ICollection<ModifiedRecord> ModifiedBy
        {
            get
            {
                return ModifiedList;
            }
            set
            {
                throw new NotImplementedException("Shouldn't need to set this explicitly");
            }
        }

        public override ICollection<SongProperty> SongProperties
        { 
            get
            {
                return Properties;
            }
            set
            {
                throw new NotImplementedException("Shouldn't need to set this explicitly");
            }
        }
        public List<AlbumDetails> Albums
        {
            get
            {
                if (_albums == null)
                {
                    _albums = new List<AlbumDetails>();
                }
                return _albums;
            }
            set
            {
                _albums = value;
            }
        }
        private List<AlbumDetails> _albums;

        public List<SongProperty> Properties
        {
            get
            {
                if (_properties == null)
                {
                    _properties = new List<SongProperty>();
                }
                return _properties;
            }
        }
        private List<SongProperty> _properties;
        public List<ModifiedRecord> ModifiedList
        {
            get
            {
                if (_modifiedList == null)
                {
                    _modifiedList = new List<ModifiedRecord>();
                }
                return _modifiedList;
            }
        }
        private List<ModifiedRecord> _modifiedList;

        public List<DanceRating> RatingsList 
        {
            get
            {
                if (_ratingsList == null)
                {
                    _ratingsList = new List<DanceRating>();
                }
                return _ratingsList;
            }
        }
        private List<DanceRating> _ratingsList;

        public TagList CurrentUserTags
        {
            get { return _currentUserTags; }
        }
        public void  SetCurrentUserTags(ApplicationUser user, DanceMusicService dms)
        {
            _currentUserTags = UserTags(user, dms);
        }
        private TagList _currentUserTags;

        public int TitleHash 
        { 
            get 
            {
                return CreateTitleHash(Title); 
            } 
        }
        #endregion

        #region Album
        public string AlbumList
        {
            get
            {
                if (HasAlbums)
                {
                    StringBuilder ret = new StringBuilder();
                    string sep = string.Empty;

                    foreach (AlbumDetails album in Albums)
                    {
                        ret.Append(sep);
                        ret.Append(album.Name);
                        sep = "|";
                    }

                    return ret.ToString();
                }
                else
                {
                    return null;
                }
            }
        }

        public AlbumDetails FindAlbum(string album)
        {
            AlbumDetails ret = null;
            List<AlbumDetails> candidates = new List<AlbumDetails>();
            string title = CleanAlbum(album,Artist);

            foreach (AlbumDetails ad in Albums)
            {
                if (string.Equals(CleanAlbum(ad.Name, Artist), title, StringComparison.CurrentCultureIgnoreCase))
                {
                    candidates.Add(ad);
                    if (string.Equals(ad.Name, album, StringComparison.CurrentCultureIgnoreCase))
                    {
                        ret = ad;
                        break;
                    }
                }
            }

            if (ret == null && candidates.Count > 0)
            {
                ret = candidates[0];
            }

            return ret;
        }

        public bool HasAlbums
        {
            get 
            {
                return Albums != null && Albums.Count > 0;
            }
        }
        // "Real" albums in this case being non-ballroom compilation-type albums
        public bool HasRealAblums
        {
            get
            {
                bool ret = false;
                if (HasAlbums)
                {
                    ret = Albums.Any(a => a.IsRealAlbum);
                }
                return ret;
            }
        }

        public List<AlbumDetails> CloneAlbums()
        {
            List<AlbumDetails> albums = new List<AlbumDetails>(Albums.Count);
            foreach (var album in Albums)
            {
                albums.Add(new AlbumDetails(album));
            }

            return albums;
        }

        public int GetNextAlbumIndex()
        {
            return GetNextAlbumIndex(Albums);
        }

        public string GetPurchaseTags()
        {
            return GetPurchaseTags(Albums);
        }

        public string MergePurchaseTags(string pi)
        {
            string oi = SortChars(pi);
            string ni = GetPurchaseTags();

            ni = ni ?? string.Empty;

            var merged = oi.Union(ni);
            return SortChars(merged);
        }

        private static string SortChars(IEnumerable<char> chars)
        {
            if (chars == null) return String.Empty;

            var a = chars.ToArray();
            Array.Sort(a);
            return new string(a);
        }
        public ICollection<PurchaseLink> GetPurchaseLinks(string service = "AIX")
        {
            List<PurchaseLink> links = new List<PurchaseLink>();
            service = service.ToUpper();

            foreach (MusicService ms in MusicService.GetServices())
            {
                if (service.Contains(ms.CID))
                {
                    foreach (AlbumDetails album in Albums)
                    {
                        PurchaseLink l = album.GetPurchaseLink(ms.ID);
                        if (l != null)
                        {
                            links.Add(l);
                            break;
                        }
                    }
                }
            }

            return links;
        }

        public static string GetPurchaseTags(ICollection<AlbumDetails> albums)
        {
            HashSet<char> added = new HashSet<char>();

            foreach (AlbumDetails d in albums)
            {
                string tags = d.GetPurchaseTags();
                if (tags != null)
                {
                    foreach (char c in tags)
                    {
                        if (!added.Contains(c))
                        {
                            added.Add(c);
                        }
                    }
                }
            }

            if (added.Count == 0)
                return null;
            else
            {
                return SortChars(added);
            }
        }
        public static int GetNextAlbumIndex(ICollection<AlbumDetails> albums)
        {
            int ret = 0;
            foreach (AlbumDetails ad in albums)
            {
                if (ad.Index >= ret)
                {
                    ret = ad.Index + 1;
                }
            }
            return ret;
        }

        public static List<AlbumDetails> BuildAlbumInfo(IList<Song> songs)
        {
            List<AlbumDetails> results = BuildAlbumInfo(songs[0]);

            for (int i = 1; i < songs.Count; i++)
            {
                List<AlbumDetails> next = BuildAlbumInfo(songs[i]);

                foreach (AlbumDetails ad in next)
                {
                    if (!results.Any(d => d.Name == ad.Name))
                    {
                        results.Add(ad);
                    }
                }
            }

            return results;
        }

        public static List<AlbumDetails> BuildAlbumInfo(Song song)
        {
            IEnumerable<SongProperty> properties =
                from prop in song.SongProperties
                //                where prop.BaseName.Equals(AlbumField)
                select prop;
            return BuildAlbumInfo(properties);
        }
        public static List<AlbumDetails> BuildAlbumInfo(IEnumerable<SongProperty> properties)
        {
            List<string> names = new List<string>(new string[] {
                AlbumField,PublisherField,TrackField,PurchaseField,AlbumPromote,AlbumOrder
            });

            // First build a hashtable of index->albuminfo, maintaining the total number and the
            // high water mark of indexed albums

            int max = 0;
            Dictionary<int, AlbumDetails> map = new Dictionary<int, AlbumDetails>();
            Dictionary<int, AlbumDetails> removed = new Dictionary<int, AlbumDetails>();

            // Also keep a list of 'promotions' - current semantics are that if an album
            //  has a promotion it is removed and re-inserted at the head of the list
            List<int> promotions = new List<int>();
            List<int> reorder = null;

            foreach (SongProperty prop in properties)
            {
                string name = prop.BaseName;
                int idx = prop.Index ?? 0;
                
                string qual = prop.Qualifier;

                if (names.Contains(name))
                {
                    AlbumDetails d;
                    if (map.ContainsKey(idx))
                    {
                        d = map[idx];
                    }
                    else
                    {
                        if (idx > max)
                        {
                            max = idx;
                        }
                        d = new AlbumDetails { Index = idx };
                        map.Add(idx, d);
                    }

                    bool remove = string.IsNullOrWhiteSpace(prop.Value);

                    switch (name)
                    {
                        case AlbumField:
                            if (remove)
                            {
                                d.Name = null; // This is an album that has been removed
                                removed[idx] = d;
                            }
                            else
                            {
                                d.Name = prop.Value;
                                if (removed.ContainsKey(idx))
                                {
                                    removed.Remove(idx);
                                }
                            }
                            break;
                        case PublisherField:
                            if (remove)
                            {
                                d.Publisher = null;
                            }
                            else
                            {
                                d.Publisher = prop.Value;
                            }
                            break;
                        case TrackField:
                            if (remove)
                            {
                                d.Track = null;
                            }
                            else
                            {
                                int t = 0;
                                int.TryParse(prop.Value, out t);
                                d.Track = t;
                            }
                            break;
                        case PurchaseField:
                            if (d.Purchase == null)
                            {
                                d.Purchase = new Dictionary<string, string>();
                            }

                            if (remove)
                            {
                                d.Purchase.Remove(qual);
                            }
                            else
                            {
                                d.Purchase[qual] = prop.Value;
                            }
                            break;
                        case AlbumPromote:
                            // Promote to first
                            promotions.Add(idx);
                            break;
                        case AlbumOrder:
                            // Forget all previous promotions and do a re-order base ond values
                            promotions.Clear();
                            reorder = prop.Value.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => int.Parse(s)).ToList();
                            break;
                    }
                }
            }

            // Remove the deleted albums
            foreach (var key in removed.Keys)
            {
                map.Remove(key);
            }

            List<AlbumDetails> albums = new List<AlbumDetails>(map.Count);

            // Do the (single) latest full re-order
            if (reorder != null)
            {
                List<AlbumDetails> orig = albums;
                albums = new List<AlbumDetails>();

                for (int i = 0; i < reorder.Count; i++)
                {
                    AlbumDetails d;
                    if (map.TryGetValue(reorder[i], out d))
                    {
                        albums.Add(d);
                    }
                }
            }
            else
            // Start with everything in its 'natural' order
            {
                for (int i = 0; i <= max; i++)
                {
                    AlbumDetails d;
                    if (map.TryGetValue(i, out d) && d.Name != null)
                    {
                        albums.Add(d);
                    }
                }
            }

            // Now do individual (trivial) promotions
            for (int i = 0; i < promotions.Count; i++)
            {
                AlbumDetails d;
                if (map.TryGetValue(promotions[i], out d) && d.Name != null)
                {
                    albums.Remove(d);
                    albums.Insert(0, d);
                }
            }

            return albums;
        }

        private void BuildAlbumInfo()
        {
            IEnumerable<SongProperty> properties =
                from prop in Properties
                select prop;

            Albums = BuildAlbumInfo(properties);
        }
        #endregion

        #region Tracks

        public AlbumDetails AlbumFromTitle(string title)
        {
            AlbumDetails ret = null;
            AlbumDetails alt = null;

            // We'll prefer the normalized album name, but if we can't find it we'll grab the stripped version...
            string stripped = CreateNormalForm(title);
            string normal = NormalizeAlbumString(title);
            foreach (var album in Albums)
            {
                if (string.Equals(normal, NormalizeAlbumString(album.Name), StringComparison.InvariantCultureIgnoreCase))
                {
                    ret = album;
                    break;
                }
                else if (string.Equals(stripped, NormalizeAlbumString(album.Name), StringComparison.InvariantCultureIgnoreCase))
                {
                    alt = album;
                }
            }

            if (ret != null)
            {
                return ret;
            }
            else
            {
                return alt;
            }
        }
        public int TrackFromAlbum(string title)
        {
            int ret = 0;

            AlbumDetails album = AlbumFromTitle(title);
            if (album != null && album.Track.HasValue)
            {
                ret = album.Track.Value;
            }
            return ret;
        }


        // <summary>
        /// Finds a representitive of the largest cluster of tracks 
        ///  (clustered by approximate duration) that is an very
        ///  close title/artist match
        /// </summary>
        /// <param name="tracks"></param>
        /// <returns></returns>
        public static ServiceTrack FindDominantTrack(IList<ServiceTrack> tracks)
        {
            IList<ServiceTrack> ordered = RankTracksByCluster(tracks,null);
            if (ordered != null)
            {
                return tracks.First();
            }
            else 
            {
                return null;
            }
            
        }

        public IList<ServiceTrack> RankTracks(IList<ServiceTrack> tracks)
        {
            if (Length.HasValue)
            {
                return RankTracksByDuration(tracks, Length.Value);
            }
            else
            {
                string album = null;
                if (Albums != null && Albums.Count > 0)
                {
                    album = Albums[0].Name;
                }
                return RankTracksByCluster(tracks,album);
            }
        }
        public static IList<ServiceTrack> RankTracksByCluster(IList<ServiceTrack> tracks, string album)
        {
            List<ServiceTrack> ret = null;

            Dictionary<int, List<ServiceTrack>> cluster = ClusterTracks(tracks);

            // If we only have one cluster, we're set
            if (cluster.Count == 1)
            {
            }
            else if (cluster.Count != 0) // Try clustering off phase if we had any clustering at all
            {
                Dictionary<int, List<ServiceTrack>> clusterT = ClusterTracks(tracks, 10, 5);
                if (clusterT.Count == 1)
                {
                    cluster = clusterT;
                }
                else
                {
                    // Neither clustering results in a clear winner, so try for the one with the
                    // smallest number
                    if (clusterT.Count < cluster.Count)
                    {
                        cluster = clusterT;
                    }
                }
            }
            else 
            {
                cluster = null;
            }

            if (cluster != null)
            {
                //ret = cluster.Values.Aggregate((seed, f) => f.Count > seed.Count ? f : seed);
                foreach (var list in cluster.Values)
                {
                    int c = list.Count;
                    foreach (var t in list)
                    {
                        t.TrackRank = c;
                    }
                }

                ret = tracks.OrderByDescending(t => t.TrackRank).ToList();
            }

            if (ret != null && album != null)
            {
                album = CleanString(album);

                List<ServiceTrack> amatches = new List<ServiceTrack>();

                foreach (var t in ret)
                {
                    if (string.Equals(CleanString(t.Album),album,StringComparison.InvariantCultureIgnoreCase))
                    {
                        amatches.Add(t);
                    }
                }

                foreach (var t in amatches)
                {
                    ret.Remove(t);
                }

                ret.InsertRange(0, amatches);
            }

            return ret;
        }

        public static IList<ServiceTrack> RankTracksByDuration(IList<ServiceTrack> tracks, int duration)
        {
            foreach (var t in tracks)
            {
                if (t.Duration.HasValue)
                {
                    t.TrackRank = Math.Abs(duration - t.Duration.Value);
                }
                else 
                {
                    t.TrackRank = int.MaxValue;
                }
            }

            return tracks.OrderBy(t => t.TrackRank).ToList();
        }
        
        private static Dictionary<int, List<ServiceTrack>> ClusterTracks(IList<ServiceTrack> tracks, int size = 10, int offset = 0)
        {
            Dictionary<int, List<ServiceTrack>> ret = new Dictionary<int, List<ServiceTrack>>();

            foreach (ServiceTrack track in tracks)
            {
                if (track.Duration.HasValue)
                {
                    int cluster = (track.Duration.Value + offset) / size;
                    List<ServiceTrack> list = null;
                    if (!ret.TryGetValue(cluster,out list))
                    {
                        list = new List<ServiceTrack>();
                        ret.Add(cluster, list);
                    }
                    list.Add(track);
                }
            }

            return ret;
        }

        public IList<ServiceTrack> TitleArtistFilter(IList<ServiceTrack> tracks)
        {
            List<ServiceTrack> tracksOut = new List<ServiceTrack>();

            foreach (var track in tracks)
            {
                if (TitleArtistMatch(track.Name, track.Artist))
                {
                    tracksOut.Add(track);
                }
            }

            return tracksOut;
        }

        public IList<ServiceTrack> DurationFilter(IList<ServiceTrack> tracks, int epsilon)
        {
            return DurationFilter(tracks, Length.Value, epsilon);
        }

        static public IList<ServiceTrack> DurationFilter(IList<ServiceTrack> tracks, int duration, int epsilon)
        {
            List<ServiceTrack> tracksOut = new List<ServiceTrack>();

            foreach (var track in tracks)
            {
                if (track.Duration.HasValue && Math.Abs(track.Duration.Value - duration) < epsilon)
                {
                    tracksOut.Add(track);
                }
            }

            return tracksOut;
        }

        #endregion
    }
}
