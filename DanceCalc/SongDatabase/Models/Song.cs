using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics;
using System.Linq;
using System.Threading;


namespace SongDatabase.Models
{    public class Song : DbObject
    {
        public int SongId { get; set; }
        public decimal? Tempo { get; set; }
        public string Title { get; set; }
        public string Artist { get; set; }
        public string Album { get; set; }
        public string Publisher { get; set; }
        public string Genre { get; set; }
        public int? Track { get; set; }
        public int? Length { get; set; }
        public DateTime Created { get; set; }
        public DateTime Modified { get; set; }
        public int TitleHash { get; set; }
        // Semi-colon separated purchase info of the form XX=YYYYYY (XX is service/type and YYYYY is id)
        public string Purchase { get; set; }
        public virtual ICollection<DanceRating> DanceRatings { get; set; }
        public virtual ICollection<UserProfile> ModifiedBy { get; set; }
        public virtual ICollection<SongProperty> SongProperties { get; set; }

        public string Signature
        {
            get
            {
                // This is not a fully unambiguous signature, should we add in a checksum with some or all of the
                //  other fields in the song?
                return BuildSignature(Artist, Album, Title);
            }
        }

        //  Two song are equivalent if Titles are equal, artists are similar or empty and all other fields are equal
        public bool Equivalent(Song song)
        {
            // No-similar titles != equivalent
            if (!string.Equals(Title,song.Title,StringComparison.InvariantCultureIgnoreCase))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(Artist) && !string.IsNullOrWhiteSpace(song.Artist) && 
                (DanceMusicContext.CreateTitleHash(Artist) != DanceMusicContext.CreateTitleHash(song.Artist)))
            {
                return false;
            }

            return EqString(Album,song.Album) &&
                EqString(Publisher, song.Publisher) &&
                EqString(Genre, song.Genre) &&
                EqNum(Tempo, song.Tempo) &&
                EqNum(Length, song.Length) &&
                EqNum(Track, song.Track);
        }

        private static bool EqString(string s1, string s2)
        {
            return string.IsNullOrWhiteSpace(s1) || string.IsNullOrWhiteSpace(s2) ||
                string.Equals(s1, s2, StringComparison.InvariantCultureIgnoreCase);
        }
        private static bool EqNum<T>(T? t1, T? t2) where T : struct
        {
            return !t1.HasValue || !t2.HasValue || t1.Value.Equals(t2.Value);
        }

        static public string SignatureFromProperties(IOrderedQueryable<SongProperty> properties)
        {
            // Again, this assumes properties are in reverse ID order...

            SongProperty artistP = properties.FirstOrDefault(p => p.Name == DanceMusicContext.ArtistField);
            SongProperty albumP = properties.FirstOrDefault(p => p.Name == DanceMusicContext.AlbumField);
            SongProperty titleP = properties.FirstOrDefault(p => p.Name == DanceMusicContext.TitleField);

            return BuildSignature(artistP != null ? artistP.Value : string.Empty, albumP != null ? albumP.Value : string.Empty, titleP != null ? titleP.Value : string.Empty);
        }

        private static string BuildSignature(string artist, string album, string title)
        {
            artist = (artist == null) ? string.Empty : artist;
            album = (album == null) ? string.Empty : album;
            title = (title == null) ? string.Empty : title;

            string ret = string.Format("{0}\t{1}\t{2}", artist, album, title);

            if (string.IsNullOrWhiteSpace(ret))
                return null;
            else
                return ret;
        }

        public override void Dump()
        {
            base.Dump();

            string output = string.Format("Id={0},Title={1},Album={2},Artist={3}",SongId,Title,Album,Artist);
            Debug.WriteLine(output);
            if (ModifiedBy != null)
            {
                foreach (UserProfile user in ModifiedBy)
                {
                    Debug.Write("\t");
                    user.Dump();
                }
            }
        }

        public static Song GetNullSong()
        {
            return new Song();
        }
    }
}