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

        public static readonly string[] ScalarFields = {TitleField, ArtistField, TempoField, LengthField};

        public static readonly PropertyInfo[] ScalarProperties = {
            typeof(SongBase).GetProperty(TitleField),
            typeof(SongBase).GetProperty(ArtistField),
            typeof(SongBase).GetProperty(TempoField),
            typeof(SongBase).GetProperty(LengthField),
        };

        public static readonly int DanceRatingCreate = 7;  // TODO: when we allow a user to manually add a song, give lots of credit
        public static readonly int DanceRatingInitial = 4;
        public static readonly int DanceRatingIncrement = 2;
        public static readonly int DanceRatingAutoCreate = 3;
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
                        currentUser = new ApplicationUser(prop.Value);
                        // TOOD: if the placeholder user works, we should use it to simplify the ModifiedRecord
                        currentModified = new ModifiedRecord {SongId = this.SongId, UserName = prop.Value};
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

        public virtual SongLog CurrentLog
        {
            get { return null; }
            // ReSharper disable once ValueParameterNotUsed
            set { }
        }

        public string AlbumName => new AlbumTrack(Album).Album;

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

            if (dr.Weight <= 0)
            {
                ret = dr;
            }

            if (!updateProperties) return ret;

            SongProperties.Add(new SongProperty { SongId = this.SongId, Name = DanceRatingField, Value = drd.ToString() });

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
                if (prop.BaseName == UserField)
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
            var prop = new SongProperty { SongId = this.SongId, Name = name, Value = value == null ? null : value.ToString() };
            SongProperties.Add(prop);

            return prop;
        }

        public bool SetTimesFromProperties()
        {
            var first = FirstProperty(TimeField);
            if (first == null) return false;

            var last = LastProperty(TimeField);

            var firstTime = first.ObjectValue as DateTime?;
            var lastTime = last.ObjectValue as DateTime?;

            if (firstTime == null || lastTime == null) return false;

            var modified = false;
            if (Created != firstTime)
            {
                Created = firstTime.Value;
                modified = true;
            }

            // ReSharper disable once InvertIf
            if (Modified != lastTime)
            {
                Modified = lastTime.Value;
                modified = true;
            }

            return modified;
        }

        public SongProperty FirstProperty(string name)
        {
            return SongProperties.FirstOrDefault(p => p.Name == name);
        }

        public SongProperty LastProperty(string name)
        {
            return SongProperties.LastOrDefault(p => p.Name == name);
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
            return
                string.Equals(CreateNormalForm(title), CreateNormalForm(Title)) &&
                string.Equals(CreateNormalForm(artist), CreateNormalForm(Artist));
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

        protected static string MungeString(string s, bool normalize)
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
                            if (Ignore.Contains(word))
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

        public static string NormalizeAlbumString(string s)
        {
            var r = s;
            if (string.IsNullOrWhiteSpace(r)) return r;

            s = r.Normalize(NormalizationForm.FormD);

            var sb = new StringBuilder();
            foreach (var c in s.Where(IsLetterOrDigit))
            {
                sb.Append(c);
            }
            r = sb.ToString();

            return r;
        }
        public static string CleanAlbum(string album, string artist)
        {
            if (string.IsNullOrWhiteSpace(album)) return null;

            album = NormalizeAlbumString(album);
            var artistN = NormalizeAlbumString(artist);

            if (!string.IsNullOrWhiteSpace(artistN))
            {
                var albumT = album.Replace(artist, "");
                if (!string.IsNullOrWhiteSpace(albumT))
                    album = albumT;
            }

            if (string.IsNullOrWhiteSpace(artist)) return album;

            var words = artist.Split(new[] {' ', ','}, StringSplitOptions.RemoveEmptyEntries);
            foreach (var albumT in words.Select(NormalizeAlbumString).Where(wordT => !string.IsNullOrWhiteSpace(wordT)).Select(wordT => album.Replace(wordT, "")).Where(albumT => !string.IsNullOrWhiteSpace(albumT)))
            {
                album = albumT;
            }

            return album;
        }

        protected static string[] Ignore =
        {
            "A",
            "AND",
            "OR",
            "THE",
            "THIS",
            "IN",
            "OF"
        };
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
