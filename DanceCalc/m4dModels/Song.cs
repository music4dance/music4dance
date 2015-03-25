using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;

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

        public void Create(ApplicationUser user, string command, string value, bool addUser, DanceMusicService dms)
        {
            var time = DateTime.Now;
            
            var log = CurrentLog;
            if (log != null)
            {
                log.Initialize(user, this, command);
            }

            if (!string.IsNullOrWhiteSpace(command))
            {
                CreateProperty(command, value, null, log, dms);
            }

            if (!addUser || user == null) return;

            Created = time;
            Modified = time;

            AddUser(user, dms);
            CreateProperty(UserField, user.UserName, null, log, dms);
            CreateProperty(TimeField, time.ToString(CultureInfo.InvariantCulture), null, log, dms);
        }

        public void Create(SongDetails sd, IEnumerable<UserTag> tags, ApplicationUser user, string command, string value, DanceMusicService dms)
        {
            var log = CurrentLog;
            var addUser = !(sd.ModifiedBy != null && sd.ModifiedList.Count > 0 && AddUser(sd.ModifiedList[0].UserName, dms));

            Create(user, command, value, addUser, dms);

            // Handle User association
            if (!addUser)
            {
                // This is the Modified record created when we computed the addUser condition above
                var mr = ModifiedBy.First();
                mr.Owned = sd.ModifiedList[0].Owned;

                CreateProperty(UserField, mr.UserName, null, log, dms);
                CreateProperty(TimeField, Created.ToString(CultureInfo.InvariantCulture), null, log, dms);
                if (mr.Owned.HasValue)
                    CreateProperty(OwnerHash, mr.Owned, null, log, dms);
            }

            Debug.Assert(!string.IsNullOrWhiteSpace(sd.Title));
            foreach (var pi in ScalarProperties)
            {
                var prop = pi.GetValue(sd);
                if (prop == null) continue;

                pi.SetValue(this, prop);
                CreateProperty(pi.Name, prop, null, log, dms);
            }

            if (tags == null)
            {
                // Handle Tags
                TagsFromProperties(user, sd, dms, sd);

                // Handle Dance Ratings
                CreateDanceRatings(sd.DanceRatings, dms);
            }
            else
            {
                InternalEditTags(user, tags, dms);
            }

            // Handle Albums
            CreateAlbums(sd.Albums, dms);

            Purchase = sd.GetPurchaseTags();
            TitleHash = CreateTitleHash(Title);
        }

        private bool EditCore(ApplicationUser user, SongDetails edit, DanceMusicService dms)
        {
            bool modified = false;

            CreateEditProperties(user, EditCommand, dms);

            foreach (string field in ScalarFields)
            {
                modified |= UpdateProperty(edit, field, dms);
            }

            List<AlbumDetails> oldAlbums = SongDetails.BuildAlbumInfo(this);

            bool foundFirst = false;

            Album = null;

            foreach (AlbumDetails album in edit.Albums)
            {
                var album1 = album;
                AlbumDetails old = oldAlbums.FirstOrDefault(a => a.Index == album1.Index);

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
        public bool Edit(ApplicationUser user, SongDetails edit, IEnumerable<UserTag> tags, DanceMusicService dms)
        {
            var modified = EditCore(user,edit,dms);

            modified |= UpdatePurchaseInfo(edit);
            modified |= UpdateModified(user, edit, dms, true);

            if (tags == null) return modified;

            modified |= InternalEditTags(user, tags, dms);
            if (modified)
            {
                InferDances(user);
            }

            return modified;
        }

        public bool Update(ApplicationUser user, SongDetails update, DanceMusicService dms)
        {
            SetupCollections();
            // Verify that our heads are the same (TODO:move this to debug mode at some point?)
            var old = SongProperties.OrderBy(p => p.Id).ToList(); // Where(p => !p.IsAction).
            var upd = update.Properties.ToList(); // Where(p => !p.IsAction).
            var c = old.Count;
            for (var i = 0; i < c; i++)
            {
                if (upd.Count < i || !string.Equals(old[i].Name, upd[i].Name))
                {
                    Trace.WriteLine(string.Format("Unexpected Update: {0}", SongId));
                    return false;
                }
            }

            // Nothing has changed
            if (c == upd.Count)
            {
                return false;
            }

            var mrg = new List<SongProperty>(upd.Skip(c));

            UpdateProperties(mrg);
            UpdateFromService(dms);

            UpdatePurchaseInfo(update);
            Album = null;
            if (update.Albums != null && update.Albums.Count > 0)
            {
                Album = update.Albums[0].AlbumTrack;
            }

            return true;
        }

        public void UpdateProperties(ICollection<SongProperty> properties, string[] excluded = null)
        {
            LoadProperties(properties);

            foreach (var prop in properties.Where(prop => excluded == null || !excluded.Contains(prop.BaseName)))
            {
                SongProperties.Add(prop);
            }
            
        }


        public void UpdateFromService(DanceMusicService dms)
        {
            UpdateUserTags(dms);

            UpdateDanceTags(dms);

            UpdateUsers(dms);

            TitleHash = CreateTitleHash(Title);            
        }

        
        // This is an additive merge - only add new things if they don't conflict with the old
        public bool AdditiveMerge(ApplicationUser user, SongDetails edit, List<string> addDances, DanceMusicService dms)
        {
            CreateEditProperties(user, EditCommand, dms);

            bool modified = ScalarFields.Aggregate(false, (current, field) => current | AddProperty(edit, field, dms));

            var oldAlbums = SongDetails.BuildAlbumInfo(this);

            foreach (AlbumDetails album in edit.Albums)
            {
                var album1 = album;
                AlbumDetails old = oldAlbums.FirstOrDefault(a => a.Index == album1.Index);

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

            if (addDances != null && addDances.Count > 0)
            {
                var tags = TagsFromDances(addDances);
                var newTags = AddTags(tags, user, dms, this);
                modified = newTags != null && !string.IsNullOrWhiteSpace(tags);

                modified |= EditDanceRatings(addDances, DanceRatingIncrement, null, 0, dms);

                InferDances(user);
            }
            else
            {
                // Handle Tags
                TagsFromProperties(user, edit, dms, edit);

                // Handle Dance Ratings
                CreateDanceRatings(edit.DanceRatings, dms);
            }

            modified |= UpdatePurchaseInfo(edit,true);
            modified |= UpdateModified(user, edit, dms, false);

            return modified;
        }

        public bool EditTags(ApplicationUser user, IEnumerable<UserTag> tags, DanceMusicService dms)
        {
            CreateEditProperties(user, EditCommand, dms);

            return InternalEditTags(user, tags, dms);
        }

        private bool InternalEditTags(ApplicationUser user, IEnumerable<UserTag> tags, DanceMusicService dms)
        {
            var modified = false;
            var hash = new Dictionary<string, TagList>();
            foreach (var tag in tags)
            {
                hash[tag.Id] = tag.Tags;
            }

            // First handle the top-level tags, this will incidently add any new danceratings
            //  implied by those tags
            modified = ChangeTags(hash[""].Summary, user, dms, "Dances");

            // Edit the tags for each of the dance ratings: Note that I'm stripping out blank dance ratings
            //  at the client, so need to make sure that we remove any tags from dance ratings on the server
            //  that aren't passed through in our tag list.

            foreach (var dr in DanceRatings)
            {
                TagList tl;
                if (!hash.TryGetValue(dr.DanceId, out tl))
                    tl = new TagList();
                modified |= dr.ChangeTags(tl.Summary, user, dms, this);
            }

            return modified;            
        }

        public override void RegisterChangedTags(TagList added, TagList removed, ApplicationUser user, DanceMusicService dms, object data)
        {
            base.RegisterChangedTags(added, removed, user, dms, data);

            var test = data as string;
            if (string.Equals("Dances", test, StringComparison.OrdinalIgnoreCase))
            {
                EditDanceRatings(TagsToDanceIds(added), 3, TagsToDanceIds(removed), -1, dms);
            }                
        }

        private bool UpdateModified(ApplicationUser user, SongDetails edit, DanceMusicService dms, bool force)
        {
            var modified = false;
            var mr = ModifiedBy.FirstOrDefault(m => m.UserName == user.UserName);
            if (mr != null)
            {
                var mrN = edit.ModifiedBy.FirstOrDefault(m => m.UserName == user.UserName);
                if (mrN != null && (force || mrN.Owned.HasValue) && (mr.Owned != mrN.Owned))
                {
                    modified = true;
                    mr.Owned = mrN.Owned;
                    CreateProperty(OwnerHash, mr.Owned, CurrentLog, dms);
                }
            }
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
                    int weight;
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
                        CreateProperty(UserField, us.ApplicationUser.UserName, null, CurrentLog, dms);
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
                dms.CreateDanceRating(this, dance.Key, dance.Value);

                var value = new DanceRatingDelta { DanceId = dance.Key, Delta = dance.Value }.ToString();

                CreateProperty(DanceRatingField, value, null, CurrentLog, dms);
            }

        }
        private bool UpdateProperty(SongDetails edit, string name, DanceMusicService dms)
        {
            // TODO: This can be optimized
            bool modified = false;

            object eP = edit.GetType().GetProperty(name).GetValue(edit);
            object oP = GetType().GetProperty(name).GetValue(this);

            if (!Equals(eP, oP))
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
            var eP = edit.GetType().GetProperty(name).GetValue(edit);
            var oP = GetType().GetProperty(name).GetValue(this);

            // Edit property is null or whitespace and Old property isn't null or whitespace
            if (NullIfWhitespace(eP) == null || NullIfWhitespace(oP) != null)
                return false;

            GetType().GetProperty(name).SetValue(this, eP);
            CreateProperty(name, eP, null, CurrentLog, dms);

            return true;
        }

        private static object NullIfWhitespace(object o)
        {
            var s = o as string;
            if (s != null && string.IsNullOrWhiteSpace(s)) o = null;

            return o;
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
            string[] rg = command.Split(new[] {'='}, StringSplitOptions.RemoveEmptyEntries);
            // Add the command into the property log
            string cmd = EditCommand;
            string val = null;

            if (rg.Length > 0)
            {
                cmd = rg[0];
            }
            if (rg.Length > 1)
            {
                val = rg[1];
            }

            CreateProperty(cmd, val, null, dms);

            // Handle User association
            if (user != null)
            {
                AddUser(user, dms);
                CreateProperty(UserField, user.UserName, null, dms);
            }

            // Handle Timestamps
            DateTime time = DateTime.Now;
            Modified = time;
            CreateProperty(TimeField, time.ToString(), null, dms);
        }

        private bool UpdatePurchaseInfo(SongDetails edit, bool additive = false)
        {
            var ret = false;
            var pi = additive ? edit.MergePurchaseTags(Purchase) : edit.GetPurchaseTags();

            if ((Purchase ?? string.Empty) != (pi ?? string.Empty))
            {
                Purchase = pi;
                ret = true;
            }
            return ret;
        }

        public bool AddUser(ApplicationUser user, DanceMusicService dms)
        {
            ModifiedRecord us;
            if (dms != null)
            {
                us = dms.CreateModified(SongId, user.Id);
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

        public void CreateAlbums(IList<AlbumDetails> albums, DanceMusicService dms)
        {
            if (albums == null) return;

            albums = AlbumDetails.MergeAlbums(albums, Artist,false);

            for (var ia = 0; ia < albums.Count; ia++)
            {
                var ad = albums[ia];
                if (string.IsNullOrWhiteSpace(ad.Name)) continue;

                if (ia == 0)
                {
                    Album = albums[0].AlbumTrack;
                }

                ad.CreateProperties(dms, this, CurrentLog);
            }
        }

        // If dms != null, create the properties
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
        public void CreateDanceRatings(IEnumerable<DanceRating> ratings, DanceMusicService dms)
        {
            if (ratings == null)
            {
                return;
            }

            foreach (DanceRating dr in ratings)
            {
                dms.CreateDanceRating(this, dr.DanceId, dr.Weight);
                CreateProperty(
                    DanceRatingField,
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
                    dms.CreateSongProperty(this, DanceRatingField, drd.ToString(), log);
                }
            }
            return true;
        }
        public bool EditDanceRatings(IList<string> add_, int addWeight, IList<string> remove_, int remWeight, DanceMusicService dms)
        {
            if (add_ == null && remove_ == null)
            {
                return false;
            }
            if (DanceRatings == null)
            {
                DanceRatings = new List<DanceRating>();
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
                if (remove != null && remove.Contains(dr.DanceId))
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

                    CreateProperty(DanceRatingField, new DanceRatingDelta { DanceId = dr.DanceId, Delta = delta }.ToString(), log, dms);

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
                            DanceRatingField,
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
            foreach (PropertyInfo pi in ScalarProperties)
            {
                object v = pi.GetValue(sd);
                pi.SetValue(this, v);
            }
            TitleHash = CreateTitleHash(Title);

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
            var users = new HashSet<string>();

            foreach (var us in ModifiedBy)
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

        public void UpdateDanceTags(DanceMusicService dms)
        {
            if (DanceRatings == null) return;
            foreach (var d in DanceRatings)
            {
                d.UpdateUserTags(dms);
            }
        }
        public bool UpdateTitleHash()
        {
            bool ret = false;
            int hash = string.IsNullOrWhiteSpace(Title) ? 0 : CreateTitleHash(Title);
            if (hash != TitleHash)
            {
                TitleHash = hash;
                ret = true;
            }
            return ret;
        }
        #endregion

        #region Serialization
        // ReSharper disable once ConvertToConstant.Local
        // ReSharper disable once FieldCanBeMadeReadOnly.Local
        static string s_guidMatch = "d40817d45b68427d86e989fa21773b48";

        public void Load(string s, DanceMusicService dms)
        {
            SongDetails sd = new SongDetails(s);

            if (sd.SongId == new Guid(s_guidMatch))
            {
                Trace.WriteLine("THis is the bad one?");
            }

            Restore(sd, dms);
            UpdateUsers(dms);            
        }        
        #endregion

        private void SetupCollections()
        {
            // TODO: Use this in constructor???  Move to SongBase???
            if (DanceRatings == null)
            {
                DanceRatings = new List<DanceRating>();
            }
            if (ModifiedBy == null)
            {
                ModifiedBy = new List<ModifiedRecord>();
            }
            if (SongProperties == null)
            {
                SongProperties = new List<SongProperty>();
            }
        }
    }
}