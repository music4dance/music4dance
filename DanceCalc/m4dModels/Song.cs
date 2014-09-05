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

        public string AlbumName
        {
            get 
            {
                return new AlbumTrack(Album).Album;
            }
        }

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
                AddUser(user,users);
                factories.CreateSongProperty(this, Song.UserField, user.UserName, log);
            }

            Created = time;
            Modified = time;
            factories.CreateSongProperty(this, Song.TimeField, time.ToString(), log);

            Debug.Assert(!string.IsNullOrWhiteSpace(sd.Title));
            foreach (PropertyInfo pi in SongBase.ScalarProperties)
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

            // Handle Tags
            CreateTags(sd.Tags, factories);

            // Handle Albums
            CreateAlbums(sd.Albums, factories);

            Purchase = sd.GetPurchaseTags();
            TitleHash = Song.CreateTitleHash(Title);
        }

        public bool Edit(ApplicationUser user, SongDetails edit, List<string> addDances, List<string> remDances, string editTags, IFactories factories, IUserMap users)
        {
            bool modified = false;

            CreateEditProperties(user, EditCommand, factories, users);

            foreach (string field in SongBase.ScalarFields)
            {
                modified |= UpdateProperty(edit, field, factories);
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
                    modified |= album.ModifyInfo(factories, this, old, CurrentLog);
                    oldAlbums.Remove(old);
                }
                else
                {
                    // We're in new territory only do something if the name field is non-empty
                    if (!string.IsNullOrWhiteSpace(album.Name))
                    {
                        album.CreateProperties(factories, this, CurrentLog);
                        modified = true;
                    }
                }
            }

            // Handle deleted albums
            foreach (AlbumDetails album in oldAlbums)
            {
                modified = true;
                album.Remove(factories, this, CurrentLog);
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
                factories.CreateSongProperty(this, AlbumOrder, t, CurrentLog);
            }

            modified |= EditDanceRatings(addDances, DanceRatingIncrement, remDances, DanceRatingDecrement, factories);

            modified |= EditTags(editTags, factories);

            modified |= UpdatePurchaseInfo(edit);

            return modified;

        }

        // This is an additive merge - only add new things if they don't conflict with the old
        public bool AdditiveMerge(ApplicationUser user, SongDetails edit, List<string> addDances, IFactories factories, IUserMap users)
        {
            bool modified = false;

            CreateEditProperties(user, EditCommand, factories, users);

            foreach (string field in SongBase.ScalarFields)
            {
                modified |= AddProperty(edit, field, factories);
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
                    modified |= album.UpdateInfo(factories, this, old, CurrentLog);
                }
                else
                {
                    // We're in new territory only do something if the name field is non-empty
                    if (!string.IsNullOrWhiteSpace(album.Name))
                    {
                        album.CreateProperties(factories, this, CurrentLog);
                        modified = true;
                    }
                }
            }

            modified |= EditDanceRatings(addDances, DanceRatingIncrement, null, 0, factories);

            modified |= UpdatePurchaseInfo(edit);

            return modified;
        }

        public void MergeDetails(IEnumerable<Song> songs, IFactories factories, IUserMap users)
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
                    if (AddUser(us.ApplicationUser, users))
                    {
                        factories.CreateSongProperty(this, Song.UserField, us.ApplicationUser.UserName, this.CurrentLog);
                    }
                }
            }

            // Dump the weight table
            foreach (KeyValuePair<string, int> dance in weights)
            {
                DanceRating dr = factories.CreateDanceRating(this, dance.Key, dance.Value);

                string value = new DanceRatingDelta { DanceId = dance.Key, Delta = dance.Value }.ToString();

                factories.CreateSongProperty(this, Song.DanceRatingField, value, this.CurrentLog);
            }

        }
        private bool UpdateProperty(SongDetails edit, string name, IFactories factories)
        {
            // TODO: This can be optimized
            bool modified = false;

            object eP = edit.GetType().GetProperty(name).GetValue(edit);
            object oP = GetType().GetProperty(name).GetValue(this);

            if (!object.Equals(eP, oP))
            {
                modified = true;

                GetType().GetProperty(name).SetValue(this, eP);

                factories.CreateSongProperty(this, name, eP, CurrentLog);
            }

            return modified;
        }

        // Only update if the old song didn't have this property
        private bool AddProperty(SongDetails edit, string name, IFactories factories)
        {
            bool modified = false;

            object eP = edit.GetType().GetProperty(name).GetValue(edit);
            object oP = GetType().GetProperty(name).GetValue(this);

            if (oP != null)
            {
                modified = true;
                GetType().GetProperty(name).SetValue(this, eP);
                factories.CreateSongProperty(this, name, eP, CurrentLog);
            }

            return modified;
        }


        public void CreateEditProperties(ApplicationUser user, string command, IFactories factories, IUserMap users)
        {
            // Add the command into the property log
            factories.CreateSongProperty(this, Song.EditCommand, string.Empty, null);

            // Handle User association
            if (user != null)
            {
                AddUser(user, users);
                factories.CreateSongProperty(this, Song.UserField, user.UserName, null);
            }

            // Handle Timestamps
            DateTime time = DateTime.Now;
            Modified = time;
            factories.CreateSongProperty(this, Song.TimeField, time.ToString(), null);
        }

        private bool UpdatePurchaseInfo(SongDetails edit)
        {
            bool ret = false;
            string pi = edit.GetPurchaseTags();
            if (!string.Equals(Purchase, pi))
            {
                Purchase = pi;
                ret = true;
            }
            return ret;
        }

        public bool AddUser(ApplicationUser user, IUserMap users)
        {
            ModifiedRecord us = users.CreateMapping(this.SongId,user.Id);
            return AddModifiedBy(us, users);
        }

        public bool AddUser(string name, IUserMap users)
        {
            ApplicationUser u = users.FindUser(name);
            return AddUser(u, users);
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
                            Album = albums[0].AlbumTrack;
                        }

                        ad.CreateProperties(factories, this, this.CurrentLog);
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
        private void CreateDanceRatings(IEnumerable<DanceRating> ratings, IFactories factories)
        {
            if (ratings == null)
            {
                return;
            }

            foreach (DanceRating dr in ratings)
            {
                factories.CreateDanceRating(this, dr.DanceId, dr.Weight);
                factories.CreateSongProperty(
                    this,
                    Song.DanceRatingField,
                    new DanceRatingDelta { DanceId = dr.DanceId, Delta = dr.Weight }.ToString(),
                    CurrentLog
                );
            }
        }

        public bool EditDanceRatings(IList<string> add_, int addWeight, List<string> remove_, int remWeight, IFactories factories)
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

                    factories.CreateSongProperty(
                        this, Song.DanceRatingField,
                        new DanceRatingDelta { DanceId = dr.DanceId, Delta = delta }.ToString(), log);

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
                    DanceRating dr = factories.CreateDanceRating(this, ndr, DanceRatingInitial);

                    if (dr != null)
                    {
                        factories.CreateSongProperty(
                            this,
                            Song.DanceRatingField,
                            new DanceRatingDelta { DanceId = ndr, Delta = DanceRatingInitial }.ToString(),
                            log
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

        public void AddTag(Tag tag)
        {
            Tag other = null;

            if (Tags == null)
            {
                Tags = new List<Tag>();
            }
            else
            {
                other = FindTag(tag.Value);
            }

            if (other != null)
            {
                other.Count += tag.Count;
            }
            else
            {
                Tags.Add(tag);
            }
        }

        private void CreateTags(IEnumerable<Tag> tags, IFactories factories)
        {
            if (tags == null)
            {
                return;
            }

            foreach (Tag tag in tags)
            {
                factories.CreateTag(this, tag.Value, tag.Count);
                factories.CreateSongProperty(
                    this,
                    TagField,
                    tag.Value,
                    CurrentLog
                );
            }

            TagSummary = string.Join("|", tags.Select(t => t.Value));
        }

        // TODO: Formalize tag passing (and clean up the UI)
        //  for right now I'm going to kludge this up saying
        //  tags are | separated and prefixed by '-' if they
        //  are removed.
        
        // TODONEXT: Get dances -> tags, then genres-> tags, then remove genres
        //  Get user based tag editing limping along
        //  Choose a control to get user based tagging nicer
        //  Once we have all that working, figure out how to hook up dance
        //  choices and tagging...
        private bool EditTags(string editTags, IFactories factories)
        {
            if (string.IsNullOrWhiteSpace(editTags))
            {
                return false;
            }

            bool ret = false;

            string[] values = editTags.Split(new char[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string value in values)
            {
                string v = value.Trim();
                string c = null;
                int bias = 1;
                if (v.Length > 0 && v[0] == '-')
                {
                    bias = -1;
                    v = v.Substring(1);
                }

                if (v.Contains('='))
                {
                    string[] cells = v.Split(new char[] { '=' });
                    if (cells.Length == 2)
                    {
                        c = cells[0];
                        v = cells[1];
                    }
                    else 
                    {
                        Trace.WriteLine(string.Format("Bad Value for Tag: {0}",v));
                    }
                }
                Tag other = FindTag(v);
                if (other != null)
                {
                    other.Count += bias;
                    if (other.Count <= 0)
                    {
                        Tags.Remove(other);
                    }
                }
                else
                {
                    other = factories.CreateTag(this,v,bias);
                    Trace.WriteLineIf(bias == -1, string.Format("Bad Bias: {0}", this.ToString()));
                }

                if (string.IsNullOrWhiteSpace(c) && other.Type != null)
                {
                    other.Type.AddCategory(c);
                }
                factories.CreateSongProperty(
                    this,
                    TagField,
                    string.Format("{0}{1}",bias==1?"":"-",v),
                    CurrentLog
                );

                ret = true;
            }

            if (ret)
            {
                TagSummary = string.Join("|", Tags.Select(t => t.Value));
            }

            return ret;
        }

        public void Delete()
        {
            foreach (PropertyInfo pi in SongBase.ScalarProperties)
            {
                pi.SetValue(this, null);
            }

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
        }
        public void Restore(SongDetails sd,IUserMap users,IFactories factories)
        {
            RestoreScalar(sd);

            if (SongProperties == null)
            {
                SongProperties = new List<SongProperty>();
            }
            Debug.Assert(SongProperties.Count == 0);
            foreach (SongProperty prop in sd.SongProperties)
            {
                SongProperties.Add(new SongProperty() { Song=this, SongId=this.SongId, Name=prop.Name, Value=prop.Value });
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

            if (Tags == null)
            {
                Tags = new List<Tag>();
            }
            Debug.Assert(Tags.Count == 0);
            foreach (Tag tag in sd.Tags)
            {
                factories.CreateTag(this, tag.Value, tag.Count);
            }
            TagSummary = sd.TagSummary;

            if (ModifiedBy == null)
            {
                ModifiedBy = new List<ModifiedRecord>();
            }
            Debug.Assert(ModifiedBy.Count == 0);
            foreach (ModifiedRecord user in sd.ModifiedBy)
            {
                AddUser(user.UserName, users);
            }
        }

        private bool AddModifiedBy(ModifiedRecord mr, IUserMap map)
        {
            if (ModifiedBy == null)
            {
                ModifiedBy = new List<ModifiedRecord>();
            }

            mr.Song = this;
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
                //Trace.WriteLine(string.Format("{0} Duplicate User Rating {1}", Title, mr.ApplicationUserId));
                return false;
            }
        }

        public void UpdateUsers(IUserMap map)
        {
            HashSet<string> users = new HashSet<string>();

            foreach (ModifiedRecord us in ModifiedBy)
            {
                us.Song = this;
                us.SongId = this.SongId;
                if (us.ApplicationUser == null && us.UserName != null)
                {
                    us.ApplicationUser = map.FindUser(us.UserName);
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

        public void Load(string s, IUserMap users, IFactories factories)
        {
            SongDetails sd = new SongDetails(s);

            Restore(sd, users, factories);
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