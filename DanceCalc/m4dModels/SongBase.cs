using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using DanceLibrary;
// ReSharper disable ArrangeThisQualifier

namespace m4dModels
{
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

        public static readonly string[] ScalarFields = new string[] {TitleField, ArtistField, TempoField, LengthField};

        public static readonly PropertyInfo[] ScalarProperties = new PropertyInfo[]
        {
            typeof(SongBase).GetProperty(TitleField),
            typeof(SongBase).GetProperty(ArtistField),
            typeof(SongBase).GetProperty(TempoField),
            typeof(SongBase).GetProperty(LengthField),
        };

        public static readonly int DanceRatingCreate = 10;  // TODO: when we allow a user to manually add a song, give lots of credit
        public static readonly int DanceRatingInitial = 6;
        public static readonly int DanceRatingIncrement = 3;
        public static readonly int DanceRatingAutoCreate = 5;
        public static readonly int DanceRatingDecrement = -2;

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
            else
            {
                string props = SongProperty.Serialize(SongProperties.OrderBy(sp => sp.Id), actions);
                if (actions != null && actions.Contains(NoSongId))
                {
                    return props;
                }
                else
                {
                    return string.Format("SongId={0}\t{1}",SongId.ToString("B"),props);
                }
            }
        }
        public override string ToString()
        {
            return Serialize(null);
        }

        protected void LoadProperties(ICollection<SongProperty> properties) 
        {
            bool created = SongProperties != null && SongProperties.Count > 0;
            ApplicationUser currentUser = null;
            ModifiedRecord currentModified = null;
            bool deleted = false;

            foreach (SongProperty prop in properties)
            {
                string bn = prop.BaseName;

                switch (bn)
                {
                    case UserField:
                        currentUser = new ApplicationUser(prop.Value);
                        // TOOD: if the placeholder user works, we should use it to simplify the ModifiedRecord
                        currentModified = new ModifiedRecord {SongId = this.SongId, UserName = prop.Value};
                        AddModifiedBy(currentModified);
                        break;
                    case DanceRatingField:
                        UpdateDanceRating(prop.Value);
                        break;
                    case AddedTags:
                        AddTags(prop.Value, currentUser);
                        break;
                    case RemovedTags:
                        RemoveTags(prop.Value, currentUser);
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
                            DateTime time = (DateTime)prop.ObjectValue;
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
                            PropertyInfo pi = GetType().GetProperty(bn);
                            if (pi != null)
                            {
                                pi.SetValue(this, prop.ObjectValue);
                            }
                        }
                        break;
                }
            }

            if (deleted)
            {
                ClearValues();
            }
        }
        #endregion

        #region Properties
        public override char IdModifier
        {
            get { return 'S'; }
        }

        public override string TagIdBase
        {
            get { return SongId.ToString("N"); }
        }

        public Guid SongId { get; set; }

        [Range(5.0, 500.0)]
        public decimal? Tempo { get; set; }
        public string Title { get; set; }
        public string Artist { get; set; }
        [Range(0, 9999)]
        public int? Length { get; set; }
        public virtual string Purchase { get; set; }
        public DateTime Created { get; set; }
        public DateTime Modified { get; set; }
        public virtual string Album { get; set; }

        public virtual ICollection<DanceRating> DanceRatings { get; set; }
        public virtual ICollection<ModifiedRecord> ModifiedBy { get; set; }
        public virtual ICollection<SongProperty> SongProperties { get; set; }

        // These are helper properties (they don't map to database columns)
        public string Signature
        {
            get
            {
                // This is not a fully unambiguous signature, should we add in a checksum with some or all of the
                //  other fields in the song?
                return BuildSignature(Artist, Title);
            }
        }
        public bool TempoConflict(SongBase s, decimal delta)
        {
            return Tempo.HasValue && s.Tempo.HasValue && Math.Abs(Tempo.Value - s.Tempo.Value) > delta;
        }
        public bool IsNull
        {
            get { return string.IsNullOrWhiteSpace(Title); }
        }
        public virtual SongLog CurrentLog
        {
            get { return null; }
            set { }
        }

        public string AlbumName
        {
            get
            {
                return new AlbumTrack(Album).Album;
            }
        }

        #endregion

        #region DanceRating
        /// <summary>
        /// Update the dance rating table based on the encoded
        ///   property value 
        /// </summary>
        /// <param name="value"></param>
        public void UpdateDanceRating(string value)
        {
            DanceRatingDelta drd = new DanceRatingDelta(value);
            UpdateDanceRating(drd);
        }

        public void UpdateDanceRating(DanceRatingDelta drd, bool updateProperties = false)
        {
            DanceRating dr = DanceRatings.FirstOrDefault(r => r.DanceId.Equals(drd.DanceId));

            if (dr == null)
            {
                dr = new DanceRating { SongId = this.SongId, DanceId = drd.DanceId, Weight = 0 };
                DanceRatings.Add(dr);
            }

            dr.Weight += drd.Delta;

            if (dr.Weight <= 0)
            {
                DanceRatings.Remove(dr);
            }

            if (updateProperties)
            {
                SongProperty prop = new SongProperty { SongId = this.SongId, Name = DanceRatingField, Value = drd.ToString() };
                SongProperties.Add(prop);
            }
        }

        public void UpdateDanceRatings(IEnumerable<string> dances, int weight)
        {
            if (dances == null)
            {
                return;
            }

            foreach (string d in dances)
            {
                DanceRatingDelta drd = new DanceRatingDelta { DanceId = d, Delta = weight };
                UpdateDanceRating(drd, true);
            }
        }

        public static string TagsFromDances(IEnumerable<string> dances)
        {
            if (dances == null)
            {
                return null;
            }

            StringBuilder tags = new StringBuilder();
            string sep = "";

            foreach (string d in dances)
            {
                tags.Append(sep);
                tags.Append(Dances.Instance.DanceDictionary[d].Name);
                tags.Append(":Dance");
                sep = "|";
            }
            return tags.ToString();
        }
        #endregion

        #region Tags

        public void ChangeDanceTags(string danceId, string tags, ApplicationUser user, DanceMusicService dms)
        {
            DanceRating dr = FindRating(danceId);
            if (dr != null)
            {
                dr.ChangeTags(tags, user, dms, this);
            }
            else
            {
                Trace.WriteLineIf(TraceLevels.General.TraceError, string.Format("Undefined DanceRating {0}:{1}", SongId.ToString(), danceId));
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
            if (list != null)
            {
                string tags = list.ToString();
                if (!string.IsNullOrWhiteSpace(tags))
                {
                    CreateProperty(command, tags, CurrentLog, dms);
                }
            }
        }

        #endregion

        #region Comparison
        //  Two song are equivalent if Titles are equal, artists are similar or empty and all other fields are equal
        public bool Equivalent(Song song)
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

            return EqString(Album, song.Album) &&
                EqNum(Tempo, song.Tempo) &&
                EqNum(Length, song.Length);
        }


        // Same as equivalent (above) except that album, Tempo and Length aren't compared.
        public bool WeakEquivalent(Song song)
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

            return EqNum(Tempo, song.Tempo) && EqNum(Length, song.Length);
        }

        #endregion

        public IDictionary<string,IList<string>> MapProperyByUsers(string name)
        {
            Dictionary<string, IList<string>> map = new Dictionary<string, IList<string>>();
            List<string> current = new List<string>() {""};

            bool inUsers = false;
            foreach (SongProperty prop in SongProperties)
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
                    if (prop.BaseName == name)
                    {
                        foreach (string user in current)
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

            if (other == null)
            {
                ModifiedBy.Add(mr);
                return true;
            }
            else
            {
                return false;
            }
        }
        protected virtual SongProperty CreateProperty(string name, object value, SongLog log, DanceMusicService dms)
        {
            return CreateProperty(name, value, null, log, dms);
        }
        protected virtual SongProperty CreateProperty(string name, object value, object old, SongLog log, DanceMusicService dms)
        {
            SongProperty prop;

            if (SongProperties == null)
            {
                SongProperties = new List<SongProperty>();
            }
            prop = new SongProperty { SongId = this.SongId, Name = name, Value = value == null ? null : value.ToString() };
            SongProperties.Add(prop);

            return prop;
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
            foreach (PropertyInfo pi in ScalarProperties)
            {
                pi.SetValue(this, null);
            }

            List<DanceRating> drs = DanceRatings.ToList();
            foreach (DanceRating dr in drs)
            {
                DanceRatings.Remove(dr);
            }

            List<ModifiedRecord> us = ModifiedBy.ToList();
            foreach (ModifiedRecord u in us)
            {
                ModifiedBy.Remove(u);
            }
        }

        #region TitleArtist
        public bool TitleArtistMatch(string title, string artist)
        {
            return
                string.Equals(CreateNormalForm(title), CreateNormalForm(Title)) &&
                string.Equals(CreateNormalForm(artist), CreateNormalForm(Artist));
        }

        public string TitleArtistString
        {
            get
            {
                return CreateNormalForm(Title) + "+" + CreateNormalForm(Artist);
            }
        }

        public string TitleArtistAlbumString
        {
            get
            {
                var ta = TitleArtistString;
                var album = Album;

                return (album == null) ? ta : ta + "+" + CreateNormalForm(album);
            }
        }

        public string CleanTitle
        {
            get
            {
                return CleanString(Title);
            }
        }
        public string CleanArtist
        {
            get
            {
                return CleanString(Artist);
            }
        }
        
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

            string ret = string.Format("{0} {1}", CreateNormalForm(artist), CreateNormalForm(title));

            if (string.IsNullOrWhiteSpace(ret))
                return null;
            else
                return ret;
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

            string baseName = SongProperty.ParseBaseName(fieldName);
            return s_albumFields.Contains(baseName);
        }
        protected static HashSet<string> s_albumFields = new HashSet<string>() { AlbumField, PublisherField, TrackField, PurchaseField, AlbumListField, AlbumPromote, AlbumOrder };

        protected static string MungeString(string s, bool normalize)
        {
            if (string.IsNullOrWhiteSpace(s))
            {
                return string.Empty;
            }

            StringBuilder sb = new StringBuilder(s.Length);

            string norm = s + '|';
            if (normalize)
            {
                norm = norm.Normalize(NormalizationForm.FormD);
            }

            int wordBreak = 0;

            bool paren = false;
            bool bracket = false;
            bool space = false;
            char lastC = ' ';

            for (int i = 0; i < norm.Length; i++)
            {
                char c = norm[i];

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
                    if (char.IsLetterOrDigit(c))
                    {
                        if (!normalize && space)
                        {
                            sb.Append(' ');
                            space = false;
                        }
                        char cNew = normalize ? char.ToUpper(c) : c;
                        sb.Append(cNew);
                        lastC = cNew;
                    }
                    else if (!normalize && c == '\'' && char.IsLetter(lastC) && norm.Length > i + 1 && char.IsLetter(norm[i + 1]))
                    {
                        // Special case apostrophe (e.g. that's)
                        sb.Append(c);
                        lastC = c;
                    }
                    else
                    {
                        UnicodeCategory uc = char.GetUnicodeCategory(c);
                        if (uc != UnicodeCategory.NonSpacingMark && sb.Length > wordBreak)
                        {
                            string word = sb.ToString(wordBreak, sb.Length - wordBreak);
                            if (s_ignore.Contains(word))
                            {
                                sb.Length = wordBreak;
                            }
                            wordBreak = sb.Length;
                        }
                        if (c == '(')
                        {
                            paren = true;
                        }
                        else if (c == '[')
                        {
                            bracket = true;
                        }

                        space = true;
                    }
                }
            }

            return sb.ToString();
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
            string r = s;
            if (!string.IsNullOrWhiteSpace(r))
            {
                s = r.Normalize(NormalizationForm.FormD);

                StringBuilder sb = new StringBuilder();
                foreach (char c in s)
                {
                    if (Char.IsLetterOrDigit(c))
                    {
                        sb.Append(c);
                    }
                }
                r = sb.ToString();
            }

            return r;
        }
        public static string CleanAlbum(string album, string artist)
        {
            album = NormalizeAlbumString(album);
            artist = NormalizeAlbumString(artist);

            if (!string.IsNullOrWhiteSpace(artist))
            {
                album = album.Replace(artist, "");
            }

            return album;
        }

        protected static string[] s_ignore =
        {
            "A",
            "AND",
            "OR",
            "THE",
            "THIS"
        };

        #endregion

        #region String Cleanup
        static public string CleanDanceName(string name)
        {
            string up = name.ToUpper();

            string[] parts = up.Split(new[] { ' ', '-', '\t', '/', '&', '-', '+', '(', ')' }, StringSplitOptions.RemoveEmptyEntries);

            string ret = string.Join("", parts);

            if (ret.LastIndexOf('S') == ret.Length - 1)
            {
                int truncate = 1;
                if (ret.LastIndexOf('E') == ret.Length - 2)
                {
                    if (ret.Length > 2)
                    {
                        char ch = ret[ret.Length - 3];
                        if (ch != 'A' && ch != 'E' && ch != 'I' && ch != 'O' && ch != 'U')
                        {
                            truncate = 2;
                        }
                    }
                }
                ret = ret.Substring(0, ret.Length - truncate);
            }

            return ret;
        }

        static public string CleanText(string text)
        {
            text = text.Replace("&nbsp;", " ");
            text = text.Replace("&nbsp", " ");
            text = text.Replace("\r", " ");
            text = text.Replace("\n", " ");
            text = text.Replace("\t", " ");
            text = text.Replace("&quot;", "\"");
            text = text.Replace("&quot", "\"");
            text = text.Replace("&amp;", "&");

            // TODO: is it worth doing a generic unicode replace?
            text = text.Replace("&#39;", "'");
            text = text.Replace("&#333;", "ō");

            text = text.Trim();

            if (text.Contains("  "))
            {
                StringBuilder sb = new StringBuilder(text.Length + 1);

                bool space = false;
                foreach (char c in text)
                {
                    if (char.IsWhiteSpace(c))
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
            }

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
            string[] parts = name.Split(',');
            if (parts.Length == 1 || parts.Any(s => s.All(c => !char.IsLetter(c))))
            {
                return name.Trim();
            }
            else if (parts.Length == 2)
            {
                return string.Format("{0} {1}", parts[1].Trim(), parts[0].Trim());
            }
            else
            {
                Trace.WriteLine(string.Format("Unusual Sort: {0}", name));
                return name;
            }
        }

        static public string CleanArtistString(string name)
        {
            if (name.IndexOf(',') != -1)
            {
                string[] parts = new string[] { name };
                if (name.IndexOf('&') != -1)
                    parts = name.Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries);
                else if (name.IndexOf('/') != -1)
                    parts = name.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

                string separator = string.Empty;
                StringBuilder sb = new StringBuilder();

                foreach (string s in parts)
                {
                    string u = Unsort(s);
                    sb.Append(separator);
                    sb.Append(u);
                    separator = " & ";
                }

                name = sb.ToString();
            }

            return name;
        }

        static public string CleanName(string name)
        {
            string up = name.ToUpper();

            string[] parts = up.Split(new[] { ' ', '-', '\t', '/', '&', '-', '+', '(', ')' }, StringSplitOptions.RemoveEmptyEntries);

            string ret = string.Join("", parts);

            if (ret.LastIndexOf('S') == ret.Length - 1)
            {
                int truncate = 1;
                if (ret.LastIndexOf('E') == ret.Length - 2)
                {
                    if (ret.Length > 2)
                    {
                        char ch = ret[ret.Length - 3];
                        if (ch != 'A' && ch != 'E' && ch != 'I' && ch != 'O' && ch != 'U')
                        {
                            truncate = 2;
                        }
                    }
                }
                ret = ret.Substring(0, ret.Length - truncate);
            }

            return ret;
        }


        #endregion
    }
}
