using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
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
        public const string GenreField = "Genre";

        // Album Fields
        public const string AlbumField = "Album";
        public const string PublisherField = "Publisher";
        public const string TrackField = "Track";
        public const string PurchaseField = "Purchase";
        public const string AlbumListField = "AlbumList";
        public const string AlbumPromote = "PromoteAlbum";

        // Dance Rating
        public const string DanceRatingField = "DanceRating";

        // Commands
        public const string CreateCommand = ".Create";
        public const string EditCommand = ".Edit";
        public const string DeleteCommand = ".Delete";
        public const string MergeCommand = ".Merge";
        public const string UndoCommand = ".Undo";
        public const string RedoCommand = ".Redo";
        public const string FailedLookup = ".FailedLookup"; // 0: Not found on Title/Artist; 1: Not found on Title/Artist/Album

        public const string SuccessResult = ".Success";
        public const string FailResult = ".Fail";
        public const string MessageData = ".Message";
        #endregion

        #region Properties
        public int SongId { get; set; }

        [Range(5.0, 500.0)]
        public decimal? Tempo { get; set; }
        public string Title { get; set; }
        public string Artist { get; set; }
        public string Genre { get; set; }
        [Range(1, 999)]
        public int? Length { get; set; }
        public string Purchase { get; set; }
        public DateTime Created { get; set; }
        public DateTime Modified { get; set; }
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
        #endregion

        #region TitleArtist
        public bool TitleArtistMatch(string title, string artist)
        {
            return
                string.Equals(Song.CreateNormalForm(title), Song.CreateNormalForm(Title)) &&
                string.Equals(Song.CreateNormalForm(artist), Song.CreateNormalForm(Artist));
        }

        public string TitleArtistString
        {
            get
            {
                return Song.CreateNormalForm(Title) + "+" + Song.CreateNormalForm(Artist);
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
        protected static HashSet<string> s_albumFields = new HashSet<string>() { AlbumField, PublisherField, TrackField, PurchaseField, AlbumListField, AlbumPromote };

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
