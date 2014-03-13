using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics;
using System.Linq;
using System.Threading;

using m4d.ViewModels;

namespace m4d.Models
{    public class Song : DbObject
    {
        public int SongId { get; set; }
        public decimal? Tempo { get; set; }
        public string Title { get; set; }
        public string Artist { get; set; }
        public string Album { get; set; }
        public string Genre { get; set; }
        public int? Length { get; set; }
        public DateTime Created { get; set; }
        public DateTime Modified { get; set; }
        public int TitleHash { get; set; }
        public virtual ICollection<DanceRating> DanceRatings { get; set; }
        public virtual ICollection<ApplicationUser> ModifiedBy { get; set; }
        public virtual ICollection<SongProperty> SongProperties { get; set; }

        public SongLog CreateEntry { get; set; }

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

            List<ApplicationUser> us = ModifiedBy.ToList();
            foreach (ApplicationUser u in us)
            {
                ModifiedBy.Remove(u);
            }
        }

        internal void RestoreScalar(DanceMusicContext danceMusicContext, SongDetails sd)
        {
            Tempo = sd.Tempo;
            Title = sd.Title;
            Artist = sd.Artist;
            Genre = sd.Genre;
            Length = sd.Length;
            TitleHash = DanceMusicContext.CreateTitleHash(Title);


            if (sd.Albums != null && sd.Albums.Count > 0)
            {
                Album = sd.Albums[0].Name;
            }
        }
        internal void Restore(DanceMusicContext danceMusicContext, SongDetails sd)
        {
            RestoreScalar(danceMusicContext, sd);

            foreach (DanceRating dr in sd.DanceRatings)
            {
                DanceRatings.Add(dr);
            }

            foreach (ApplicationUser user in sd.ModifiedBy)
            {
                ModifiedBy.Add(user);
            }
        }

        public string Signature
        {
            get
            {
                // This is not a fully unambiguous signature, should we add in a checksum with some or all of the
                //  other fields in the song?
                return BuildSignature(Artist, Title);
            }
        }

        //  Two song are equivalent if Titles are equal, artists are similar or empty and all other fields are equal
        public bool Equivalent(Song song)
        {
            // No-similar titles != equivalent
            if (DanceMusicContext.CreateTitleHash(Title) != DanceMusicContext.CreateTitleHash(song.Title))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(Artist) && !string.IsNullOrWhiteSpace(song.Artist) && 
                (DanceMusicContext.CreateTitleHash(Artist) != DanceMusicContext.CreateTitleHash(song.Artist)))
            {
                return false;
            }

            return EqString(Album,song.Album) &&
                EqString(Genre, song.Genre) &&
                EqNum(Tempo, song.Tempo) &&
                EqNum(Length, song.Length);
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

        //static public string SignatureFromProperties(IOrderedQueryable<SongProperty> properties)
        //{
        //    // Again, this assumes properties are in reverse ID order...

        //    SongProperty artistP = properties.FirstOrDefault(p => p.Name == DanceMusicContext.ArtistField);
        //    SongProperty albumP = properties.FirstOrDefault(p => p.Name == DanceMusicContext.AlbumField);
        //    SongProperty titleP = properties.FirstOrDefault(p => p.Name == DanceMusicContext.TitleField);

        //    return BuildSignature(artistP != null ? artistP.Value : string.Empty, albumP != null ? albumP.Value : string.Empty, titleP != null ? titleP.Value : string.Empty);
        //}

        private static string BuildSignature(string artist, string title)
        {
            artist = (artist == null) ? string.Empty : artist;
            title = (title == null) ? string.Empty : title;

            string ret = string.Format("{0} {1}", DanceMusicContext.CreateNormalForm(artist), DanceMusicContext.CreateNormalForm(title));

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
            //if (ModifiedBy != null)
            //{
            //    foreach (ApplicationUser user in ModifiedBy)
            //    {
            //        Debug.Write("\t");
            //        user.Dump();
            //    }
            //}
        }

        public bool IsNull
        {
            get { return string.IsNullOrWhiteSpace(Title); }
        }

        public static Song GetNullSong()
        {
            return new Song();
        }
    }
}