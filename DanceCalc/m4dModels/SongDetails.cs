using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Text.RegularExpressions;
using DanceLibrary;

namespace m4dModels
{
    // This is a transitory object (really a ViewModel object) that is used for 
    // viewing and editing a song, it shouldn't ever end up in a database,
    // it's meant to aggregate the information about a song in an easily digestible way
    [DataContract]
    [KnownType(typeof(DanceRatingInfo))]
    public class SongDetails : SongBase
    {
        #region Construction
        public SongDetails() 
        {
        }

        public SongDetails(Song song, string user=null, DanceMusicService dms=null, bool forSerialization=true)
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
            if (song.SongProperties != null)
            {
                foreach (var prop in song.SongProperties)
                {
                    Properties.Add(new SongProperty(prop));
                }                
            }

            if (song.ModifiedBy != null)
            {
                foreach (var mod in song.ModifiedBy)
                {
                    ModifiedList.Add(new ModifiedRecord(mod));
                }            
            }

            BuildAlbumInfo();

            if (dms == null) return;

            ApplicationUser au = null;
            if (user != null)
            {
                 au = dms.FindUser(user);
                if (au != null)
                {
                    SetCurrentUserTags(au, dms);
                }
            }

            if (forSerialization)
                SetupSerialization(au, dms);
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
                    Guid g;
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

        public SongDetails(string title, string artist, decimal? tempo, int? length, IList<AlbumDetails> albums)
        {
            Title = title;
            Artist = artist;
            Tempo = tempo;
            Length = length;
            _albums = (albums as List<AlbumDetails>) ?? albums.ToList();
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
        public static SongDetails CreateFromRow(ApplicationUser user, IList<string> fields, IList<string> cells, int weight=1)
        {
            var properties = new List<SongProperty>();
            var specifiedUser = false;
            var specifiedAction = false;
            SongProperty tagProperty = null;
            IList<string> tags = null;
            List<DanceRatingDelta> ratings = null;
            IList<string> danceTags = null;
            List<SongProperty> danceTagProperties = null;

            for (var i = 0; i < cells.Count; i++)
            {
                if (fields[i] == null) continue;

                var cell = cells[i];

                var baseName = SongProperty.ParseBaseName(fields[i]);
                string qual = null;
                cell = cell.Trim();
                if ((cell.Length > 0) && (cell[0] == '"') && (cell[cell.Length-1] == '"'))
                {
                    cell = cell.Trim('"');
                }

                specifiedAction |= SongProperty.IsActionName(baseName);
                switch (baseName)
                {
                    case DanceRatingField:
                        // Any positive delta here will be translated into whatever the creator
                        //  decides is appropriate, just need this property to be appropriately
                        //  parsable as a DRD.
                        {
                            var w = weight;
                            if (fields.Count > i + 1 && fields[i + 1] == "R")
                            {
                                if (!int.TryParse(cells[i + 1], out w))
                                    w = weight;
                                i += 1;
                            }
                            ratings = DanceRating.BuildDeltas(cell, w).ToList();
                            tagProperty = new SongProperty(Guid.Empty, AddedTags,
                                TagsFromDances(ratings.Select(r => r.DanceId)));
                            properties.Add(tagProperty);
                            properties.AddRange(ratings.Select(rating => new SongProperty(Guid.Empty, baseName, rating.ToString())));
                            cell = null;
                        }
                        break;
                    case LengthField:
                        if (!string.IsNullOrWhiteSpace(cell))
                        {
                            try
                            {
                                var d = new SongDuration(cell);
                                var l = d.Length;
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
                    case TitleArtistCell:
                        var re = new Regex(@"""(?<title>[^""]*)""\s*[―—](?<artist>.*)");
                        var m = re.Match(cell);
                        if (m.Success)
                        {
                            properties.Add(new SongProperty(Guid.Empty, TitleField, m.Groups["title"].Value));
                            properties.Add(new SongProperty(Guid.Empty, ArtistField, m.Groups["artist"].Value));
                        }
                        cell = null;
                        break;
                    case PurchaseField:
                        qual = SongProperty.ParseQualifier(fields[i]);
                        break;
                    case OwnerHash:
                        cell = cell.GetHashCode().ToString("X");
                        break;
                    case UserField:
                        specifiedUser = true;
                        break;
                    case AddedTags:
                        {
                            tags = new List<string>();
                            danceTags = new List<string>();

                            cell = cell.ToUpper();
                            if (cell.Contains("ENGLISH LANGUAGE"))
                            {
                                tags.Add("English:Other");
                            }
                            if (cell.Contains("SPANISH LANGUAGE"))
                            {
                                tags.Add("Spanish:Other");
                            }
                            if (cell.Contains("HIGH ENERGY"))
                            {
                                tags.Add("High Energy:Other");
                            }
                            if (cell.Contains("LOW ENERGY"))
                            {
                                tags.Add("Low Energy:Other");
                            }
                            if (cell.Contains("MEDIUM ENERGY"))
                            {
                                tags.Add("Medium Energy:Other");
                            }
                            if (cell.Contains("INSTRUMENTAL"))
                            {
                                tags.Add("Instrumental:Other");
                            }

                            if (cell.Contains("TRADITIONAL") || cell.Contains("TYPICAL") || cell.Contains("OLD SOUNDING") || cell.Contains("CLASSIC"))
                            {
                                danceTags.Add("Traditional:Style");
                            }
                            if (cell.Contains("CONTEMPORARY"))
                            {
                                danceTags.Add("Contemporary:Style");
                            }
                            if (cell.Contains("MODERN"))
                            {
                                danceTags.Add("Modern:Style");
                            }

                            if (tags.Count == 0) tags = null;
                            if (danceTags.Count == 0) danceTags = null;

                            cell = null;
                        }
                        break;
                    case SongTags:
                        if (!string.IsNullOrWhiteSpace(cell))
                        {
                            var tcs = SongProperty.ParsePart(fields[i], 1);
                            if (string.IsNullOrWhiteSpace(tcs)) tcs = "Other";
                            tags = new TagList(cell).Normalize(tcs).ToStringList();
                        }
                        cell = null;
                        break;
                    case DancersCell:
                        var dancers = cell.Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries);
                        danceTags = dancers.Select(dancer => dancer.Trim() + ":Other").ToList();
                        cell = null;
                        break;
                    case DanceTags:
                        if (!string.IsNullOrWhiteSpace(cell))
                        {
                            var tc = SongProperty.ParsePart(fields[i], 1);
                            if (string.IsNullOrWhiteSpace(tc)) tc = "Other";
                            danceTags = new TagList(cell).Normalize(tc).ToStringList();
                        }
                        cell = null;
                        break;
                    case MeasureTempo:
                        decimal tempo;
                        if (decimal.TryParse(cell, out tempo))
                        {
                            var numerator = 4;
                            if (ratings != null && ratings.Count > 0)
                            {
                                var did = ratings[0].DanceId;
                                var d = Dances.Instance.DanceDictionary[did];
                                if (d != null)
                                {
                                    numerator = d.Meter.Numerator;
                                }
                            }
                            tempo = tempo*numerator;
                            cell = tempo.ToString(CultureInfo.InvariantCulture);
                            baseName = TempoField;
                        }
                        else
                        {
                            cell = null;
                        }
                        
                        break;
                }

                if (tags != null && tags.Count > 0)
                {
                    var tl = new TagList(tags);

                    if (tagProperty != null)
                    {
                        tl = tl.Add(new TagList(tagProperty.Value));
                        tagProperty.Value = tl.ToString();
                    }
                    else
                    {
                        tagProperty = new SongProperty(Guid.Empty, AddedTags, tl.ToString());
                        properties.Add(tagProperty);
                    }
                }
                tags = null;

                if (danceTags != null && ratings != null)
                {
                    var tl = new TagList(danceTags);
                    if (danceTagProperties != null  && danceTagProperties.Count > 0)
                    {
                        tl = tl.Add(new TagList(danceTagProperties[0].Value));
                        foreach (var p in danceTagProperties)
                        {
                            p.Value = tl.ToString();
                        }
                    }
                    else
                    {
                        danceTagProperties = new List<SongProperty>();
                        foreach (var p in ratings.Select(drd => new SongProperty(Guid.Empty, AddedTags, tl.ToString(), -1, drd.DanceId)))
                        {
                            properties.Add(p);
                            danceTagProperties.Add(p);
                        }
                    }
                    danceTags = null;
                }

                if (string.IsNullOrWhiteSpace(cell)) continue;

                var idx = IsAlbumField(fields[i]) ? 0 : -1;
                var prop = new SongProperty(Guid.Empty, baseName, cell, idx, qual);
                properties.Add(prop);
            }


            // ReSharper disable once InvertIf
            if (user != null)
            {
                if (!specifiedUser)
                {
                    properties.Insert(0, new SongProperty(Guid.Empty, TimeField, DateTime.Now.ToString()));
                    properties.Insert(0, new SongProperty(Guid.Empty, UserField, user.UserName));
                }
                if (!specifiedAction)
                {
                    properties.Insert(0, new SongProperty(Guid.Empty, CreateCommand));
                }
            }

            return new SongDetails(Guid.Empty, properties);
        }

        public static List<string> BuildHeaderMap(string line, char separator = '\t')
        {
            var map = new List<string>();
            var headers = line.Split(separator);

            foreach (var header in headers.Select(t => t.Trim()))
            {
                var parts = header.Split(':');
                string field;
                // If this fails, we want to add a null to our list because
                // that indicates a column we don't care about
                if (parts.Length > 0 &&  s_propertyMap.TryGetValue(parts[0].ToUpper(), out field))
                {
                    map.Add((parts.Length > 1) ? field + ":" + parts[1] : field);
                }
                else
                {
                    map.Add(null);
                }
            }

            return map;
        }

        private static readonly Dictionary<string, string> s_propertyMap = new Dictionary<string, string>()
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
            {"AMAZONTRACK", SongProperty.FormatName(PurchaseField,null,"AS")},
            {"PATH",OwnerHash},
            {"TIME",LengthField},
            {"COMMENT",AddedTags},
            {"RATING","R"},
            {"DANCERS",DancersCell},
            {"TITLE+ARTIST",TitleArtistCell},
            {"DANCETAGS",DanceTags},
            {"SONGTAGS",SongTags},
            {"MPM", MeasureTempo},
        };

        public static IList<SongDetails> CreateFromRows(ApplicationUser user, string separator, IList<string> headers, IEnumerable<string> rows, int weight)
        {
            Dictionary<string, SongDetails> songs = new Dictionary<string, SongDetails>();
            bool itc = string.Equals(separator.Trim(), "ITC");
            bool itcd = string.Equals(separator.Trim(), "ITC-");

            foreach (string line in rows)
            {
                List<string> cells;
                
                if (itc || itcd)
                {
                    cells = new List<string>();
                    var re = itc ? new Regex(@"\b*(?<bpm>\d+)(?<title>[^\t]*)\t(?<artist>.*)") : new Regex(@"\b*(?<bpm>\d+)(?<title>[^-]*)-(?<artist>.*)");
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
                    SongDetails sd = CreateFromRow(user, headers, cells, weight);
                    if (sd != null)
                    {
                        string ta = sd.TitleArtistAlbumString;
                        if (string.Equals(sd.Title,sd.Artist))
                        {
                            Trace.WriteLine(string.Format("Title and Artist are the same ({0})",sd.Title));
                        }
                        SongDetails old;
                        if (songs.TryGetValue(ta, out old))
                        {
                            old.MergeRow(sd);
                        }
                        else
                        {
                            songs.Add(ta,sd);
                        }
                    }
                }
                else
                {
                    Trace.WriteLineIf(TraceLevels.General.TraceInfo,
                        string.Format("Bad cell count {0} != {1}: {2}", cells.Count, headers.Count, line));
                }
            }

            var ret = new List<SongDetails>(songs.Values);

            foreach (var sd in ret)
            {
                sd.InferDances(user);
            }
            return ret;
        }

        private void MergeRow(SongBase other)
        {
            if (other.Length.HasValue && !Length.HasValue)
            {
                Length = other.Length;
                CreateProperty(LengthField, other.Length.Value,null,null);
            }
            if (other.Tempo.HasValue && !Tempo.HasValue)
            {
                Tempo = other.Tempo;
                CreateProperty(TempoField, other.Tempo.Value, null, null);
            }

            var tagPropOther = other.LastProperty(AddedTags);
            if (tagPropOther != null)
            {
                var tagProp = LastProperty(AddedTags);
                tagProp.Value = (new TagList(tagProp.Value)).Add(new TagList(tagPropOther.Value)).ToString();
            }

            foreach (var dr in other.DanceRatings)
            {
                UpdateDanceRating(new DanceRatingDelta(dr.DanceId,dr.Weight),true);
            }
        }

        //private static readonly List<string> s_trackFields = new List<string>(new string[] {""});
        public static SongDetails CreateFromTrack(ApplicationUser user, ServiceTrack track)
        {
            var properties = new List<SongProperty>
            {
                SongProperty.Create(TimeField, DateTime.Now.ToString()),
                SongProperty.Create(UserField, user.UserName),
                SongProperty.Create(CreateCommand),
                SongProperty.Create(TitleField, track.Name)
            };

            AddProperty(properties,ArtistField,track.Artist);
            AddProperty(properties, LengthField, track.Duration);
            AddProperty(properties, AlbumField, track.Album, 0);
            AddProperty(properties, TrackField, track.TrackNumber, 0);
            // ReSharper disable once InvertIf
            if (!string.IsNullOrEmpty(track.PurchaseInfo))
            {
                var infos = track.PurchaseInfo.Split(new[] {';'}, StringSplitOptions.RemoveEmptyEntries);

                foreach (var info in infos)
                {
                    PurchaseType pt;
                    ServiceType st;
                    string id;

                    if (MusicService.TryParsePurchaseInfo(info, out pt, out st, out id))
                    {
                        AddProperty(properties, PurchaseField, id, 0, AlbumDetails.BuildPurchaseKey(pt, st));
                    }
                }
            }
            
            return new SongDetails(Guid.Empty, properties);
        }

        public static void AddProperty(IList<SongProperty> properties, string baseName, object value = null, int index = -1, string qual = null)
        {
            if (value != null)
                properties.Add(SongProperty.Create(baseName, value.ToString(), index, qual));
        }

        public static void AddProperty(IList<SongProperty> properties, string baseName, string value, int index = -1, string qual = null)
        {
            if (!string.IsNullOrWhiteSpace(value))
                properties.Add(SongProperty.Create(baseName, value, index, qual));
        }

        public void SetupSerialization(ApplicationUser user, DanceMusicService dms)
        {
            if (RatingsList == null || RatingsList.Count == 0) return;

            var ratings = new List<DanceRating>(RatingsList.Count);
            ratings.AddRange(RatingsList.Select(rating => new DanceRatingInfo(rating, user, dms)));
            _ratingsList = ratings;
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
        [DataMember]
        public List<AlbumDetails> Albums
        {
            get { return _albums ?? (_albums = new List<AlbumDetails>()); }
            set
            {
                _albums = value;
            }
        }
        private List<AlbumDetails> _albums;

        public List<SongProperty> Properties
        {
            get { return _properties ?? (_properties = new List<SongProperty>()); }
        }
        private List<SongProperty> _properties;
        public List<ModifiedRecord> ModifiedList
        {
            get { return _modifiedList ?? (_modifiedList = new List<ModifiedRecord>()); }
        }
        private List<ModifiedRecord> _modifiedList;

        public List<DanceRating> RatingsList 
        {
            get { return _ratingsList ?? (_ratingsList = new List<DanceRating>()); }
        }
        private List<DanceRating> _ratingsList;

        [DataMember]
        public TagList CurrentUserTags
        {
            get { return _currentUserTags; }
            set { throw new NotImplementedException("Shouldn't hit the setter for this.");}
        }
        public void SetCurrentUserTags(ApplicationUser user, DanceMusicService dms)
        {
            if (user == null) return;

            _currentUserTags = UserTags(user, dms);

            if (user.UserName == null || (_currentUserTags != null && !_currentUserTags.IsEmpty)) return;

            _currentUserTags = GetUserTags(user);
        }
        private TagList _currentUserTags;

        public int TitleHash 
        { 
            get 
            {
                return CreateTitleHash(Title); 
            } 
        }

        public void UpdateDanceRatingsAndTags(ApplicationUser user, IEnumerable<string> dances, int weight)
        {
            var enumerable = dances as IList<string> ?? dances.ToList();
            var tags = TagsFromDances(enumerable);
            var added = AddTags(tags, user);
            if (added != null && !added.IsEmpty)
                Properties.Add(new SongProperty(Guid.Empty, AddedTags,added.ToString()));
            UpdateDanceRatings(enumerable,weight);
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
            if (string.IsNullOrWhiteSpace(album)) return null;

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
        public ICollection<PurchaseLink> GetPurchaseLinks(string service = "AIXS", string region=null)
        {
            var links = new List<PurchaseLink>();
            service = service.ToUpper();

            foreach (var ms in MusicService.GetServices())
            {
                if (!service.Contains(ms.CID)) continue;

                foreach (var album in Albums)
                {
                    var l = album.GetPurchaseLink(ms.Id,region);
                    if (l == null) continue;

                    links.Add(l);
                    break;
                }
            }

            return links;
        }

        public string GetPurchaseId(ServiceType service)
        {
            string ret = null;
            foreach (var album in Albums)
            {
                ret = album.GetPurchaseIdentifier(service,PurchaseType.Song);
                if (ret != null)
                    break;
            }
            return ret;
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
                    if (results.All(d => d.Name != ad.Name))
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
            List<string> names = new List<string>(new[] {
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
                            d.Publisher = remove ? null : prop.Value;
                            break;
                        case TrackField:
                            if (remove)
                            {
                                d.Track = null;
                            }
                            else
                            {
                                int t;
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
                            reorder = prop.Value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => int.Parse(s)).ToList();
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
                albums = new List<AlbumDetails>();

                foreach (int t in reorder)
                {
                    AlbumDetails d;
                    if (map.TryGetValue(t, out d))
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
            foreach (int t in promotions)
            {
                AlbumDetails d;
                if (map.TryGetValue(t, out d) && d.Name != null)
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


        /// <summary>
        /// Finds a representitive of the largest cluster of tracks 
        ///  (clustered by approximate duration) that is an very
        ///  close title/artist match
        /// </summary>
        /// <param name="tracks"></param>
        /// <returns></returns>
        public static ServiceTrack FindDominantTrack(IList<ServiceTrack> tracks)
        {
            var ordered = RankTracksByCluster(tracks,null);
            return ordered != null ? tracks.First() : null;
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
                t.TrackRank = t.Duration.HasValue ? Math.Abs(duration - t.Duration.Value) : int.MaxValue;
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
                    List<ServiceTrack> list;
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
