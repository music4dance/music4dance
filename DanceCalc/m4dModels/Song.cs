using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;

namespace m4dModels
{
    public class Song : SongBase
    {

        #region Properties
        public int TitleHash { get; set; }

        // These are helper properties (they don't map to database columns)

        public override SongLog CurrentLog { get; set; }

        #endregion

        #region Actions

        public void Create(SongDetails sd, ApplicationUser user, string command, string value, DanceMusicService dms)
        {
            DateTime time = DateTime.Now;

            SongLog log = CurrentLog;
            if (log != null)
            {
                log.Initialize(user, this, command);
            }

            CreateProperty(command, value, null, log, dms);

            // Handle User association
            if (user != null)
            {
                AddUser(user,dms);
                CreateProperty(Song.UserField, user.UserName, null, log, dms);
            }

            Created = time;
            Modified = time;
            CreateProperty(Song.TimeField, time.ToString(), null, log, dms);

            Debug.Assert(!string.IsNullOrWhiteSpace(sd.Title));
            foreach (PropertyInfo pi in SongBase.ScalarProperties)
            {
                object prop = pi.GetValue(sd);
                if (prop != null)
                {
                    pi.SetValue(this, prop);
                    CreateProperty(pi.Name, prop, null, log, dms);
                }
            }

            // Handle Dance Ratings
            CreateDanceRatings(sd.DanceRatings, dms);

            // Handle Tags
            TagsFromProperties(user, sd, dms, sd);

            // Handle Albums
            CreateAlbums(sd.Albums, dms);

            Purchase = sd.GetPurchaseTags();
            TitleHash = Song.CreateTitleHash(Title);
        }

        private bool EditCore(ApplicationUser user, SongDetails edit, DanceMusicService dms)
        {
            bool modified = false;

            CreateEditProperties(user, EditCommand, dms);

            foreach (string field in SongBase.ScalarFields)
            {
                modified |= UpdateProperty(edit, field, dms);
            }

            List<AlbumDetails> oldAlbums = SongDetails.BuildAlbumInfo(this);

            bool foundFirst = false;

            List<int> promotions = new List<int>();

            Album = null;

            for (int aidx = 0; aidx < edit.Albums.Count; aidx++)
            {
                AlbumDetails album = edit.Albums[aidx];
                AlbumDetails old = oldAlbums.FirstOrDefault(a => a.Index == album.Index);

                if (!foundFirst && !string.IsNullOrEmpty(album.Name))
                {
                    foundFirst = true;
                    Album = album.AlbumTrack;
                }

                if (old != null)
                {
                    // We're in existing album territory
                    modified |= album.ModifyInfo(dms, this, old, CurrentLog);
                    oldAlbums.Remove(old);
                }
                else
                {
                    // We're in new territory only do something if the name field is non-empty
                    if (!string.IsNullOrWhiteSpace(album.Name))
                    {
                        album.CreateProperties(dms, this, CurrentLog);
                        modified = true;
                    }
                }
            }

            // Handle deleted albums
            foreach (AlbumDetails album in oldAlbums)
            {
                modified = true;
                album.Remove(dms, this, CurrentLog);
            }

            // Now check order and insert a re-order record if they aren't line up...
            // TODO: Linq???
            bool needReorder = false;
            List<int> reorder = new List<int>();
            int prev = -1;

            foreach (var album in edit.Albums)
            {
                int t = album.Index;
                if (prev > t)
                {
                    needReorder = true;
                }
                prev = t;
                reorder.Add(t);
            }

            if (needReorder)
            {
                modified = true;
                string t = string.Join(",", reorder.Select(x => x.ToString()));
                CreateProperty(AlbumOrder, t, null, CurrentLog, dms);
            }

            return modified;
        }

        // Edit 'this' based on songdetails + extras
        public bool Edit(ApplicationUser user, SongDetails edit, List<string> addDances, List<string> remDances, string editTags, DanceMusicService dms)
        {
            bool modified = EditCore(user,edit,dms);

            modified |= EditDanceRatings(addDances, DanceRatingIncrement, remDances, DanceRatingDecrement, dms);

            TagList tags = new TagList(editTags).Add(new TagList(TagsFromDances(addDances)));

            //TAGTEST: Basic editting
            modified |= ChangeTags(tags.Summary, user, dms, this);

            modified |= UpdatePurchaseInfo(edit);

            return modified;
        }

        public bool Update(ApplicationUser user, SongDetails update, DanceMusicService dms)
        {
            // Verify that our heads are the same (TODO:move this to debug mode at some point?)
            List<SongProperty> old = SongProperties.Where(p => !p.IsAction).OrderBy(p => p.Id).ToList();
            List<SongProperty> upd = update.Properties.Where(p => !p.IsAction).ToList();
            int c = old.Count;
            for (int i = 0; i < c; i++)
            {
                if (upd.Count < i || !string.Equals(old[i].Name, upd[i].Name))
                {
                    Trace.WriteLine(string.Format("Unexpected Update: {0}", SongId));
                    return false;
                }
            }

            if (c == upd.Count)
            {
                return false;
            }

            List<SongProperty> mrg = new List<SongProperty>(upd.Skip(c));
            LoadProperties(mrg);

            foreach (var prop in mrg)
            {
                SongProperties.Add(prop);
            }

            UpdatePurchaseInfo(update);
            Album = null;
            if (update.Albums != null && update.Albums.Count > 0)
            {
                Album = update.Albums[0].AlbumTrack;
            }

            UpdateUsers(dms);
            //TODO???FixupMappings(dms);

            return true;
        }

        // This is an additive merge - only add new things if they don't conflict with the old
        public bool AdditiveMerge(ApplicationUser user, SongDetails edit, List<string> addDances, DanceMusicService dms)
        {
            bool modified = false;

            CreateEditProperties(user, EditCommand, dms);

            foreach (string field in SongBase.ScalarFields)
            {
                modified |= AddProperty(edit, field, dms);
            }

            List<AlbumDetails> oldAlbums = SongDetails.BuildAlbumInfo(this);

            for (int aidx = 0; aidx < edit.Albums.Count; aidx++)
            {
                AlbumDetails album = edit.Albums[aidx];
                AlbumDetails old = oldAlbums.FirstOrDefault(a => a.Index == album.Index);

                if (string.IsNullOrWhiteSpace(Album) && !string.IsNullOrEmpty(album.Name))
                {
                    Album = album.AlbumTrack;
                }

                if (old != null)
                {
                    // We're in existing album territory
                    modified |= album.UpdateInfo(dms, this, old, CurrentLog);
                }
                else
                {
                    // We're in new territory only do something if the name field is non-empty
                    if (!string.IsNullOrWhiteSpace(album.Name))
                    {
                        album.CreateProperties(dms, this, CurrentLog);
                        modified = true;
                    }
                }
            }

            modified |= EditDanceRatings(addDances, DanceRatingIncrement, null, 0, dms);

            //TAGTEST: Additive merge
            if (addDances != null && addDances.Count > 0)
            {
                string tags = string.Join("|", addDances);
                tags = dms.NormalizeTags(tags, "Dance");
                TagList newTags = AddTags(tags, user, dms, this);
                modified = newTags != null && !string.IsNullOrWhiteSpace(tags);
            }

            modified |= UpdatePurchaseInfo(edit,true);

            return modified;
        }

        public void MergeDetails(IEnumerable<Song> songs, DanceMusicService dms)
        {
            // Add in the to/from properties and create new weight table as well as creating the user associations
            Dictionary<string, int> weights = new Dictionary<string, int>();
            foreach (Song from in songs)
            {
                foreach (DanceRating dr in from.DanceRatings)
                {
                    int weight = 0;
                    if (weights.TryGetValue(dr.DanceId, out weight))
                    {
                        weights[dr.DanceId] = weight + dr.Weight;
                    }
                    else
                    {
                        weights[dr.DanceId] = dr.Weight;
                    }
                }

                foreach (ModifiedRecord us in from.ModifiedBy)
                {
                    if (AddUser(us.ApplicationUser, dms))
                    {
                        CreateProperty(Song.UserField, us.ApplicationUser.UserName, null, this.CurrentLog, dms);
                    }
                }

                // TAGTEST: Try merging two songs with tags.
                ApplicationUser currentUser = null;
                bool userWritten = false;
                foreach (SongProperty prop in from.SongProperties)
                {
                    string bn = prop.BaseName;

                    switch (bn)
                    {
                        case UserField:
                            currentUser = dms.FindUser(prop.Value);
                            userWritten = false;
                            break;
                        case AddedTags:
                        case RemovedTags:
                            if (!userWritten)
                            {
                                CreateEditProperties(currentUser, EditCommand, dms);
                                userWritten = true;
                            }
                            if (bn == AddedTags)
                            {
                                AddTags(prop.Value, currentUser, dms, this);
                            }
                            else
                            {
                                RemoveTags(prop.Value, currentUser, dms, this);
                            }
                            break;
                    }
                }
            }

            // Dump the weight table
            foreach (KeyValuePair<string, int> dance in weights)
            {
                DanceRating dr = dms.CreateDanceRating(this, dance.Key, dance.Value);

                string value = new DanceRatingDelta { DanceId = dance.Key, Delta = dance.Value }.ToString();

                CreateProperty(Song.DanceRatingField, value, null, this.CurrentLog, dms);
            }

        }
        private bool UpdateProperty(SongDetails edit, string name, DanceMusicService dms)
        {
            // TODO: This can be optimized
            bool modified = false;

            object eP = edit.GetType().GetProperty(name).GetValue(edit);
            object oP = GetType().GetProperty(name).GetValue(this);

            if (!object.Equals(eP, oP))
            {
                modified = true;

                GetType().GetProperty(name).SetValue(this, eP);

                CreateProperty(name, eP, oP, CurrentLog, dms);
            }

            return modified;
        }

        // Only update if the old song didn't have this property
        private bool AddProperty(SongDetails edit, string name, DanceMusicService dms)
        {
            bool modified = false;

            object eP = edit.GetType().GetProperty(name).GetValue(edit);
            object oP = GetType().GetProperty(name).GetValue(this);

            if (oP == null || (oP is string && string.IsNullOrWhiteSpace(oP as string)))
            {
                modified = true;
                GetType().GetProperty(name).SetValue(this, eP);
                CreateProperty(name, eP, null, CurrentLog, dms);
            }

            return modified;
        }

        protected override SongProperty CreateProperty(string name, object value, object old, SongLog log, DanceMusicService dms)
        {
            SongProperty prop = null;
            if (dms != null)
            {
                prop = dms.CreateSongProperty(this, name, value, old, log);
            }
            else
            {
                base.CreateProperty(name, value, old, log, dms);
            }

            return prop;
        }

        public void CreateEditProperties(ApplicationUser user, string command, DanceMusicService dms)
        {
            string[] rg = command.Split(new char[] {'='}, StringSplitOptions.RemoveEmptyEntries);
            // Add the command into the property log
            string cmd = SongBase.EditCommand;
            string val = null;

            if (rg.Length > 0)
            {
                cmd = rg[0];
            }
            if (rg.Length > 1)
            {
                cmd = rg[1];
            }

            CreateProperty(rg[0], cmd, val, null, dms);

            // Handle User association
            if (user != null)
            {
                AddUser(user, dms);
                CreateProperty(Song.UserField, user.UserName, null, dms);
            }

            // Handle Timestamps
            DateTime time = DateTime.Now;
            Modified = time;
            CreateProperty(Song.TimeField, time.ToString(), null, dms);
        }

        private bool UpdatePurchaseInfo(SongDetails edit, bool additive = false)
        {
            bool ret = false;
            string pi = null;

            if (additive)
            {
                pi = edit.MergePurchaseTags(Purchase);
            }
            else
            {
                pi = edit.GetPurchaseTags();
            }
            if (!string.Equals(Purchase, pi))
            {
                Purchase = pi;
                ret = true;
            }
            return ret;
        }

        public bool AddUser(ApplicationUser user, DanceMusicService dms)
        {
            ModifiedRecord us = null;
            if (dms != null)
            {
                us = dms.CreateModified(this.SongId, user.Id);
                us.ApplicationUser = user;
            }
            else
            {
                us = new ModifiedRecord { ApplicationUser = user, Song = this, SongId = this.SongId };
            }
            return AddModifiedBy(us);
        }

        public bool AddUser(string name, DanceMusicService dms)
        {
            ApplicationUser u = dms.FindUser(name);
            return AddUser(u, dms);
        }

        private void CreateAlbums(IList<AlbumDetails> albums, DanceMusicService dms)
        {
            if (albums != null)
            {
                albums = AlbumDetails.MergeAlbums(albums, Artist,false);

                for (int ia = 0; ia < albums.Count; ia++)
                {
                    AlbumDetails ad = albums[ia];
                    if (!string.IsNullOrWhiteSpace(ad.Name))
                    {
                        if (ia == 0)
                        {
                            Album = albums[0].AlbumTrack;
                        }

                        ad.CreateProperties(dms, this, this.CurrentLog);
                    }
                }
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
        private void CreateDanceRatings(IEnumerable<DanceRating> ratings, DanceMusicService dms)
        {
            if (ratings == null)
            {
                return;
            }

            foreach (DanceRating dr in ratings)
            {
                dms.CreateDanceRating(this, dr.DanceId, dr.Weight);
                CreateProperty(
                    Song.DanceRatingField,
                    new DanceRatingDelta { DanceId = dr.DanceId, Delta = dr.Weight }.ToString(),
                    CurrentLog,
                    dms
                );
            }
        }

        //  TODO: Ought to be able to refactor both of these into one that calls the other
        public bool EditDanceRatings(IEnumerable<DanceRatingDelta> deltas, DanceMusicService dms)
        {
            SongLog log = CurrentLog;

            foreach (var drd in deltas)
            {
                bool valid = true;
                DanceRating dro = DanceRatings.FirstOrDefault(r => r.DanceId == drd.DanceId);
                if (drd.Delta > 0)
                {
                    if (dro == null)
                    {
                        dms.CreateDanceRating(this, drd.DanceId, drd.Delta);
                    }
                    else
                    {
                        dro.Weight += drd.Delta;
                    }
                }
                else if (drd.Delta < 0)
                {
                    if (dro == null)
                    {
                        valid = false;
                    }
                    else if (dro.Weight + drd.Delta < 0)
                    {
                        DanceRatings.Remove(dro);
                    }
                    else 
                    {
                        dro.Weight += drd.Delta;
                    }
                }
                else
                {
                    valid = false;
                }

                if (valid)
                {
                    dms.CreateSongProperty(this, Song.DanceRatingField, drd.ToString(), log);
                }
            }
            return true;
        }
        public bool EditDanceRatings(IList<string> add_, int addWeight, List<string> remove_, int remWeight, DanceMusicService dms)
        {
            if (add_ == null && remove_ == null)
            {
                return false;
            }

            SongLog log = CurrentLog;

            bool changed = false;

            List<string> add = null;
            if (add_ != null)
                add = new List<string>(add_);

            List<string> remove = null;
            if (remove_ != null)
                remove = new List<string>(remove_);

            List<DanceRating> del = new List<DanceRating>();

            // Cleaner way to get old dance ratings?
            foreach (DanceRating dr in DanceRatings)
            {
                bool added = false;
                int delta = 0;

                // This handles the incremental weights
                if (add != null && add.Contains(dr.DanceId))
                {
                    delta = addWeight;
                    add.Remove(dr.DanceId);
                    added = true;
                }

                // This handles the decremented weights
                if (remove != null && !remove.Contains(dr.DanceId))
                {
                    if (!added)
                    {
                        delta += remWeight;
                    }

                    if (dr.Weight + delta <= 0)
                    {
                        del.Add(dr);
                    }
                }

                if (delta != 0)
                {
                    dr.Weight += delta;

                    CreateProperty(Song.DanceRatingField, new DanceRatingDelta { DanceId = dr.DanceId, Delta = delta }.ToString(), log, dms);

                    changed = true;
                }
            }

            // This handles the deleted weights
            foreach (DanceRating dr in del)
            {
                DanceRatings.Remove(dr);
            }

            // This handles the new ratings
            if (add != null)
            {
                foreach (string ndr in add)
                {
                    DanceRating dr = dms.CreateDanceRating(this, ndr, DanceRatingInitial);

                    if (dr != null)
                    {
                        CreateProperty(
                            Song.DanceRatingField,
                            new DanceRatingDelta { DanceId = ndr, Delta = DanceRatingInitial }.ToString(),
                            log, dms
                        );

                        changed = true;
                    }
                    else
                    {
                        Trace.WriteLine(string.Format("Invalid DanceId={0}", ndr));
                    }

                }
            }

            return changed;
        }

        // TAGTEST: Reload database & log restore?
        private void TagsFromProperties(ApplicationUser user, SongDetails sd, DanceMusicService dms, object data)
        {
            foreach (SongProperty p in sd.SongProperties)
            {
                switch (p.BaseName)
                {
                    case UserField:
                        user = dms.FindUser(p.Value);
                        break;
                    case AddedTags:
                        AddTags(p.Value, user, dms, data);
                        break;
                    case RemovedTags:
                        RemoveTags(p.Value, user, dms, data);
                        break;
                }
            }
        }

        public void Delete(ApplicationUser user, DanceMusicService dms)
        {
            CreateEditProperties(user, DeleteCommand, dms);

            ClearValues();
            TitleHash = 0;

            Modified = DateTime.Now;
        }

        public void RestoreScalar(SongDetails sd)
        {
            if (!sd.SongId.Equals(Guid.Empty))
            {
                SongId = sd.SongId;
            }
            foreach (PropertyInfo pi in SongBase.ScalarProperties)
            {
                object v = pi.GetValue(sd);
                pi.SetValue(this, v);
            }
            TitleHash = Song.CreateTitleHash(Title);

            if (sd.HasAlbums)
            {
                Album = sd.Albums[0].AlbumTrack;
            }

            Purchase = sd.GetPurchaseTags();
            //TagSummary = sd.TagSummary;
        }
        public void Restore(SongDetails sd,DanceMusicService dms)
        {
            RestoreScalar(sd);

            if (SongProperties == null)
            {
                SongProperties = new List<SongProperty>();
            }
            Debug.Assert(SongProperties.Count == 0 || SongProperties.Count == sd.SongProperties.Count);
            if (SongProperties.Count == 0)
            {
                foreach (SongProperty prop in sd.SongProperties)
                {
                    SongProperties.Add(new SongProperty() { Song = this, SongId = this.SongId, Name = prop.Name, Value = prop.Value });
                }
            }

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
                AddUser(user.UserName, dms);
            }

            ApplicationUser currentUser = null;
            ModifiedRecord mr = ModifiedBy.LastOrDefault();
            if (mr != null)
            {
                currentUser = mr.ApplicationUser;
            }
            TagsFromProperties(currentUser, sd, dms, null);
        }

        protected override bool AddModifiedBy(ModifiedRecord mr)
        {
            if (ModifiedBy == null)
            {
                ModifiedBy = new List<ModifiedRecord>();
            }

            mr.Song = this;

            return base.AddModifiedBy(mr);
        }

        public void UpdateUsers(DanceMusicService dms)
        {
            HashSet<string> users = new HashSet<string>();

            foreach (ModifiedRecord us in ModifiedBy)
            {
                us.Song = this;
                us.SongId = this.SongId;
                if (us.ApplicationUser == null && us.UserName != null)
                {
                    us.ApplicationUser = dms.FindUser(us.UserName);
                }
                if (us.ApplicationUser != null && us.ApplicationUserId == null)
                {
                    us.ApplicationUserId = us.ApplicationUser.Id;
                }

                if (users.Contains(us.ApplicationUserId))
                {
                    Trace.WriteLineIf(TraceLevels.General.TraceVerbose,string.Format("Duplicate Mapping: Song = {0} User = {1}", SongId, us.ApplicationUserId));
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
        static string _guidMatch = "d40817d45b68427d86e989fa21773b48";

        public void Load(string s, DanceMusicService dms)
        {
            SongDetails sd = new SongDetails(s);

            if (sd.SongId == new Guid(_guidMatch))
            {
                Trace.WriteLine("THis is the bad one?");
            }

            Restore(sd, dms);
            UpdateUsers(dms);
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