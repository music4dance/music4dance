using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;
using DanceLibrary;
using Microsoft.Azure.Search.Models;

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

        public SongDetails(SongBase song, string user=null, DanceMusicService dms=null, bool forSerialization=true)
        {
            SongId = song.SongId;
            Created = song.Created;
            Modified = song.Modified;
            TagSummary = song.TagSummary;

            foreach (var pi in ScalarProperties)
            {
                var v = pi.GetValue(song);
                pi.SetValue(this, v);
            }

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
                    var mr = new ModifiedRecord(mod);
                    if (mr.UserName == user)
                    {
                        _currentUserLike = mr.Like;
                    }
                    ModifiedList.Add(mr);
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

            if (song.Tags != null)
            {
                Tags = new List<Tag>();
                foreach (var tag in song.Tags)
                {
                    Tags.Add(tag);
                }
            }
            if (forSerialization)
                SetupSerialization(au, dms);
        }

        // TODO: I want to be able to create SongDetails as a completely disconnected object
        //  but mapping all of the information from songs.  
        public SongDetails(Guid songId, ICollection<SongProperty> properties, DanceStatsInstance stats)
        {
            Load(songId, properties, stats);
        }

        public SongDetails(SongBase s, DanceStatsInstance stats, string userName = null, bool forSerialization = true)
        {
            Init(s.SongId, SongProperty.Serialize(s.OrderedProperties,null), stats, userName, forSerialization);
        }

        public SongDetails(Guid guid, string s, DanceStatsInstance stats, string userName = null, bool forSerialization = true)
        {
            Init(guid,s,stats,userName,forSerialization);
        }

        public SongDetails(string s, DanceStatsInstance stats, string userName = null, bool forSerialization = true)
        {
            // Take a guid parameter?
            Guid id;
            var ich = TryParseId(s, out id);
            if (ich > 0)
            {
                s = s.Substring(ich);
            }
            else
            {
                id = Guid.NewGuid();
            }
            Init(id,s,stats,userName,forSerialization);
        }

        private void Init(Guid id, string s, DanceStatsInstance stats, string userName, bool forSerialization)
        {
            SongId = id;
            var properties = new List<SongProperty>();
            SongProperty.Load(SongId, s, properties);
            Load(SongId, properties, stats);

            if (forSerialization && stats != null) SetupSerialization(userName, stats);

            if (userName == null) return;
            _currentUserTags = GetUserTags(userName);
        }

        public SongDetails(string title, string artist, decimal? tempo, int? length, IList<AlbumDetails> albums)
        {
            Title = title;
            Artist = artist;
            Tempo = tempo;
            Length = length;
            _albums = (albums as List<AlbumDetails>) ?? albums?.ToList();
        }
        private void Load(Guid songId, ICollection<SongProperty> properties, DanceStatsInstance stats)
        {
            SongId = songId;

            LoadProperties(properties, stats);

            Albums = BuildAlbumInfo(properties);
            Properties.AddRange(properties);
        }

        #endregion

        #region Serialization
        public static SongDetails CreateFromRow(ApplicationUser user, IList<string> fields, IList<string> cells, DanceStatsInstance stats,int weight=1)
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
                // ReSharper disable once SwitchStatementMissingSomeCases
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
                        if (!m.Success)
                        {
                            re = new Regex(@"(?<title>[^―—]*)\s*[―—](?<artist>.*)");
                            m = re.Match(cell);
                        }
                        if (m.Success)
                        {
                            properties.Add(new SongProperty(Guid.Empty, TitleField, m.Groups["title"].Value));
                            properties.Add(new SongProperty(Guid.Empty, ArtistField, m.Groups["artist"].Value));
                        }
                        else
                        {
                            // TODO: Figure out a clean way to propagate errors
                            Trace.WriteLineIf(TraceLevels.General.TraceError,$"Invalid TitleArtist: {cell}");
                            return null;
                        }
                        cell = null;
                        break;
                    case PurchaseField:
                        qual = SongProperty.ParseQualifier(fields[i]);
                        if (qual == "AS" && !cell.Contains(':'))
                        {
                            cell = "D:" + cell;
                        }
                        else
                        if (qual == "IS")
                        {
                            var ids = cell.Split('|');
                            if (ids.Length == 2)
                            {
                                cell = ids[0];
                                properties.Add(new SongProperty(Guid.Empty, baseName, ids[1], 0, "IA"));
                            }
                        }
                        break;
                    case OwnerHash:
                        cell = cell.GetHashCode().ToString("X");
                        break;
                    case UserField:
                    case UserProxy:
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
                                var d = Dances.Instance.DanceFromId(did);
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

            const string sep = "|";
            Trace.WriteLineIf(user == null && !specifiedUser,$"Bad User for {string.Join(sep,cells)}");

            // ReSharper disable once InvertIf
            if (user != null)
            {
                if (!specifiedUser)
                {
                    properties.Insert(0, new SongProperty(Guid.Empty, TimeField, DateTime.Now.ToString(CultureInfo.InvariantCulture)));
                    properties.Insert(0, new SongProperty(Guid.Empty, UserField, user.UserName));
                }
                if (!specifiedAction)
                {
                    properties.Insert(0, new SongProperty(Guid.Empty, CreateCommand));
                }
            }

            return new SongDetails(Guid.NewGuid(), properties, stats);
        }

        public static List<string> BuildHeaderMap(string line, char separator = '\t')
        {
            var map = new List<string>();
            var headers = line.Split(separator);

            foreach (var parts in headers.Select(t => t.Trim()).Select(header => header.Split(':')))
            {
                string field;
                // If this fails, we want to add a null to our list because
                // that indicates a column we don't care about
                if (parts.Length > 0 &&  PropertyMap.TryGetValue(parts[0].ToUpper(), out field))
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

        private static readonly Dictionary<string, string> PropertyMap = new Dictionary<string, string>()
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
            {"AMAZON", SongProperty.FormatName(PurchaseField,null,"AS")},
            {"ITUNES", SongProperty.FormatName(PurchaseField,null,"IS")},
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

        public static IList<SongDetails> CreateFromRows(ApplicationUser user, string separator, IList<string> headers, IEnumerable<string> rows, DanceStatsInstance stats, int weight)
        {
            var songs = new Dictionary<string, SongDetails>();
            var itc = string.Equals(separator.Trim(), "ITC");
            var itcd = string.Equals(separator.Trim(), "ITC-");

            foreach (var line in rows)
            {
                Trace.WriteLineIf(TraceLevels.General.TraceVerbose,"Create Song From Row:" + line);
                List<string> cells;
                
                if (itc || itcd)
                {
                    cells = new List<string>();
                    var re = itc ? new Regex(@"\w*(?<bpm>\d+)(?<title>[^\t]*)\t(?<artist>.*)") : new Regex(@"\w*(?<bpm>\d+)(?<title>[^-]*)-(?<artist>.*)");
                    var m = re.Match(line.Trim());
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
                    cells[headers.Count - 1] = $"{cells[headers.Count - 1]}{separator}{(cells[headers.Count])}";
                    cells.RemoveAt(headers.Count);
                }

                if (cells.Count == headers.Count)
                {
                    var sd = CreateFromRow(user, headers, cells, stats, weight);
                    if (sd != null)
                    {
                        var ta = sd.TitleArtistAlbumString;
                        if (string.Equals(sd.Title,sd.Artist))
                        {
                            Trace.WriteLine($"Title and Artist are the same ({sd.Title})");
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
                        $"Bad cell count {cells.Count} != {headers.Count}: {line}");
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
        public static SongDetails CreateFromTrack(ApplicationUser user, ServiceTrack track, string dances, string songTags, string danceTags, DanceStatsInstance stats)
        {
            // Title;Artist;Duration;Album;Track;DanceRating;SongTags;DanceTags;PurchaseInfo;

            var fields = new List<string>
            {
                TitleField,
                ArtistField,
                LengthField,
                AlbumField,
                TrackField,
                DanceRatingField,
                SongTags,
                DanceTags
            };

            var cells = new List<string>
            {
                track.Name,
                track.Artist,
                track.Duration?.ToString(),
                track.Album,
                track.TrackNumber?.ToString(),
                dances,
                songTags,
                danceTags
            };

            if (track.CollectionId != null)
            {
                fields.Add(PurchaseField + ":00:" + AlbumDetails.BuildPurchaseKey(PurchaseType.Album, track.Service));
                cells.Add(track.CollectionId);
            }
            if (track.TrackId != null)
            {
                fields.Add(PurchaseField + ":00:" + AlbumDetails.BuildPurchaseKey(PurchaseType.Song, track.Service));
                cells.Add(track.TrackId);
            }

            var sd = CreateFromRow(user, fields, cells, stats, DanceRatingIncrement);
            sd.InferDances(user);
            return sd;
            //var properties = new List<SongProperty>
            //{
            //    SongProperty.Create(TimeField, DateTime.Now.ToString(CultureInfo.InvariantCulture)),
            //    SongProperty.Create(UserField, user.UserName),
            //    SongProperty.Create(CreateCommand),
            //    SongProperty.Create(TitleField, track.Name)
            //};

            //AddProperty(properties,ArtistField,track.Artist);
            //AddProperty(properties, LengthField, track.Duration);
            //AddProperty(properties, AlbumField, track.Album, 0);
            //AddProperty(properties, TrackField, track.TrackNumber, 0);
            //// ReSharper disable once InvertIf
            //if (!string.IsNullOrEmpty(track.PurchaseInfo))
            //{
            //    var infos = track.PurchaseInfo.Split(new[] {';'}, StringSplitOptions.RemoveEmptyEntries);

            //    foreach (var info in infos)
            //    {
            //        PurchaseType pt;
            //        ServiceType st;
            //        string id;

            //        if (MusicService.TryParsePurchaseInfo(info, out pt, out st, out id))
            //        {
            //            AddProperty(properties, PurchaseField, id, 0, AlbumDetails.BuildPurchaseKey(pt, st));
            //        }
            //    }
            //}

            //return new SongDetails(Guid.Empty, properties);
        }

        public string ToJson()
        {
            var stream = new MemoryStream();
            var serializer = new DataContractJsonSerializer(typeof(SongDetails));
            serializer.WriteObject(stream, this);
            return Encoding.UTF8.GetString(stream.ToArray());
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

        public void SetupSerialization(string userName, DanceStatsInstance stats)
        {
            if (RatingsList == null || RatingsList.Count == 0) return;

            var ratings = new List<DanceRating>(RatingsList.Count);
            ratings.AddRange(RatingsList.Select(rating => new DanceRatingInfo(rating, GetUserTags(userName,rating.DanceId), stats)));
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
                return null;
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

        public List<SongProperty> Properties => _properties ?? (_properties = new List<SongProperty>());
        private List<SongProperty> _properties;
        public List<ModifiedRecord> ModifiedList => _modifiedList ?? (_modifiedList = new List<ModifiedRecord>());
        private List<ModifiedRecord> _modifiedList;

        public List<DanceRating> RatingsList => _ratingsList ?? (_ratingsList = new List<DanceRating>());
        private List<DanceRating> _ratingsList;

        [DataMember]
        public TagList CurrentUserTags
        {
            get { return _currentUserTags; }
            set { throw new NotImplementedException("Shouldn't hit the setter for this.");}
        }

        [DataMember]
        public bool? CurrentUserLike
        {
            get { return _currentUserLike; }
            set { throw new NotImplementedException("Shouldn't hit the setter for this."); }
        }

        public void SetCurrentUserTags(ApplicationUser user, DanceMusicService dms)
        {
            if (user == null) return;

            _currentUserTags = UserTags(user, dms);

            if (user.UserName == null || (_currentUserTags != null && !_currentUserTags.IsEmpty)) return;

            _currentUserTags = GetUserTags(user);
        }

        private TagList _currentUserTags;
        private bool? _currentUserLike;

        public int TitleHash => CreateTitleHash(Title);

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
                if (!HasAlbums) return null;

                var ret = new StringBuilder();
                var sep = string.Empty;

                foreach (var album in Albums)
                {
                    ret.Append(sep);
                    ret.Append(album.Name);
                    sep = "|";
                }

                return ret.ToString();
            }
        }

        public AlbumDetails FindAlbum(string album, int? track = null)
        {
            if (string.IsNullOrWhiteSpace(album)) return null;

            AlbumDetails ret = null;
            var candidates = new List<AlbumDetails>();
            var title = CleanAlbum(album,Artist);

            foreach (var ad in Albums.Where(ad => string.Equals(CleanAlbum(ad.Name, Artist), title, StringComparison.CurrentCultureIgnoreCase)))
            {
                candidates.Add(ad);
                if (!string.Equals(ad.Name, album, StringComparison.CurrentCultureIgnoreCase))
                    continue;

                ret = ad;
                break;
            }

            if (ret == null && candidates.Count > 0)
            {
                if (track.HasValue)
                {
                    foreach (var ad in candidates)
                    {
                        if (ad.Track == track)
                        {
                            ret = ad;
                        }
                    }
                }
                else
                {
                    ret = candidates[0];
                }
            }

            return ret;
        }

        public bool HasAlbums => Albums != null && Albums.Count > 0;
        // "Real" albums in this case being non-ballroom compilation-type albums
        public bool HasRealAblums
        {
            get
            {
                var ret = false;
                if (HasAlbums)
                {
                    ret = Albums.Any(a => a.IsRealAlbum);
                }
                return ret;
            }
        }

        public List<AlbumDetails> CloneAlbums()
        {
            var albums = new List<AlbumDetails>(Albums.Count);
            albums.AddRange(Albums.Select(album => new AlbumDetails(album)));

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
            var oi = SortChars(pi);
            var ni = GetPurchaseTags();

            ni = ni ?? string.Empty;

            var merged = oi.Union(ni);
            return SortChars(merged);
        }

        private static string SortChars(IEnumerable<char> chars)
        {
            if (chars == null) return string.Empty;

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

        public ICollection<string> GetPurchaseIds(MusicService service)
        {
            return Albums.Select(album => album.GetPurchaseIdentifier(service.Id, PurchaseType.Song)).Where(id => id != null).ToList();
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
            var added = new HashSet<char>();

            foreach (var d in albums)
            {
                var tags = d.GetPurchaseTags();
                if (tags == null) continue;

                foreach (var c in tags.Where(c => !added.Contains(c)))
                {
                    added.Add(c);
                }
            }

            return added.Count == 0 ? null : SortChars(added);
        }
        public static int GetNextAlbumIndex(ICollection<AlbumDetails> albums)
        {
            int[] ret = {0};
            foreach (var ad in albums.Where(ad => ad.Index >= ret[0]))
            {
                ret[0] = ad.Index + 1;
            }
            return ret[0];
        }

        public static List<AlbumDetails> BuildAlbumInfo(IList<Song> songs)
        {
            var results = BuildAlbumInfo(songs[0]);

            for (var i = 1; i < songs.Count; i++)
            {
                var next = BuildAlbumInfo(songs[i]);

                foreach (var ad in next.Where(ad => results.All(d => d.Name != ad.Name)))
                {
                    results.Add(ad);
                }
            }

            return results;
        }

        public static List<AlbumDetails> BuildAlbumInfo(Song song)
        {
            var properties =
                from prop in song.SongProperties
                //                where prop.BaseName.Equals(AlbumField)
                select prop;
            return BuildAlbumInfo(properties);
        }
        public static List<AlbumDetails> BuildAlbumInfo(IEnumerable<SongProperty> properties)
        {
            var names = new List<string>(new[] {
                AlbumField,PublisherField,TrackField,PurchaseField,AlbumPromote,AlbumOrder
            });

            // First build a hashtable of index->albuminfo, maintaining the total number and the
            // high water mark of indexed albums

            var max = 0;
            var map = new Dictionary<int, AlbumDetails>();
            var removed = new Dictionary<int, AlbumDetails>();

            // Also keep a list of 'promotions' - current semantics are that if an album
            //  has a promotion it is removed and re-inserted at the head of the list
            var promotions = new List<int>();
            List<int> reorder = null;

            foreach (var prop in properties)
            {
                var name = prop.BaseName;
                var idx = prop.Index ?? 0;
                
                var qual = prop.Qualifier;

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

                    var remove = string.IsNullOrWhiteSpace(prop.Value);

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
                            reorder = prop.Value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList();
                            break;
                    }
                }
            }

            // Remove the deleted albums
            foreach (var key in removed.Keys)
            {
                map.Remove(key);
            }

            var albums = new List<AlbumDetails>(map.Count);

            // Do the (single) latest full re-order
            if (reorder != null)
            {
                albums = new List<AlbumDetails>();

                foreach (var t in reorder)
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
                for (var i = 0; i <= max; i++)
                {
                    AlbumDetails d;
                    if (map.TryGetValue(i, out d) && d.Name != null)
                    {
                        albums.Add(d);
                    }
                }
            }

            // Now do individual (trivial) promotions
            foreach (var t in promotions)
            {
                AlbumDetails d;
                if (!map.TryGetValue(t, out d) || d.Name == null) continue;

                albums.Remove(d);
                albums.Insert(0, d);
            }

            return albums;
        }

        private void BuildAlbumInfo()
        {
            var properties =
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
            var stripped = CreateNormalForm(title);
            var normal = NormalizeAlbumString(title);
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

            return ret ?? alt;
        }
        public int TrackFromAlbum(string title)
        {
            var ret = 0;

            var album = AlbumFromTitle(title);
            if (album?.Track != null)
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

            var cluster = ClusterTracks(tracks);

            // If we only have one cluster, we're set
            if (cluster.Count == 1)
            {
            }
            else if (cluster.Count != 0) // Try clustering off phase if we had any clustering at all
            {
                var clusterT = ClusterTracks(tracks, 10, 5);
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
                    var c = list.Count;
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

                var amatches = ret.Where(t => string.Equals(CleanString(t.Album), album, StringComparison.InvariantCultureIgnoreCase)).ToList();

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
            var ret = new Dictionary<int, List<ServiceTrack>>();

            foreach (var track in tracks)
            {
                if (track.Duration.HasValue)
                {
                    var cluster = (track.Duration.Value + offset) / size;
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
            return tracks.Where(track => TitleArtistMatch(track.Name, track.Artist)).ToList();
        }

        public IList<ServiceTrack> DurationFilter(IList<ServiceTrack> tracks, int epsilon)
        {
            return !Length.HasValue ? null : DurationFilter(tracks, Length.Value, epsilon);
        }

        static public IList<ServiceTrack> DurationFilter(IList<ServiceTrack> tracks, int duration, int epsilon)
        {
            return tracks.Where(track => track.Duration.HasValue && Math.Abs(track.Duration.Value - duration) < epsilon).ToList();
        }

        #endregion

        #region Index
        public static Index GetIndex(DanceMusicService dms)
        {
            if (s_index != null) return s_index;

            var fields = new List<Field>
            {
                new Field("SongId", DataType.String) {IsKey = true},
                new Field("Title", DataType.String) {IsSearchable = true, IsSortable = true, IsFilterable = true, IsFacetable = true},
                new Field("Artist", DataType.String) {IsSearchable = true, IsSortable = true, IsFilterable = false, IsFacetable = false},
                new Field("Albums", DataType.Collection(DataType.String)) {IsSearchable = true, IsSortable = false, IsFilterable = true, IsFacetable = true},
                new Field("Users", DataType.Collection(DataType.String)) {IsSearchable = true, IsSortable = false, IsFilterable = true, IsFacetable = true},
                new Field("Created", DataType.DateTimeOffset) {IsSearchable = false, IsSortable = true, IsFilterable = true, IsFacetable = true},
                new Field("Modified", DataType.DateTimeOffset) {IsSearchable = false, IsSortable = true, IsFilterable = true, IsFacetable = true},
                new Field("Tempo", DataType.Double) {IsSearchable = false, IsSortable = true, IsFilterable = true, IsFacetable = true},
                new Field("Length", DataType.Int32) {IsSearchable = false, IsSortable = true, IsFilterable = true, IsFacetable = true},
                new Field("Beat", DataType.Double) {IsSearchable = false, IsSortable = true, IsFilterable = true, IsFacetable = true},
                new Field("Energy", DataType.Double) {IsSearchable = false, IsSortable = true, IsFilterable = true, IsFacetable = true},
                new Field("Mood", DataType.Double) {IsSearchable = false, IsSortable = true, IsFilterable = true, IsFacetable = true},
                new Field("Purchase", DataType.Collection(DataType.String)) {IsSearchable = true, IsSortable = false, IsFilterable = true, IsFacetable = true},
                new Field("DanceTags", DataType.Collection(DataType.String)) {IsSearchable = true, IsSortable = false, IsFilterable = true, IsFacetable = true},
                new Field("DanceTagsInferred", DataType.Collection(DataType.String)) {IsSearchable = true, IsSortable = false, IsFilterable = true, IsFacetable = true},
                new Field("GenreTags", DataType.Collection(DataType.String)) {IsSearchable = true, IsSortable = false, IsFilterable = true, IsFacetable = true},
                new Field("StyleTags", DataType.Collection(DataType.String)) {IsSearchable = true, IsSortable = false, IsFilterable = true, IsFacetable = true},
                new Field("TempoTags", DataType.Collection(DataType.String)) {IsSearchable = true, IsSortable = false, IsFilterable = true, IsFacetable = true},
                new Field("OtherTags", DataType.Collection(DataType.String)) {IsSearchable = true, IsSortable = false, IsFilterable = true, IsFacetable = true},
                new Field("Sample", DataType.String) {IsSearchable = false, IsSortable = false, IsFilterable = false, IsFacetable = false},
                new Field("Properties", DataType.String) {IsSearchable = false, IsSortable = false, IsFilterable = false, IsFacetable = false, IsRetrievable = true},
            };

            var fsc = DanceStatsManager.GetFlatDanceStats(dms);
            fields.AddRange(
                from sc in fsc
                where sc.SongCount != 0 && sc.DanceId != "ALL"
                select new Field(BuildDanceFieldName(sc.DanceId), DataType.Int32) { IsSearchable = false, IsSortable = true, IsFilterable = false, IsFacetable = false, IsRetrievable = false });

            s_index = new Index
            {
                Name = "songs",
                Fields = fields.ToArray(),
                Suggesters = new[]
                {
                    new Suggester("songs",SuggesterSearchMode.AnalyzingInfixMatching, "Title", "Artist", "Albums", "DanceTags", "Purchase", "GenreTags", "TempoTags", "StyleTags", "OtherTags")
                }
            };

            return s_index;
        }

        public static void ResetIndex()
        {
            s_index = null;
        }
        private static Index s_index;

        public Document GetIndexDocument()
        {
            // Set up the purchase flags
            var purchase = string.IsNullOrWhiteSpace(Purchase) ? new List<string>() : Purchase.ToCharArray().Select(c => MusicService.GetService(c).Name).ToList();
            if (HasSample) purchase.Add("Sample");
            if (HasEchoNest) purchase.Add("EchoNest");

            // And the tags
            var genre = TagSummary.GetTagSet("Music");
            var other = TagSummary.GetTagSet("Other");
            var tempo = TagSummary.GetTagSet("Tempo");
            var style = new HashSet<string>();

            var dance = TagSummary.GetTagSet("Dance");
            var inferred = new HashSet<string>();

            foreach (var dr in DanceRatings)
            {
                var d = Dances.Instance.DanceFromId(dr.DanceId).Name.ToLower();
                if (!dance.Contains(d))
                {
                    inferred.Add(d);
                }
                other.UnionWith(dr.TagSummary.GetTagSet("Other"));
                tempo.UnionWith(dr.TagSummary.GetTagSet("Tempo"));
                style.UnionWith(dr.TagSummary.GetTagSet("Style"));
            }

            var users = ModifiedBy.Select(m => m.UserName.ToLower() + (m.Like.HasValue ? (m.Like.Value ? "|l" : "|h") : string.Empty)).ToArray();

            var doc = new Document
            {
                ["SongId"] = SongId.ToString(),
                [TitleField] = Title,
                [ArtistField] = Artist,
                [LengthField] = Length,
                ["Beat"] = Danceability,
                ["Energy"] = Energy,
                ["Mood"] = Valence,
                [TempoField] = (double?) Tempo,
                ["Created"] = Created,
                ["Modified"] = Modified,
                [SampleField] = Sample,
                [PurchaseField] = purchase.ToArray(),
                ["Albums"] = Albums.Select(ad => ad.Name).ToArray(),
                ["Users"] = users,
                ["DanceTags"] = dance.ToArray(),
                ["DanceTagsInferred"] = inferred.ToArray(),
                ["GenreTags"] = genre.ToArray(),
                ["TempoTags"] = tempo.ToArray(),
                ["StyleTags"] = style.ToArray(),
                ["OtherTags"] = other.ToArray(),
                ["Properties"] = SongProperty.Serialize(OrderedProperties, null)
            };

            // Set the dance ratings
            foreach (var dr in DanceRatings)
            {

                doc[BuildDanceFieldName(dr.DanceId)] = dr.Weight;
            }

            return doc;
        }

        public SongDetails(Document d, DanceStatsInstance stats)
        {
            var s = d["Properties"] as string;
            var sid = d["SongId"] as string;
            if (s == null || sid == null) throw new ArgumentOutOfRangeException(nameof(d));

            Guid id;
            if (!Guid.TryParse(sid, out id)) throw new ArgumentOutOfRangeException(nameof(d));
            SongId = id;

            var properties = new List<SongProperty>();
            SongProperty.Load(SongId, s, properties);
            Load(SongId, properties,stats);
        }

        private static string BuildDanceFieldName(string id)
        {
            return $"dance_{id}";
        }


        #endregion
    }
}
