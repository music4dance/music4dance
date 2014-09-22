using DanceLibrary;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace m4dModels
{
    public abstract class SongBase : DbObject
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
        public const string TagField = "Tag";

        // Commands
        public const string CreateCommand = ".Create";
        public const string EditCommand = ".Edit";
        public const string DeleteCommand = ".Delete";
        public const string MergeCommand = ".Merge";
        public const string UndoCommand = ".Undo";
        public const string RedoCommand = ".Redo";
        public const string FailedLookup = ".FailedLookup"; // 0: Not found on Title/Artist; 1: Not found on Title/Artist/Album
        public const string NoSongId = ".NoSongId"; // Pseudo action for serialization

        public const string SuccessResult = ".Success";
        public const string FailResult = ".Fail";
        public const string MessageData = ".Message";

        public static readonly string[] ScalarFields = new string[] {Song.TitleField, Song.ArtistField, Song.TempoField, Song.LengthField};

        public static readonly PropertyInfo[] ScalarProperties = new PropertyInfo[]
        {
            typeof(SongBase).GetProperty(SongBase.TitleField),
            typeof(SongBase).GetProperty(SongBase.ArtistField),
            typeof(SongBase).GetProperty(SongBase.TempoField),
            typeof(SongBase).GetProperty(SongBase.LengthField),
            typeof(SongBase).GetProperty(SongBase.LengthField),
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
            if (string.IsNullOrWhiteSpace(Title))
            {
                return null;
            }
            else
            {
                string props = SongProperty.Serialize(SongProperties, actions);
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

        protected void LoadProperties(ICollection<SongProperty> properties) // , IFactories factories, IUserMap users
        {
            bool created = false;

            foreach (SongProperty prop in properties)
            {
                string bn = prop.BaseName;

                if (!prop.IsAction)
                {
                    switch (bn)
                    {
                        case UserField:
                            AddModifiedBy(new ModifiedRecord { SongId = this.SongId, UserName = prop.Value });
                            break;
                        case DanceRatingField:
                            UpdateDanceRating(prop.Value);
                            break;
                        case TagField:
                            UpdateTags(prop.Value);
                            break;
                        case AlbumField:
                        case PublisherField:
                        case TrackField:
                        case PurchaseField:
                            // All of these are taken care of with build album
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
                        default:
                            // All of the simple properties we can just set
                            {
                                PropertyInfo pi = this.GetType().GetProperty(bn);
                                if (pi != null)
                                {
                                    pi.SetValue(this, prop.ObjectValue);
                                }
                            }
                            break;
                    }
                }
            }
        }
        #endregion

        #region Properties
        public Guid SongId { get; set; }

        [Range(5.0, 500.0)]
        public decimal? Tempo { get; set; }
        public string Title { get; set; }
        public string Artist { get; set; }
        [Range(0, 9999)]
        public int? Length { get; set; }
        public string Purchase { get; set; }
        public DateTime Created { get; set; }
        public DateTime Modified { get; set; }
        public virtual string Album { get; set; }
        public virtual string TagSummary { get; set; }

        public virtual ICollection<DanceRating> DanceRatings { get; set; }
        public virtual ICollection<ModifiedRecord> ModifiedBy { get; set; }
        public virtual ICollection<Tag> Tags { get; set; }
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

        public void UpdateDanceRating(DanceRatingDelta drd)
        {
            DanceRating dr = DanceRatings.FirstOrDefault(r => r.DanceId.Equals(drd.DanceId));

            if (dr == null)
            {
                dr = new DanceRating { SongId = this.SongId, DanceId = drd.DanceId, Weight = 0 };
                DanceRatings.Add(dr);
            }

            dr.Weight += drd.Delta;
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
                UpdateDanceRating(drd);
                SongProperty prop = new SongProperty { SongId = this.SongId, Name = DanceRatingField, Value = drd.ToString() };
                SongProperties.Add(prop);
            }

            UpdateTags(TagsFromDances(dances));
        }

        public static string TagsFromDances(IEnumerable<string> dances)
        {
            StringBuilder tags = new StringBuilder();
            string sep = "";

            foreach (string d in dances)
            {
                tags.Append(sep);
                tags.Append(Dances.Instance.DanceDictionary[d].Name);
                sep = "|";
            }
            return tags.ToString();
        }
        #endregion

        #region Tags
        public void AddTag(string value)
        {
            value = value.Trim();
            Tag tag = Tags.FirstOrDefault(t => t.Value == value);

            if (tag != null)
            {
                tag.Count += 1;
            }
            else
            {
                tag = new Tag { SongId = this.SongId, Value = value, Count = 1 };
                Tags.Add(tag);
            }
        }

        public void AddTags(string values)
        {
            string[] tags = values.Split(new char[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string tag in tags)
            {
                AddTag(tag);
            }
        }

        public void UpdateTags(string values)
        {
            string[] tags = values.Split(new char[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string tag in tags)
            {
                UpdateTag(tag);
            }
        }

        public void UpdateTag(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            int delta = 1;
            if (value[0] == '-')
            {
                delta = -1;
                value = value.Substring(1);
            }

            Tag other = FindTag(value);

            if (other != null)
            {
                other.Count += delta;
            }
            else if (delta == 1)
            {
                Tags.Add(new Tag() { SongId = this.SongId, Value = value, Count = 1 });
            }
            else
            {
                Trace.WriteLine(string.Format("Bad Tag: value={0}, songId={1}", value, this.SongId));
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
                            IList<string> values = null;
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

        public Tag FindTag(string value)
        {
            return Tags.FirstOrDefault(t => string.Equals(t.Value, value, StringComparison.OrdinalIgnoreCase));
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
        public string CleanTitle
        {
            get
            {
                return Song.CleanString(Title);
            }
        }
        public string CleanArtist
        {
            get
            {
                return Song.CleanString(Artist);
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
            artist = (artist == null) ? string.Empty : artist;
            title = (title == null) ? string.Empty : title;

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

            string[] parts = up.Split(new char[] { ' ', '-', '\t', '/', '&', '-', '+', '(', ')' }, StringSplitOptions.RemoveEmptyEntries);

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
                name = name.Trim(new char[] { quote });
            }
            return name;
        }

        static public string Unsort(string name)
        {
            string[] parts = name.Split(new char[] { ',' });
            if (parts.Length == 1)
            {
                return parts[0].Trim();
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
                    parts = name.Split(new char[] { '&' }, StringSplitOptions.RemoveEmptyEntries);
                else if (name.IndexOf('/') != -1)
                    parts = name.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

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

            string[] parts = up.Split(new char[] { ' ', '-', '\t', '/', '&', '-', '+', '(', ')' }, StringSplitOptions.RemoveEmptyEntries);

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
