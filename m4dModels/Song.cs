using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;
using DanceLibrary;
using Microsoft.Azure.Search.Models;
using Newtonsoft.Json;
using static System.Char;

// ReSharper disable ArrangeThisQualifier

namespace m4dModels
{
    [DataContract]
    [JsonConverter(typeof(ToStringJsonConverter))]
    public class Song : TaggableObject
    {
        #region Constants
        // These are the constants that define fields, virtual fields and command
        // TODO: Should I factor these into their own class??

        // Field names - note that these must be kept in sync with the actual property names
        public const string UserField = "User";
        public const string TimeField = "Time";
        public const string TitleField = "Title";
        public const string ArtistField = "Artist";
        public const string TempoField = "Tempo";
        public const string LengthField = "Length";
        public const string SampleField = "Sample";
        public const string DanceabilityField = "Danceability";
        public const string EnergyField = "Energy";
        public const string ValenceFiled = "Valence";
        public const string TitleHashField = "TitleHash";

        // Album Fields
        public const string AlbumField = "Album";
        public const string PublisherField = "Publisher";
        public const string TrackField = "Track";
        public const string PurchaseField = "Purchase";
        public const string AlbumListField = "AlbumList";
        public const string AlbumPromote = "PromoteAlbum";
        public const string AlbumOrder = "OrderAlbums";

        // Dance Rating
        public const string DanceRatingField = "DanceRating";

        // Tags
        public const string AddedTags = "Tag+";
        public const string RemovedTags = "Tag-";

        // User/Song info
        public const string OwnerHash = "OwnerHash";
        public const string LikeTag = "Like";

        // Proxy Fields
        public const string UserProxy = "UserProxy";

        // Azure Search Fields
        public const string SongIdField = "SongId";
        public const string AltIdField = "AlternateIds";
        public const string MoodField = "Mood";
        public const string BeatField = "Beat";
        public const string AlbumsField = "Albums";
        public const string CreatedField = "Created";
        public const string ModifiedField = "Modified";
        public const string DancesField = "Dances";
        public const string UsersField = "Users";
        public const string DanceTagsInferred = "DanceTagsInferred";
        public const string GenreTags = "GenreTags";
        public const string TempoTags = "TempoTags";
        public const string StyleTags = "StyleTags";
        public const string OtherTags = "OtherTags";
        public const string PropertiesField = "Properties";
        public const string ServiceIds = "ServiceIds";
        public const string LookupStatus = "LookupStatus";

        // Special cases for reading scraped data
        public const string TitleArtistCell = "TitleArtist";
        public const string DancersCell = "Dancers";
        public const string DanceTags = "DanceTags";
        public const string SongTags = "SongTags";
        public const string MeasureTempo = "MPM";
        public const string MultiDance = "MultiDance";

        // Commands
        public const string CreateCommand = ".Create";
        public const string EditCommand = ".Edit";
        public const string DeleteCommand = ".Delete";
        public const string MergeCommand = ".Merge";
        public const string UndoCommand = ".Undo";
        public const string RedoCommand = ".Redo";
        public const string FailedLookup = ".FailedLookup"; 
        public const string NoSongId = ".NoSongId"; // Pseudo action for serialization
        public const string SerializeDeleted = ".SerializeDeleted"; // Pseudo action for serialization

        public const string SuccessResult = ".Success";
        public const string FailResult = ".Fail";
        public const string MessageData = ".Message";

        public static readonly string[] ScalarFields = {TitleField, ArtistField, TempoField, LengthField, SampleField, DanceabilityField,EnergyField,ValenceFiled};

        public static readonly PropertyInfo[] ScalarProperties = {
            typeof(Song).GetProperty(TitleField),
            typeof(Song).GetProperty(ArtistField),
            typeof(Song).GetProperty(TempoField),
            typeof(Song).GetProperty(LengthField),
            typeof(Song).GetProperty(SampleField),
            typeof(Song).GetProperty(DanceabilityField),
            typeof(Song).GetProperty(EnergyField),
            typeof(Song).GetProperty(ValenceFiled),
        };

        public static readonly int DanceRatingCreate = 2;
        public static readonly int DanceRatingInitial = 2;
        public static readonly int DanceRatingIncrement = 2;
        public static readonly int DanceRatingAutoCreate = 1;
        public static readonly int DanceRatingDecrement = -1;

        #endregion

        #region Construction

        public Song()
        {
        }

        public Song(Guid songId, ICollection<SongProperty> properties, DanceStatsInstance stats)
        {
            Load(songId, properties, stats);
        }

        public Song(Song s, DanceStatsInstance stats, string userName = null, bool forSerialization = true)
        {
            Init(s.SongId, SongProperty.Serialize(s.SongProperties, null), stats, userName, forSerialization);
        }

        public Song(Guid guid, string s, DanceStatsInstance stats, string userName = null, bool forSerialization = true)
        {
            Init(guid, s, stats, userName, forSerialization);
        }

        public Song(string s, DanceStatsInstance stats, string userName = null, bool forSerialization = true)
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
            Init(id, s, stats, userName, forSerialization);
        }

        private void Init(Guid id, string s, DanceStatsInstance stats, string userName, bool forSerialization)
        {
            SongId = id;
            var properties = new List<SongProperty>();
            SongProperty.Load(s, properties);
            Load(SongId, properties, stats);

            if (forSerialization && stats != null) SetupSerialization(userName, stats);

            if (userName == null) return;

            _currentUserLike = ModifiedBy.FirstOrDefault(mr => mr.UserName == userName)?.Like;
        }

        public Song(string title, string artist, decimal? tempo, int? length, IList<AlbumDetails> albums)
        {
            Title = title;
            Artist = artist;
            Tempo = tempo;
            Length = length;
            _albums = (albums as List<AlbumDetails>) ?? albums?.ToList();
        }

        public void Load(Guid songId, ICollection<SongProperty> properties, DanceStatsInstance stats)
        {
            SongId = songId;

            LoadProperties(properties, stats);

            Albums = BuildAlbumInfo(properties);
            SongProperties.AddRange(properties);
        }

        public void Load(string properties, DanceStatsInstance stats)
        {
            var props = new List<SongProperty>();
            SongProperty.Load(properties, props);
            Load(SongId, props, stats);
        }

        #endregion

        #region Serialization

        public void SetupSerialization(string userName, DanceStatsInstance stats)
        {
            CurrentUserTags = GetUserTags(userName, this);
            if (DanceRatings == null || DanceRatings.Count == 0) return;

            foreach (var dr in _danceRatings)
            {
                dr.SetupSerialization(stats, dr.GetUserTags(userName, this));
            }
        }

        /// <summary>
        /// Serialize the song to a single string
        /// </summary>
        /// <param name="actions">Actions to include in the serialization</param>
        /// <returns></returns>
        public string Serialize(string[] actions)
        {
            if (string.IsNullOrWhiteSpace(Title) && (actions == null || !actions.Contains(SerializeDeleted)))
            {
                return null;
            }

            var props = SongProperty.Serialize(SongProperties, actions);
            if (actions != null && actions.Contains(NoSongId))
            {
                return props;
            }

            return Serialize(SongId.ToString("B"),props);
        }

        public static string Serialize(string id, string properties)
        {
            return $"SongId={id}\t{properties}";
        }

        public static string Serialize(string id, IEnumerable<string> properties)
        {
            return Serialize(id, string.Join("\t", properties));
        }

        public override string ToString()
        {
            return Serialize(null);
        }

        public static Song CreateFromRow(string user, IList<string> fields, IList<string> cells, DanceStatsInstance stats, int weight = 1)
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
                if ((cell.Length > 0) && (cell[0] == '"') && (cell[cell.Length - 1] == '"'))
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
                            tagProperty = UpdateFromRatings(properties, tagProperty, ratings);
                            cell = null;
                        }
                        break;
                    case MultiDance:
                        // DID|dancetag|dancetag||DID2||DID3|dancetag
                        {
                            var dts = new List<string>();
                            ratings = new List<DanceRatingDelta>();
                            foreach (var dnc in cell.Split(new[] { "||" }, StringSplitOptions.RemoveEmptyEntries))
                            {
                                var drg = dnc.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries).ToList();
                                if (drg.Count == 0 || Dances.Instance.DanceFromId(drg[0]) == null)
                                    continue;
                                ratings.Add(new DanceRatingDelta(drg[0],DanceRatingCreate));
                                string dt = null;
                                drg.RemoveAt(0);
                                if (drg.Count > 0)
                                {
                                    dt = new TagList(string.Join("|", drg)).ToString();
                                }
                                dts.Add(dt);
                            }
                            tagProperty = UpdateFromRatings(properties, tagProperty, ratings);
                            for (var di = 0; di < ratings.Count; di++)
                            {
                                if (dts[di] == null) continue;
                                properties.Add(new SongProperty(AddedTags, dts[di], -1, ratings[di].DanceId));
                            }
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
                            properties.Add(new SongProperty(TitleField, m.Groups["title"].Value));
                            properties.Add(new SongProperty(ArtistField, m.Groups["artist"].Value));
                        }
                        else
                        {
                            // TODO: Figure out a clean way to propagate errors
                            Trace.WriteLineIf(TraceLevels.General.TraceError, $"Invalid TitleArtist: {cell}");
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
                                properties.Add(new SongProperty(baseName, ids[1], 0, "IA"));
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
                            tempo = tempo * numerator;
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
                    tagProperty = UpdateTagProperty(properties, tagProperty, new TagList(tags).ToString());
                }
                tags = null;

                if (danceTags != null && ratings != null)
                {
                    var tl = new TagList(danceTags);
                    if (danceTagProperties != null && danceTagProperties.Count > 0)
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
                        foreach (var p in ratings.Select(drd => new SongProperty(AddedTags, tl.ToString(), -1, drd.DanceId)))
                        {
                            properties.Add(p);
                            danceTagProperties.Add(p);
                        }
                    }
                    danceTags = null;
                }

                if (string.IsNullOrWhiteSpace(cell)) continue;

                var idx = IsAlbumField(fields[i]) ? 0 : -1;
                var prop = new SongProperty(baseName, cell, idx, qual);
                properties.Add(prop);
            }

            const string sep = "|";
            Trace.WriteLineIf(user == null && !specifiedUser, $"Bad User for {string.Join(sep, cells)}");

            // ReSharper disable once InvertIf
            if (user != null)
            {
                if (!specifiedUser)
                {
                    properties.Insert(0, new SongProperty(TimeField, DateTime.Now.ToString(CultureInfo.InvariantCulture)));
                    properties.Insert(0, new SongProperty(UserField, user));
                }
                if (!specifiedAction)
                {
                    properties.Insert(0, new SongProperty(CreateCommand));
                }
            }

            return new Song(Guid.NewGuid(), properties, stats);
        }

        private static SongProperty UpdateTagProperty(ICollection<SongProperty> properties, SongProperty tagProperty, string extra)
        {
            if (tagProperty != null)
            {
                tagProperty.Value = TagList.Concatenate(tagProperty.Value, extra);
            }
            else
            {
                tagProperty = new SongProperty(AddedTags, extra);
                properties.Add(tagProperty);
            }
            return tagProperty;
        }

        private static SongProperty UpdateFromRatings(List<SongProperty> properties, SongProperty tagProperty, IReadOnlyCollection<DanceRatingDelta> ratings)
        {
            tagProperty = UpdateTagProperty(properties, tagProperty, TagsFromDances(ratings.Select(r => r.DanceId)));
            properties.AddRange(ratings.Select(rating => new SongProperty(DanceRatingField, rating.ToString())));

            return tagProperty;
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
                if (parts.Length > 0 && PropertyMap.TryGetValue(parts[0].ToUpper(), out field))
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
            {"MULTIDANCE", MultiDance }
        };

        public static IList<Song> CreateFromRows(string user, string separator, IList<string> headers, IEnumerable<string> rows, DanceStatsInstance stats, int weight)
        {
            var songs = new Dictionary<string, Song>();
            var itc = string.Equals(separator.Trim(), "ITC");
            var itcd = string.Equals(separator.Trim(), "ITC-");

            foreach (var line in rows)
            {
                Trace.WriteLineIf(TraceLevels.General.TraceVerbose, "Create Song From Row:" + line);
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
                        if (string.Equals(sd.Title, sd.Artist))
                        {
                            Trace.WriteLine($"Title and Artist are the same ({sd.Title})");
                        }
                        Song old;
                        if (songs.TryGetValue(ta, out old))
                        {
                            old.MergeRow(sd);
                        }
                        else
                        {
                            songs.Add(ta, sd);
                        }
                    }
                }
                else
                {
                    Trace.WriteLineIf(TraceLevels.General.TraceInfo,
                        $"Bad cell count {cells.Count} != {headers.Count}: {line}");
                }
            }

            var ret = new List<Song>(songs.Values);

            foreach (var sd in ret)
            {
                sd.InferDances(user);
            }
            return ret;
        }

        private void MergeRow(Song other)
        {
            if (other.Length.HasValue && !Length.HasValue)
            {
                Length = other.Length;
                CreateProperty(LengthField, other.Length.Value);
            }
            if (other.Tempo.HasValue && !Tempo.HasValue)
            {
                Tempo = other.Tempo;
                CreateProperty(TempoField, other.Tempo.Value);
            }

            var tagPropOther = other.LastProperty(AddedTags);
            if (tagPropOther != null)
            {
                var tagProp = LastProperty(AddedTags);
                tagProp.Value = (new TagList(tagProp.Value)).Add(new TagList(tagPropOther.Value)).ToString();
            }

            foreach (var dr in other.DanceRatings)
            {
                UpdateDanceRating(new DanceRatingDelta(dr.DanceId, dr.Weight), true);
            }
        }

        //private static readonly List<string> s_trackFields = new List<string>(new string[] {""});
        public static Song CreateFromTrack(string user, ServiceTrack track, string dances, string songTags, string danceTags, DanceStatsInstance stats)
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
        }

        public string ToJson()
        {
            var stream = new MemoryStream();
            var serializer = new DataContractJsonSerializer(typeof(Song));
            serializer.WriteObject(stream, this);
            return Encoding.UTF8.GetString(stream.ToArray());
        }


        protected void LoadProperties(ICollection<SongProperty> properties, DanceStatsInstance stats) 
        {
            var created = SongProperties != null && SongProperties.Count > 0;
            string user = null;
            ModifiedRecord currentModified = null;
            var deleted = false;

            var drDelete = new List<DanceRating>();

            foreach (var prop in properties)
            {
                var bn = prop.BaseName;

                switch (bn)
                {
                    case UserField:
                    case UserProxy:
                        user = prop.Value;
                        // TOOD: if the placeholder user works, we should use it to simplify the ModifiedRecord
                        currentModified = new ModifiedRecord {UserName = prop.Value};
                        currentModified = AddModifiedBy(currentModified);
                        break;
                    case DanceRatingField:
                        {
                            var del = SoftUpdateDanceRating(prop.Value);
                            if (del != null) drDelete.Add(del);
                        }
                        break;
                    case AddedTags:
                        if (user == null)
                        {
                            Trace.WriteLineIf(TraceLevels.General.TraceError,$"Null User when attempting to ad tag {prop.Value} to song {SongId}");
                        }
                        else
                        {
                            AddObjectTags(prop.DanceQualifier, prop.Value, stats);
                        }
                        break;
                    case RemovedTags:
                        if (user == null)
                        {
                            Trace.WriteLineIf(TraceLevels.General.TraceError, $"Null User when attempting to ad tag {prop.Value} to song {SongId}");
                        }
                        else
                        {
                            RemoveObjectTags(prop.DanceQualifier, prop.Value, stats);
                        }
                        break;
                    case AlbumField:
                    case PublisherField:
                    case TrackField:
                    case PurchaseField:
                        // All of these are taken care of with build album
                        break;
                    case DeleteCommand:
                        deleted = string.IsNullOrEmpty(prop.Value) ||
                            string.Equals(prop.Value, "true",StringComparison.OrdinalIgnoreCase);
                        break;
                    case TimeField:
                        {
                            var time = (DateTime)prop.ObjectValue;
                            if (!created)
                            {
                                Created = time;
                                created = true;
                            }
                            Modified = time;
                        }
                        break;
                    case OwnerHash:
                        if (currentModified != null)
                        {
                            currentModified.Owned = (int?) prop.ObjectValue;
                        }
                        break;
                    case LikeTag:
                        if (currentModified != null)
                        {
                            currentModified.Like = prop.ObjectValue as bool?;
                        }
                        break;
                    default:
                        // All of the simple properties we can just set
                        if (!prop.IsAction)
                        {
                            var pi = GetType().GetProperty(bn);
                            pi?.SetValue(this, prop.ObjectValue);
                        }
                        break;
                }
            }

            foreach (var dr in drDelete)
            {
                DanceRatings.Remove(dr);
            }

            if (deleted)
            {
                ClearValues();
            }
        }
        #endregion

        #region Properties

        [DataMember]
        public Guid SongId { get; set; }

        [Range(5.0, 500.0)]
        [DataMember]
        public decimal? Tempo { get; set; }
        [DataMember]
        public string Title { get; set; }
        [DataMember]
        public string Artist { get; set; }
        [Range(0, 9999)]
        [DataMember]
        public int? Length { get; set; }

        [DataMember]
        public string Purchase { get { return GetPurchaseTags(); } set {} }

        [DataMember]
        public string Sample { get; set; }
        [DataMember]
        public float? Danceability { get; set; }
        [DataMember]
        public float? Energy { get; set; }
        [DataMember]
        public float? Valence { get; set; }

        [DataMember]
        public DateTime Created { get; set; }
        [DataMember]
        public DateTime Modified { get; set; }

        [DataMember]
        public List<DanceRating> DanceRatings => _danceRatings ?? (_danceRatings = new List<DanceRating>());
        private List<DanceRating> _danceRatings;

        [DataMember]
        public List<ModifiedRecord> ModifiedBy => _modifiedBy ?? (_modifiedBy = new List<ModifiedRecord>());
        private List<ModifiedRecord> _modifiedBy;

        [DataMember]
        public List<SongProperty> SongProperties => _properties ?? (_properties = new List<SongProperty>());
        private List<SongProperty> _properties;

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

        public int TitleHash => CreateTitleHash(Title);

        [DataMember]
        public bool? CurrentUserLike
        {
            get { return _currentUserLike; }
            set { throw new NotImplementedException("Shouldn't hit the setter for this."); }
        }

        private bool? _currentUserLike;


        public bool TempoConflict(Song s, decimal delta)
        {
            return Tempo.HasValue && s.Tempo.HasValue && Math.Abs(Tempo.Value - s.Tempo.Value) > delta;
        }
        public bool IsNull => string.IsNullOrWhiteSpace(Title);

        public bool HasSample => Sample != null && Sample != ".";
        public bool HasEchoNest => Danceability != null && !float.IsNaN(Danceability.Value);

        public TimeSpan ModifiedSpan => DateTime.Now - Modified;

        public TimeSpan CreatedSpan => DateTime.Now - Created;

        public string ModifiedOrder => TimeOrder(ModifiedSpan);
        public string CreatedOrder => TimeOrder(CreatedSpan);

        public string ModifiedOrderVerbose => TimeOrderVerbose(ModifiedSpan);
        public string CreatedOrderVerbose => TimeOrderVerbose(CreatedSpan);

        private static string TimeOrder(TimeSpan span)
        {
            var seconds = span.TotalSeconds;
            if (seconds < 60) return "s";
            if (seconds < 60 * 60) return "m";
            if (seconds < 60 * 60 * 24) return "h";
            if (seconds < 60 * 60 * 24 * 7) return "D";
            if (seconds < 60*60*24*30) return "W";
            return seconds < 60*60*24*365 ? "M" : "Y";
        }

        private static string TimeOrderVerbose(TimeSpan span)
        {
            switch (TimeOrder(span))
            {
                case "s": return "seconds";
                case "m": return "minutes";
                case "h": return "hours";
                case "D": return "days";
                case "W": return "weeks";
                case "M": return "months";
                default: return "years";
            }
        }

        #endregion

        #region Actions

        public void Create(string user, string command, string value, bool addUser)
        {
            var time = DateTime.Now;

            if (!string.IsNullOrWhiteSpace(command))
            {
                CreateProperty(command, value);
            }

            Created = time;
            Modified = time;

            if (!addUser || user == null) return;

            AddUser(user);
            CreateProperty(UserField, user);
            CreateProperty(TimeField, time.ToString(CultureInfo.InvariantCulture));
        }

        public void Create(Song sd, IEnumerable<UserTag> tags, string user, string command, string value, DanceStatsInstance stats)
        {
            var addUser = !(sd.ModifiedBy != null && sd.ModifiedBy.Count > 0 && AddUser(sd.ModifiedBy[0].UserName));

            Create(user, command, value, addUser);

            // Handle User association
            if (!addUser)
            {
                // This is the Modified record created when we computed the addUser condition above
                var mr = ModifiedBy.First();
                mr.Owned = sd.ModifiedBy[0].Owned;

                CreateProperty(UserField, mr.UserName);
                CreateProperty(TimeField, Created.ToString(CultureInfo.InvariantCulture));
                if (mr.Owned.HasValue)
                    CreateProperty(OwnerHash, mr.Owned);
            }

            Debug.Assert(!string.IsNullOrWhiteSpace(sd.Title));
            foreach (var pi in ScalarProperties)
            {
                var prop = pi.GetValue(sd);
                if (prop == null) continue;

                pi.SetValue(this, prop);
                CreateProperty(pi.Name, prop);
            }

            if (tags == null)
            {
                // Handle Tags
                TagsFromProperties(user, sd.SongProperties, stats, this);

                // Handle Dance Ratings
                CreateDanceRatings(sd.DanceRatings, stats);

                DanceTagsFromProperties(user, sd.SongProperties, stats, this);
            }
            else
            {
                InternalEditTags(user, tags, stats);
            }

            // Handle Albums
            CreateAlbums(sd.Albums);

            SetTimesFromProperties();
        }

        private bool EditCore(string user, Song edit)
        {
            CreateEditProperties(user, EditCommand);

            var modified = ScalarFields.Aggregate(false, (current, field) => current | UpdateProperty(edit, field));

            var oldAlbums = BuildAlbumInfo(this);

            var foundFirst = false;

            foreach (var album in edit.Albums)
            {
                var album1 = album;
                var old = oldAlbums.FirstOrDefault(a => a.Index == album1.Index);

                if (!foundFirst && !string.IsNullOrEmpty(album.Name))
                {
                    foundFirst = true;
                }

                if (old != null)
                {
                    // We're in existing album territory
                    modified |= album.ModifyInfo(this, old);
                    oldAlbums.Remove(old);
                }
                else
                {
                    // We're in new territory only do something if the name field is non-empty
                    if (string.IsNullOrWhiteSpace(album.Name)) continue;

                    album.CreateProperties(this);
                    modified = true;
                }
            }

            // Handle deleted albums
            foreach (var album in oldAlbums)
            {
                modified = true;
                album.Remove(this);
            }

            // Now check order and insert a re-order record if they aren't line up...
            // TODO: Linq???
            var needReorder = false;
            var reorder = new List<int>();
            var prev = -1;

            foreach (var t in edit.Albums.Select(album => album.Index))
            {
                if (prev > t)
                {
                    needReorder = true;
                }
                prev = t;
                reorder.Add(t);
            }

            if (!needReorder) return modified;

            var temp = string.Join(",", reorder.Select(x => x.ToString()));
            var order = LastProperty(AlbumOrder);
            if (order?.Value == temp)
            {
                return modified;
            }
            CreateProperty(AlbumOrder, temp);

            return true;
        }

        internal bool AdminEdit(string properties, DanceStatsInstance stats)
        {
            DanceRatings.Clear();
            ModifiedBy.Clear();

            SongProperties.Clear();

            ClearValues();

            Load(properties,stats);

            Modified = DateTime.Now;

            return true;
        }

        // Edit 'this' based on SongBase + extras
        public bool Edit(string user, Song edit, IEnumerable<UserTag> tags, DanceStatsInstance stats)
        {
            var modified = EditCore(user, edit);

            modified |= UpdatePurchaseInfo(edit);
            modified |= UpdateModified(user, edit, true);

            if (tags != null)
            {
                modified |= InternalEditTags(user, tags, stats);
            }

            if (modified)
            {
                InferDances(user,true);
                Modified = DateTime.Now;
                return true;
            }

            RemoveEditProperties(user, EditCommand);

            return false;
        }

        public bool EditLike(string user, bool? like)
        {
            var modified = AddUser(user);
            var modrec = FindModified(user);
            modified |= EditLike(modrec, like);
            return modified;
        }

        public bool EditLike(ModifiedRecord modrec, bool? like)
        {
            if (modrec.Like == like) return false;

            CreateEditProperties(modrec.UserName, EditCommand);
            modrec.Like = like;
            CreateProperty(LikeTag, modrec.LikeString);
            return true;
        }

        public bool EditDanceLike(string user, bool? like, string danceId, DanceStatsInstance stats)
        {
            var r = UserDanceRating(user, danceId);

            // If the existing like value is in line with the current rating, do nothing
            if ((like.HasValue && (like.Value && r > 0 || !like.Value && r < 0)) || (!like.HasValue && r == 0))
            {
                return false;
            }

            CreateEditProperties(user, EditCommand);

            // First, neutralize existing rating
            var delta = -r;
            var tagDelta = TagsFromDances(new[] { danceId });
            var tagNeg = "!" + tagDelta;
            if (like.HasValue)
            {
                // Then, update the value for our current nudge factor in the appropriate direction
                if (like.Value)
                {
                    delta += DanceRatingIncrement;
                    AddTags(tagDelta, user, stats, this);
                    RemoveTags(tagNeg, user, stats, this);
                }
                else
                {
                    delta += DanceRatingDecrement;
                    AddTags(tagNeg, user, stats, this);
                    RemoveTags(tagDelta, user, stats, this);
                }
            }
            else
            {
                RemoveTags(tagDelta + "|" + tagNeg, user, stats, this);
            }

            UpdateDanceRating(new DanceRatingDelta { DanceId = danceId, Delta = delta }, true);
            return true;
        }

        public bool Update(string user, Song update, DanceStatsInstance stats)
        {
            // Verify that our heads are the same (TODO:move this to debug mode at some point?)
            var old = SongProperties; // Where(p => !p.IsAction).
            var upd = update.SongProperties; // Where(p => !p.IsAction).
            var c = old.Count;
            for (var i = 0; i < c; i++)
            {
                if (upd.Count >= i && string.Equals(old[i].Name, upd[i].Name)) continue;

                Trace.WriteLine($"Unexpected Update: {SongId}");
                return false;
            }

            // Nothing has changed
            if (c == upd.Count)
            {
                return false;
            }

            var mrg = new List<SongProperty>(upd.Skip(c));

            UpdateProperties(mrg, stats);

            UpdatePurchaseInfo(update);

            return true;
        }

        public void UpdateProperties(ICollection<SongProperty> properties, DanceStatsInstance stats, string[] excluded = null)
        {
            LoadProperties(properties, stats);

            foreach (var prop in properties.Where(prop => excluded == null || !excluded.Contains(prop.BaseName)))
            {
                SongProperties.Add(new SongProperty {Name = prop.Name, Value = prop.Value});
            }
        }

        public bool UpdateTagSummaries(DanceStatsInstance stats)
        {
            var changed = false;
            var delta = new Song(SongId, SongProperties, stats);
            if (!Equals(TagSummary.Summary, delta.TagSummary.Summary))
            {
                changed = UpdateTagSummary(delta.TagSummary);
            }

            foreach (var dr in DanceRatings)
            {
                var drDelta = delta.DanceRatings.FirstOrDefault(drd => drd.DanceId == dr.DanceId);
                if (drDelta == null)
                {
                    Trace.WriteLine($"Bad Comparison: {SongId}:{dr.DanceId}");
                    continue;
                }

                changed |= dr.UpdateTagSummary(drDelta.TagSummary);
            }
            return changed;
        }

        // This is an additive merge - only add new things if they don't conflict with the old
        public bool AdditiveMerge(string user, Song edit, List<string> addDances, DanceStatsInstance stats)
        {
            CreateEditProperties(user, EditCommand);

            var modified = ScalarFields.Aggregate(false, (current, field) => current | AddProperty(edit, field));

            var oldAlbums = BuildAlbumInfo(this);

            foreach (var album in edit.Albums)
            {
                var album1 = album;
                var old = oldAlbums.FirstOrDefault(a => a.Index == album1.Index);

                if (old != null)
                {
                    // We're in existing album territory
                    modified |= album.UpdateInfo(this, old);
                }
                else
                {
                    // We're in new territory only do something if the name field is non-empty
                    if (string.IsNullOrWhiteSpace(album.Name)) continue;

                    album.CreateProperties(this);
                    modified = true;
                }
            }

            if (addDances != null && addDances.Count > 0)
            {
                var tags = TagsFromDances(addDances);
                var newTags = AddTags(tags, user, stats, this);
                modified = newTags != null && !string.IsNullOrWhiteSpace(tags);

                modified |= EditDanceRatings(addDances, DanceRatingIncrement, null, 0, stats);

                InferDances(user,true);
            }
            else
            {
                // Handle Tags
                modified |= TagsFromProperties(user, edit.SongProperties, stats, this);

                // Handle Dance Ratings
                CreateDanceRatings(edit.DanceRatings, stats);

                modified |= DanceTagsFromProperties(user, edit.SongProperties, stats, this);
            }

            modified |= UpdatePurchaseInfo(edit, true);
            modified |= UpdateModified(user, edit, false);

            return modified;
        }

        public bool EditTags(string user, IEnumerable<UserTag> tags, DanceStatsInstance stats)
        {
            CreateEditProperties(user, EditCommand);

            return InternalEditTags(user, tags, stats);
        }

        public bool AddLookupFail()
        {
            if (LookupFailed()) return false;

            CreateProperty(FailedLookup,null);

            return true;
        }

        public bool LookupFailed()
        {
            return SongProperties.Any(p => p.Name == FailedLookup);
        }

        public bool LookupTried()
        {
            return SongProperties.Any(p => p.Name == FailedLookup || p.Name.StartsWith(PurchaseField));
        }

        private bool InternalEditTags(string user, IEnumerable<UserTag> tags, DanceStatsInstance stats)
        {
            var hash = new Dictionary<string, TagList>();
            foreach (var tag in tags)
            {
                hash[tag.Id] = tag.Tags;
            }

            // Possibly a bit of a kludge, but we're going to handle vote (Like/Hate) as a top level tag up to this point
            // So:  null:Like, true:Like, false:Like converts to the appropriate nullable boolean on the modified record.
            var modified = false;
            var songTags = new TagList(hash[""].Summary);
            var likeTags = songTags.Filter("Like");
            if (!likeTags.IsEmpty)
            {
                songTags = songTags.Subtract(likeTags);
                var lt = likeTags.StripType()[0];
                var like = ModifiedRecord.ParseLike(lt);

                // TODO: See if we can easily add this into the full editor
                //  Fix songfilter text to include user
                //  Fix move to advanced form to include user
                //  Make sure that login loop isn't broken
                var mr = ModifiedBy.FirstOrDefault(m => m.UserName == user);
                if (mr != null && mr.Like != like)
                {
                    CreateProperty(LikeTag, lt);
                    mr.Like = like;
                    modified = true;
                }
            }

            // First strip out all of the deleted dances and save them for later
            var deleted = songTags.ExtractPrefixed('^');
            songTags = songTags.ExtractNotPrefixed('^');

            // Next handle the top-level tags, this will incidently add any new danceratings
            //  implied by those tags
            modified |= ChangeTags(songTags, user, stats, "Dances");

            // Edit the tags for each of the dance ratings: Note that I'm stripping out blank dance ratings
            //  at the client, so need to make sure that we remove any tags from dance ratings on the server
            //  that aren't passed through in our tag list.

            foreach (var dr in DanceRatings)
            {
                TagList tl;
                if (!hash.TryGetValue(dr.DanceId, out tl))
                    tl = new TagList();
                modified |= dr.ChangeTags(tl.Summary, user, stats, this);
            }

            // Finally do the full removal of all danceratings/tags associated with the removed tags
            modified |= DeleteDanceRatings(user, deleted, stats);

            return modified;
        }

        private bool DeleteDanceRatings(string user, TagList deleted, DanceStatsInstance stats)
        {
            var ratings = new List<DanceRating>();

            // For each entry find the actual dance rating
            // ReSharper disable once LoopCanBeConvertedToQuery
            foreach (var dance in deleted.StripType())
            {
                var d = Dances.Instance.DanceFromName(dance);
                if (d == null) continue;

                var did = d.Id;
                var rating = DanceRatings.FirstOrDefault(dr => dr.DanceId == did);
                if (rating == null) continue;

                ratings.Add(rating);
            }
            if (!ratings.Any())
                return false;

            // For each user that has modified the song, back out anything having to do with the deleted dances
            var lastUser = user;
            var remove = deleted.Add(deleted.AddQualifier('!'));
            foreach (var mr in ModifiedBy)
            {
                var userModified = false;
                var u = mr.UserName;

                // Add the user property into the SongProperties
                var userProp = CreateProperty(UserProxy, u);

                // Back out any top-level tags related to the dance styles
                var songTags = GetUserTags(u);
                if (songTags != null)
                {
                    var newSongTags = songTags.Subtract(remove);
                    if (newSongTags.Summary != songTags.Summary)
                    {
                        userModified = true;
                        userModified |= ChangeTags(newSongTags, u, stats, this);
                    }
                }

                // Back out the tags directly associated with the dance style
                userModified = ratings.Aggregate(userModified, (current, rating) => current | rating.ChangeTags(string.Empty, user, stats, this));

                // If this user didn't touch anything, back out the user property
                if (userModified)
                    lastUser = u;
                else
                    TruncateProperty(userProp.Name, userProp.Value);
            }

            // If any other user 
            if (lastUser != user)
            {
                CreateProperty(UserProxy, user);
            }

            foreach (var r in ratings.Select(rating => new DanceRatingDelta { DanceId = rating.DanceId, Delta = -rating.Weight }))
            {
                UpdateDanceRating(r, true);
            }

            return true;
        }

        private void UpdateUserDanceRatings(string userName, IEnumerable<string> danceIds, int rating)
        {
            foreach (var did in danceIds)
            {
                var delta = -UserDanceRating(userName, did) + rating;
                UpdateDanceRating(new DanceRatingDelta { DanceId = did, Delta = delta }, true);
            }
        }

        private bool UpdateModified(string user, Song edit, bool force)
        {
            var mr = ModifiedBy.FirstOrDefault(m => m.UserName == user);
            if (mr == null) return false;

            var mrN = edit.ModifiedBy.FirstOrDefault(m => m.UserName == user);
            if (mrN == null || (!force && !mrN.Owned.HasValue) || (mr.Owned == mrN.Owned)) return false;

            mr.Owned = mrN.Owned;
            CreateProperty(OwnerHash, mr.Owned);
            return true;
        }

        public void MergeDetails(IEnumerable<Song> songs, DanceStatsInstance stats)
        {
            // Add in the to/from properties and create new weight table as well as creating the user associations
            var weights = new Dictionary<string, int>();
            foreach (var from in songs)
            {
                foreach (var dr in from.DanceRatings)
                {
                    int weight;
                    if (weights.TryGetValue(dr.DanceId, out weight))
                    {
                        weights[dr.DanceId] = weight + dr.Weight;
                    }
                    else
                    {
                        weights[dr.DanceId] = dr.Weight;
                    }
                }

                foreach (var us in @from.ModifiedBy.Where(us => AddUser(us.UserName)))
                {
                    CreateProperty(UserField, us.UserName);
                }

                string user = null;
                var userWritten = false;
                foreach (var prop in from.SongProperties)
                {
                    var bn = prop.BaseName;

                    // ReSharper disable once SwitchStatementMissingSomeCases
                    switch (bn)
                    {
                        case UserField:
                        case UserProxy:
                            user = prop.Value;
                            userWritten = false;
                            break;
                        case AddedTags:
                        case RemovedTags:
                            if (!userWritten)
                            {
                                CreateEditProperties(user, EditCommand);
                                userWritten = true;
                            }
                            if (bn == AddedTags)
                            {
                                AddTags(prop.Value, user, stats, this);
                            }
                            else
                            {
                                RemoveTags(prop.Value, user, stats, this);
                            }
                            break;
                    }
                }
            }

            // Dump the weight table
            foreach (var value in weights.Select(dance => new DanceRatingDelta { DanceId = dance.Key, Delta = dance.Value }.ToString()))
            {
                CreateProperty(DanceRatingField, value);
            }

        }
        private bool UpdateProperty(Song edit, string name)
        {
            // TODO: This can be optimized
            var eP = edit.GetType().GetProperty(name).GetValue(edit);
            var oP = GetType().GetProperty(name).GetValue(this);

            if (Equals(eP, oP)) return false;

            GetType().GetProperty(name).SetValue(this, eP);

            CreateProperty(name, eP);

            return true;
        }

        // Only update if the old song didn't have this property
        private bool AddProperty(Song edit, string name)
        {
            var eP = edit.GetType().GetProperty(name).GetValue(edit);
            var oP = GetType().GetProperty(name).GetValue(this);

            // Edit property is null or whitespace and Old property isn't null or whitespace
            if (NullIfWhitespace(eP) == null || NullIfWhitespace(oP) != null)
                return false;

            GetType().GetProperty(name).SetValue(this, eP);
            CreateProperty(name, eP);

            return true;
        }

        private static object NullIfWhitespace(object o)
        {
            var s = o as string;
            if (s != null && string.IsNullOrWhiteSpace(s)) o = null;

            return o;
        }

        public void CreateEditProperties(string user, string command,DateTime? time = null)
        {
            var rg = command.Split(new[] { '=' }, StringSplitOptions.RemoveEmptyEntries);
            // Add the command into the property log
            var cmd = EditCommand;
            string val = null;

            if (rg.Length > 0)
            {
                cmd = rg[0];
            }
            if (rg.Length > 1)
            {
                val = rg[1];
            }

            CreateProperty(cmd, val);

            // Handle User association
            if (user != null)
            {
                AddUser(user);
                CreateProperty(UserField, user);
            }

            // Handle Timestamps
            if (!time.HasValue)
            {
                time = DateTime.Now;
            }
            Modified = time.Value;
            CreateProperty(TimeField, time.Value.ToString(CultureInfo.InvariantCulture));
        }

        public void RemoveEditProperties(string user, string command)
        {
            TruncateProperty(TimeField);
            TruncateProperty(UserField, user);
            TruncateProperty(EditCommand);
        }

        private void TruncateProperty(string name, string value = null)
        {
            var prop = SongProperties.Last();
            if (prop.Name != name || (value != null && prop.Value != value)) return;

            SongProperties.Remove(prop);
        }

        private bool UpdatePurchaseInfo(Song edit, bool additive = false)
        {
            var pi = additive ? edit.MergePurchaseTags(Purchase) : edit.GetPurchaseTags();

            return (Purchase ?? string.Empty) != (pi ?? string.Empty);
        }

        public bool AddUser(string user, bool? like = null)
        {
            var us = new ModifiedRecord { UserName = user, Like = like };
            return AddModifiedBy(us) == us;
        }

        public void CreateAlbums(IList<AlbumDetails> albums)
        {
            if (albums == null) return;

            albums = AlbumDetails.MergeAlbums(albums, Artist, false);

            foreach (var ad in albums.Where(ad => !string.IsNullOrWhiteSpace(ad.Name)))
            {
                ad.CreateProperties(this);
            }
        }

        // If stats != null, create the properties
        public void AddDanceRating(DanceRating dr)
        {
            if (dr.DanceId == null)
            {
                dr.DanceId = dr.Dance.Id;
            }

            var other = DanceRatings.FirstOrDefault(r => r.DanceId == dr.DanceId);

            if (other == null)
            {
                DanceRatings.Add(dr);
            }
            else
            {
                other.Weight += dr.Weight;
            }
        }
        public bool CreateDanceRatings(IEnumerable<DanceRating> ratings, DanceStatsInstance stats)
        {
            if (ratings == null)
            {
                return false;
            }

            foreach (var dr in ratings)
            {
                AddDanceRating(dr.DanceId, dr.Weight, stats);
                CreateProperty(
                    DanceRatingField,
                    new DanceRatingDelta { DanceId = dr.DanceId, Delta = dr.Weight }.ToString()
                );
            }

            return true;
        }

        //  TODO: Ought to be able to refactor both of these into one that calls the other
        public bool EditDanceRatings(IEnumerable<DanceRatingDelta> deltas, DanceStatsInstance stats)
        {
            foreach (var drd in deltas)
            {
                var valid = true;
                var dro = DanceRatings.FirstOrDefault(r => r.DanceId == drd.DanceId);
                if (drd.Delta > 0)
                {
                    if (dro == null)
                    {
                        AddDanceRating(drd.DanceId, drd.Delta, stats);
                    }
                    else
                    {
                        dro.Weight += drd.Delta;
                    }
                }
                else if (drd.Delta < 0)
                {
                    if (dro == null)
                    {
                        valid = false;
                    }
                    else if (dro.Weight + drd.Delta < 0)
                    {
                        DanceRatings.Remove(dro);
                    }
                    else
                    {
                        dro.Weight += drd.Delta;
                    }
                }
                else
                {
                    valid = false;
                }

                if (valid)
                {
                    CreateProperty(DanceRatingField, drd.ToString());
                }
            }
            return true;
        }
        public bool EditDanceRatings(IList<string> addIn, int addWeight, IList<string> removeIn, int remWeight, DanceStatsInstance stats)
        {
            if (addIn == null && removeIn == null)
            {
                return false;
            }

            var changed = false;

            List<string> add = null;
            if (addIn != null)
                add = new List<string>(addIn);

            List<string> remove = null;
            if (removeIn != null)
                remove = new List<string>(removeIn);

            var del = new List<DanceRating>();

            // Cleaner way to get old dance ratings?
            foreach (var dr in DanceRatings)
            {
                var added = false;
                var delta = 0;

                // This handles the incremental weights
                if (add != null && add.Contains(dr.DanceId))
                {
                    delta = addWeight;
                    add.Remove(dr.DanceId);
                    added = true;
                }

                // This handles the decremented weights
                if (remove != null && remove.Contains(dr.DanceId))
                {
                    if (!added)
                    {
                        delta += remWeight;
                    }

                    if (dr.Weight + delta <= 0)
                    {
                        del.Add(dr);
                    }
                }

                if (delta == 0) continue;

                dr.Weight += delta;

                CreateProperty(DanceRatingField, new DanceRatingDelta { DanceId = dr.DanceId, Delta = delta }.ToString());

                changed = true;
            }

            // This handles the deleted weights
            foreach (var dr in del)
            {
                DanceRatings.Remove(dr);
            }

            // This handles the new ratings
            if (add == null) return changed;

            foreach (var ndr in add)
            {
                var dr = AddDanceRating(ndr, DanceRatingInitial, stats);

                if (dr != null)
                {
                    CreateProperty(
                        DanceRatingField,
                        new DanceRatingDelta { DanceId = ndr, Delta = DanceRatingInitial }.ToString());

                    changed = true;
                }
                else
                {
                    Trace.WriteLine($"Invalid DanceId={ndr}");
                }

            }

            return changed;
        }

        private bool BaseTagsFromProperties(string user, IEnumerable<SongProperty> properties, DanceStatsInstance stats, object data, bool dance)
        {
            var modified = false;
            foreach (var p in properties)
            {
                // ReSharper disable once SwitchStatementMissingSomeCases
                switch (p.BaseName)
                {
                    case UserField:
                    case UserProxy:
                        user = p.Value;
                        break;
                    case AddedTags:
                        var qual = p.DanceQualifier;
                        if (qual == null && !dance)
                        {
                            modified |= !AddTags(p.Value, user, stats, data).IsEmpty;
                        }
                        else if (qual != null && dance)
                        {
                            var rating = DanceRatings.FirstOrDefault(r => r.DanceId == qual);

                            if (rating != null)
                            {
                                modified = !rating.AddTags(p.Value, user, stats, data).IsEmpty;
                            }
                            // Else case is where the dancerating has been fully removed, we
                            //  can safely drop this on the floor
                        }
                        break;
                    case RemovedTags:
                        qual = p.DanceQualifier;
                        if (qual == null && !dance)
                        {
                            modified |= !RemoveTags(p.Value, user, stats, data).IsEmpty;
                        }
                        else if (qual != null && dance)
                        {
                            var rating = DanceRatings.FirstOrDefault(r => r.DanceId == qual);

                            if (rating != null)
                            {
                                modified |= !rating.RemoveTags(p.Value, user, stats, data).IsEmpty;
                            }
                            // Else case is where the dancerating has been fully removed, we
                            //  can safely drop this on the floor
                        }
                        break;
                }
            }

            return modified;
        }

        private bool TagsFromProperties(string user, IEnumerable<SongProperty> properties, DanceStatsInstance stats, object data)
        {
            return BaseTagsFromProperties(user, properties, stats, data, false);
        }

        private bool DanceTagsFromProperties(string user, IEnumerable<SongProperty> properties, DanceStatsInstance stats, object data)
        {
            // Clear out cached user tags
            return BaseTagsFromProperties(user, properties, stats, data, true);
        }

        public void Delete(string user)
        {
            if (user != null)
                CreateEditProperties(user, DeleteCommand);

            ClearValues();

            if (user != null)
                Modified = DateTime.Now;
        }

        public void RestoreScalar(Song sd)
        {
            if (!sd.SongId.Equals(Guid.Empty))
            {
                SongId = sd.SongId;
            }
            foreach (var pi in ScalarProperties)
            {
                var v = pi.GetValue(sd);
                pi.SetValue(this, v);
            }

            TagSummary = sd.TagSummary;
        }

        public bool RemoveEmptyEdits()
        {
            // Cleanup null edits
            var buffer = new List<SongProperty>();

            var users = new Dictionary<string, List<SongProperty>>();
            var activeUsers = new HashSet<string>();

            var inEmpty = false;
            string stats = null;

            foreach (var prop in SongProperties)
            {
                // Run through the properties and add all clusters of empties
                if (prop.IsAction)
                {
                    if (inEmpty)
                    {
                        if (stats != null)
                        {
                            List<SongProperty> r;
                            if (!users.TryGetValue(stats, out r))
                            {
                                r = new List<SongProperty>();
                                users[stats] = r;
                            }
                            r.AddRange(buffer);
                        }
                        buffer.Clear();
                    }
                    if (prop.Name != EditCommand) continue;

                    inEmpty = true;
                    buffer.Add(prop);
                }
                else if (prop.Name == UserField || prop.Name == TimeField)
                {
                    if (prop.Name == UserField)
                    {
                        // Count == 1 case is where the .Edit command is the only thing there
                        if (inEmpty && buffer.Count > 1)
                        {
                            if (stats != null)
                            {
                                List<SongProperty> r;
                                if (!users.TryGetValue(stats, out r))
                                {
                                    r = new List<SongProperty>();
                                    users[stats] = r;
                                }
                                r.AddRange(buffer);
                            }
                            buffer.Clear();
                        }
                        else if (stats != null)
                        {
                            activeUsers.Add(stats);
                        }

                        stats = prop.Value;
                        inEmpty = true;
                    }

                    if (inEmpty)
                    {
                        buffer.Add(prop);
                    }
                }
                else
                {
                    inEmpty = false;
                    buffer.Clear();
                }
            }

            var remove = new List<SongProperty>();
            foreach (var user in users)
            {
                if (activeUsers.Contains(user.Key))
                {
                    remove.AddRange(user.Value);
                }
                else
                {
                    var props = user.Value;
                    var u = props.FirstOrDefault(p => p.Name == UserField);
                    if (u == null) continue;

                    props.Remove(u);
                    remove.AddRange(props);
                }
            }

            if (remove.Count == 0) return false;

            foreach (var prop in remove)
            {
                SongProperties.Remove(prop);
            }

            return true;
        }

        public bool RemoveDuplicateDurations()
        {
            // Cleanup durations that are within 20 seconds of an average

            var count = 0;
            var outliers = 0;
            var avg = 0;
            SongProperty first = null;
            var remove = new List<SongProperty>();

            foreach (var prop in SongProperties)
            {
                if (prop.Name != LengthField) continue;

                var val = prop.ObjectValue;
                if (!(val is int)) continue;

                var current = (int)val;

                if (count == 0)
                {
                    avg = current;
                    first = prop;
                    count = 1;
                }
                else if (Math.Abs(avg - current) > 20)
                {
                    outliers += 1;
                    remove.Add(prop);
                }
                else
                {
                    avg = ((avg * count) + current) / (count + 1);
                    count += 1;
                    remove.Add(prop);
                }
            }

            if (remove.Count == 0 || first == null || outliers > count / 2) return false;

            first.Value = avg.ToString();

            foreach (var prop in remove)
            {
                SongProperties.Remove(prop);
            }

            return true;
        }

        public bool CleanupAlbums()
        {
            // Remove the properties for album info that has been 'deleted'
            // and if any have been removed, also get rid of promote and order

            var albums = new Dictionary<int, List<SongProperty>>();
            var remove = new List<SongProperty>();
            var deleted = new HashSet<int>();

            var changed = false;
            foreach (var prop in SongProperties)
            {
                var bn = prop.BaseName;
                var index = prop.Index ?? -1;

                switch (bn)
                {
                    case AlbumOrder:
                    case AlbumPromote:
                        remove.Add(prop);
                        break;
                    case AlbumField:
                    case TrackField:
                    case PublisherField:
                    case PurchaseField:
                        if (prop.IsNull)
                        {
                            if (bn == AlbumField)
                            {
                                deleted.Add(index);
                                // pull the previous properties and add this to removed
                                List<SongProperty> old;
                                if (albums.TryGetValue(index, out old))
                                {
                                    remove.AddRange(old);
                                    albums.Remove(index);
                                }
                                remove.Add(prop);
                                changed = true;
                            }
                            else if (deleted.Contains(index))
                            {
                                remove.Add(prop);
                                changed = true;
                            }
                        }
                        else
                        {
                            List<SongProperty> old;
                            if (!albums.TryGetValue(index, out old))
                            {
                                old = new List<SongProperty>();
                                albums[index] = old;
                            }
                            old.Add(prop);
                            if (deleted.Contains(index))
                            {
                                deleted.Remove(index);
                            }
                        }
                        break;
                }
            }

            if (remove.Count == 0 || !changed) return false;

            foreach (var prop in remove)
            {
                SongProperties.Remove(prop);
            }

            return true;
        }

        private class TagTracker
        {
            public TagTracker()
            {
                Tags = new TagList();
            }
            public TagList Tags { get; set; }
            public SongProperty Property { get; set; }
        }

        private class RatingTracker
        {
            public int Rating { get; set; }
            public SongProperty Property { get; set; }
        }

        private class UserEdits
        {
            public UserEdits()
            {
                UserTags = new Dictionary<string, TagTracker>();
                Ratings = new Dictionary<string, RatingTracker>();
            }
            public Dictionary<string, TagTracker> UserTags { get; }

            public Dictionary<string, RatingTracker> Ratings { get; }
        }

        public bool NormalizeRatings(int max = 2, int min = -1)
        {
            // This function should not semantically change the tags, but it will potentially
            //  reduce the danceratings where there were redundant entries previously and normalize based
            // on max/min

            // For each user, keep a list of the tags and danceratings that they have applied
            var users = new Dictionary<string, UserEdits>();
            var remove = new List<SongProperty>();

            string stats = null;
            UserEdits currentEdits = null;
            var changed = false;

            foreach (var prop in SongProperties)
            {
                var bn = prop.BaseName;
                switch (prop.BaseName)
                {
                    case UserField:
                    case UserProxy:
                        if (!string.Equals(stats, prop.Value, StringComparison.OrdinalIgnoreCase))
                        {
                            stats = prop.Value;
                            if (!users.TryGetValue(stats, out currentEdits))
                            {
                                currentEdits = new UserEdits();
                                users[stats] = currentEdits;
                            }
                        }
                        break;
                    case AddedTags:
                    case RemovedTags:
                        var qual = prop.DanceQualifier ?? string.Empty;
                        if (currentEdits == null)
                        {
                            Trace.WriteLine($"Tag property {prop} comes before user.");
                            break;
                        }
                        TagTracker acc;
                        if (!currentEdits.UserTags.TryGetValue(qual, out acc))
                        {
                            acc = new TagTracker { Property = prop };
                            currentEdits.UserTags[qual] = acc;
                        }
                        else
                        {
                            remove.Add(prop);
                        }
                        acc.Tags = (bn == AddedTags) ?
                            acc.Tags.Add(new TagList(prop.Value)) :
                            acc.Tags.Subtract(new TagList(prop.Value));
                        break;
                    case DanceRatingField:
                        if (currentEdits == null)
                        {
                            Trace.WriteLine($"DanceRating property {prop} comes before user.");
                            break;
                        }
                        var drd = new DanceRatingDelta(prop.Value);
                        RatingTracker rating;
                        var delta = drd.Delta;

                        // Enforce normalization of max/min values
                        if (delta > max) delta = max;
                        else if (delta < min) delta = min;

                        if (drd.Delta != delta)
                        {
                            changed = true;
                            drd.Delta = delta;
                            prop.Value = drd.ToString();
                        }

                        if (!currentEdits.Ratings.TryGetValue(drd.DanceId, out rating))
                        {
                            currentEdits.Ratings[drd.DanceId] = new RatingTracker { Rating = delta, Property = prop };
                        }
                        else
                        {
                            // Keep the vote that is in the direction that is most recent for this user, then the largest value
                            if ((Math.Sign(rating.Rating) != Math.Sign(delta)) || (Math.Abs(rating.Rating) <= Math.Abs(delta)))
                            {
                                changed = true;
                                rating.Rating = delta;
                                rating.Property.Value = drd.ToString();
                            }
                            remove.Add(prop);
                        }
                        break;
                }
            }

            foreach (var prop in remove)
            {
                SongProperties.Remove(prop);
                changed = true;
            }

            foreach (var edit in users.Values)
            {
                foreach (var tracker in edit.UserTags.Values)
                {
                    var tags = tracker.Tags.ToString();
                    if (string.Equals(tags, tracker.Property.Value))
                        continue;
                    tracker.Property.Value = tags;
                    changed = true;
                }
            }

            if (changed) SetRatingsFromProperties();

            return changed;
        }

        public bool CleanupProperties()
        {
            var changed = RemoveDuplicateDurations();
            changed |= CleanupAlbums();
            changed |= NormalizeRatings();
            changed |= RemoveEmptyEdits();

            return changed;
        }
        #endregion

        #region DanceRating

        /// <summary>
        /// Update the dance rating table based on the encoded
        ///   property value 
        /// </summary>
        /// <param name="value"></param>
        public DanceRating SoftUpdateDanceRating(string value)
        {
            var drd = new DanceRatingDelta(value);
            return SoftUpdateDanceRating(drd);
        }
        
        public void UpdateDanceRating(string value)
        {
            var drd = new DanceRatingDelta(value);
            UpdateDanceRating(drd);
        }

        public DanceRating SoftUpdateDanceRating(DanceRatingDelta drd, bool updateProperties = false)
        {
            DanceRating ret = null;

            var dr = DanceRatings.FirstOrDefault(r => r.DanceId.Equals(drd.DanceId));

            if (dr == null)
            {
                dr = new DanceRating { DanceId = drd.DanceId, Weight = 0 };
                DanceRatings.Add(dr);
            }

            dr.Weight += drd.Delta;

            if (dr.Weight == 0)
            {
                ret = dr;
            }

            if (!updateProperties) return ret;

            SongProperties.Add(new SongProperty { Name = DanceRatingField, Value = drd.ToString() });

            return ret;
        }

        public void UpdateDanceRating(DanceRatingDelta drd, bool updateProperties = false)
        {
            var dr = SoftUpdateDanceRating(drd, updateProperties);
            if (dr != null)
            {
                DanceRatings.Remove(dr);
            }
        }

        public void UpdateDanceRatings(IEnumerable<string> dances, int weight)
        {
            if (dances == null)
            {
                return;
            }

            // ReSharper disable once LoopCanBePartlyConvertedToQuery
            foreach (var d in dances)
            {
                var drd = new DanceRatingDelta { DanceId = d, Delta = weight };
                UpdateDanceRating(drd, true);
            }
        }

        public void SetRatingsFromProperties()
        {
            // First zero out all of the ratings
            foreach (var dr in DanceRatings)
            {
                dr.Weight = 0;
            }

            foreach (var prop in SongProperties.Where(p => p.Name == DanceRatingField))
            {
                SoftUpdateDanceRating(prop.Value);
            }
        }

        public static string TagsFromDances(IEnumerable<string> dances)
        {
            if (dances == null)
            {
                return null;
            }

            var tags = new StringBuilder();
            var sep = "";

            foreach (var d in dances)
            {
                tags.Append(sep);
                tags.Append(Dances.Instance.DanceFromId(d).Name);
                tags.Append(":Dance");
                sep = "|";
            }
            return tags.ToString();
        }

        public static IEnumerable<string> DancesFromTags(TagList tags)
        {
            if (tags == null || tags.IsEmpty)
            {
                return new List<string>();
            }

            return Dances.Instance.FromNames(tags.Strip()).Select(d => d.Id);
        }

        public void InferDances(string user, bool recent = false)
        {
            // Get the dances from the current user's tags
            var tags = GetUserTags(user,null,recent);

            // Infer dance groups != MSC
            var dances = TagsToDances(tags);
            var ngs = new Dictionary<string, DanceRatingDelta>();
            var groups = new HashSet<string>();

            // ReSharper disable once LoopCanBePartlyConvertedToQuery
            foreach (var dance in dances)
            {
                var dt = dance as DanceType;
                if (dt == null)
                {
                    var dg = dance as DanceGroup;
                    if (dg == null)
                        continue;
                    if (dg.Id != "MSC" && dg.Id != "LTN" && dg.Id != "PRF")
                        groups.Add(dg.Id);
                }
                else
                {
                    var g = dt.GroupId;
                    if (g == "MSC" || g == "PRF") continue;

                    if (g != "LTN")
                        groups.Add(g);

                    DanceRatingDelta drd;
                    if (ngs.TryGetValue(g, out drd))
                    {
                        drd.Delta += 1;
                    }
                    else
                    {
                        drd = new DanceRatingDelta(g, 1);
                        ngs.Add(g, drd);
                    }
                }

            }

            foreach (var ng in ngs.Values)
            {
                UpdateDanceRating(ng, true);
            }

            // If we have tempo, infer dances from group (SWG, FXT, TNG, WLZ)
            if (!Tempo.HasValue) return;

            var tempo = Tempo.Value;

            // ReSharper disable once LoopCanBeConvertedToQuery
            // ReSharper disable once LoopCanBePartlyConvertedToQuery
            foreach (var gid in groups)
            {
                var dg = Dances.Instance.DanceFromId(gid) as DanceGroup;
                if (dg == null) continue;

                // ReSharper disable once LoopCanBePartlyConvertedToQuery
                foreach (var dto in dg.Members)
                {
                    var dt = dto as DanceType;
                    if (dt == null || !dt.TempoRange.ToBpm(dt.Meter).Contains(tempo)) continue;

                    var drd = new DanceRatingDelta(dt.Id, 1);
                    UpdateDanceRating(drd, true);
                }
            }
        }

        public int UserDanceRating(string user, string danceId)
        {
            var level = 0;
            var ratings = FilteredProperties(DanceRatingField, null, new HashSet<string>(new[] {user}));
            foreach (var rating in ratings.Where(p => p.Value.StartsWith(danceId)))
            {
                int t;
                var v = rating.Value;
                var i = v.IndexOfAny(new[] { '+', '-' });
                if (i == -1 || !int.TryParse(v.Substring(i), out t)) continue;

                level += t;
            }
            return level;
        }

        public void UpdateDanceRatingsAndTags(string user, IEnumerable<string> dances, int weight, DanceStatsInstance stats)
        {
            var enumerable = dances as IList<string> ?? dances.ToList();
            var tags = TagsFromDances(enumerable);
            var added = AddTags(tags, user,stats);
            if (added != null && !added.IsEmpty)
                SongProperties.Add(new SongProperty(AddedTags, added.ToString()));
            UpdateDanceRatings(enumerable, weight);
        }

        private DanceRating AddDanceRating(string danceId, int weight, DanceStatsInstance stats)
        {
            var ds = stats.FromId(danceId);
            if (ds == null) return null;

            return new DanceRating {Dance = ds.Dance, DanceId = danceId, Weight = weight};
        }

        #endregion

        #region Tags
        public void ChangeDanceTags(string danceId, string tags, string user, DanceStatsInstance stats)
        {
            var dr = FindRating(danceId);
            if (dr != null)
            {
                dr.ChangeTags(tags, user, stats, this);
            }
            else
            {
                Trace.WriteLineIf(TraceLevels.General.TraceError, $"Undefined DanceRating {SongId.ToString()}:{danceId}");
            }
        }

        public override void RegisterChangedTags(TagList added, TagList removed, string user, object data)
        {
            var test = data as string;
            if (string.Equals("Dances", test, StringComparison.OrdinalIgnoreCase))
            {
                var dts = added?.Filter("Dance") ?? new TagList();
                var dtr = removed?.Filter("Dance") ?? new TagList();

                if (!dts.IsEmpty || !dtr.IsEmpty)
                {
                    var likes = DancesFromTags(dts.ExtractNotPrefixed('!'));
                    var hates = DancesFromTags(dts.ExtractPrefixed('!'));
                    var nulls =
                        DancesFromTags(dtr.ExtractPrefixed('!').Add(dtr.ExtractNotPrefixed('!')))
                            .Where(x => !likes.Contains(x) && !hates.Contains(x));
                    UpdateUserDanceRatings(user, likes, DanceRatingIncrement);
                    UpdateUserDanceRatings(user, hates, DanceRatingDecrement);
                    UpdateUserDanceRatings(user, nulls, 0);

                    // TODO:Dance tags have a property where there may be a "!" version (hate), we need
                    //  to explicity disallow having both the like and hate, but if we do it here we'll remove
                    //  things that don't need to be removed.
                    //removed = removed ?? new TagList();
                    //removed = dts.Tags.Aggregate(removed, 
                    //    (current, tag) => current.Add(tag.StartsWith("!") ? tag.Substring(1) : "!" + tag));
                }
            }

            base.RegisterChangedTags(added, removed, user, data);

            if (data == null) return;

            ChangeTag(AddedTags, added);
            ChangeTag(RemovedTags, removed);
        }

        public void ChangeTag(string command, TagList list)
        {
            // NOTE: The user is implied by this operation because there should be an edit header record before it
            var tags = list?.ToString();
            if (!string.IsNullOrWhiteSpace(tags))
            {
                CreateProperty(command, tags);
            }
        }

        public TagList AddObjectTags(string qualifier, string tags, DanceStatsInstance stats)
        {
            TaggableObject tobj = this;
            if (!string.IsNullOrWhiteSpace(qualifier))
                tobj = FindRating(qualifier);

            return tobj.AddTags(tags, stats);
        }

        public TagList RemoveObjectTags(string qualifier, string tags, DanceStatsInstance stats)
        {
            TaggableObject tobj = this;
            if (!string.IsNullOrWhiteSpace(qualifier))
                tobj = FindRating(qualifier);

            return tobj?.RemoveTags(tags, stats);
        }

        protected override HashSet<string> ValidClasses => s_validClasses;

        private static readonly HashSet<string> s_validClasses = new HashSet<string> { "dance","music","tempo","other" };

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
            var title = CleanAlbum(album, Artist);

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
        public ICollection<PurchaseLink> GetPurchaseLinks(string service = "AIXS", string region = null)
        {
            var links = new List<PurchaseLink>();
            service = service.ToUpper();

            foreach (var ms in MusicService.GetServices())
            {
                if (!service.Contains(ms.CID)) continue;

                foreach (var album in Albums)
                {
                    var l = album.GetPurchaseLink(ms.Id, region);
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

        public ICollection<string> GetExtendedPurchaseIds()
        {
            return Albums.SelectMany(album => album.GetExtendedPurchaseIds(PurchaseType.Song)).ToList();
        }

        public string GetPurchaseId(ServiceType service)
        {
            string ret = null;
            foreach (var album in Albums)
            {
                ret = album.GetPurchaseIdentifier(service, PurchaseType.Song);
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
            int[] ret = { 0 };
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

        //private void BuildAlbumInfo()
        //{
        //    var properties =
        //        from prop in SongProperties
        //        select prop;

        //    Albums = BuildAlbumInfo(properties);
        //}
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
            var ordered = RankTracksByCluster(tracks, null);
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
                return RankTracksByCluster(tracks, album);
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
                    if (!ret.TryGetValue(cluster, out list))
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
                new Field(SongIdField, Microsoft.Azure.Search.Models.DataType.String) {IsKey = true},
                new Field(AltIdField, Microsoft.Azure.Search.Models.DataType.Collection(Microsoft.Azure.Search.Models.DataType.String)) {IsSearchable = false, IsSortable = false, IsFilterable = true, IsFacetable = false},
                new Field(TitleField, Microsoft.Azure.Search.Models.DataType.String) {IsSearchable = true, IsSortable = true, IsFilterable = true, IsFacetable = true},
                new Field(TitleHashField, Microsoft.Azure.Search.Models.DataType.Int32) {IsSearchable = false, IsSortable = false, IsFilterable = true, IsFacetable = false},
                new Field(ArtistField, Microsoft.Azure.Search.Models.DataType.String) {IsSearchable = true, IsSortable = true, IsFilterable = false, IsFacetable = false},
                new Field(AlbumsField, Microsoft.Azure.Search.Models.DataType.Collection(Microsoft.Azure.Search.Models.DataType.String)) {IsSearchable = true, IsSortable = false, IsFilterable = true, IsFacetable = true},
                new Field(UsersField, Microsoft.Azure.Search.Models.DataType.Collection(Microsoft.Azure.Search.Models.DataType.String)) {IsSearchable = true, IsSortable = false, IsFilterable = true, IsFacetable = true},
                new Field(CreatedField, Microsoft.Azure.Search.Models.DataType.DateTimeOffset) {IsSearchable = false, IsSortable = true, IsFilterable = true, IsFacetable = true},
                new Field(ModifiedField, Microsoft.Azure.Search.Models.DataType.DateTimeOffset) {IsSearchable = false, IsSortable = true, IsFilterable = true, IsFacetable = true},
                new Field(TempoField, Microsoft.Azure.Search.Models.DataType.Double) {IsSearchable = false, IsSortable = true, IsFilterable = true, IsFacetable = true},
                new Field(LengthField, Microsoft.Azure.Search.Models.DataType.Int32) {IsSearchable = false, IsSortable = true, IsFilterable = true, IsFacetable = true},
                new Field(BeatField, Microsoft.Azure.Search.Models.DataType.Double) {IsSearchable = false, IsSortable = true, IsFilterable = true, IsFacetable = true},
                new Field(EnergyField, Microsoft.Azure.Search.Models.DataType.Double) {IsSearchable = false, IsSortable = true, IsFilterable = true, IsFacetable = true},
                new Field(MoodField, Microsoft.Azure.Search.Models.DataType.Double) {IsSearchable = false, IsSortable = true, IsFilterable = true, IsFacetable = true},
                new Field(PurchaseField, Microsoft.Azure.Search.Models.DataType.Collection(Microsoft.Azure.Search.Models.DataType.String)) {IsSearchable = true, IsSortable = false, IsFilterable = true, IsFacetable = true},
                new Field(LookupStatus, Microsoft.Azure.Search.Models.DataType.Boolean) {IsSearchable = false, IsSortable = false, IsFilterable = true, IsFacetable = false},
                new Field(DanceTags, Microsoft.Azure.Search.Models.DataType.Collection(Microsoft.Azure.Search.Models.DataType.String)) {IsSearchable = true, IsSortable = false, IsFilterable = true, IsFacetable = true},
                new Field(DanceTagsInferred, Microsoft.Azure.Search.Models.DataType.Collection(Microsoft.Azure.Search.Models.DataType.String)) {IsSearchable = true, IsSortable = false, IsFilterable = true, IsFacetable = true},
                new Field(GenreTags, Microsoft.Azure.Search.Models.DataType.Collection(Microsoft.Azure.Search.Models.DataType.String)) {IsSearchable = true, IsSortable = false, IsFilterable = true, IsFacetable = true},
                new Field(StyleTags, Microsoft.Azure.Search.Models.DataType.Collection(Microsoft.Azure.Search.Models.DataType.String)) {IsSearchable = true, IsSortable = false, IsFilterable = true, IsFacetable = true},
                new Field(TempoTags, Microsoft.Azure.Search.Models.DataType.Collection(Microsoft.Azure.Search.Models.DataType.String)) {IsSearchable = true, IsSortable = false, IsFilterable = true, IsFacetable = true},
                new Field(OtherTags, Microsoft.Azure.Search.Models.DataType.Collection(Microsoft.Azure.Search.Models.DataType.String)) {IsSearchable = true, IsSortable = false, IsFilterable = true, IsFacetable = true},
                new Field(SampleField, Microsoft.Azure.Search.Models.DataType.String) {IsSearchable = false, IsSortable = false, IsFilterable = true, IsFacetable = false},
                new Field(ServiceIds, Microsoft.Azure.Search.Models.DataType.Collection(Microsoft.Azure.Search.Models.DataType.String)) {IsSearchable = true, IsSortable = false, IsFilterable = false, IsFacetable = false},
                new Field(PropertiesField, Microsoft.Azure.Search.Models.DataType.String) {IsSearchable = false, IsSortable = false, IsFilterable = false, IsFacetable = false, IsRetrievable = true},
            };

            var fsc = DanceStatsManager.GetFlatDanceStats(dms);
            fields.AddRange(
                from sc in fsc
                where sc.SongCount != 0 && sc.DanceId != "ALL"
                select new Field(BuildDanceFieldName(sc.DanceId), Microsoft.Azure.Search.Models.DataType.Int32) { IsSearchable = false, IsSortable = true, IsFilterable = false, IsFacetable = false, IsRetrievable = false });

            s_index = new Index
            {
                Name = "songs",
                Fields = fields.ToArray(),
                Suggesters = new[]
                {
                    new Suggester("songs",SuggesterSearchMode.AnalyzingInfixMatching, TitleField, ArtistField, AlbumsField, DanceTags, PurchaseField, GenreTags, TempoTags, StyleTags, OtherTags)
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

            var purchaseIds = GetExtendedPurchaseIds();

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

            var altIds = new string[0];
            var merges = FilteredProperties(MergeCommand).ToList();
            if (merges.Any())
            {
                altIds =
                    merges.SelectMany(m => m.Value.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                        .ToArray();
            }

            var doc = new Document
            {
                [SongIdField] = SongId.ToString(),
                [AltIdField] = altIds,
                [TitleField] = Title,
                [TitleHashField] = TitleHash,
                [ArtistField] = Artist,
                [LengthField] = Length,
                [BeatField] = Danceability,
                [EnergyField] = Energy,
                [MoodField] = Valence,
                [TempoField] = (double?)Tempo,
                [CreatedField] = Created,
                [ModifiedField] = Modified,
                [SampleField] = Sample,
                [PurchaseField] = purchase.ToArray(),
                [ServiceIds] = purchaseIds.ToArray(),
                [LookupStatus] = LookupTried(),
                [AlbumsField] = Albums.Select(ad => ad.Name).ToArray(),
                [UsersField] = users,
                [DanceTags] = dance.ToArray(),
                [DanceTagsInferred] = inferred.ToArray(),
                [GenreTags] = genre.ToArray(),
                [TempoTags] = tempo.ToArray(),
                [StyleTags] = style.ToArray(),
                [OtherTags] = other.ToArray(),
                [PropertiesField] = SongProperty.Serialize(SongProperties, null)
            };

            // Set the dance ratings
            foreach (var dr in DanceRatings)
            {
                doc[BuildDanceFieldName(dr.DanceId)] = dr.Weight;
            }

            return doc;
        }

        public Song(Document d, DanceStatsInstance stats, string userName = null)
        {
            var s = d[PropertiesField] as string;
            var sid = d[SongIdField] as string;
            if (s == null || sid == null) throw new ArgumentOutOfRangeException(nameof(d));

            Guid id;
            if (!Guid.TryParse(sid, out id)) throw new ArgumentOutOfRangeException(nameof(d));

            Init(id, s, stats, userName, true);
        }

        private static string BuildDanceFieldName(string id)
        {
            return $"dance_{id}";
        }


        #endregion

        #region Comparison
        //  Two song are equivalent if Titles are equal, artists are similar or empty and all other fields are equal
        public bool Equivalent(Song song)
        {
            return WeakEquivalent(song); // TDKILL - Match any album in this to any album in song....
        }


        public bool WeakEquivalent(Song song)
        {
            return TitleArtistEquivalent(song) &&
                EqNum(Tempo, song.Tempo) && EqNum(Length, song.Length);
        }

        // Same as equivalent (above) except that album, Tempo and Length aren't compared.
        public bool TitleArtistEquivalent(Song song)
        {
            // No-similar titles != equivalent
            if (CreateTitleHash(Title) != CreateTitleHash(song.Title))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(Artist) && !string.IsNullOrWhiteSpace(song.Artist) &&
                (CreateTitleHash(Artist) != CreateTitleHash(song.Artist)))
            {
                return false;
            }

            return true;
        }

        #endregion

        public IDictionary<string,IList<string>> MapProperyByUsers(string name)
        {
            var map = new Dictionary<string, IList<string>>();
            var current = new List<string> {""};

            var inUsers = false;
            foreach (var prop in SongProperties)
            {
                if (prop.BaseName == UserField || prop.BaseName == UserProxy)
                {
                    if (!inUsers)
                    {
                        current = new List<string>();
                        inUsers = true;
                    }
                    current.Add(prop.Value);
                }
                else
                {
                    inUsers = false;
                    if (prop.BaseName != name) continue;

                    foreach (var user in current)
                    {
                        IList<string> values;
                        if (!map.TryGetValue(user, out values))
                        {
                            values = new List<string>();
                            map[user] = values;
                        }
                        values.Add(prop.Value);
                    }
                }
            }

            return map;
        }

        public DanceRating FindRating(string id)
        {
            return DanceRatings.FirstOrDefault(r => string.Equals(r.DanceId, id, StringComparison.OrdinalIgnoreCase));
        }

        public ModifiedRecord FindModified(string userName)
        {
            return ModifiedBy.FirstOrDefault(mr => string.Equals(mr.UserName, userName, StringComparison.OrdinalIgnoreCase));
        }

        protected ModifiedRecord AddModifiedBy(ModifiedRecord mr)
        {
            ModifiedRecord other = null;

            if (mr.UserName != null)
            {
                other = ModifiedBy.FirstOrDefault(r => r.UserName == mr.UserName);
            }

            if (other != null) return other;

            ModifiedBy.Add(mr);
            return mr;
        }

        public SongProperty CreateProperty(string name, object value)
        {
            var prop = new SongProperty { Name = name, Value = value?.ToString() };
            SongProperties.Add(prop);

            return prop;
        }

        public bool SetTimesFromProperties(IEnumerable<string> excludedUsers = null)
        {
            var modified = false;
            var first = FirstProperty(TimeField);
            var firstTime = first?.ObjectValue as DateTime?;
            if (firstTime == null) return false;
            if (Created != firstTime)
            {
                Created = firstTime.Value;
                modified = true;
            }

            var last = LastProperty(TimeField,excludedUsers);
            var lastTime = last?.ObjectValue as DateTime?;

            if (lastTime == null || Modified == lastTime) return modified;

            Modified = lastTime.Value;
            return true;
        }

        public SongProperty FirstProperty(string name)
        {
            return SongProperties.FirstOrDefault(p => p.Name == name);
        }

        public SongProperty LastProperty(string name, IEnumerable<string> excludeUsers = null, IEnumerable<string> includeUsers = null)
        {
            return FilteredProperties(excludeUsers,includeUsers).LastOrDefault(p => p.Name == name);
        }

        public IEnumerable<SongProperty> FilteredProperties(string baseName, IEnumerable<string> excludeUsers = null, IEnumerable<string> includeUsers = null)
        {
            return FilteredProperties(excludeUsers, includeUsers).Where(p => p.BaseName == baseName);
        }

        public IEnumerable<SongProperty> FilteredProperties(IEnumerable<string> excludeUsers = null, IEnumerable<string> includeUsers = null)
        {
            if (excludeUsers == null && includeUsers == null) return SongProperties;

            var eu = (excludeUsers == null) ? null : (excludeUsers as HashSet<string> ?? new HashSet<string>(excludeUsers));
            var iu = (includeUsers == null) ? null : (includeUsers as HashSet<string> ?? new HashSet<string>(includeUsers));

            var ret = new List<SongProperty>();

            var inFilter = includeUsers != null;
            foreach (var prop in SongProperties)
            {
                if (prop.BaseName == UserField || prop.BaseName == UserProxy)
                {
                    if (eu != null)
                    {
                        inFilter =  eu.Contains(prop.Value);
                    }
                    else // (ie != null)
                    {
                        inFilter = !iu.Contains(prop.Value);
                    }
                }

                if (!inFilter)
                    ret.Add(prop);
            }

            return ret;
        }

        protected void ClearValues()
        {
            foreach (var pi in ScalarProperties)
            {
                pi.SetValue(this, null);
            }

            TagSummary.Clean();

            if (DanceRatings != null)
            {
                var drs = DanceRatings.ToList();
                foreach (var dr in drs)
                {
                    DanceRatings.Remove(dr);
                }
            }

            if (ModifiedBy != null)
            {
                var us = ModifiedBy.ToList();
                foreach (var u in us)
                {
                    ModifiedBy.Remove(u);
                }
            }
        }

        #region TitleArtist

        public bool TitleArtistMatch(string title, string artist)
        {
            if (!SoftArtistMatch(artist, Artist))
                return false;

            return DoMatch(CreateNormalForm(title), CreateNormalForm(Title)) |
                   DoMatch(NormalizeAlbumString(title), NormalizeAlbumString(Title)) |
                   DoMatch(CleanAlbum(title, artist), CleanAlbum(Title, Artist));
        }

        public static bool SoftArtistMatch(string artist1, string artist2)
        {
            // Artist Soft Match
            var a1 = BreakDownArtist(artist1);
            var a2 = BreakDownArtist(artist2);

            // Start with the easy case where we've got a single name artist on one side or the other
            // If not, we require an overlap of two
            if (!((a1.Count == 1 && a2.Contains(a1.First()) || a2.Count == 1 && a1.Contains(a2.First())) ||
                  a1.Count(s => a2.Contains(s)) > 1))
            {
                Trace.WriteLineIf(TraceLevels.General.TraceVerbose, $"AFAIL '{string.Join(",", a1)}' - '{string.Join(",", a2)}'");
                return false;
            }

            Trace.WriteLineIf(TraceLevels.General.TraceVerbose, $"ASUCC '{string.Join(",", a1)}' - '{string.Join(",", a2)}'");
            return true;
        }

        private static HashSet<string> BreakDownArtist(string artist)
        {
            var bits = NormalizeAlbumString(artist??"", true).ToUpper().Split(new[] {' '}, StringSplitOptions.RemoveEmptyEntries);
            var init = new HashSet<string>(bits);
            init.RemoveWhere(s => ArtistIgnore.Contains(s));
            return init;
        }
        private static bool DoMatch(string s1, string s2)
        {
            var ret = string.Equals(s1, s2, StringComparison.OrdinalIgnoreCase);
            var rv = ret ? "==" : "!=";
            Trace.WriteLineIf(TraceLevels.General.TraceVerbose,$"{rv}{s1}{s2}");
            return ret;
        }
        public string TitleArtistString => CreateNormalForm(Title) + "+" + CreateNormalForm(Artist);

        public string TitleArtistAlbumString
        {
            get
            {
                var ta = TitleArtistString;
                var album = Albums.FirstOrDefault()?.Name;

                return (album == null) ? ta : ta + "+" + CreateNormalForm(album);
            }
        }

        public string CleanTitle => CleanString(Title);
        public string CleanArtist => CleanString(Artist);

        #endregion

        #region Static Utility Functions
        protected static bool EqString(string s1, string s2)
        {
            return string.IsNullOrWhiteSpace(s1) || string.IsNullOrWhiteSpace(s2) ||
                string.Equals(s1, s2, StringComparison.InvariantCultureIgnoreCase);
        }
        protected static bool EqNum<T>(T? t1, T? t2) where T : struct
        {
            return !t1.HasValue || !t2.HasValue || t1.Value.Equals(t2.Value);
        }

        protected static string BuildSignature(string artist, string title)
        {
            artist = artist ?? string.Empty;
            title = title ?? string.Empty;

            var ret = $"{CreateNormalForm(artist)} {CreateNormalForm(title)}";

            return string.IsNullOrWhiteSpace(ret) ? null : ret;
        }
        public static Song GetNullSong()
        {
            return new Song();
        }

        public static int TryParseId(string s, out Guid id)
        {
            const string idField = SongIdField + "=";
            id = Guid.Empty;
            if (!s.StartsWith(SongIdField)) return 0;

            var t = s.IndexOf('\t');
            if (t == -1) return 0;

            var sg = s.Substring(idField.Length, t - idField.Length);
            Guid g;
            if (Guid.TryParse(sg, out g))
            {
                id = g;
            }

            return t + 1;
        }

        public static int CreateTitleHash(string title)
        {
            return CreateNormalForm(title).GetHashCode();
        }

        public static bool IsAlbumField(string fieldName)
        {
            if (string.IsNullOrWhiteSpace(fieldName))
            {
                return false;

            }

            var baseName = SongProperty.ParseBaseName(fieldName);
            return AlbumFields.Contains(baseName);
        }

        protected static HashSet<string> AlbumFields = new HashSet<string> { AlbumField, PublisherField, TrackField, PurchaseField, AlbumListField, AlbumPromote, AlbumOrder };

        // TOOD: This should really end up in a utility class as some point
        public static string MungeString(string s, bool normalize, IEnumerable<string> extraIgnore = null)
        {
            if (string.IsNullOrWhiteSpace(s))
            {
                return string.Empty;
            }

            var sb = new StringBuilder(s.Length);

            var norm = s + '|';
            if (normalize)
            {
                norm = norm.Normalize(NormalizationForm.FormD);
            }

            var wordBreak = 0;

            var paren = false;
            var bracket = false;
            var space = false;
            var lastC = ' ';

            var ignore = new HashSet<string>(Ignore);
            if (extraIgnore != null)
            {
                foreach (var i in extraIgnore)
                {
                    ignore.Add(i);
                }
            }

            for (var i = 0; i < norm.Length; i++)
            {
                var c = norm[i];

                if (paren)
                {
                    if (c == ')')
                    {
                        paren = false;
                    }
                }
                else if (bracket)
                {
                    if (c == ']')
                    {
                        bracket = false;
                    }
                }
                else
                {
                    if (IsLetterOrDigit(c))
                    {
                        if (!normalize && space)
                        {
                            sb.Append(' ');
                            space = false;
                        }
                        var cNew = normalize ? ToUpper(c) : c;
                        sb.Append(cNew);
                        lastC = cNew;
                    }
                    else if (!normalize && c == '\'' && IsLetter(lastC) && norm.Length > i + 1 && IsLetter(norm[i + 1]))
                    {
                        // Special case apostrophe (e.g. that's)
                        sb.Append(c);
                        lastC = c;
                    }
                    else
                    {
                        var uc = GetUnicodeCategory(c);
                        if (uc != UnicodeCategory.NonSpacingMark && sb.Length > wordBreak)
                        {
                            var word = sb.ToString(wordBreak, sb.Length - wordBreak).ToUpper().Trim();
                            if (ignore.Contains(word))
                            {
                                sb.Length = wordBreak;
                            }
                            wordBreak = sb.Length;
                        }
                        // ReSharper disable once SwitchStatementMissingSomeCases
                        switch (c)
                        {
                            case '(':
                                paren = true;
                                break;
                            case '[':
                                bracket = true;
                                break;
                        }

                        space = true;
                    }
                }
            }

            return sb.ToString().Trim();
        }
        public static string CreateNormalForm(string s)
        {
            return MungeString(s, true);
        }
        public static string CleanString(string s)
        {
            return MungeString(s, false);
        }

        public static string NormalizeAlbumString(string s, bool keepWhitespace=false)
        {
            var r = s;
            if (string.IsNullOrWhiteSpace(r)) return r;

            s = r.Normalize(NormalizationForm.FormD);

            var sb = new StringBuilder();
            foreach (var c in s.Where(t => IsLetterOrDigit(t) || (keepWhitespace && IsWhiteSpace(t))))
            {
                sb.Append(c);
            }
            r = sb.ToString();

            return r;
        }

        private static readonly char[] Digits = {'0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '.', '-'};

        private static string TruncateAlbum(string album, char delimiter)
        {
            var idx = album.LastIndexOf(delimiter);
            return (idx == -1 || idx < 10 || idx < album.Length/4) ? album : album.Substring(0, idx);
        }

        public static string CleanAlbum(string album, string artist)
        {
            if (string.IsNullOrWhiteSpace(album)) return null;

            var ignore = new List<string> { "VOL", "VOLS", "VOLUME", "VOLUMES" };

            if (!string.IsNullOrWhiteSpace(artist))
            {
                var words = artist.ToUpper().Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                if (words.Length > 0)
                {
                    ignore.AddRange(words.Select(s => NormalizeAlbumString(s)).ToList());
                }
            }

            album = album.Trim();

            var albumBase = album;
            album = album.TrimEnd(Digits);
            album = TruncateAlbum(album, '-');
            album = TruncateAlbum(album, ':');

            var ret = MungeString(album,true,ignore);
            if (string.IsNullOrWhiteSpace(ret))
            {
                ret = MungeString(albumBase, true, ignore);
            }
            return ret;
        }

        protected static string[] Ignore =
        {
            "A",
            "AT",
            "AND",
            "FROM",
            "IN",
            "OF",
            "OR",
            "THE",
            "THAT",
            "THIS"
        };

        private static readonly string[] ArtIgnore = { "BAND", "FEAT", "FEATURING", "HIS", "HER", "ORCHESTRA", "WITH"};

        private static readonly HashSet<string> ArtistIgnore = new HashSet<string>(Ignore.Concat(ArtIgnore));
        protected static IList<string> TagsToDanceIds(TagList tags)
        {
            return TagsToDances(tags).Select(d => d.Id).ToList();
        }

        protected static IList<DanceObject> TagsToDances(TagList tags)
        {
            return Dances.Instance.FromNames(tags.Filter("Dance").StripType()).ToList();
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

        #endregion

        #region String Cleanup
        static public string CleanDanceName(string name)
        {
            var up = name.ToUpper();

            var parts = up.Split(new[] { ' ', '-', '\t', '/', '&', '-', '+', '(', ')' }, StringSplitOptions.RemoveEmptyEntries);

            var ret = string.Join("", parts);

            if (ret.LastIndexOf('S') != ret.Length - 1) return ret;

            var truncate = 1;
            if (ret.LastIndexOf('E') == ret.Length - 2)
            {
                if (ret.Length > 2)
                {
                    var ch = ret[ret.Length - 3];
                    if (ch != 'A' && ch != 'E' && ch != 'I' && ch != 'O' && ch != 'U')
                    {
                        truncate = 2;
                    }
                }
            }
            ret = ret.Substring(0, ret.Length - truncate);

            return ret;
        }

        static public string CleanText(string text)
        {
            text = WebUtility.HtmlDecode(text);
            text = text.Replace("\r", " ");
            text = text.Replace("\n", " ");
            text = text.Replace("\t", " ");
            text = text.Trim();

            if (!text.Contains("  ")) return text;

            var sb = new StringBuilder(text.Length + 1);

            var space = false;
            foreach (var c in text)
            {
                if (IsWhiteSpace(c))
                {
                    if (!space)
                    {
                        sb.Append(c);
                    }
                    space = true;
                }
                else
                {
                    sb.Append(c);
                    space = false;
                }
            }

            text = sb.ToString();

            return text;
        }

        static public string CleanQuotes(string name)
        {
            return CustomTrim(CustomTrim(name,'"'),'\'');
        }

        static public string CustomTrim(string name, char quote)
        {
            if ((name.Length > 0) && (name[0] == quote) && (name[name.Length - 1] == quote))
            {
                name = name.Trim(quote);
            }
            return name;
        }

        static public string Unsort(string name)
        {
            var parts = name.Split(',');
            if (parts.Length == 1 || parts.Any(s => s.All(c => !IsLetter(c))))
            {
                return name.Trim();
            }
            if (parts.Length == 2)
            {
                return $"{parts[1].Trim()} {parts[0].Trim()}";
            }
            Trace.WriteLine($"Unusual Sort: {name}");
            return name;
        }

        static public string CleanArtistString(string name)
        {
            if (name.IndexOf(',') == -1) return name;

            var parts = new[] { name };
            if (name.IndexOf('&') != -1)
                parts = name.Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries);
            else if (name.IndexOf('/') != -1)
                parts = name.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

            var separator = string.Empty;
            var sb = new StringBuilder();

            foreach (var u in parts.Select(Unsort))
            {
                sb.Append(separator);
                sb.Append(u);
                separator = " & ";
            }

            name = sb.ToString();

            return name;
        }

        static public string CleanName(string name)
        {
            var up = name.ToUpper();

            var parts = up.Split(new[] { ' ', '-', '\t', '/', '&', '-', '+', '(', ')' }, StringSplitOptions.RemoveEmptyEntries);

            var ret = string.Join("", parts);

            if (ret.LastIndexOf('S') != ret.Length - 1) return ret;

            var truncate = 1;
            if (ret.LastIndexOf('E') == ret.Length - 2)
            {
                if (ret.Length > 2)
                {
                    var ch = ret[ret.Length - 3];
                    if (ch != 'A' && ch != 'E' && ch != 'I' && ch != 'O' && ch != 'U')
                    {
                        truncate = 2;
                    }
                }
            }
            ret = ret.Substring(0, ret.Length - truncate);

            return ret;
        }


        #endregion
    }
}
