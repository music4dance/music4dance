using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;

namespace m4dModels
{    public class Song : SongBase
    {

        #region Properties
        public int TitleHash { get; set; }

        // These are helper properties (they don't map to database columns)

        public SongLog CurrentLog { get; set; }

        #endregion

        #region Actions

        public void Create(SongDetails sd, ApplicationUser user, string command, string value, IFactories factories, IUserMap users)
        {
            DateTime time = DateTime.Now;

            SongLog log = CurrentLog;
            if (log != null)
            {
                log.Initialize(user, this, command);
            }

            factories.CreateSongProperty(this, command, value, log);

            // Handle User association
            if (user != null)
            {
                ModifiedRecord us = users.CreateMapping(SongId, user.Id); 
                us.Song = this;
                us.ApplicationUser = user;
                if (ModifiedBy == null)
                {
                    ModifiedBy = new List<ModifiedRecord>();
                }
                ModifiedBy.Add(us);
                factories.CreateSongProperty(this, Song.UserField, user.UserName, log);
            }

            Created = time;
            Modified = time;
            factories.CreateSongProperty(this, Song.TimeField, time.ToString(), log);

            Debug.Assert(!string.IsNullOrWhiteSpace(sd.Title));
            foreach (PropertyInfo pi in SongBase.ScalerProperties)
            {
                object prop = pi.GetValue(sd);
                if (prop != null)
                {
                    pi.SetValue(this, prop);
                    factories.CreateSongProperty(this, pi.Name, prop, log);
                }
            }

            // Handle Dance Ratings
            CreateDanceRatings(sd.DanceRatings, factories);

            // Handle Albums
            CreateAlbums(sd.Albums, factories);

            Purchase = sd.GetPurchaseTags();
            TitleHash = Song.CreateTitleHash(Title);
        }

        private void CreateAlbums(IList<AlbumDetails> albums, IFactories factories)
        {
            if (albums != null)
            {
                albums = AlbumDetails.MergeAlbums(albums);

                for (int ia = 0; ia < albums.Count; ia++)
                {
                    AlbumDetails ad = albums[ia];
                    if (!string.IsNullOrWhiteSpace(ad.Name))
                    {
                        if (ia == 0)
                        {
                            Album = albums[0].Name;
                        }

                        ad.CreateProperties(factories, this, this.CurrentLog);
                    }
                }
            }
        }

        private void CreateDanceRatings(IEnumerable<DanceRating> ratings, IFactories factories)
        {
            if (ratings == null)
            {
                return;
            }

            foreach (DanceRating dr in ratings)
            {
                // TODO: Should CreateDanceRating create the property as well?
                factories.CreateDanceRating(this, dr.DanceId, dr.Weight);
                factories.CreateSongProperty(
                    this,
                    Song.DanceRatingField,
                    new DanceRatingDelta { DanceId = dr.DanceId, Delta = dr.Weight }.ToString(),
                    CurrentLog
                );
            }
        }
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

            if (DanceRatings == null)
            {
                DanceRatings = new List<DanceRating>();
            }
            Debug.Assert(DanceRatings.Count == 0);
            foreach (DanceRating dr in sd.DanceRatings)
            {
                AddDanceRating(dr);
            }

            if (ModifiedBy == null)
            {
                ModifiedBy = new List<ModifiedRecord>();
            }
            Debug.Assert(ModifiedBy.Count == 0);
            foreach (ModifiedRecord user in sd.ModifiedBy)
            {
                AddModifiedBy(user);
            }
        }

        public void AddDanceRating(DanceRating dr)
        {
            dr.Song = this;
            dr.SongId = SongId;
            if (dr.DanceId == null)
            {
                dr.DanceId = dr.Dance.Id;
            }

            DanceRating other = null;
            
            if (DanceRatings == null)
            {
                DanceRatings = new List<DanceRating>();
            }
            else 
            {
                other = DanceRatings.FirstOrDefault(r => r.DanceId == dr.DanceId);
            }

            if (other == null)
            {
                DanceRatings.Add(dr);
            }
            else
            {
                Trace.WriteLine(string.Format("{0} Duplicate Dance Rating {1}", Title, dr.DanceId));
            }
        }

        public void AddModifiedBy(ModifiedRecord mr)
        {
            mr.Song = this;
            mr.SongId = SongId;

            ModifiedRecord other = ModifiedBy.FirstOrDefault(r => r.ApplicationUserId == mr.ApplicationUserId);
            if (other == null)
            {
                ModifiedBy.Add(mr);
            }
            else
            {
                Trace.WriteLine(string.Format("{0} Duplicate User Rating {1}", Title, mr.ApplicationUserId));
            }
        }

        public void UpdateUsers(IUserMap map)
        {
            HashSet<string> users = new HashSet<string>();

            foreach (ModifiedRecord us in ModifiedBy)
            {
                us.Song = this;
                us.SongId = this.SongId;
                if (us.ApplicationUser == null && us.ApplicationUser != null)
                {
                    us.ApplicationUser = map.FindUser(us.ApplicationUserId);
                }
                if (us.ApplicationUser != null && us.ApplicationUserId == null)
                {
                    us.ApplicationUserId = us.ApplicationUser.Id;
                }

                // TODO: We should figure out how to just enable this in verbose mode (haven't pushed down the trace flags
                //  into this module yet.
                if (users.Contains(us.ApplicationUserId))
                {
                    Trace.WriteLine(string.Format("Duplicate Mapping: Song = {0} User = {1}", SongId, us.ApplicationUserId));
                }
                else
                {
                    users.Add(us.ApplicationUserId);
                }
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

        public void Load(string s, IUserMap users)
        {
            SongDetails sd = new SongDetails(s);

            Restore(sd);
            UpdateUsers(users);
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