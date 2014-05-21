using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;

namespace m4dModels
{    public class Song : DbObject
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
        public const string AlbumList = "AlbumList";
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
        public decimal? Tempo { get; set; }
        public string Title { get; set; }
        public string Artist { get; set; }
        public string Album { get; set; }
        public string Genre { get; set; }
        public int? Length { get; set; }
        public string Purchase { get; set; }
        public DateTime Created { get; set; }
        public DateTime Modified { get; set; }
        public int TitleHash { get; set; }
        public virtual ICollection<DanceRating> DanceRatings { get; set; }
        public virtual ICollection<ModifiedRecord> ModifiedBy { get; set; }
        public virtual ICollection<SongProperty> SongProperties { get; set; }

        // These are helper properties (they don't map to database columns
        public string Signature
        {
            get
            {
                // This is not a fully unambiguous signature, should we add in a checksum with some or all of the
                //  other fields in the song?
                return BuildSignature(Artist, Title);
            }
        }

        public bool IsNull
        {
            get { return string.IsNullOrWhiteSpace(Title); }
        }
        public SongLog CreateEntry { get; set; }
        #endregion

        #region Comparison
        //  Two song are equivalent if Titles are equal, artists are similar or empty and all other fields are equal
        public bool Equivalent(Song song)
        {
            // No-similar titles != equivalent
            if (Song.CreateTitleHash(Title) != Song.CreateTitleHash(song.Title))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(Artist) && !string.IsNullOrWhiteSpace(song.Artist) &&
                (Song.CreateTitleHash(Artist) != Song.CreateTitleHash(song.Artist)))
            {
                return false;
            }

            return EqString(Album, song.Album) &&
                EqString(Genre, song.Genre) &&
                EqNum(Tempo, song.Tempo) &&
                EqNum(Length, song.Length);
        }


        // Same as equivalent (above) except that album, Tempo and Length aren't compared.
        public bool WeakEquivalent(Song song)
        {
            // No-similar titles != equivalent
            if (Song.CreateTitleHash(Title) != Song.CreateTitleHash(song.Title))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(Artist) && !string.IsNullOrWhiteSpace(song.Artist) &&
                (Song.CreateTitleHash(Artist) != Song.CreateTitleHash(song.Artist)))
            {
                return false;
            }

            return EqNum(Tempo, song.Tempo) && EqNum(Length, song.Length);
        }
        
        #endregion

        #region Actions
        public void Delete()
        {
            Tempo = null;
            Title = null;
            Artist = null;
            Album = null;
            Genre = null;
            Length = null;
            TitleHash = 0;

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

        public void RestoreScalar(SongDetails sd)
        {
            Tempo = sd.Tempo;
            Title = sd.Title;
            Artist = sd.Artist;
            Genre = sd.Genre;
            Length = sd.Length;
            TitleHash = Song.CreateTitleHash(Title);


            if (sd.Albums != null && sd.Albums.Count > 0)
            {
                Album = sd.Albums[0].Name;
            }

            Purchase = sd.GetPurchaseTags();
        }
        public void Restore(SongDetails sd)
        {
            RestoreScalar(sd);

            Debug.Assert(DanceRatings.Count == 0);
            foreach (DanceRating dr in sd.DanceRatings)
            {
                DanceRatings.Add(dr);
            }

            Debug.Assert(ModifiedBy.Count == 0);
            foreach (ModifiedRecord user in sd.ModifiedBy)
            {
                ModifiedBy.Add(user);
            }
        }
        
        public bool UpdateTitleHash()
        {
            bool ret = false;
            int hash = CreateTitleHash(Title);
            if (hash != TitleHash)
            {
                TitleHash = hash;
                ret = true;
            }
            return ret;
        }
        #endregion

        #region Serialization
        public override string ToString()
        {
            if (string.IsNullOrWhiteSpace(Title))
            {
                return null;
            }
            else
            {
                StringBuilder sb = new StringBuilder();

                string sep = string.Empty;
                foreach (SongProperty sp in SongProperties)
                {
                    if (!sp.IsAction)
                    {
                        string p = sp.ToString();

                        sb.AppendFormat("{0}{1}", sep, p);

                        sep = "\t";
                    }
                }

                return sb.ToString();
            }
        }

        public void Load(string s, IUserMap users)
        {

            string[] cells = s.Split(new char[] { '\t' });
            List<SongProperty> properties = new List<SongProperty>(cells.Length);

            SongProperties.Add(new SongProperty(SongId, Song.CreateCommand, null));

            foreach (string cell in cells)
            {
                string[] values = cell.Split(new char[] { '=' });

                if (values.Length == 2)
                {
                    SongProperties.Add(new SongProperty(SongId, values[0], values[1]));
                }
                else
                {
                    Trace.WriteLine("Bad SongProperty: {0}", cell);
                }
            }
            SongDetails sd = new SongDetails(SongId, SongProperties, users);

            Restore(sd);
        }



        public override void Dump()
        {
            base.Dump();

            string output = string.Format("Id={0},Title={1},Album={2},Artist={3}", SongId, Title, Album, Artist);
            Trace.WriteLine(output);
            //if (ModifiedBy != null)
            //{
            //    foreach (ApplicationUser user in ModifiedBy)
            //    {
            //        Debug.Write("\t");
            //        user.Dump();
            //    }
            //}
        }
        
        #endregion

        #region Static Utility Functions
        private static bool EqString(string s1, string s2)
        {
            return string.IsNullOrWhiteSpace(s1) || string.IsNullOrWhiteSpace(s2) ||
                string.Equals(s1, s2, StringComparison.InvariantCultureIgnoreCase);
        }
        private static bool EqNum<T>(T? t1, T? t2) where T : struct
        {
            return !t1.HasValue || !t2.HasValue || t1.Value.Equals(t2.Value);
        }

        private static string BuildSignature(string artist, string title)
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

        private static string MungeString(string s, bool normalize)
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
                    else if (!normalize && c == '\'' && char.IsLetter(lastC) && norm.Length > i+1 && char.IsLetter(norm[i+1]))
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

        static string[] s_ignore =
        {
            "A",
            "AND",
            "OR",
            "THE",
            "THIS"
        };

        #endregion

    //#region IEqualityComparer
    //    override public bool Equals(object x, object y)
    //    {
    //        Song songX = x as Song;
    //        Song songY = y as Song;

    //        if (songX == null || songY == null)
    //        {
    //            return false;
    //        }

    //        return songX.SongId == songY.SongId;
    //    }

    //    public int GetHashCode(object obj)
    //    {
    //        return SongId.GetHashCode();
    //    }
    //    #endregion
    }
}