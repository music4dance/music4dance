using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using DanceLibrary;
using static System.Char;

// ReSharper disable ArrangeThisQualifier

namespace m4dModels
{
    [DataContract]
    public abstract class SongBase : TaggableObject
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

        // Special cases for reading scraped data
        public const string TitleArtistCell = "TitleArtist";
        public const string DancersCell = "Dancers";
        public const string DanceTags = "DanceTags";
        public const string SongTags = "SongTags";
        public const string MeasureTempo = "MPM";

        // Commands
        public const string CreateCommand = ".Create";
        public const string EditCommand = ".Edit";
        public const string DeleteCommand = ".Delete";
        public const string MergeCommand = ".Merge";
        public const string UndoCommand = ".Undo";
        public const string RedoCommand = ".Redo";
        public const string FailedLookup = ".FailedLookup"; // 0: Not found on Title/Artist; 1: Not found on Title/Artist/Album
        public const string NoSongId = ".NoSongId"; // Pseudo action for serialization
        public const string SerializeDeleted = ".SerializeDeleted"; // Pseudo action for serialization

        public const string SuccessResult = ".Success";
        public const string FailResult = ".Fail";
        public const string MessageData = ".Message";

        public static readonly string[] ScalarFields = {TitleField, ArtistField, TempoField, LengthField, SampleField, DanceabilityField,EnergyField,ValenceFiled};

        public static readonly PropertyInfo[] ScalarProperties = {
            typeof(SongBase).GetProperty(TitleField),
            typeof(SongBase).GetProperty(ArtistField),
            typeof(SongBase).GetProperty(TempoField),
            typeof(SongBase).GetProperty(LengthField),
            typeof(SongBase).GetProperty(SampleField),
            typeof(SongBase).GetProperty(DanceabilityField),
            typeof(SongBase).GetProperty(EnergyField),
            typeof(SongBase).GetProperty(ValenceFiled),
        };

        public static readonly int DanceRatingCreate = 3;  // TODO: when we allow a user to manually add a song, give lots of credit
        public static readonly int DanceRatingInitial = 2;
        public static readonly int DanceRatingIncrement = 2;
        public static readonly int DanceRatingAutoCreate = 1;
        public static readonly int DanceRatingDecrement = -1;

        #endregion

        #region Serialization
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

            var props = SongProperty.Serialize(OrderedProperties, actions);
            if (actions != null && actions.Contains(NoSongId))
            {
                return props;
            }

            return $"SongId={SongId.ToString("B")}\t{props}";
        }
        public override string ToString()
        {
            return Serialize(null);
        }

        protected void LoadProperties(ICollection<SongProperty> properties) 
        {
            var created = SongProperties != null && SongProperties.Count > 0;
            ApplicationUser currentUser = null;
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
                        currentUser = new ApplicationUser(prop.Value);
                        // TOOD: if the placeholder user works, we should use it to simplify the ModifiedRecord
                        currentModified = new ModifiedRecord {SongId = SongId, UserName = prop.Value};
                        AddModifiedBy(currentModified);
                        break;
                    case DanceRatingField:
                        {
                            var del = SoftUpdateDanceRating(prop.Value);
                            if (del != null) drDelete.Add(del);
                        }
                        break;
                    case AddedTags:
                        if (currentUser == null)
                        {
                            Trace.WriteLineIf(TraceLevels.General.TraceError,$"Null User when attempting to ad tag {prop.Value} to song {SongId}");
                        }
                        else
                        {
                            AddObjectTags(prop.DanceQualifier, prop.Value, currentUser);
                        }
                        break;
                    case RemovedTags:
                        if (currentUser == null)
                        {
                            Trace.WriteLineIf(TraceLevels.General.TraceError, $"Null User when attempting to ad tag {prop.Value} to song {SongId}");
                        }
                        else
                        {
                            RemoveObjectTags(prop.DanceQualifier, prop.Value, currentUser);
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
        public override char IdModifier => 'S';

        public override string TagIdBase => SongId.ToString("N");

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
        public virtual string Purchase { get; set; }
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
        public virtual string Album { get; set; }

        [DataMember]
        public virtual ICollection<DanceRating> DanceRatings { get; set; }
        [DataMember]
        public virtual ICollection<ModifiedRecord> ModifiedBy { get; set; }
        [DataMember]
        public virtual ICollection<SongProperty> SongProperties { get; set; }

        // These are helper properties (they don't map to database columns)
        public string Signature => BuildSignature(Artist, Title);

        public bool TempoConflict(SongBase s, decimal delta)
        {
            return Tempo.HasValue && s.Tempo.HasValue && Math.Abs(Tempo.Value - s.Tempo.Value) > delta;
        }
        public bool IsNull => string.IsNullOrWhiteSpace(Title);

        public bool HasSample => Sample != null && Sample != ".";
        public bool HasEchoNest => Danceability != null && !float.IsNaN(Danceability.Value);

        public virtual SongLog CurrentLog
        {
            get { return null; }
            // ReSharper disable once ValueParameterNotUsed
            set { }
        }

        public string AlbumName => new AlbumTrack(Album).Album;

        public int TrackNumber  => new AlbumTrack(Album).Track;

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

        public IOrderedEnumerable<SongProperty> OrderedProperties
        {
            get { return SongProperties.OrderBy(sp => sp.Id); }
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

            if (DanceRatings == null)
            {
                DanceRatings = new List<DanceRating>();
            }

            var dr = DanceRatings.FirstOrDefault(r => r.DanceId.Equals(drd.DanceId));

            if (dr == null)
            {
                dr = new DanceRating { SongId = this.SongId, DanceId = drd.DanceId, Weight = 0 };
                DanceRatings.Add(dr);
            }

            dr.Weight += drd.Delta;

            if (dr.Weight == 0)
            {
                ret = dr;
            }

            if (!updateProperties) return ret;

            SongProperties.Add(new SongProperty { SongId = SongId, Name = DanceRatingField, Value = drd.ToString() });

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

        public void InferDances(ApplicationUser user)
        {
            // Get the dances from the current user's tags
            var tags = GetUserTags(user);

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

        #endregion

        #region Tags
        public void ChangeDanceTags(string danceId, string tags, ApplicationUser user, DanceMusicService dms)
        {
            var dr = FindRating(danceId);
            if (dr != null)
            {
                dr.ChangeTags(tags, user, dms, this);
            }
            else
            {
                Trace.WriteLineIf(TraceLevels.General.TraceError, $"Undefined DanceRating {SongId.ToString()}:{danceId}");
            }
        }

        public override void RegisterChangedTags(TagList added, TagList removed, ApplicationUser user, DanceMusicService dms, object data)
        {
            base.RegisterChangedTags(added, removed, user, dms, data);

            if (data != null)
            {
                ChangeTag(AddedTags, added, dms);
                ChangeTag(RemovedTags, removed, dms);
            }
        }

        public void ChangeTag(string command, TagList list, DanceMusicService dms)
        {
            // NOTE: The user is implied by this operation because there should be an edit header record before it
            var tags = list?.ToString();
            if (!string.IsNullOrWhiteSpace(tags))
            {
                CreateProperty(command, tags, CurrentLog, dms);
            }
        }

        public TagList AddObjectTags(string qualifier, string tags, ApplicationUser user, DanceMusicService dms=null)
        {
            if (user == null)
            {
                Trace.WriteLine($"Null user when adding tags ({tags}) with qualifer ({qualifier}) to song: {this}");
                return null;
            }

            TaggableObject tobj = this;
            if (!string.IsNullOrWhiteSpace(qualifier))
                tobj = FindRating(qualifier);

            return tobj.AddTags(tags, user, dms, (dms == null) ? null : this);
        }

        public TagList RemoveObjectTags(string qualifier, string tags, ApplicationUser user, DanceMusicService dms = null)
        {
            TaggableObject tobj = this;
            if (!string.IsNullOrWhiteSpace(qualifier))
                tobj = FindRating(qualifier);

            return tobj.RemoveTags(tags, user, dms, (dms == null) ? null : this);
        }

        public TagList GetUserTags(ApplicationUser user)
        {
            var userName = user.UserName;

            // Build the tags from the properties
            var acc = new TagList();
            string cu = null;
            foreach (var prop in OrderedProperties)
            {
                // ReSharper disable once SwitchStatementMissingSomeCases
                switch (prop.Name)
                {
                    case UserField:
                    case UserProxy:
                        cu = prop.Value;
                        break;
                    case AddedTags:
                        if (userName.Equals(cu))
                        {
                            acc = acc.Add(new TagList(prop.Value));
                        }
                        break;
                    case RemovedTags:
                        if (userName.Equals(cu))
                        {
                            acc = acc.Subtract(new TagList(prop.Value));
                        }
                        break;
                }
            }

            return acc;
        }

        #endregion

        #region Comparison
        //  Two song are equivalent if Titles are equal, artists are similar or empty and all other fields are equal
        public bool Equivalent(Song song)
        {
            return WeakEquivalent(song) && EqString(Album, song.Album);
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

        protected virtual bool AddModifiedBy(ModifiedRecord mr)
        {
            mr.SongId = SongId;

            ModifiedRecord other = null;

            if (mr.ApplicationUserId != null)
            {
                other = ModifiedBy.FirstOrDefault(r => r.ApplicationUserId == mr.ApplicationUserId);
            }
            else if (mr.UserName != null)
            {
                other = ModifiedBy.FirstOrDefault(r => r.UserName == mr.UserName);
            }

            if (other != null) return false;

            ModifiedBy.Add(mr);
            return true;
        }
        protected virtual SongProperty CreateProperty(string name, object value, SongLog log, DanceMusicService dms)
        {
            return CreateProperty(name, value, null, log, dms);
        }
        protected virtual SongProperty CreateProperty(string name, object value, object old, SongLog log, DanceMusicService dms)
        {
            if (SongProperties == null)
            {
                SongProperties = new List<SongProperty>();
            }
            var prop = new SongProperty { SongId = SongId, Name = name, Value = value?.ToString() };
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
            return OrderedProperties.FirstOrDefault(p => p.Name == name);
        }

        public SongProperty LastProperty(string name, IEnumerable<string> excludeUsers = null, IEnumerable<string> includeUsers = null)
        {
            return FilteredProperties(excludeUsers,includeUsers).LastOrDefault(p => p.Name == name);
        }

        public IOrderedEnumerable<SongProperty> FilteredProperties(string baseName, IEnumerable<string> excludeUsers = null, IEnumerable<string> includeUsers = null)
        {
            return FilteredProperties(excludeUsers, includeUsers).Where(p => p.BaseName == baseName).OrderBy(p => p.Id);
        }

        public IOrderedEnumerable<SongProperty> FilteredProperties(IEnumerable<string> excludeUsers = null, IEnumerable<string> includeUsers = null)
        {
            if (excludeUsers == null && includeUsers == null) return OrderedProperties;

            var eu = (excludeUsers == null) ? null : (excludeUsers as HashSet<string> ?? new HashSet<string>(excludeUsers));
            var iu = (includeUsers == null) ? null : (includeUsers as HashSet<string> ?? new HashSet<string>(includeUsers));

            var ret = new List<SongProperty>();

            var inFilter = includeUsers != null;
            foreach (var prop in OrderedProperties)
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

            return ret.OrderBy(sp => sp.Id);
        }

        protected void ClearValues()
        {
            foreach (var pi in ScalarProperties)
            {
                pi.SetValue(this, null);
            }

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
                var album = Album;

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
