using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;

namespace m4dModels
{    public class Song : SongBase
    {

        #region Properties
        public string Album { get; set; }
        public int TitleHash { get; set; }

        // These are helper properties (they don't map to database columns

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


            if (sd.HasAlbums)
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
            return Serialize(null);
        }

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
                return SongProperty.Serialize(SongProperties, actions);
            }
        }

        public void Load(string s, IUserMap users)
        {
            SongProperty.Load(SongId, s, SongProperties);
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

    }
}