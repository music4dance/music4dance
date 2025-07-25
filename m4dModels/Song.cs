﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using AutoMapper;

using Azure.Search.Documents.Models;

using DanceLibrary;

using Newtonsoft.Json;

using static System.Char;

// ReSharper disable ArrangeThisQualifier

namespace m4dModels
{
    public enum ExportLevel
    {
        Sparse,
        Personal,
        Global
    }

    [DataContract]
    [JsonConverter(typeof(ToStringJsonConverter))]
    public class Song : TaggableObject
    {

        public DanceRating FindRating(string id)
        {
            return DanceRatings.FirstOrDefault(
                r =>
                    string.Equals(r.DanceId, id, StringComparison.OrdinalIgnoreCase));
        }

        public ModifiedRecord FindModified(string userName)
        {
            return ModifiedBy.FirstOrDefault(
                mr =>
                    string.Equals(mr.UserName, userName, StringComparison.OrdinalIgnoreCase));
        }

        protected ModifiedRecord AddModifiedBy(ModifiedRecord mr)
        {
            ModifiedRecord other = null;

            if (mr.UserName != null)
            {
                other = ModifiedBy.FirstOrDefault(r => r.UserName == mr.UserName);
            }

            if (other != null)
            {
                return other;
            }

            ModifiedBy.Add(mr);
            return mr;
        }

        public SongProperty CreateProperty(string name, object value)
        {
            var prop = new SongProperty { Name = name, Value = value?.ToString() };
            SongProperties.Add(prop);

            return prop;
        }

        private void SetTimesFromProperties(IEnumerable<string> excludedUsers = null)
        {
            var first = FirstProperty(TimeField);
            var firstTime = first?.ObjectValue as DateTime?;
            if (firstTime == null)
            {
                return;
            }

            if (Created != firstTime)
            {
                Created = firstTime.Value;
            }

            var last = LastProperty(TimeField, excludedUsers);
            var lastTime = last?.ObjectValue as DateTime?;

            if (lastTime == null || Modified == lastTime)
            {
                return;
            }

            Modified = lastTime.Value;
        }

        public SongProperty FirstProperty(string name)
        {
            return SongProperties.FirstOrDefault(p => p.Name == name);
        }

        public SongProperty LastProperty(string name, IEnumerable<string> excludeUsers = null,
            IEnumerable<string> includeUsers = null)
        {
            return FilteredProperties(excludeUsers, includeUsers)
                .LastOrDefault(p => p.Name == name);
        }

        public IEnumerable<SongProperty> FilteredProperties(string baseName,
            IEnumerable<string> excludeUsers = null, IEnumerable<string> includeUsers = null)
        {
            return FilteredProperties(excludeUsers, includeUsers)
                .Where(p => p.BaseName == baseName);
        }

        public IEnumerable<SongProperty> FilteredProperties(IEnumerable<string> excludeUsers = null,
            IEnumerable<string> includeUsers = null)
        {
            var eu = excludeUsers == null
                ? null
                : excludeUsers as HashSet<string> ?? [.. excludeUsers];
            var iu = includeUsers == null
                ? null
                : includeUsers as HashSet<string> ?? [.. includeUsers];

            if ((eu == null || eu.Count == 0) && (iu == null || iu.Count == 0))
            {
                return SongProperties;
            }

            var ret = new List<SongProperty>();

            var inFilter = includeUsers != null;
            foreach (var prop in SongProperties)
            {
                if (prop.BaseName == UserField || prop.BaseName == UserProxy)
                {
                    var name = new ModifiedRecord(prop.Value).UserName.ToLowerInvariant();
                    if (eu != null)
                    {
                        inFilter = eu.Contains(name);
                    }
                    else // (ie != null)
                    {
                        inFilter = !iu.Contains(name);
                    }
                }

                if (!inFilter)
                {
                    ret.Add(prop);
                }
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
            DanceRatings?.Clear();
            ModifiedBy?.Clear();

            _albums = null;
        }

        public bool RecordFail(MusicService service)
        {
            if (ServiceFailed(service))
            {
                return false;
            }

            var failed = FirstProperty(FailedLookup);
            if (failed == null)
            {
                CreateEditProperties(ApplicationUser.AdminUser, EditCommand);
                SongProperties.Add(new SongProperty(FailedLookup, service.CID.ToString()));
            }
            else
            {
                failed.Value += service.CID;
            }

            return true;
        }

        public bool ServiceTried(MusicService service)
        {
            return ServiceFailed(service) || GetPurchaseId(service.Id) != null;
        }

        private bool ServiceFailed(MusicService service)
        {
            var failed = FirstProperty(FailedLookup);
            return failed != null && failed.Value.Contains(
                service.CID, StringComparison.OrdinalIgnoreCase);
        }

        #region Constants

        // These are the constants that define fields, virtual fields and command
        // TODO: Should I factor these into their own class??

        // TODO: Get Comment field in next to MultiDance when importing CSV (*)
        //  Fix the comment field in search (can we kill it and rebuild it)
        //  Make sure that import/export isn't broken by tabs/cr/lf
        //  Continue to validate advanced search service lookup

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
        public const string ValenceField = "Valence";
        public const string TitleHashField = "TitleHash"; // TODOIDX: Remove once index is updated
        public const string PlaylistField = "Playlist";

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

        // Comments
        public const string AddCommentField = "Comment+";
        public const string RemoveCommentField = "Comment-";

        // User/Song info
        public const string OwnerHash = "OwnerHash";
        public const string LikeTag = "Like";

        // Proxy Fields
        public const string UserProxy = "UserProxy";

        // Curator Fields
        public const string DeleteTagLabel = "DeleteTag";

        // Special cases for reading scraped data
        public const string TitleArtistCell = "TitleArtist";
        public const string DancersCell = "Dancers";
        public const string DanceTags = "DanceTags";
        public const string SongTags = "SongTags";
        public const string MeasureTempo = "MPM";
        public const string MultiDance = "MultiDance";
        public const string SongComment = "SongComment";
        public const string DanceComment = "DanceComment";

        // Commands
        public const string CreateCommand = ".Create";
        public const string EditCommand = ".Edit";
        public const string DeleteCommand = ".Delete";
        public const string UndoCommand = ".Undo";
        public const string MergeCommand = ".Merge";
        public const string FailedLookup = ".FailedLookup";
        public const string NoSongId = ".NoSongId"; // Pseudo action for serialization

        public const string
            SerializeDeleted = ".SerializeDeleted"; // Pseudo action for serialization

        public const string SuccessResult = ".Success";
        public const string FailResult = ".Fail";
        public const string MessageData = ".Message";

        public static readonly string[] ScalarFields =
        [
            TitleField, ArtistField,
            TempoField, LengthField, SampleField, DanceabilityField, EnergyField,
            ValenceField
        ];

        public static readonly PropertyInfo[] ScalarProperties =
        [
            typeof(Song).GetProperty(TitleField),
            typeof(Song).GetProperty(ArtistField),
            typeof(Song).GetProperty(TempoField),
            typeof(Song).GetProperty(LengthField),
            typeof(Song).GetProperty(SampleField),
            typeof(Song).GetProperty(DanceabilityField),
            typeof(Song).GetProperty(EnergyField),
            typeof(Song).GetProperty(ValenceField)
        ];

        public static readonly int DanceRatingCreate = 1;
        public static readonly int DanceRatingInitial = 1;
        public static readonly int DanceRatingIncrement = 1;
        public static readonly int DanceRatingDecrement = -1;

        #endregion

        #region Construction

        public Song()
        {
        }

        public static async Task<Song> Create(Guid songId, ICollection<SongProperty> properties,
            DanceMusicCoreService database)

        {
            var song = new Song();
            await song.Load(songId, properties, database);
            return song;
        }

        public static async Task<Song> Create(Song s, DanceMusicCoreService database)
        {
            var song = new Song();
            await song.Init(
                s.SongId, SongProperty.Serialize(s.SongProperties, null), database);
            return song;
        }

        public static async Task<Song> Create(Guid guid, string s, DanceMusicCoreService database)
        {
            var song = new Song();
            await song.Init(guid, s, database);
            return song;
        }

        public static async Task<Song> Create(string s, DanceMusicCoreService database)
        {
            var ich = TryParseId(s, out var id);
            if (ich > 0)
            {
                s = s[ich..];
            }
            else
            {
                id = Guid.NewGuid();
            }

            var song = new Song();
            await song.Init(id, s, database);
            return song;
        }

        public static async Task<Song> Create(Guid id, ICollection<SongProperty> properties,
            DanceMusicService dms)
        {
            var song = new Song { SongId = id };
            await song.LoadProperties(properties, dms);
            return song;
        }

        public static Song CreateLightSong(SearchDocument doc)
        {
            var title = doc[TitleField] as string;
            if (string.IsNullOrEmpty(title))
            {
                return null;
            }

            var sid = doc.GetString(SongIndex.SongIdField);
            var length = doc.GetInt64(LengthField);
            var tempo = doc.GetDouble(TempoField);
            var artist = doc.GetString(ArtistField);

            var history = new List<SongProperty>
            {
                new(TitleField, title),
                new(ArtistField, artist)
            };
            if (length != null)
            {
                history.Add(
                    new SongProperty(
                        LengthField, length.ToString()));
            }

            if (tempo != null)
            {
                history.Add(
                    new SongProperty(
                        TempoField, tempo.ToString()));
            }

            return new Song
            {
                SongId = sid == null ? Guid.NewGuid() : new Guid(sid),
                Title = title,
                Artist = artist,
                Length = (int?)length,
                Tempo = (decimal?)tempo,
                _properties = history
            };
        }

        private async Task Init(Guid id, string s, DanceMusicCoreService database)
        {
            SongId = id;
            var properties = new List<SongProperty>();
            SongProperty.Load(s, properties);
            await Load(SongId, properties, database);
        }

        public Song(string title, string artist, decimal? tempo, int? length,
            IList<AlbumDetails> albums)
        {
            Title = title;
            Artist = artist;
            Tempo = tempo;
            Length = length;
            _albums = albums as List<AlbumDetails> ?? albums?.ToList();
        }

        public async Task Load(Guid songId, ICollection<SongProperty> properties,
            DanceMusicCoreService database)
        {
            // In the case that we're rebuilding the song in place, we need to copy out the properties
            //  before doing anything else
            var props = properties.ToList();
            SongProperties.Clear();

            ClearValues();

            SongId = songId;

            await LoadProperties(props, database);

            Albums = BuildAlbumInfo(props);
            SongProperties.AddRange(props);
        }

        public async Task Reload(ICollection<SongProperty> properties,
            DanceMusicCoreService database)
        {
            SongProperties.Clear();
            await Load(properties, database);
        }

        public async Task Load(ICollection<SongProperty> properties, DanceMusicCoreService database)
        {
            var id = SongId;

            ClearValues();

            SongId = id;

            await LoadProperties(properties, database);

            Albums = BuildAlbumInfo(properties);
            SongProperties.AddRange(properties);
        }

        public async Task Load(string properties, DanceMusicCoreService database)
        {
            var props = new List<SongProperty>();
            SongProperty.Load(properties, props);
            await Load(SongId, props, database);
        }

        public async Task Reload(string properties, DanceMusicCoreService database)
        {
            var props = new List<SongProperty>();
            SongProperties.Clear();
            SongProperty.Load(properties, props);
            await Load(SongId, props, database);
        }

        public async Task Reload(DanceMusicCoreService database)
        {
            var props = new List<SongProperty>(SongProperties);
            SongProperties.Clear();

            await Load(SongId, props, database);
        }

        public async Task<bool> CheckProperties(DanceMusicCoreService dms)
        {
            var result = await CheckPropertiesInternal(dms);
            if (!result)
            {
                Trace.WriteLineIf(
                    TraceLevels.General.TraceError,
                    $"Invalid Song: {SongId}");
            }
            return result;
        }

        private async Task<bool> CheckPropertiesInternal(DanceMusicCoreService dms)
        {
            var failed = false;

            if (DanceRatings.Any(dr => dr.TagSummary.Tags.Any(t => t.TagClass == "Music")))
            {
                Trace.WriteLine($"Dance/Genre Conflict {SongId}");
                failed = true;
            }

            if (DanceRatings.Any(dr => s_groupIds.Contains(dr.DanceId)))
            {
                var ids = string.Join(",", s_groupIds.Where(gid => DanceRatings.Any(dr => dr.DanceId == gid)));
                Trace.WriteLine(
                    $"Invalid Group: {SongId}, DanceIds: {ids}");
                failed = true;
            }

            var badDanceTags = TagSummary.GetTagSet("Dance").Where(t => Dances.Instance.AllDanceGroups.Any(g => g.Name == t.TrimStart('!')));
            if (badDanceTags.Any())
            {
                Trace.WriteLine(
                    $"Invalid Dance Tags: {SongId}, Tags: {string.Join(",", badDanceTags)}");
                failed = true;
            }

            for (var i = 0; i < SongProperties.Count; i++)
            {
                var property = SongProperties[i];
                var name = property.BaseName;

                switch (name)
                {
                    case CreateCommand:
                    case EditCommand:
                        if (!CheckHeader(i))
                        {
                            Trace.WriteLineIf(
                                TraceLevels.General.TraceError,
                                $"Invalid Song: {SongId}, Name: {name}, Index: {i}");
                            failed = true;
                        }
                        i += 2;
                        break;
                    case UserField:
                        if (i == 0 || !CheckHeader(i - 1))
                        {
                            Trace.WriteLineIf(
                                TraceLevels.General.TraceError,
                                $"Invalid Song: {SongId}, Name: {name}, Index: {i}");
                            failed = true;
                        }

                        i += 1;
                        break;
                    case TimeField:
                        if (i < 2 || CheckHeader(i - 2))
                        {
                            Trace.WriteLineIf(
                                TraceLevels.General.TraceError,
                                $"Invalid Song: {SongId}, Name: {name}, Index: {i}");
                            failed = true;
                        }
                        break;
                    case FailedLookup:
                        Trace.WriteLineIf(
                            TraceLevels.General.TraceError,
                            $"Invalid Song: {SongId}, Name: {name}, Index: {i}");
                        failed = true;
                        break;
                    case DanceRatingField:
                        var rating = new DanceRatingDelta(property.Value);
                        switch (rating.Delta)
                        {
                            case -2:
                            case -1:
                            case 1:
                            case 2:
                                break;
                            default:
                                Trace.WriteLineIf(
                                    TraceLevels.General.TraceError,
                                    $"Invalid Song: {SongId}, Name: {name}, Index: {i}, Value: {property.Value}");
                                failed = true;
                                break;
                        }
                        break;
                }
            }

            var chunked = new ChunkedSong(this);

            //failed |= !await chunked.AreRatingsBalanced(dms);
            //failed |= chunked.HasInvalidBatch();
            //failed |= chunked.HasOrphanedVote();

            return await Task.FromResult(!failed);
        }

        private static readonly string[] CommandField = [CreateCommand, EditCommand, UndoCommand];
        private static readonly string[] UserFieldList = [UserField];
        private static readonly string[] TimeFieldList = [TimeField];

        private bool CheckHeader(int i) =>
            CheckHeaderCUT(i) || CheckHeaderCTU(i);

        private bool CheckHeaderCUT(int i) =>
            CheckHeaderBase(i, [CommandField, UserFieldList, TimeFieldList]);

        private bool CheckHeaderCTU(int i) =>
            CheckHeaderBase(i, [CommandField, TimeFieldList, UserFieldList]);

        private bool CheckHeaderBase(int i, IEnumerable<IEnumerable<string>> fields)
        {
            var props = SongProperties;
            var count = SongProperties.Count;

            foreach (var field in fields)
            {
                if (!CheckField(i, field))
                {
                    return false;
                }

                i += 1;
            }
            return true;
        }

        private bool CheckField(int i, IEnumerable<string> names)
        {
            return i < SongProperties.Count && names.Contains(SongProperties[i].BaseName);
        }

        private void CleanDwg(List<SongProperty> props)
        {
            SongProperty dwg = null;
            var hasRealProp = false;
            var psuedo = new HashSet<string>(
                [
                    TimeField, TempoField, ArtistField, TitleField,
                    AlbumField, PublisherField, TrackField, PurchaseField
                ]);

            foreach (var prop in props)
            {
                if (prop.BaseName == UserField)
                {
                    if (prop.Value == "dwgray")
                    {
                        dwg = prop;
                    }
                    else if (dwg != null)
                    {
                        if (!hasRealProp)
                        {
                            dwg.Value = "batch|P";
                        }

                        dwg = null;
                        hasRealProp = false;
                    }
                }
                else if (dwg != null)
                {
                    hasRealProp |= !psuedo.Contains(prop.BaseName);
                }
            }

            if (dwg != null && !hasRealProp)
            {
                dwg.Value = "batch|P";
            }
        }

        #endregion

        #region Serialization

        /// <summary>
        ///     Serialize the song to a single string
        /// </summary>
        /// <param name="actions">Actions to include in the serialization</param>
        /// <returns></returns>
        public string Serialize(string[] actions = null, string options = null)
        {
            if (string.IsNullOrWhiteSpace(Title) &&
                (actions == null || !actions.Contains(SerializeDeleted)))
            {
                return null;
            }

            var props = SongProperty.Serialize(SongProperties, actions, options);
            return actions != null && actions.Contains(NoSongId)
                ? props
                : Serialize(SongId.ToString("B"), props, options);
        }

        public string SerializeProperties(string[] actions = null, string options = null)
        {
            return SongProperty.Serialize(SongProperties, actions ?? [], options);
        }

        public static string Serialize(string id, string properties, string options = null)
        {
            return $"SongId={id}{SongProperty.SerializationSeparator(options)}{properties}";
        }


        public override string ToString()
        {
            return Serialize(null);
        }

        public static async Task<Song> CreateFromRow(
            ApplicationUser user, IList<string> fields, IList<string> cells,
            DanceMusicCoreService database, int weight = 1)
        {
            return await Create(
                Guid.NewGuid(),
                CreatePropertiesFromRow(user, fields, cells, weight),
                database);
        }

        private static List<SongProperty> CreatePropertiesFromRow(
            ApplicationUser user, IList<string> fields, IList<string> cells,
            int weight = 1)
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
                if (fields[i] == null || cells[i] == null)
                {
                    continue;
                }

                var cell = cells[i];

                var baseName = SongProperty.ParseBaseName(fields[i]);
                string qual = null;
                cell = cell.Trim();
                if (cell.Length > 0 && cell[0] == '"' && cell[^1] == '"')
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
                                {
                                    w = weight;
                                }

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
                            ratings = [];
                            foreach (var dnc in cell.Split(
                                         new[] { "||" },
                                         StringSplitOptions.RemoveEmptyEntries))
                            {
                                var drg = dnc.Split(
                                        new[] { '|' }, StringSplitOptions.RemoveEmptyEntries)
                                    .ToList();
                                if (drg.Count == 0 || Dances.Instance.DanceFromId(drg[0]) == null)
                                {
                                    continue;
                                }

                                ratings.Add(new DanceRatingDelta(drg[0], DanceRatingCreate));
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
                                if (dts[di] == null)
                                {
                                    continue;
                                }

                                properties.Add(
                                    new SongProperty(
                                        AddedTags, dts[di], -1,
                                        ratings[di].DanceId));
                            }

                            cell = null;
                        }
                        break;
                    case LengthField:
                        if (!string.IsNullOrWhiteSpace(cell))
                        {
                            decimal l = 0;

                            if (cell.IndexOfAny([':', 'm', 's']) == -1 &&
                                decimal.TryParse(cell, out l))
                            {
                                if (l > 1000)
                                {
                                    l /= 1000;
                                }
                            }
                            else
                            {
                                try
                                {
                                    var d = new SongDuration(cell);
                                    l = d.Length;
                                }
                                catch (ArgumentOutOfRangeException)
                                {
                                }
                            }

                            cell = l == 0 ? null : l.ToString("F0");
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

                        if (!m.Success)
                        {
                            re = new Regex(@"“(?<title>[^”]*)”\s*(?<artist>.*)");
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
                            Trace.WriteLineIf(
                                TraceLevels.General.TraceError,
                                $"Invalid TitleArtist: {cell}");
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
                        else if (qual == "IS")
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
                            tags = [];
                            danceTags = [];

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

                            if (cell.Contains("TRADITIONAL") || cell.Contains("TYPICAL") ||
                                cell.Contains("OLD SOUNDING") ||
                                cell.Contains("CLASSIC"))
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

                            if (tags.Count == 0)
                            {
                                tags = null;
                            }

                            if (danceTags.Count == 0)
                            {
                                danceTags = null;
                            }

                            cell = null;
                        }
                        break;
                    case SongTags:
                        if (!string.IsNullOrWhiteSpace(cell))
                        {
                            var tcs = SongProperty.ParsePart(fields[i], 1);
                            if (string.IsNullOrWhiteSpace(tcs))
                            {
                                tcs = "Other";
                            }

                            tags = new TagList(cell).Normalize(tcs).ToStringList();
                        }

                        cell = null;
                        break;
                    case DancersCell:
                        var dancers = cell.Split(
                            new[] { '&' },
                            StringSplitOptions.RemoveEmptyEntries);
                        danceTags = dancers.Select(dancer => dancer.Trim() + ":Other").ToList();
                        cell = null;
                        break;
                    case DanceTags:
                        if (!string.IsNullOrWhiteSpace(cell))
                        {
                            var tc = SongProperty.ParsePart(fields[i], 1);
                            if (string.IsNullOrWhiteSpace(tc))
                            {
                                tc = "Other";
                            }

                            danceTags = new TagList(cell).Normalize(tc).ToStringList();
                        }

                        cell = null;
                        break;
                    case MeasureTempo:
                        if (decimal.TryParse(cell, out var tempo))
                        {
                            var numerator = 4;
                            if (ratings is { Count: > 0 })
                            {
                                var did = ratings[0].DanceId;
                                var d = Dances.Instance.DanceFromId(did);
                                if (d != null)
                                {
                                    numerator = d.Meter.Numerator;
                                }
                            }

                            tempo *= numerator;
                            cell = tempo.ToString(CultureInfo.InvariantCulture);
                            baseName = TempoField;
                        }
                        else
                        {
                            cell = null;
                        }

                        break;
                    case DanceComment:
                        // TODO: Verify that this works
                        // TODO: Add SongComment and generalize DanceComment to allow for different
                        //  comments per dancer
                        foreach (var r in ratings)
                        {
                            properties.Add(new SongProperty(baseName, cell, -1, r.DanceId));
                        }

                        cell = null;
                        break;
                    case PlaylistField:
                        cell = string.IsNullOrWhiteSpace(cell) ? null : cell.Trim();
                        break;
                }

                if (tags is { Count: > 0 })
                {
                    tagProperty = UpdateTagProperty(
                        properties, tagProperty,
                        new TagList(tags).ToString());
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
                        danceTagProperties = [];
                        foreach (var p in ratings.Select(
                                     drd =>
                                         new SongProperty(
                                             AddedTags, tl.ToString(), -1, drd.DanceId)))
                        {
                            properties.Add(p);
                            danceTagProperties.Add(p);
                        }
                    }

                    danceTags = null;
                }

                if (string.IsNullOrWhiteSpace(cell))
                {
                    continue;
                }

                var idx = IsAlbumField(fields[i]) ? 0 : -1;
                properties.Add(new SongProperty(baseName, cell, idx, qual));
            }

            const string sep = "|";
            Trace.WriteLineIf(
                user == null && !specifiedUser,
                $"Bad User for {string.Join(sep, cells)}");

            // ReSharper disable once InvertIf
            if (user != null)
            {
                if (!specifiedUser)
                {
                    properties.Insert(
                        0,
                        new SongProperty(
                            TimeField,
                            DateTime.Now.ToString(CultureInfo.InvariantCulture)));
                    properties.Insert(0, new SongProperty(UserField, user.DecoratedName));
                }

                if (!specifiedAction)
                {
                    properties.Insert(0, new SongProperty(CreateCommand, string.Empty));
                }
            }

            return properties;
        }

        private static SongProperty UpdateTagProperty(ICollection<SongProperty> properties,
            SongProperty tagProperty, string extra)
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

        private static SongProperty UpdateFromRatings(List<SongProperty> properties,
            SongProperty tagProperty, IReadOnlyCollection<DanceRatingDelta> ratings)
        {
            tagProperty = UpdateTagProperty(
                properties, tagProperty,
                TagsFromDances(ratings.Select(r => r.DanceId)));
            properties.AddRange(
                ratings.Select(
                    rating =>
                        new SongProperty(DanceRatingField, rating.ToString())));

            return tagProperty;
        }

        public static List<string> BuildHeaderMap(string line, char separator = '\t')
        {
            var map = new List<string>();
            var headers = line.Split(separator);

            foreach (var parts in headers.Select(t => t.Trim()).Select(header => header.Split(':')))
            {
                // If this fails, we want to add a null to our list because
                // that indicates a column we don't care about
                if (parts.Length > 0 && PropertyMap.TryGetValue(parts[0].ToUpper(), out var field))
                {
                    map.Add(parts.Length > 1 ? field + ":" + parts[1] : field);
                }
                else
                {
                    map.Add(null);
                }
            }

            return map;
        }

        private static readonly Dictionary<string, string> PropertyMap =
            new()
            {
                { "DANCE", DanceRatingField },
                { "TITLE", TitleField },
                { "ARTIST", ArtistField },
                { "CONTRIBUTING ARTISTS", ArtistField },
                { "LABEL", PublisherField },
                { "USER", UserField },
                { "TEMPO", TempoField },
                { "BPM", TempoField },
                { "BEATS-PER-MINUTE", TempoField },
                { "LENGTH", LengthField },
                { "TRACK", TrackField },
                { "ALBUM", AlbumField },
                { "#", TrackField },
                { "PUBLISHER", PublisherField },
                { "AMAZONTRACK", SongProperty.FormatName(PurchaseField, null, "AS") },
                { "AMAZON", SongProperty.FormatName(PurchaseField, null, "AS") },
                { "ITUNES", SongProperty.FormatName(PurchaseField, null, "IS") },
                { "PATH", OwnerHash },
                { "TIME", LengthField },
                { "COMMENT", AddedTags },
                { "RATING", "R" },
                { "DANCERS", DancersCell },
                { "TITLE+ARTIST", TitleArtistCell },
                { "DANCETAGS", DanceTags },
                { "SONGTAGS", SongTags },
                { "MPM", MeasureTempo },
                { "MULTIDANCE", MultiDance },
                { "DANCECOMMENT", DanceComment }
            };

        public static async Task<IList<Song>> CreateFromRows(
            ApplicationUser user, string separator, IList<string> headers, IEnumerable<string> rows,
            DanceMusicCoreService database, int weight)
        {
            var songs = new Dictionary<string, Song>();
            var itc = string.Equals(separator.Trim(), "ITC");
            var itcd = string.Equals(separator.Trim(), "ITC-");

            foreach (var line in rows)
            {
                Trace.WriteLineIf(TraceLevels.General.TraceInfo, "Create Song From Row:" + line);
                List<string> cells;

                if (itc || itcd)
                {
                    cells = [];
                    var re = itc
                        ? new Regex(@"\w*(?<bpm>\d+)(?<title>[^\t]*)\t(?<artist>.*)")
                        : new Regex(@"\w*(?<bpm>\d+)(?<title>[^-]*)-(?<artist>.*)");
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
                    cells = [.. Regex.Split(line, separator)];
                }


                // Concat back the last field (which seems a typical pattern)
                while (cells.Count > headers.Count)
                {
                    cells[headers.Count - 1] =
                        $"{cells[headers.Count - 1]}{separator}{cells[headers.Count]}";
                    cells.RemoveAt(headers.Count);
                }

                if (cells.Count == headers.Count)
                {
                    var sd = await CreateFromRow(user, headers, cells, database, weight);
                    if (sd != null)
                    {
                        var ta = sd.TitleArtistAlbumString;
                        if (string.Equals(sd.Title, sd.Artist))
                        {
                            Trace.WriteLineIf(
                                TraceLevels.General.TraceInfo,
                                $"Title and Artist are the same ({sd.Title})");
                        }

                        if (songs.TryGetValue(ta, out var old))
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
                    Trace.WriteLineIf(
                        TraceLevels.General.TraceInfo,
                        $"Bad cell count {cells.Count} != {headers.Count}: {line}");
                }
            }

            var ret = new List<Song>(songs.Values);

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
                tagProp.Value = new TagList(tagProp.Value).Add(new TagList(tagPropOther.Value))
                    .ToString();
            }

            foreach (var dr in other.DanceRatings)
            {
                UpdateDanceRating(new DanceRatingDelta(dr.DanceId, dr.Weight), true);
            }
        }

        public static async Task<Song> UserCreateFromTrack(DanceMusicCoreService database,
            ApplicationUser user, ServiceTrack track
        )
        {
            // Title;Artist;Duration;Album;Track;DanceRating;SongTags;DanceTags;PurchaseInfo;

            var fields = new List<string>
            {
                TitleField,
                ArtistField,
                LengthField,
                AlbumField,
                TrackField
            };

            var cells = new List<string>
            {
                track.Name,
                track.Artist,
                track.Duration?.ToString(),
                track.Album,
                track.TrackNumber?.ToString()
            };

            // Now fix up the user: This leaves the creating user w/ a like
            //  and attributes the rest of the edits to the service user
            var service = MusicService.GetService(track.Service);
            var serviceUser = service.ApplicationUser.DecoratedName;
            var props =
                (await CreateFromTrack(user, track, fields, cells, database)).SongProperties;
            props.Insert(3, new SongProperty(LikeTag, "true"));
            props.Insert(4, new SongProperty(EditCommand, string.Empty));
            props.Insert(5, new SongProperty(UserField, serviceUser));
            props.Insert(6, props[2]);

            return await Create(Guid.NewGuid(), props, database);
        }

        public static async Task<Song> CreateFromTrack(
            ApplicationUser user, ServiceTrack track, string multiDance, string songTags, string playlist,
            DanceMusicCoreService database)
        {
            // Title;Artist;Duration;Album;Track;multiDance;PurchaseInfo;

            var fields = new List<string>
            {
                TitleField,
                ArtistField,
                LengthField,
                AlbumField,
                TrackField,
                MultiDance,
                SongTags,
                PlaylistField,
            };

            var cells = new List<string>
            {
                track.Name,
                track.Artist,
                track.Duration?.ToString(),
                track.Album,
                track.TrackNumber?.ToString(),
                multiDance,
                songTags,
                playlist
            };

            return await CreateFromTrack(user, track, fields, cells, database);
        }

        public static async Task<Song> CreateFromTrack(DanceMusicCoreService database,
            ApplicationUser user, ServiceTrack track,
            string dances = null, string songTags = null, string danceTags = null
        )
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

            return await CreateFromTrack(user, track, fields, cells, database);
        }

        private static async Task<Song> CreateFromTrack(
            ApplicationUser user, ServiceTrack track, IList<string> fields, IList<string> cells,
            DanceMusicCoreService database)
        {
            if (track.CollectionId != null)
            {
                fields.Add(
                    PurchaseField + ":00:" +
                    AlbumDetails.BuildPurchaseKey(PurchaseType.Album, track.Service));
                cells.Add(track.CollectionId);
            }

            if (track.TrackId != null)
            {
                fields.Add(
                    PurchaseField + ":00:" +
                    AlbumDetails.BuildPurchaseKey(PurchaseType.Song, track.Service));
                cells.Add(track.TrackId);
            }

            return await CreateFromRow(user, fields, cells, database, DanceRatingIncrement);
        }


        protected async Task LoadProperties(ICollection<SongProperty> properties,
            DanceMusicCoreService database)
        {
            var stats = database.DanceStats;
            var created = SongProperties is { Count: > 0 };
            string user = null;
            ModifiedRecord currentModified = null;
            var deleted = false;

            var drDelete = new List<DanceRating>();

            HashSet<string> isUserModified = [];

            foreach (var prop in properties)
            {
                var bn = prop.BaseName;

                switch (bn)
                {
                    case UserField:
                    case UserProxy:
                        currentModified = new ModifiedRecord(prop.Value);
                        user = currentModified.UserName;
                        currentModified = AddModifiedBy(currentModified);

                        // TODO: Once we've updated all songs with the PseudoUser
                        //  flag this will be redundant
                        {
                            var applicationUser = await database.FindUser(user) ??
                                new ApplicationUser(user, true);
                            var isPseudo = applicationUser.IsPseudo;
                            currentModified.IsPseudo = isPseudo;
                            prop.Value = isPseudo
                                ? currentModified.DecoratedName
                                : user;
                        }
                        prop.Value = currentModified.DecoratedName;
                        break;
                    case DanceRatingField:
                        {
                            var del = SoftUpdateDanceRating(prop.Value);
                            if (del != null)
                            {
                                drDelete.Add(del);
                            }
                        }
                        break;
                    case AddedTags:
                        if (user == null)
                        {
                            Trace.WriteLineIf(
                                TraceLevels.General.TraceError,
                                $"Null User when attempting to add tag {prop.Value} to song {SongId}");
                        }
                        else
                        {
                            prop.Value = AddObjectTags(prop.DanceQualifier, prop.Value, stats)
                                .ToString();
                        }

                        break;
                    case RemovedTags:
                        if (user == null)
                        {
                            Trace.WriteLineIf(
                                TraceLevels.General.TraceError,
                                $"Null User when attempting to remove tag {prop.Value} from song {SongId}");
                        }
                        else
                        {
                            RemoveObjectTags(prop.DanceQualifier, prop.Value, stats);
                        }

                        break;
                    case AddCommentField:
                        {
                            if (user == null)
                            {
                                Trace.WriteLineIf(
                                    TraceLevels.General.TraceError,
                                    $"Null User when attempting to add comment {prop.Value} to song {SongId}");
                            }
                            else
                            {
                                AddObjectComment(prop.DanceQualifier, prop.Value, user);
                            }
                        }
                        break;
                    case RemoveCommentField:
                        if (user == null)
                        {
                            Trace.WriteLineIf(
                                TraceLevels.General.TraceError,
                                $"Null User when attempting to remove comment {prop.Value} from song {SongId}");
                        }
                        else
                        {
                            RemoveObjectComment(prop.DanceQualifier, user);
                        }

                        break;
                    case DeleteTagLabel:
                        ForceDeleteTag(prop.DanceQualifier, prop.Value, stats);
                        break;
                    case AlbumField:
                    case PublisherField:
                    case TrackField:
                    case PurchaseField:
                        // All of these are taken care of with build album
                        break;
                    case DeleteCommand:
                        deleted = string.IsNullOrEmpty(prop.Value) ||
                            string.Equals(prop.Value, "true", StringComparison.OrdinalIgnoreCase);
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
                            if (currentModified is { IsPseudo: false })
                            {
                                Edited = time;
                            }
                        }
                        break;
                    case OwnerHash:
                        if (currentModified != null)
                        {
                            currentModified.Owned = (int?)prop.ObjectValue;
                        }

                        break;
                    case LikeTag:
                        if (currentModified != null)
                        {
                            currentModified.Like = prop.ObjectValue as bool?;
                        }

                        break;
                    default:
                        // All of the simple properties we can just set except
                        //  for the ones that are bots overriding user values
                        if (prop.IsAction)
                        {
                            break;
                        }

                        var isUser = !currentModified?.ApplicationUser?.IsPseudo ?? false;
                        var wasUser = isUserModified.Contains(bn);

                        if (wasUser && !isUser)
                        {
                            break;
                        }

                        var pi = GetType().GetProperty(bn);
                        pi?.SetValue(this, prop.ObjectValue);

                        if (isUser)
                        {
                            isUserModified.Add(bn);
                        }

                        break;
                }
            }

            foreach (var dr in drDelete.Where(r => r.Weight <= 0))
            {
                DanceRatings.Remove(dr);
                if (stats != null)
                {
                    var danceName = stats.FromId(dr.DanceId).DanceName;
                    TagSummary.ChangeTags(
                        null, new TagList(
                            $"{danceName}:Dance|!{danceName}:Dance"));
                }
            }

            if (deleted)
            {
                ClearValues();
            }
        }

        public SongHistory GetHistory(IMapper mapper)
        {
            return new SongHistory
            {
                Id = SongId,
                Properties = SongProperties.Select(mapper.Map<SongPropertySparse>).ToList()
            };
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
        public string Purchase
        {
            get => GetPurchaseTags();
            set { }
        }

        public IEnumerable<PurchaseInfo> PurchaseInfo =>
            GetPurchaseLinks().Select(p => new PurchaseInfo(p, true));

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
        public DateTime Edited { get; set; }

        [DataMember]
        public List<DanceRating> DanceRatings => _danceRatings ??= [];

        private List<DanceRating> _danceRatings;

        [DataMember]
        public List<ModifiedRecord> ModifiedBy => _modifiedBy ??= [];

        private List<ModifiedRecord> _modifiedBy;

        [DataMember]
        public List<SongProperty> SongProperties => _properties ??= [];

        private List<SongProperty> _properties;

        [DataMember]
        public List<AlbumDetails> Albums
        {
            get => _albums ??= [];
            set => _albums = value;
        }

        private List<AlbumDetails> _albums;

        public int TitleHash => CreateTitleHash(Title);

        public bool TempoConflict(Song s, decimal delta)
        {
            return Tempo.HasValue && s.Tempo.HasValue &&
                Math.Abs(Tempo.Value - s.Tempo.Value) > delta;
        }

        public bool IsNull => string.IsNullOrWhiteSpace(Title);

        public bool HasSample => Sample != null && Sample != ".";
        public bool HasEchoNest => Danceability != null && !float.IsNaN(Danceability.Value);

        public IEnumerable<string> GetAltids()
        {
            var merges = FilteredProperties(MergeCommand).ToList();
            return merges.Any()
                ? merges.SelectMany(
                    m =>
                        m.Value.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                : [];
        }

        public bool BatchProcessed { get; set; }

        #endregion

        #region Actions

        public void Create(ApplicationUser user, string command, string value, bool addUser)
        {
            var time = DateTime.Now;

            if (!string.IsNullOrWhiteSpace(command))
            {
                CreateProperty(command, value);
            }

            Created = time;
            Modified = time;
            Edited = new DateTime(2000, 1, 1);

            if (!addUser || user == null)
            {
                return;
            }

            AddUser(user);
            CreateProperty(UserField, user.DecoratedName);
            CreateProperty(TimeField, time.ToString(CultureInfo.InvariantCulture));
        }

        public void Create(Song sd, IEnumerable<UserTag> tags, ApplicationUser user, string command,
            string value, DanceStatsInstance stats)
        {
            var addUser = !(sd.ModifiedBy != null && sd.ModifiedBy.Count > 0 && AddUser(user));

            Create(user, command, value, addUser);

            // Handle User association
            if (!addUser)
            {
                // This is the Modified record created when we computed the addUser condition above
                var mr = ModifiedBy.First();
                mr.Owned = sd.ModifiedBy[0].Owned;

                CreateProperty(UserField, mr.DecoratedName);
                CreateProperty(TimeField, Created.ToString(CultureInfo.InvariantCulture));
                if (mr.Owned.HasValue)
                {
                    CreateProperty(OwnerHash, mr.Owned);
                }
            }

            Debug.Assert(!string.IsNullOrWhiteSpace(sd.Title));
            foreach (var pi in ScalarProperties)
            {
                var prop = pi.GetValue(sd);
                if (prop == null)
                {
                    continue;
                }

                pi.SetValue(this, prop);
                CreateProperty(pi.Name, prop);
            }

            if (tags == null)
            {
                // Handle Tags
                TagsFromProperties(user.UserName, sd.SongProperties, stats, this);

                // Handle Dance Ratings
                CreateDanceRatings(sd.DanceRatings, stats);

                DanceTagsFromProperties(user.UserName, sd.SongProperties, stats, this);
            }
            else
            {
                InternalEditTags(user, tags, stats);
            }

            // Handle Albums
            CreateAlbums(sd.Albums);

            SetTimesFromProperties();
        }

        private bool EditCore(ApplicationUser user, Song edit)
        {
            CreateEditProperties(user, EditCommand);

            var modified = ScalarFields.Aggregate(
                false,
                (current, field) => current | UpdateProperty(edit, field));

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
                    if (string.IsNullOrWhiteSpace(album.Name))
                    {
                        continue;
                    }

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

            if (!needReorder)
            {
                return modified;
            }

            var temp = string.Join(",", reorder.Select(x => x.ToString()));
            var order = LastProperty(AlbumOrder);
            if (order?.Value == temp)
            {
                return modified;
            }

            CreateProperty(AlbumOrder, temp);

            return true;
        }

        internal async Task<bool> AdminEdit(ICollection<SongProperty> properties,
            DanceMusicCoreService database)
        {
            ClearValues();
            await Reload(properties, database);
            return true;
        }

        internal async Task<bool> AdminEdit(string properties, DanceMusicCoreService database)
        {
            var props = new List<SongProperty>();
            SongProperty.Load(properties, props);
            return await AdminEdit(props, database);
        }

        internal async Task<bool> AdminAppend(ApplicationUser user, string newProperties,
            DanceMusicCoreService database)
        {
            CreateEditProperties(user, EditCommand);

            var oldProperties = Serialize([NoSongId]);

            await Reload($"{oldProperties}\t{newProperties}", database);

            Modified = DateTime.Now;

            return true;
        }

        internal async Task<bool> AppendHistory(SongHistory history, IMapper mapper,
            DanceMusicCoreService database)
        {
            var properties =
                SongProperties.Concat(history.Properties.Select(mapper.Map<SongProperty>));
            ClearValues();
            await Reload(properties.ToList(), database);
            return true;
        }

        public async Task<bool> AdminModify(string modInfo, DanceMusicCoreService database)
        {
            var changed = false;
            var songMod = SongModifier.Build(modInfo);

            if (songMod == null)
            {
                throw new ArgumentException($"Failed to deserialize ${modInfo}");
            }

            await ExpandTags(database);

            var props = FilteredProperties(songMod.ExcludeUsers).ToList();
            foreach (var modifier in songMod.Properties)
            {
                var modList = props.Where(
                        p =>
                            string.Equals(
                                p.Name, modifier.Name, StringComparison.OrdinalIgnoreCase) &&
                            (string.Equals(
                                    p.Value, modifier.Value, StringComparison.OrdinalIgnoreCase) ||
                                p.Name == DanceRatingField && p.Value.StartsWith(
                                    modifier.Value, StringComparison.InvariantCultureIgnoreCase) ||
                                p.Name.StartsWith("Tag") &&
                                modifier.Action == PropertyAction.ReplaceName))
                    .ToList();

                changed |= modList.Count > 0;
                foreach (var prop in modList)
                {
                    var index = SongProperties.FindIndex(p => p == prop);
                    if (modifier.Action == PropertyAction.Remove ||
                        modifier.Action == PropertyAction.Replace)
                    {
                        SongProperties.RemoveAt(index);
                    }

                    if (modifier.Action == PropertyAction.Append)
                    {
                        index += 1;
                    }

                    if (modifier.Action == PropertyAction.Replace ||
                        modifier.Action == PropertyAction.Append ||
                        modifier.Action == PropertyAction.Prepend)
                    {
                        SongProperties.InsertRange(index, modifier.Properties);
                    }
                    else if (modifier.Action == PropertyAction.ReplaceValue)
                    {
                        SongProperties[index].Value = prop.Name == DanceRatingField &&
                            modifier.Replace.Length == 3
                                ? modifier.Replace + SongProperties[index].Value[3..]
                                : modifier.Replace;
                    }
                    else if (modifier.Action == PropertyAction.ReplaceName)
                    {
                        SongProperties[index].Name = modifier.Replace;
                    }
                }
            }

            await Reload([.. SongProperties], database);

            await CollapseTags(database);

            return changed;
        }

        public async Task<bool> AdminAddUserProperties(string userName, IEnumerable<SongProperty> properties, DanceMusicCoreService database)
        {
            // Filter to the properties that aren't already set
            var props = properties.Where(p => !HasUserProperty(userName, p)).ToList();
            if (props.Count == 0)
            {
                return false;
            }

            // Find the first user block of properties
            var idx = SongProperties.FindIndex(p => p.BaseName == UserField && p.Value == userName);
            if (idx == -1)
            {
                return false;
            }
            // Find the last index
            idx = SongProperties.FindIndex(idx, p => p.IsAction);
            if (idx == -1)
            {
                idx = SongProperties.Count;
            }
            // Insert each of the properties after the index
            foreach (var prop in props)
            {
                SongProperties.Insert(idx++, prop);
            }
            // Wrap this in a SongIndex method to save the songs
            // Is there a way to abstract out SongController BatchAdminExecute so we can use it in playlist controller?
            await Reload([.. SongProperties], database);

            return true;
        }

        private bool HasUserProperty(string userName, SongProperty property)
        {
            var inUser = false;
            foreach (var prop in SongProperties)
            {
                if (prop.BaseName == UserField)
                {
                    inUser = userName == prop.Value;
                }
                else if (inUser && property == prop)
                {
                    return true;
                }
            }
            return false;
        }

        public async Task<bool> ExpandTags(DanceMusicCoreService database)
        {
            var changed = false;
            var trg = new List<SongProperty>();
            foreach (var prop in SongProperties)
            {
                if (prop.BaseName.StartsWith("Tag"))
                {
                    var tags = new TagList(prop.Value).Tags;
                    changed |= tags.Count > 1;
                    foreach (var tag in tags)
                    {
                        trg.Add(new SongProperty(prop.Name, tag));
                    }
                }
                else
                {
                    trg.Add(prop);
                }
            }

            await Reload(trg, database);
            return changed;
        }

        public async Task<bool> CollapseTags(DanceMusicCoreService database)
        {
            var changed = false;
            var trg = new List<SongProperty>();
            SongProperty lastProp = null;
            foreach (var prop in SongProperties)
            {
                if (prop.BaseName.StartsWith("Tag"))
                {
                    if (lastProp != null && prop.Name == lastProp.Name)
                    {
                        lastProp.Value += $"|{prop.Value}";
                        changed = true;
                    }
                    else
                    {
                        lastProp = prop;
                        trg.Add(prop);
                    }
                }
                else
                {
                    lastProp = null;
                    trg.Add(prop);
                }
            }

            await Reload(trg, database);
            return changed;
        }

        // Edit 'this' based on SongBase + extras
        public bool Edit(ApplicationUser user, Song edit, IEnumerable<UserTag> tags,
            DanceStatsInstance stats)
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
                Modified = DateTime.Now;
                return true;
            }

            RemoveEditProperties(EditCommand);

            return false;
        }


        public async Task<bool> Update(string user, Song update, DanceMusicCoreService database)
        {
            // Verify that our heads are the same (TODO:move this to debug mode at some point?)
            var old = SongProperties; // Where(p => !p.IsAction).
            var upd = update.SongProperties; // Where(p => !p.IsAction).
            var c = old.Count;
            for (var i = 0; i < c; i++)
            {
                if (upd.Count >= i && string.Equals(old[i].Name, upd[i].Name))
                {
                    continue;
                }

                Trace.WriteLineIf(TraceLevels.General.TraceWarning, $"Unexpected Update: {SongId}");
                return false;
            }

            // Nothing has changed
            if (c == upd.Count)
            {
                return false;
            }

            var mrg = new List<SongProperty>(upd.Skip(c));

            await UpdateProperties(mrg, database);

            UpdatePurchaseInfo(update);

            return true;
        }

        public async Task UpdateProperties(ICollection<SongProperty> properties,
            DanceMusicCoreService database, string[] excluded = null)
        {
            await LoadProperties(properties, database);

            foreach (var prop in properties.Where(
                         prop =>
                             excluded == null || !excluded.Contains(prop.BaseName)))
            {
                SongProperties.Add(new SongProperty { Name = prop.Name, Value = prop.Value });
            }
        }

        // This is an additive merge - only add new things if they don't conflict with the old
        public bool AdditiveMerge(ApplicationUser user, Song edit, List<string> addDances,
            DanceStatsInstance stats)
        {
            CreateEditProperties(user, EditCommand);

            var modified = ScalarFields.Aggregate(
                false,
                (current, field) => current | AddProperty(edit, field));

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
                    if (string.IsNullOrWhiteSpace(album.Name))
                    {
                        continue;
                    }

                    album.CreateProperties(this);
                    modified = true;
                }
            }

            if (addDances is { Count: > 0 })
            {
                var tags = TagsFromDances(addDances);
                var newTags = AddTags(tags, user.UserName, stats, this);
                modified = newTags != null && !string.IsNullOrWhiteSpace(tags);

                modified |= EditDanceRatings(addDances, DanceRatingIncrement, stats);
            }
            else
            {
                // Handle Tags
                modified |= TagsFromProperties(user.UserName, edit.SongProperties, stats, this);

                // Handle Dance Ratings
                modified |= CreateDanceRatings(edit.DanceRatings, stats);

                modified |=
                    DanceTagsFromProperties(user.UserName, edit.SongProperties, stats, this);
            }

            modified |= UpdatePurchaseInfo(edit, true);
            modified |= UpdateModified(user, edit, false);

            return modified;
        }

        public bool EditTags(ApplicationUser user, IEnumerable<UserTag> tags,
            DanceStatsInstance stats)
        {
            CreateEditProperties(user, EditCommand);

            var changed = InternalEditTags(user, tags, stats);
            if (!changed)
            {
                RemoveEditProperties(EditCommand);
            }

            return changed;
        }

        public bool EditSongTags(ApplicationUser user, TagList tags, DanceStatsInstance stats)
        {
            return EditTags(user, [ new()
                { Tags = tags } ], stats);
        }

        // TODOIDX: Clean up FailedLookup once index is updates
        public bool LookupTried()
        {
            return SongProperties.Any(
                p =>
                    p.Name == FailedLookup || p.Name.StartsWith(PurchaseField));
        }

        private bool InternalEditTags(ApplicationUser user, IEnumerable<UserTag> tags,
            DanceStatsInstance stats)
        {
            var hash = new Dictionary<string, TagList>();
            foreach (var tag in tags)
            {
                hash[tag.Id ?? ""] = tag.Tags;
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
                //  Fix songfilter text to include userName
                //  Fix move to advanced form to include userName
                //  Make sure that login loop isn't broken
                var mr = ModifiedBy
                    .FirstOrDefault(m => m.UserName == user.UserName);
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
            modified |= ChangeTags(songTags, user.UserName, stats, "Dances");

            // Edit the tags for each of the dance ratings: Note that I'm stripping out blank dance ratings
            //  at the client, so need to make sure that we remove any tags from dance ratings on the server
            //  that aren't passed through in our tag list.

            foreach (var dr in DanceRatings)
            {
                if (!hash.TryGetValue(dr.DanceId, out var tl))
                {
                    tl = new TagList();
                }

                modified |= dr.ChangeTags(tl.Summary, user.UserName, stats, this);
            }

            // Finally do the full removal of all danceratings/tags associated with the removed tags
            modified |= DeleteDanceRatings(user, deleted, stats);

            return modified;
        }

        private bool DeleteDanceRatings(ApplicationUser user,
            TagList deleted, DanceStatsInstance stats)
        {
            var ratings = new List<DanceRating>();

            // For each entry find the actual dance rating
            // ReSharper disable once LoopCanBeConvertedToQuery
            foreach (var dance in deleted.StripType())
            {
                var d = Dances.Instance.DanceFromName(dance);
                if (d == null)
                {
                    continue;
                }

                var did = d.Id;
                var rating = DanceRatings.FirstOrDefault(dr => dr.DanceId == did);
                if (rating == null)
                {
                    continue;
                }

                ratings.Add(rating);
            }

            if (!ratings.Any())
            {
                return false;
            }

            // For each user that has modified the song, back out anything having
            // to do with the deleted dances
            var lastUser = user;
            var remove = deleted.Add(deleted.AddQualifier('!'));
            foreach (var mr in ModifiedBy)
            {
                var userModified = false;
                var u = mr.ApplicationUser;

                // Add the userName property into the SongProperties
                var userProp = CreateProperty(UserProxy, u);

                // Back out any top-level tags related to the dance styles
                var songTags = GetUserTags(u.UserName);
                if (songTags != null)
                {
                    var newSongTags = songTags.Subtract(remove);
                    if (newSongTags.Summary != songTags.Summary)
                    {
                        userModified = true;
                        userModified |= ChangeTags(newSongTags, u.UserName, stats, this);
                    }
                }

                // Back out the tags directly associated with the dance style
                userModified = ratings.Aggregate(
                    userModified,
                    (current, rating) => current | rating.ChangeTags(
                        string.Empty, user.UserName, stats, this));

                // If this userName didn't touch anything, back out the userName property
                if (userModified)
                {
                    lastUser = u;
                }
                else
                {
                    TruncateProperty(userProp.Name, userProp.Value);
                }
            }

            // If any other user 
            if (lastUser.UserName != user.UserName)
            {
                CreateProperty(UserProxy, user.DecoratedName);
            }

            foreach (var r in ratings.Select(
                         rating => new DanceRatingDelta
                         { DanceId = rating.DanceId, Delta = -rating.Weight }))
            {
                UpdateDanceRating(r, true);
            }

            return true;
        }

        private void UpdateUserDanceRatings(string userName, IEnumerable<string> danceIds,
            int rating)
        {
            foreach (var did in danceIds)
            {
                var delta = -UserDanceRating(userName, did) + rating;
                UpdateDanceRating(new DanceRatingDelta { DanceId = did, Delta = delta }, true);
            }
        }

        private bool UpdateModified(ApplicationUser user, Song edit, bool force)
        {
            var mr = ModifiedBy
                .FirstOrDefault(m => m.UserName == user.UserName);
            if (mr == null)
            {
                return false;
            }

            var mrN = edit.ModifiedBy
                .FirstOrDefault(m => m.UserName == user.UserName);
            if (mrN == null || !force && !mrN.Owned.HasValue || mr.Owned == mrN.Owned)
            {
                return false;
            }

            mr.Owned = mrN.Owned;
            CreateProperty(OwnerHash, mr.Owned);
            return true;
        }

        private bool UpdateProperty(Song edit, string name)
        {
            // TODO: This can be optimized
            var eP = edit.GetType().GetProperty(name)?.GetValue(edit);
            var oP = GetType().GetProperty(name)?.GetValue(this);

            if (Equals(eP, oP))
            {
                return false;
            }

            GetType().GetProperty(name)?.SetValue(this, eP);

            CreateProperty(name, eP);

            return true;
        }

        // Only update if the old song didn't have this property
        private bool AddProperty(Song edit, string name)
        {
            var eP = edit.GetType().GetProperty(name)?.GetValue(edit);
            var oP = GetType().GetProperty(name)?.GetValue(this);

            // Edit property is null or whitespace and Old property isn't null or whitespace
            if (NullIfWhitespace(eP) == null || NullIfWhitespace(oP) != null)
            {
                return false;
            }

            GetType().GetProperty(name)?.SetValue(this, eP);
            CreateProperty(name, eP);

            return true;
        }

        private static object NullIfWhitespace(object o)
        {
            if (o is string s && string.IsNullOrWhiteSpace(s))
            {
                o = null;
            }

            return o;
        }

        public void CreateEditProperties(ApplicationUser user, string command,
            DateTime? time = null)
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
                CreateProperty(UserField, user.DecoratedName);
            }

            // Handle Timestamps
            time ??= DateTime.Now;

            Modified = time.Value;
            CreateProperty(TimeField, time.Value.ToString(CultureInfo.InvariantCulture));
        }

        public void RemoveEditProperties(string command)
        {
            TruncateProperty(TimeField);
            TruncateProperty(UserField);
            TruncateProperty(EditCommand);
        }

        private void TruncateProperty(string name, string value = null)
        {
            var i = SongProperties.Count - 1;
            var prop = SongProperties[i];
            if (prop.Name != name || value != null && prop.Value != value)
            {
                return;
            }

            SongProperties.RemoveAt(i);
        }

        private bool UpdatePurchaseInfo(Song edit, bool additive = false)
        {
            var pi = additive ? edit.MergePurchaseTags(Purchase) : edit.GetPurchaseTags();

            return (Purchase ?? string.Empty) != (pi ?? string.Empty);
        }

        public bool AddUser(ApplicationUser user, bool? like = null)
        {
            var us = new ModifiedRecord
            {
                UserName = user.UserName, IsPseudo = user.IsPseudo, Like = like
            };
            return AddModifiedBy(us) == us;
        }

        public void CreateAlbums(IList<AlbumDetails> albums)
        {
            if (albums == null)
            {
                return;
            }

            albums = AlbumDetails.MergeAlbums(albums, Artist, false);

            foreach (var ad in albums.Where(ad => !string.IsNullOrWhiteSpace(ad.Name)))
            {
                ad.CreateProperties(this);
            }
        }

        public bool CreateDanceRatings(IEnumerable<DanceRating> ratings, DanceStatsInstance stats)
        {
            return ratings != null &&
                EditDanceRatings(
                    ratings.Select(
                        add => new DanceRatingDelta
                        { DanceId = add.DanceId, Delta = add.Weight }), stats);
        }

        public void EditDanceRating(DanceRatingDelta drd, DanceStatsInstance stats)
        {
            var dro = DanceRatings.FirstOrDefault(r => r.DanceId == drd.DanceId);
            if (drd.Delta > 0)
            {
                if (dro == null)
                {
                    var ds = stats.FromId(drd.DanceId);
                    if (ds == null)
                    {
                        return; // Invalid
                    }

                    DanceRatings.Add(new DanceRating { DanceId = drd.DanceId, Weight = drd.Delta });
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
                    return; // Invalid
                }

                if (dro.Weight + drd.Delta < 0)
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
                return; // Invalid
            }

            CreateProperty(DanceRatingField, drd.ToString());
        }

        public bool EditDanceRatings(IEnumerable<DanceRatingDelta> deltas, DanceStatsInstance stats)
        {
            if (deltas == null)
            {
                return false;
            }

            var changed = false;
            foreach (var drd in deltas)
            {
                EditDanceRating(drd, stats);
                changed = true;
            }

            return changed;
        }

        public bool EditDanceRatings(IEnumerable<string> addIn, int addWeight,
            DanceStatsInstance stats)
        {
            return addIn != null &&
                EditDanceRatings(
                    addIn.Select(
                        add => new DanceRatingDelta
                        { DanceId = add, Delta = addWeight }),
                    stats);
        }

        private bool BaseTagsFromProperties(string userName, IEnumerable<SongProperty> properties,
            DanceStatsInstance stats, object data, bool dance)
        {
            var modified = false;
            foreach (var p in properties)
            // ReSharper disable once SwitchStatementMissingSomeCases
            {
                switch (p.BaseName)
                {
                    case UserField:
                    case UserProxy:
                        userName = new ModifiedRecord(p.Value).UserName;
                        break;
                    case AddedTags:
                        var qual = p.DanceQualifier;
                        if (qual == null && !dance)
                        {
                            modified |= !AddTags(p.Value, userName, stats, data).IsEmpty;
                        }
                        else if (qual != null && dance)
                        {
                            var rating = DanceRatings.FirstOrDefault(r => r.DanceId == qual);

                            if (rating != null)
                            {
                                modified |= !rating.AddTags(p.Value, userName, stats, data).IsEmpty;
                            }
                            // Else case is where the dancerating has been fully removed, we
                            //  can safely drop this on the floor
                        }

                        break;
                    case RemovedTags:
                        qual = p.DanceQualifier;
                        if (qual == null && !dance)
                        {
                            modified |= !RemoveTags(p.Value, userName, stats, data).IsEmpty;
                        }
                        else if (qual != null && dance)
                        {
                            var rating = DanceRatings.FirstOrDefault(r => r.DanceId == qual);

                            if (rating != null)
                            {
                                modified |= !rating.RemoveTags(p.Value, userName, stats, data)
                                    .IsEmpty;
                            }
                            // Else case is where the dancerating has been fully removed, we
                            //  can safely drop this on the floor
                        }

                        break;
                }
            }

            return modified;
        }

        private bool TagsFromProperties(string userName, IEnumerable<SongProperty> properties,
            DanceStatsInstance stats, object data)
        {
            return BaseTagsFromProperties(userName, properties, stats, data, false);
        }

        private bool DanceTagsFromProperties(string userName, IEnumerable<SongProperty> properties,
            DanceStatsInstance stats, object data)
        {
            // Clear out cached user tags
            return BaseTagsFromProperties(userName, properties, stats, data, true);
        }

        public void Delete(ApplicationUser user)
        {
            if (user != null)
            {
                CreateEditProperties(user, DeleteCommand);
            }

            ClearValues();

            if (user != null)
            {
                Modified = DateTime.Now;
            }
        }

        public async Task<bool> UndoUserChanges(ApplicationUser user, DanceMusicCoreService service)
        {
            // Find sections that user has contributed
            var keep = new List<SongProperty>();
            var current = new List<SongProperty>();
            var isUser = false;
            foreach (var prop in SongProperties)
            {
                if (prop.IsAction)
                {
                    if (!isUser)
                    {
                        keep.AddRange(current);
                    }
                    isUser = false;
                    current.Clear();
                }
                else if (prop.BaseName == UserField)
                {
                    isUser = prop.Value == user.DecoratedName;
                }
                current.Add(prop);
            }
            if (!isUser)
            {
                keep.AddRange(current);
            }

            if (keep.Count < SongProperties.Count)
            {
                await Reload(keep, service);
                return true;
            }

            return false;
        }

        public bool RemoveEmptyEdits()
        {
            // Cleanup null edits
            var props = SongProperties;
            var buffer = new List<int>();
            var remove = new List<int>();

            var inEmpty = false;

            for (var i = 0; i < props.Count; i++)
            {
                var prop = props[i];
                if (prop.IsAction)
                {
                    if (inEmpty)
                    {
                        remove.AddRange(buffer);
                        buffer.Clear();
                    }

                    if (prop.Name != EditCommand)
                    {
                        continue;
                    }

                    inEmpty = true;
                    buffer.Add(i);
                }
                else if ((prop.Name == UserField || prop.Name == TimeField) && inEmpty)
                {
                    buffer.Add(i);
                }
                else
                {
                    inEmpty = false;
                    buffer.Clear();
                }
            }

            if (inEmpty)
            {
                remove.AddRange(buffer);
            }

            if (remove.Count == 0)
            {
                return false;
            }

            remove.Reverse();
            foreach (var i in remove)
            {
                SongProperties.RemoveAt(i);
            }

            return true;
        }

        private (bool, int) HandleOneMerge(int i, string command)
        {
            var props = SongProperties;
            var iStart = i;
            var changed = false;

            while (i < props.Count && props[i].BaseName == MergeCommand)
            {
                i += 1;
            }

            var wellFormed = false;
            if (i != iStart && !props[i].IsAction)
            {
                for (var j = 0; j < 2; j++)
                {
                    if (props[i + j].BaseName != UserField && props[i + j].BaseName != TimeField)
                    {
                        break;
                    }
                    if (props[i + j + 1].IsAction)
                    {
                        wellFormed = true;
                        break;
                    }
                }

                if (!wellFormed)
                {
                    props.Insert(i, new SongProperty { Name = command });
                    changed = true;
                    i += 1;
                }
            }

            return (changed, i - iStart);
        }

        public bool FixupMerges()
        {
            var props = SongProperties;
            bool changed = false;

            var i = 0;
            while (i < props.Count)
            {
                while (i < props.Count && props[i].BaseName != MergeCommand)
                {
                    i += 1;
                }
                var (chng, cnt) = HandleOneMerge(i, i == 0 ? CreateCommand : EditCommand);
                changed |= chng;
                i += cnt;
            }
            return changed;
        }

        private (bool, int) HandleOneEditTime(int i, string command)
        {
            var props = SongProperties;
            var count = props.Count;
            var iStart = i;

            if (i + 2 >= count)
            {
                return (false, 1);
            }

            i += 1;
            var p1 = props[i];
            var p2 = props[i + 1];

            if (p1.BaseName == TimeField && p2.BaseName == UserField ||
                p2.BaseName == TimeField && p1.BaseName == UserField)
            {
                return (false, 2);
            }

            if (p1.BaseName == UserField)
            {
                i += 1;
            }

            var iInsert = i;

            string time = null;
            var passedAction = false;

            while (i < count)
            {
                if (props[i].IsAction)
                {
                    passedAction = true;
                }
                else if (props[i].BaseName == TimeField)
                {
                    time = props[i].Value;
                    if (!passedAction)
                    {
                        props.RemoveAt(i);
                    }
                    break;
                }
                i += 1;
            }

            if (time == null)
            {
                i -= 1;
                while (i > 0)
                {
                    if (props[i].BaseName == TimeField)
                    {
                        time = props[i].Value;
                        break;
                    }
                    i -= 1;
                }
            }

            if (time == null)
            {
                Trace.WriteLineIf(TraceLevels.General.TraceWarning, $"No time found for {SongId}");
                return (false, i - iStart);
            }

            props.Insert(iInsert, new SongProperty { Name = TimeField, Value = time });
            return (true, i - iStart);
        }

        public bool FixupTime()
        {
            var props = SongProperties;
            bool changed = false;

            var i = 0;
            while (i < props.Count)
            {
                while (i < props.Count && !props[i].IsEdit)
                {
                    i += 1;
                }
                var (chng, cnt) = HandleOneEditTime(i, i == 0 ? CreateCommand : EditCommand);
                changed |= chng;
                i += cnt;
            }
            return changed;
        }

        public bool AddSpotifyTag()
        {
            var props = SongProperties;
            var c = props.Count;
            var changed = false;

            for (var i = 0; i < c - 2; i++)
            {
                if (props[i].IsEdit)
                {
                    var hasUser = false;
                    for (var j = i + 1; j < c && !props[j].IsAction; j++)
                    {
                        if (props[j].BaseName == UserField)
                        {
                            hasUser = true;
                        }
                    }

                    if (!hasUser)
                    {
                        props.Insert(i + 1, new SongProperty(UserField, "batch-s|P"));
                        i += 1;
                        changed = true;
                    }
                }
            }

            return changed;
        }


        public bool RemoveOwner()
        {
            bool changed = false;
            var props = SongProperties;
            for (var i = props.Count - 1; i >= 0; i--)
            {
                if (props[i].BaseName == OwnerHash)
                {
                    props.RemoveAt(i);
                    changed = true;
                }
            }

            return changed;
        }

        public bool RemoveEmptyProperties()
        {
            bool changed = false;
            var props = SongProperties;
            for (var i = props.Count - 1; i >= 0; i--)
            {
                var prop = props[i];
                var value = prop.Value;
                if ((s_emptyNames.Contains(prop.BaseName) && (string.IsNullOrWhiteSpace(value) || value == "."))
                    || prop.BaseName == FailedLookup)
                {
                    props.RemoveAt(i);
                    changed = true;
                }
            }

            return changed;
        }

        public bool RemoveGroupVotes()
        {
            bool changed = false;
            var props = SongProperties;
            for (var i = props.Count - 1; i >= 0; i--)
            {
                var prop = props[i];
                var value = prop.Value;
                if (prop.BaseName == DanceRatingField)
                {
                    var rating = new DanceRatingDelta(prop.Value);
                    if (s_groupIds.Contains(rating.DanceId))
                    {
                        props.RemoveAt(i);
                        changed = true;
                    }
                }
            }

            return changed;

        }

        private static string[] s_emptyNames = [AddedTags, RemovedTags, SampleField];

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
                if (prop.Name != LengthField)
                {
                    continue;
                }

                var val = prop.ObjectValue;
                if (val is not int current)
                {
                    continue;
                }

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
                    avg = (avg * count + current) / (count + 1);
                    count += 1;
                    remove.Add(prop);
                }
            }

            if (remove.Count == 0 || first == null || outliers > count / 2)
            {
                return false;
            }

            first.Value = avg.ToString();

            foreach (var prop in remove)
            {
                SongProperties.Remove(prop);
            }

            return true;
        }

        public bool CleanBadBatches()
        {
            var chunked = new ChunkedSong(this);
            if (!chunked.RemoveInvalidBatches())
            {
                return false;
            }
            SongProperties.Clear();
            SongProperties.AddRange(chunked.SongProperties);
            return true;
        }

        public async Task<bool> CleanOrphanedVotes(DanceMusicCoreService dms)
        {
            var chunked = new ChunkedSong(this);
            if (!await chunked.CleanOrphanedVotes(dms, addUnmatched: true))
            {
                return false;
            }
            SongProperties.Clear();
            SongProperties.AddRange(chunked.SongProperties);
            return true;
        }

        public async Task<bool> MergeChunks(DanceMusicCoreService dms)
        {
            var chunked = new ChunkedSong(this);
            if (!await chunked.MergeChunks(dms))
            {
                return false;
            }
            SongProperties.Clear();
            SongProperties.AddRange(chunked.SongProperties);
            return true;
        }


        public bool CleanupAlbums()
        {
            // Remove the properties for album info that has been 'deleted'
            // Also get rid of promote and order for everything

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
                                if (albums.TryGetValue(index, out var old))
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
                            if (!albums.TryGetValue(index, out var old))
                            {
                                old = [];
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

            if (remove.Count == 0 && !changed)
            {
                return false;
            }

            foreach (var prop in remove)
            {
                SongProperties.Remove(prop);
            }

            return true;
        }

        public bool CleanupSamples()
        {
            return CleanupHeader(SampleField, (prop) =>
            {
                var val = prop.Value;

                var userName = "batch-i|P";
                if (val.Contains("p.scdn.co", StringComparison.OrdinalIgnoreCase))
                {
                    userName = "batch-s|P";
                }
                else if (val != "." && !val.Contains("apple", StringComparison.OrdinalIgnoreCase))
                {
                    Trace.WriteLine($"Unknown sample origin {val} on {SongId}");
                }

                return userName;
            });
        }

        public bool CleanupPurchases()
        {
            return CleanupHeader(PurchaseField, (prop) =>
            {
                var userName = "batch-i|P";
                var qualifier = prop.Qualifier;
                if (qualifier.StartsWith("S", StringComparison.OrdinalIgnoreCase))
                {
                    userName = "batch-s|P";
                }
                else if (qualifier.StartsWith("A", StringComparison.OrdinalIgnoreCase))
                {
                    userName = "batch-a|P";
                }
                else if (!qualifier.StartsWith("I", StringComparison.OrdinalIgnoreCase))
                {
                    Trace.WriteLine($"Unknown purchase type {qualifier} on {SongId}");
                }
                return userName;
            });
        }

        public bool CleanupEchoNest()
        {
            return CleanupHeader(DanceabilityField, _ => "batch-e|P");
        }

        public bool RemoveStutters()
        {
            var stutter = new List<int>();
            for (var i = 1; i < SongProperties.Count; i++)
            {
                if (SongProperties[i] == SongProperties[i - 1])
                {
                    stutter.Add(i);
                }
            }
            stutter.Reverse();
            foreach (var i in stutter)
            {
                SongProperties.RemoveAt(i);
            }
            return stutter.Count > 0;
        }

        public async Task<bool> CleanupMissingEdits(DanceMusicCoreService dms)
        {
            var changed = false;
            var props = SongProperties;
            var i = 0;
            var edit = new SongProperty(EditCommand);

            while (i != -1 && (i = props.FindIndex(i, p => p.BaseName == UserField)) != -1)
            {
                var userProp = props[i];
                // Insert an edit property before the user if it doesn't exist
                if (i == 0 || !props[i - 1].IsAction)
                {
                    props.Insert(i, edit);
                    i += 1;
                    changed = true;
                }

                // Insert a time property after the user if it doesn't exist
                if (i == SongProperties.Count - 1 || props[i + 1].BaseName != TimeField)
                {
                    // Create a time field
                    var user = userProp.Value.StartsWith("batch") ? null : await dms.FindUser(userProp.Value);
                    var time = user?.StartDate.ToString("g");
                    if (time == null)
                    {
                        var idx = props.FindLastIndex(i, p => p.BaseName == TimeField);
                        if (idx == -1)
                        {
                            idx = props.FindLastIndex(i, p => p.BaseName == TimeField);
                        }
                        if (idx != -1)
                        {
                            time = props[idx].Value;
                        }
                    }

                    if (time != null)
                    {
                        props.Insert(i + 1, new SongProperty(TimeField, time));
                        changed = true;
                    }
                    else
                    {
                        Trace.WriteLine($"Unable to determine time for ${SongId}");
                    }
                }
                i += 1;
            }
            return changed;

        }

        bool CleanupHeader(string name, Func<SongProperty, string> getUser)
        {
            var changed = false;
            var props = SongProperties;
            var i = 0;
            while (i != -1 && (i = props.FindIndex(i, p => p.BaseName == name)) != -1)
            {
                var cmd = props.FindLastIndex(i, p => p.IsAction);

                if (cmd == -1)
                {
                    Trace.WriteLine($"{name} without Command on {SongId}");
                }

                var val = props[i].Value;

                var userName = getUser(props[i]);
                var user = props[cmd + 1];
                if (user.BaseName == UserField)
                {
                    if (user.Value.StartsWith("batch") && user.Value != userName && val != ".")
                    {
                        user.Value = userName;
                        changed = true;
                    }
                }
                else
                {
                    props.Insert(cmd + 1, new SongProperty(UserField, userName));
                    changed = true;
                }

                var time = cmd + 2 < props.Count - 1 ? props[cmd + 2] : null;
                if (time == null || time.BaseName != TimeField)
                {
                    var prevIdx = props.FindLastIndex(i, p => p.BaseName == TimeField);
                    if (prevIdx != -1)
                    {
                        props.Insert(cmd + 2, new SongProperty(TimeField, props[prevIdx].Value));
                        changed = true;
                    }
                    else
                    {
                        Trace.WriteLine($"Now timestamp on ${SongId}");
                    }
                }

                i = props.FindIndex(i, p => p.IsAction);
            }
            return changed;
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
                UserTags = [];
                Ratings = [];
            }

            public Dictionary<string, TagTracker> UserTags { get; }

            public Dictionary<string, RatingTracker> Ratings { get; }
        }

        public bool DedupRatings()
        {
            var chunked = new ChunkedSong(this);
            if (!chunked.DedupRatings())
            {
                return false;
            }
            SongProperties.Clear();
            SongProperties.AddRange(chunked.SongProperties);
            return true;

        }

        //public bool NormalizeRatings(int max = 1, int min = -1)
        //{
        //    // This function should not semantically change the tags, but it will potentially
        //    //  reduce the danceratings where there were redundant entries previously and normalize based
        //    // on max/min

        //    // For each user, keep a list of the tags and danceratings that they have applied
        //    var users = new Dictionary<string, UserEdits>();
        //    var remove = new List<int>();

        //    string stats = null;
        //    UserEdits currentEdits = null;
        //    var changed = false;

        //    for (var i = 0; i < SongProperties.Count; i++)
        //    {
        //        var prop = SongProperties[i];
        //        var bn = prop.BaseName;
        //        switch (prop.BaseName)
        //        {
        //            case UserField:
        //            case UserProxy:
        //                if (!string.Equals(stats, prop.Value, StringComparison.OrdinalIgnoreCase))
        //                {
        //                    stats = prop.Value;
        //                    if (!users.TryGetValue(stats, out currentEdits))
        //                    {
        //                        currentEdits = new UserEdits();
        //                        users[stats] = currentEdits;
        //                    }
        //                }

        //                break;
        //            case AddedTags:
        //            case RemovedTags:
        //                var qual = prop.DanceQualifier ?? string.Empty;
        //                if (currentEdits == null)
        //                {
        //                    Trace.WriteLineIf(
        //                        TraceLevels.General.TraceWarning,
        //                        $"Tag property {prop} comes before user.");
        //                    break;
        //                }

        //                if (!currentEdits.UserTags.TryGetValue(qual, out var acc))
        //                {
        //                    acc = new TagTracker { Property = prop };
        //                    currentEdits.UserTags[qual] = acc;
        //                }
        //                else
        //                {
        //                    remove.Add(i);
        //                }

        //                acc.Tags = bn == AddedTags
        //                    ? acc.Tags.Add(new TagList(prop.Value))
        //                    : acc.Tags.Subtract(new TagList(prop.Value));
        //                break;
        //            case DanceRatingField:
        //                if (currentEdits == null)
        //                {
        //                    Trace.WriteLineIf(
        //                        TraceLevels.General.TraceWarning,
        //                        $"DanceRating property {prop} comes before user.");
        //                    break;
        //                }

        //                var drd = new DanceRatingDelta(prop.Value);
        //                RatingTracker rating;
        //                var delta = drd.Delta;

        //                // Enforce normalization of max/min values
        //                if (delta > max)
        //                {
        //                    delta = max;
        //                }
        //                else if (delta < min)
        //                {
        //                    delta = min;
        //                }

        //                if (drd.Delta != delta)
        //                {
        //                    changed = true;
        //                    drd.Delta = delta;
        //                    prop.Value = drd.ToString();
        //                }

        //                if (!currentEdits.Ratings.TryGetValue(drd.DanceId, out rating))
        //                {
        //                    currentEdits.Ratings[drd.DanceId] = new RatingTracker
        //                        { Rating = delta, Property = prop };
        //                }
        //                else
        //                {
        //                    // Keep the vote that is in the direction that is most recent for this user, then the largest value
        //                    if (Math.Sign(rating.Rating) != Math.Sign(delta) ||
        //                        Math.Abs(rating.Rating) <= Math.Abs(delta))
        //                    {
        //                        changed = true;
        //                        rating.Rating = delta;
        //                        rating.Property.Value = drd.ToString();
        //                    }

        //                    remove.Add(i);
        //                }

        //                break;
        //        }
        //    }

        //    remove.Reverse();
        //    foreach (var idx in remove)
        //    {
        //        SongProperties.RemoveAt(idx);
        //        changed = true;
        //    }

        //    foreach (var edit in users.Values)
        //    foreach (var tracker in edit.UserTags.Values)
        //    {
        //        var tags = tracker.Tags.ToString();
        //        if (string.Equals(tags, tracker.Property.Value))
        //        {
        //            continue;
        //        }

        //        tracker.Property.Value = tags;
        //        changed = true;
        //    }

        //    if (changed)
        //    {
        //        SetRatingsFromProperties();
        //    }

        //    return changed;
        //}

        private bool FixupProperties(string name, Func<SongProperty, string> fixup)
        {
            var deleted = new List<int>();

            var changed = false;
            for (var i = 0; i < SongProperties.Count; i++)
            {
                var prop = SongProperties[i];
                if (prop.BaseName != name)
                {
                    continue;
                }

                var value = fixup(prop);
                if (value == null)
                {
                    deleted.Add(i);
                }
                else if (value != prop.Value)
                {
                    changed = true;
                    prop.Value = value;
                }
            }

            deleted.Reverse();
            foreach (var del in deleted)
            {
                SongProperties.RemoveAt(del);
            }

            return deleted.Count > 0 || changed;
        }

        public bool RemoveObsoletePurchases()
        {
            return FixupProperties(
                PurchaseField, prop =>
                {
                    var qualifier = prop.Qualifier;
                    return string.Equals(qualifier, "ms", StringComparison.OrdinalIgnoreCase) ||
                        qualifier.StartsWith("X", StringComparison.OrdinalIgnoreCase)
                            ? null
                            : prop.Value;
                });
        }

        public bool FixupLengths()
        {
            return FixupProperties(
                LengthField, prop =>
                {
                    var value = prop.Value;
                    if (value.Contains(':'))
                    {
                        value = $"0:{value}";
                        return TimeSpan.TryParse(value, out var length)
                            ? Math.Round(length.TotalSeconds).ToString()
                            : null;
                    }

                    return value;
                });
        }

        public bool FixDuplicateTags(DanceMusicCoreService dms)
        {
            return FixupProperties(
                AddedTags,
                prop => new TagList(prop.Value).RemoveDuplicates(dms).ToString());
        }

        public bool RemoveTagRing(DanceMusicCoreService dms)
        {
            return FixupProperties(
                    AddedTags,
                    prop => new TagList(prop.Value).ConvertToPrimary(dms).ToString()) ||
                FixupProperties(
                    RemovedTags,
                    prop => new TagList(prop.Value).ConvertToPrimary(dms).ToString());
        }

        private static HashSet<string> s_groupIds = ["MSC", "TNG", "LTN", "SWG", "WLZ"];

        public bool FixBadTagCategory()
        {
            return FixupProperties(
                AddedTags,
                prop => new TagList(prop.Value).FixBadCategory().ToString());
        }

        public bool RemoveBadDanceTags()
        {
            return FixupProperties(
                AddedTags,
                prop =>
                {
                    var tags = new TagList(prop.Value).Tags
                        .Where(t => !Dances.Instance.AllDanceGroups.Any(g => g.Name + ":Dance" == t.TrimStart('!')));
                    return tags.Any() ? new TagList(tags).ToString() : null;
                });
        }


        public async Task<bool> CleanupProperties(DanceMusicCoreService dms, string actions = "E")
        {
            var changed = false;

            foreach (var action in actions)
            {
                switch (action)
                {
                    case 'A':
                        changed |= HandleCleanupCount(action, CleanupAlbums());
                        break;
                    case 'B':
                        changed |= HandleCleanupCount(action, AddSpotifyTag());
                        break;
                    case 'C':
                        changed |= HandleCleanupCount(action, FixBadTagCategory());
                        break;
                    case 'D':
                        changed |= HandleCleanupCount(action, RemoveDuplicateDurations());
                        break;
                    case 'E':
                        changed |= HandleCleanupCount(action, RemoveEmptyEdits());
                        break;
                    case 'F':
                        changed |= HandleCleanupCount(action, CleanBadBatches());
                        break;
                    case 'G':
                        changed |= HandleCleanupCount(action, FixupMerges());
                        break;
                    case 'H':
                        changed |= HandleCleanupCount(action, await CleanOrphanedVotes(dms));
                        break;
                    case 'I':
                        changed |= HandleCleanupCount(action, FixupTime());
                        break;
                    case 'J':
                        changed |= HandleCleanupCount(action, RemoveBadDanceTags());
                        break;
                    case 'K':
                        changed |= HandleCleanupCount(action, await MergeChunks(dms));
                        break;
                    case 'L':
                        changed |= HandleCleanupCount(action, FixupLengths());
                        break;
                    case 'M':
                        changed |= HandleCleanupCount(action, await CleanupMissingEdits(dms));
                        break;
                    case 'N':
                        changed |= HandleCleanupCount(action, CleanupEchoNest());
                        break;
                    case 'O':
                        changed |= HandleCleanupCount(action, RemoveOwner());
                        break;
                    case 'P':
                        changed |= HandleCleanupCount(action, CleanupPurchases());
                        break;
                    case 'Q':
                        changed |= HandleCleanupCount(action, RemoveStutters());
                        break;
                    case 'R':
                        changed |= HandleCleanupCount(action, DedupRatings());
                        break;
                    case 'S':
                        changed |= HandleCleanupCount(action, CleanupSamples());
                        break;
                    case 'T':
                        changed |= HandleCleanupCount(action, FixDuplicateTags(dms));
                        break;
                    case 'V':
                        changed |= HandleCleanupCount(action, RemoveGroupVotes());
                        break;
                    case 'X':
                        changed |= HandleCleanupCount(action, RemoveTagRing(dms));
                        break;
                    case 'Y':
                        changed |= HandleCleanupCount(action, RemoveEmptyProperties());
                        break;
                }
            }

            return changed;
        }

        public static void DumpCleanupCount()
        {
            foreach (var pair in s_cleanupCounts)
            {
                Trace.WriteLine($"{pair.Key}: {pair.Value}");
            }
        }

        private bool HandleCleanupCount(char c, bool f)
        {
            if (f)
            {
                if (s_cleanupCounts.TryGetValue(c, out var count))
                {
                    count += 1;
                }
                else
                {
                    count = 1;
                }
                s_cleanupCounts[c] = count;
                return true;
            }

            return false;
        }

        private static readonly Dictionary<char, int> s_cleanupCounts = [];


        #endregion

        #region DanceRating

        /// <summary>
        ///     Update the dance rating table based on the encoded
        ///     property value
        /// </summary>
        /// <param name="value"></param>
        public DanceRating SoftUpdateDanceRating(string value)
        {
            var drd = new DanceRatingDelta(value);
            return SoftUpdateDanceRating(drd);
        }

        public DanceRating SoftUpdateDanceRating(DanceRatingDelta drd,
            bool updateProperties = false)
        {
            DanceRating ret = null;

            var dr = DanceRatings.FirstOrDefault(r => r.DanceId.Equals(drd.DanceId));

            if (dr == null)
            {
                dr = new DanceRating { DanceId = drd.DanceId, Weight = 0 };
                DanceRatings.Add(dr);
            }

            dr.Weight += drd.Delta;

            if (dr.Weight <= 0)
            {
                ret = dr;
            }

            if (updateProperties)
            {
                SongProperties.Add(
                    new SongProperty
                    { Name = DanceRatingField, Value = drd.ToString() });
            }

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
                var rating = SoftUpdateDanceRating(prop.Value);
                if (rating != null)
                {
                    DanceRatings.Remove(rating);
                }
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
            return tags == null || tags.IsEmpty ? [] : Dances.Instance.FromNames(tags.Strip()).Select(d => d.Id);
        }

        public int NormalizedUserDanceRating(string userName, string danceId)
        {
            var rating = UserDanceRating(userName, danceId);
            return rating == 0 ? 0 : rating < 0 ? -1 : 1;
        }

        public int UserDanceRating(string userName, string danceId)
        {
            var level = 0;
            var ratings = FilteredProperties(
                DanceRatingField, null,
                new HashSet<string>([userName]));
            foreach (var rating in ratings.Where(p => p.Value.StartsWith(danceId)))
            {
                var v = rating.Value;
                var i = v.IndexOfAny(['+', '-']);
                if (i == -1 || !int.TryParse(v[i..], out var t))
                {
                    continue;
                }

                level += t;
            }

            return level;
        }

        public void UpdateDanceRatingsAndTags(string user, IEnumerable<string> dances, int weight,
            DanceStatsInstance stats)
        {
            var enumerable = dances as IList<string> ?? dances.ToList();
            var tags = TagsFromDances(enumerable);
            var added = AddTags(tags, user, stats);
            if (added is { IsEmpty: false })
            {
                SongProperties.Add(new SongProperty(AddedTags, added.ToString()));
            }

            UpdateDanceRatings(enumerable, weight);
        }

        public static string GetCsvHeader(ExportLevel level)
        {
            switch (level)
            {
                case ExportLevel.Sparse:
                    return "SongId,Dance,My Vote,Favorite,music4dance,My Song Tags,My Dance Tags";
                case ExportLevel.Personal:
                    return
                        "SongId,Title,Artist,Length,Tempo,Meter,Dance,Votes,My Vote,Favorite,music4dance,Spotify,ITunes,Song Tags,My Song Tags,Dance Tags,My Dance Tags";
                case ExportLevel.Global:
                    return
                        "SongId,Title,Artist,Length,Tempo,Meter,Dance,Votes,Beat,Energy,Mood,Song Tags, Dance Tags,Sample";
            }
            throw new ArgumentException($"{level} is invalid ", nameof(level));
        }

        public string ToCsv(string user, ExportLevel level = ExportLevel.Personal)
        {
            var lines = new StringBuilder();
            foreach (var danceRating in DanceRatings)
            {
                lines.Append(CsvForDance(danceRating.DanceId, user, level));
            }
            return lines.ToString();
        }

        public string CsvForDance(string danceId, string user, ExportLevel level = ExportLevel.Personal)
        {
            var danceRating = DanceRatings.FirstOrDefault(r => r.DanceId == danceId);
            if (danceRating == null)
            {
                return null;
            }

            var isUser = level != ExportLevel.Global;
            var title = Title.Replace(@"""", @"""""");
            var artist = Artist.Replace(@"""", @"""""");
            var meter = Meter?.ToString();
            var danceName = Dances.Instance.DanceFromId(danceId).Name;
            var userRating = isUser ? NormalizedUserDanceRating(user, danceId) : 0;
            var modified = isUser ? ModifiedBy.LastOrDefault(m => m.UserName == user) : null;
            var favorite = modified?.Like == null ? "" : modified.Like.ToString();
            var link = $"https://www.music4dance.net/song/details/{SongId}";
            var spotify = GetPurchaseLinks("S").FirstOrDefault()?.Link;
            var itunes = GetPurchaseLinks("I").FirstOrDefault()?.Link;
            var songTags = TagSummary.Description;
            var danceTags = danceRating.TagSummary.Description;
            var mySongTags = isUser ? GetUserTags(user).Description : null;
            var myDanceTags = isUser ? danceRating.GetUserTags(user, this).Description : null;

            switch (level)
            {
                case ExportLevel.Sparse:
                    return
                        $"{SongId},{danceName},{userRating},{favorite},{link},{myDanceTags},{myDanceTags}{Environment.NewLine}";
                case ExportLevel.Personal:
                    return
                        $"{SongId},\"{title}\",\"{artist}\",{Length},{Tempo},{meter},{danceName},{danceRating.Weight},{userRating},{favorite},{link},{spotify},{itunes},{songTags},{mySongTags},{danceTags},{myDanceTags}{Environment.NewLine}";
                case ExportLevel.Global:
                    return
                        $"{SongId},\"{title}\",\"{artist}\",{Length},{Tempo},{meter},{danceName},{danceRating.Weight},{Danceability},{Energy},{Valence},{songTags},{danceTags},{Sample},{Environment.NewLine}";
            }
            throw new ArgumentException($"{level} is invalid ", nameof(level));
        }

        public Meter Meter
        {
            get
            {
                var tagSummary = TagSummary;
                if (tagSummary.HasTag("4/4:Tempo"))
                {
                    return new Meter(4, 4);
                }
                if (tagSummary.HasTag("3/4:Tempo"))
                {
                    return new Meter(3, 4);
                }
                if (tagSummary.HasTag("2/4:Tempo"))
                {
                    return new Meter(2, 4);
                }

                return null;
            }
        }
        #endregion

        #region Tags

        public void ChangeDanceTags(string danceId, string tags, string user,
            DanceStatsInstance stats)
        {
            var dr = FindRating(danceId);
            if (dr != null)
            {
                dr.ChangeTags(tags, user, stats, this);
            }
            else
            {
                Trace.WriteLineIf(
                    TraceLevels.General.TraceError,
                    $"Undefined DanceRating {SongId}:{danceId}");
            }
        }

        public override void RegisterChangedTags(TagList added, TagList removed, string user,
            object data)
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

            if (data == null)
            {
                return;
            }

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
            {
                tobj = FindRating(qualifier);
            }

            if (tobj == null)
            {
                Trace.WriteLine($"Bad tag on {Title} by {Artist} with {qualifier} ({SongId}");
                return new TagList();
            }

            return tobj.AddTags(tags, stats);
        }

        public TagList RemoveObjectTags(string qualifier, string tags, DanceStatsInstance stats)
        {
            TaggableObject tobj = this;
            if (!string.IsNullOrWhiteSpace(qualifier))
            {
                tobj = FindRating(qualifier);
            }

            return tobj?.RemoveTags(tags, stats);
        }

        public void AddObjectComment(string qualifier, string comment, string userName)
        {
            TaggableObject tobj = this;
            if (!string.IsNullOrWhiteSpace(qualifier))
            {
                tobj = FindRating(qualifier);
            }

            if (tobj == null)
            {
                Trace.WriteLine($"Bad comment on {Title} by {Artist}");
                return;
            }

            tobj.AddComment(comment, userName);
        }

        public void RemoveObjectComment(string qualifier, string userName)
        {
            TaggableObject tobj = this;
            if (!string.IsNullOrWhiteSpace(qualifier))
            {
                tobj = FindRating(qualifier);
            }

            tobj?.RemoveComment(userName);
        }

        public void ForceDeleteTag(string qualifier, string tagString,
            DanceStatsInstance stats)
        {
            TaggableObject tobj = this;
            var tag = new TagCount(tagString);
            if (!string.IsNullOrWhiteSpace(qualifier))
            {
                tobj = FindRating(qualifier);
            }

            tobj?.DeleteTag(tag, stats);
            if (tag.TagClass == "Dance")
            {
                var dance = stats.FromName(tag.TagValue);
                var rating = DanceRatings.FirstOrDefault(dr => dr.DanceId == dance.DanceId);
                if (rating != null)
                {
                    DanceRatings.Remove(rating);
                }
            }
        }

        protected override HashSet<string> ValidClasses => s_validClasses;

        private static readonly HashSet<string> s_validClasses = ["dance", "music", "tempo", "other"];

        #endregion

        #region Album

        public string AlbumList
        {
            get
            {
                if (!HasAlbums)
                {
                    return null;
                }

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
            if (string.IsNullOrWhiteSpace(album))
            {
                return null;
            }

            AlbumDetails ret = null;
            var candidates = new List<AlbumDetails>();
            var title = CleanAlbum(album, Artist);

            foreach (var ad in Albums.Where(
                ad => string.Equals(
                    CleanAlbum(ad.Name, Artist), title,
                    StringComparison.CurrentCultureIgnoreCase)))
            {
                candidates.Add(ad);
                if (!string.Equals(ad.Name, album, StringComparison.CurrentCultureIgnoreCase))
                {
                    continue;
                }

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

            ni ??= string.Empty;

            var merged = oi.Union(ni);
            return SortChars(merged);
        }

        private static string SortChars(IEnumerable<char> chars)
        {
            if (chars == null)
            {
                return string.Empty;
            }

            var a = chars.ToArray();
            Array.Sort(a);
            return new string(a);
        }

        public ICollection<PurchaseLink> GetPurchaseLinks(string service = "AIS",
            string region = null)
        {
            var links = new List<PurchaseLink>();
            service = service.ToUpper();

            foreach (var ms in MusicService.GetServices())
            {
                if (!service.Contains(ms.CID))
                {
                    continue;
                }

                foreach (var album in Albums)
                {
                    var l = album.GetPurchaseLink(ms.Id, region);
                    if (l == null)
                    {
                        continue;
                    }

                    links.Add(l);
                    break;
                }
            }

            return links;
        }

        public ICollection<string> GetPurchaseIds(MusicService service)
        {
            return Albums.Select(
                    album => album.GetPurchaseIdentifier(service.Id, PurchaseType.Song))
                .Where(id => id != null)
                .ToList();
        }

        public ICollection<string> GetExtendedPurchaseIds()
        {
            return Albums.SelectMany(album => album.GetExtendedPurchaseIds(PurchaseType.Song))
                .ToList();
        }

        public string GetPurchaseId(ServiceType service)
        {
            string ret = null;
            foreach (var album in Albums)
            {
                ret = album.GetPurchaseIdentifier(service, PurchaseType.Song);
                if (ret != null)
                {
                    break;
                }
            }

            return ret;
        }

        public static string GetPurchaseTags(ICollection<AlbumDetails> albums)
        {
            var added = new HashSet<char>();

            foreach (var d in albums)
            {
                var tags = d.GetPurchaseTags();
                if (tags == null)
                {
                    continue;
                }

                foreach (var c in tags.Where(c => !added.Contains(c)))
                {
                    added.Add(c);
                }
            }

            return added.Count == 0 ? null : SortChars(added);
        }

        public static int GetNextAlbumIndex(ICollection<AlbumDetails> albums)
        {
            int[] ret = [0];
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
                // CompareAlbum
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
            var names = new List<string>(
                [
                    AlbumField, PublisherField, TrackField, PurchaseField, AlbumPromote, AlbumOrder
                ]);

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
                var index = prop.Index;
                if (!index.HasValue)
                {
                    continue;
                }

                var idx = index.Value;
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
                                int.TryParse(prop.Value, out var t);
                                d.Track = t;
                            }

                            break;
                        case PurchaseField:
                            if (d.Purchase == null)
                            {
                                d.Purchase = [];
                            }

                            if (remove)
                            {
                                d.Purchase.Remove(qual);
                            }
                            else
                            {
                                // Filter out AMG for now (we should probably just delete AMG)
                                if (!string.Equals(qual, "ms", StringComparison.OrdinalIgnoreCase))
                                {
                                    d.Purchase[qual] = prop.Value;
                                }
                            }

                            break;
                        case AlbumPromote:
                            // Promote to first
                            promotions.Add(idx);
                            break;
                        case AlbumOrder:
                            // Forget all previous promotions and do a re-order base ond values
                            promotions.Clear();
                            reorder = prop.Value
                                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                .Select(int.Parse).ToList();
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
                albums = [];

                foreach (var t in reorder)
                {
                    if (map.TryGetValue(t, out var d))
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
                    if (map.TryGetValue(i, out var d) && d.Name != null)
                    {
                        albums.Add(d);
                    }
                }
            }

            // Now do individual (trivial) promotions
            foreach (var t in promotions)
            {
                if (!map.TryGetValue(t, out var d) || d.Name == null)
                {
                    continue;
                }

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
                if (string.Equals(
                    normal, NormalizeAlbumString(album.Name),
                    StringComparison.InvariantCultureIgnoreCase))
                {
                    ret = album;
                    break;
                }

                if (string.Equals(
                    stripped, NormalizeAlbumString(album.Name),
                    StringComparison.InvariantCultureIgnoreCase))
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
        ///     Finds a representitive of the largest cluster of tracks
        ///     (clustered by approximate duration) that is an very
        ///     close title/artist match
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

            string album = null;
            if (Albums is { Count: > 0 })
            {
                album = Albums[0].Name;
            }

            return RankTracksByCluster(tracks, album);
        }

        public static IList<ServiceTrack> RankTracksByCluster(IList<ServiceTrack> tracks,
            string album)
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

                var amatches = ret.Where(
                    t => string.Equals(
                        CleanString(t.Album), album,
                        StringComparison.InvariantCultureIgnoreCase)).ToList();

                foreach (var t in amatches)
                {
                    ret.Remove(t);
                }

                ret.InsertRange(0, amatches);
            }

            return ret;
        }

        public static IList<ServiceTrack> RankTracksByDuration(IList<ServiceTrack> tracks,
            int duration)
        {
            foreach (var t in tracks)
            {
                t.TrackRank = t.Duration.HasValue
                    ? Math.Abs(duration - t.Duration.Value)
                    : int.MaxValue;
            }

            return tracks.OrderBy(t => t.TrackRank).ToList();
        }

        private static Dictionary<int, List<ServiceTrack>> ClusterTracks(IList<ServiceTrack> tracks,
            int size = 10, int offset = 0)
        {
            var ret = new Dictionary<int, List<ServiceTrack>>();

            foreach (var track in tracks)
            {
                if (track.Duration.HasValue)
                {
                    var cluster = (track.Duration.Value + offset) / size;
                    if (!ret.TryGetValue(cluster, out var list))
                    {
                        list = [];
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

        public static IList<ServiceTrack> DurationFilter(IList<ServiceTrack> tracks, int duration,
            int epsilon)
        {
            return tracks.Where(
                    track =>
                        track.Duration.HasValue &&
                        Math.Abs(track.Duration.Value - duration) < epsilon)
                .ToList();
        }
        #endregion

        #region Comparison

        //  Two song are equivalent if Titles are equal, artists are similar or empty and all other fields are equal
        public bool Equivalent(Song song)
        {
            return
                WeakEquivalent(song); // TDKILL - Match any album in this to any album in song....
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
            return CreateTitleHash(Title) == CreateTitleHash(song.Title) && (string.IsNullOrWhiteSpace(Artist) || string.IsNullOrWhiteSpace(song.Artist) ||
                CreateTitleHash(Artist) == CreateTitleHash(song.Artist));
        }

        #endregion

        #region TitleArtist

        public bool TitleArtistMatch(string title, string artist)
        {
            return SoftArtistMatch(artist, Artist) && (DoMatch(CreateNormalForm(title), CreateNormalForm(Title)) ||
                DoMatch(NormalizeAlbumString(title), NormalizeAlbumString(Title)) ||
                DoMatch(CleanAlbum(title, artist), CleanAlbum(Title, Artist)));
        }

        public static bool SoftArtistMatch(string artist1, string artist2)
        {
            // Artist Soft Match
            var a1 = BreakDownArtist(artist1);
            var a2 = BreakDownArtist(artist2);

            // Start with the easy case where we've got a single name artist on one side or the other
            // If not, we require an overlap of two
            if (!(a1.Count == 1 && a2.Contains(a1.First()) ||
                a2.Count == 1 && a1.Contains(a2.First()) ||
                a1.Count(s => a2.Contains(s)) > 1))
            {
                Trace.WriteLineIf(
                    TraceLevels.General.TraceError,
                    $"AFAIL '{string.Join(",", a1)}' - '{string.Join(",", a2)}'");
                return false;
            }

            Trace.WriteLineIf(
                TraceLevels.General.TraceWarning,
                $"ASUCC '{string.Join(",", a1)}' - '{string.Join(",", a2)}'");
            return true;
        }

        private static HashSet<string> BreakDownArtist(string artist)
        {
            var bits = NormalizeAlbumString(artist ?? "", true).ToUpper()
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var init = new HashSet<string>(bits);
            init.RemoveWhere(s => ArtistIgnore.Contains(s));
            return init;
        }

        private static bool DoMatch(string s1, string s2)
        {
            var ret = string.Equals(s1, s2, StringComparison.OrdinalIgnoreCase);
            var rv = ret ? "==" : "!=";
            Trace.WriteLineIf(TraceLevels.General.TraceWarning, $"{rv}{s1}{s2}");
            return ret;
        }

        public string TitleArtistString => CreateNormalForm(Title) + "+" + CreateNormalForm(Artist);

        public string TitleArtistAlbumString
        {
            get
            {
                var ta = TitleArtistString;
                var album = Albums.FirstOrDefault()?.Name;

                return album == null ? ta : ta + "+" + CreateNormalForm(album);
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
            artist ??= string.Empty;
            title ??= string.Empty;

            var ret = $"{CreateNormalForm(artist)} {CreateNormalForm(title)}";

            return string.IsNullOrWhiteSpace(ret) ? null : ret;
        }

        public static Song GetNullSong()
        {
            return new Song();
        }

        public static int TryParseId(string s, out Guid id)
        {
            const string idField = SongIndex.SongIdField + "=";
            id = Guid.Empty;
            if (!s.StartsWith(SongIndex.SongIdField))
            {
                return 0;
            }

            var t = s.IndexOf('\t');
            if (t == -1)
            {
                return 0;
            }

            var sg = s[idField.Length..t];
            if (Guid.TryParse(sg, out var g))
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

        protected static HashSet<string> AlbumFields =
        [
            AlbumField, PublisherField, TrackField, PurchaseField, AlbumListField, AlbumPromote,
            AlbumOrder
        ];

        // TOOD: This should really end up in a utility class as some point
        public static string MungeString(string s, bool normalize,
            IEnumerable<string> extraIgnore = null)
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
                    else if (!normalize && c == '\'' && IsLetter(lastC) && norm.Length > i + 1 &&
                        IsLetter(norm[i + 1]))
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
                            var word = sb.ToString(wordBreak, sb.Length - wordBreak).ToUpper()
                                .Trim();
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

        public static string NormalizeAlbumString(string s, bool keepWhitespace = false)
        {
            var r = s;
            if (string.IsNullOrWhiteSpace(r))
            {
                return r;
            }

            s = r.Normalize(NormalizationForm.FormD);

            var sb = new StringBuilder();
            foreach (var c in s.Where(t => IsLetterOrDigit(t) || keepWhitespace && IsWhiteSpace(t)))
            {
                sb.Append(c);
            }

            r = sb.ToString();

            return r;
        }

        private static readonly char[] Digits =
            ['0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '.', '-'];

        private static string TruncateAlbum(string album, char delimiter)
        {
            var idx = album.LastIndexOf(delimiter);
            return idx == -1 || idx < 10 || idx < album.Length / 4
                ? album
                : album[..idx];
        }

        public static string CleanAlbum(string album, string artist)
        {
            if (string.IsNullOrWhiteSpace(album))
            {
                return null;
            }

            var ignore = new List<string> { "VOL", "VOLS", "VOLUME", "VOLUMES" };

            if (!string.IsNullOrWhiteSpace(artist))
            {
                var words = artist.ToUpper().Split(
                    new[] { ' ', ',', ';' },
                    StringSplitOptions.RemoveEmptyEntries);
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

            var ret = MungeString(album, true, ignore);
            if (string.IsNullOrWhiteSpace(ret))
            {
                ret = MungeString(albumBase, true, ignore);
            }

            return ret;
        }

        protected static string[] Ignore =
        [
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
        ];

        private static readonly string[] ArtIgnore =
            ["BAND", "FEAT", "FEATURING", "HIS", "HER", "ORCHESTRA", "WITH"];

        private static readonly HashSet<string> ArtistIgnore =
            [.. Ignore.Concat(ArtIgnore)];

        protected static IList<string> TagsToDanceIds(TagList tags)
        {
            return TagsToDances(tags).Select(d => d.Id).ToList();
        }

        protected static IList<DanceObject> TagsToDances(TagList tags)
        {
            return Dances.Instance.FromNames(tags.Filter("Dance").StripType()).ToList();
        }

        public void AddProperty(string baseName,
            object value = null, int index = -1, string qual = null)
        {
            if (value != null)
            {
                SongProperties.Add(SongProperty.Create(baseName, value.ToString(), index, qual));
            }
        }

        #endregion

        #region String Cleanup

        public static string CleanDanceName(string name)
        {
            var up = name.ToUpper();

            var parts = up.Split(
                new[] { ' ', '-', '\t', '/', '&', '-', '+', '(', ')' },
                StringSplitOptions.RemoveEmptyEntries);

            var ret = string.Join("", parts);

            if (ret.LastIndexOf('S') != ret.Length - 1)
            {
                return ret;
            }

            var truncate = 1;
            if (ret.LastIndexOf('E') == ret.Length - 2)
            {
                if (ret.Length > 2)
                {
                    var ch = ret[^3];
                    if (ch != 'A' && ch != 'E' && ch != 'I' && ch != 'O' && ch != 'U')
                    {
                        truncate = 2;
                    }
                }
            }

            ret = ret[..^truncate];

            return ret;
        }

        public static string CleanText(string text)
        {
            text = WebUtility.HtmlDecode(text);
            text = text.Replace("\r", " ");
            text = text.Replace("\n", " ");
            text = text.Replace("\t", " ");
            text = text.Trim();

            if (!text.Contains("  "))
            {
                return text;
            }

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

        public static string CleanQuotes(string name)
        {
            return CustomTrim(CustomTrim(name, '"'), '\'');
        }

        public static string CustomTrim(string name, char quote)
        {
            if (name.Length > 0 && name[0] == quote && name[^1] == quote)
            {
                name = name.Trim(quote);
            }

            return name;
        }

        public static string Unsort(string name)
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

            Trace.WriteLineIf(TraceLevels.General.TraceWarning, $"Unusual Sort: {name}");
            return name;
        }

        public static string CleanArtistString(string name)
        {
            if (!name.Contains(','))
            {
                return name;
            }

            var parts = new[] { name };
            if (name.IndexOf('&') != -1)
            {
                parts = name.Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries);
            }
            else if (name.IndexOf('/') != -1)
            {
                parts = name.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            }

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

        public static string CleanName(string name)
        {
            var up = name.ToUpper();

            var parts = up.Split(
                new[] { ' ', '-', '\t', '/', '&', '-', '+', '(', ')' },
                StringSplitOptions.RemoveEmptyEntries);

            var ret = string.Join("", parts);

            if (ret.LastIndexOf('S') != ret.Length - 1)
            {
                return ret;
            }

            var truncate = 1;
            if (ret.LastIndexOf('E') == ret.Length - 2)
            {
                if (ret.Length > 2)
                {
                    var ch = ret[^3];
                    if (ch != 'A' && ch != 'E' && ch != 'I' && ch != 'O' && ch != 'U')
                    {
                        truncate = 2;
                    }
                }
            }

            ret = ret[..^truncate];

            return ret;
        }

        #endregion
    }
}
