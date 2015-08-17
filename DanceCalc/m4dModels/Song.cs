using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;

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
            log?.Initialize(user, this, command);

            if (!string.IsNullOrWhiteSpace(command))
            {
                CreateProperty(command, value, null, log, dms);
            }

            Created = time;
            Modified = time;

            if (!addUser || user == null) return;

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
                TagsFromProperties(user, sd.Properties, dms, this);

                // Handle Dance Ratings
                CreateDanceRatings(sd.DanceRatings, dms);

                DanceTagsFromProperties(user, sd.Properties, dms, this);
            }
            else
            {
                InternalEditTags(user, tags, dms);
            }

            // Handle Albums
            CreateAlbums(sd.Albums, dms);

            Purchase = sd.GetPurchaseTags();
            TitleHash = CreateTitleHash(Title);
            SetTimesFromProperties();
        }

        private bool EditCore(ApplicationUser user, SongDetails edit, DanceMusicService dms)
        {
            CreateEditProperties(user, EditCommand, dms);

            var modified = ScalarFields.Aggregate(false, (current, field) => current | UpdateProperty(edit, field, dms));

            var oldAlbums = SongDetails.BuildAlbumInfo(this);

            var foundFirst = false;

            Album = null;

            foreach (var album in edit.Albums)
            {
                var album1 = album;
                var old = oldAlbums.FirstOrDefault(a => a.Index == album1.Index);

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
            foreach (var album in oldAlbums)
            {
                modified = true;
                album.Remove(dms, this, CurrentLog);
            }

            // Now check order and insert a re-order record if they aren't line up...
            // TODO: Linq???
            var needReorder = false;
            var reorder = new List<int>();
            var prev = -1;

            foreach (var album in edit.Albums)
            {
                var t = album.Index;
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
                var t = string.Join(",", reorder.Select(x => x.ToString()));
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
            var old = OrderedProperties.ToList(); // Where(p => !p.IsAction).
            var upd = update.Properties.ToList(); // Where(p => !p.IsAction).
            var c = old.Count;
            for (var i = 0; i < c; i++)
            {
                if (upd.Count >= i && string.Equals(old[i].Name, upd[i].Name)) continue;

                Trace.WriteLine($"Unexpected Update: {SongId}");
                return false;
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
                SongProperties.Add(prop.CopyTo(this));
            }
            
        }


        public void UpdateFromService(DanceMusicService dms)
        {
            UpdateUserTags(dms);

            UpdateDanceTags(dms);

            UpdateUsers(dms);

            TitleHash = CreateTitleHash(Title);
        }

        public void RebuildUserTags(ApplicationUser user, DanceMusicService tms)
        {
            var properties = OrderedProperties.ToList();

            TagsFromProperties(user, properties, tms, this);
            DanceTagsFromProperties(user, properties, tms, this);
        }

        // This is an additive merge - only add new things if they don't conflict with the old
        public bool AdditiveMerge(ApplicationUser user, SongDetails edit, List<string> addDances, DanceMusicService dms)
        {
            CreateEditProperties(user, EditCommand, dms);

            var modified = ScalarFields.Aggregate(false, (current, field) => current | AddProperty(edit, field, dms));

            var oldAlbums = SongDetails.BuildAlbumInfo(this);

            foreach (var album in edit.Albums)
            {
                var album1 = album;
                var old = oldAlbums.FirstOrDefault(a => a.Index == album1.Index);

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
                modified |= TagsFromProperties(user, edit.Properties, dms, this);

                // Handle Dance Ratings
                CreateDanceRatings(edit.DanceRatings, dms);

                modified |= DanceTagsFromProperties(user, edit.Properties, dms, this);
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
            var hash = new Dictionary<string, TagList>();
            foreach (var tag in tags)
            {
                hash[tag.Id] = tag.Tags;
            }

            // First handle the top-level tags, this will incidently add any new danceratings
            //  implied by those tags
            var modified = ChangeTags(hash[""].Summary, user, dms, "Dances");

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
            var weights = new Dictionary<string, int>();
            foreach (var from in songs)
            {
                foreach (var dr in from.DanceRatings)
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

                foreach (var us in from.ModifiedBy)
                {
                    if (AddUser(us.ApplicationUser, dms))
                    {
                        CreateProperty(UserField, us.ApplicationUser.UserName, null, CurrentLog, dms);
                    }
                }

                // TAGTEST: Try merging two songs with tags.
                ApplicationUser currentUser = null;
                var userWritten = false;
                foreach (var prop in from.SongProperties)
                {
                    var bn = prop.BaseName;

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
            foreach (var dance in weights)
            {
                dms.CreateDanceRating(this, dance.Key, dance.Value);

                var value = new DanceRatingDelta { DanceId = dance.Key, Delta = dance.Value }.ToString();

                CreateProperty(DanceRatingField, value, null, CurrentLog, dms);
            }

        }
        private bool UpdateProperty(SongDetails edit, string name, DanceMusicService dms)
        {
            // TODO: This can be optimized
            var modified = false;

            var eP = edit.GetType().GetProperty(name).GetValue(edit);
            var oP = GetType().GetProperty(name).GetValue(this);

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
                base.CreateProperty(name, value, old, log, null);
            }

            return prop;
        }

        public void CreateEditProperties(ApplicationUser user, string command, DanceMusicService dms, DateTime? time = null)
        {
            var rg = command.Split(new[] {'='}, StringSplitOptions.RemoveEmptyEntries);
            // Add the command into the property log
            var cmd = EditCommand;
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
            if (!time.HasValue)
            {
                time = DateTime.Now;
            }
            Modified = time.Value;
            CreateProperty(TimeField, time.Value.ToString(CultureInfo.InvariantCulture), null, dms);
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
                us = new ModifiedRecord { ApplicationUser = user, Song = this, SongId = SongId };
            }
            return AddModifiedBy(us);
        }

        public bool AddUser(string name, DanceMusicService dms)
        {
            var u = dms.FindUser(name);
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
                other.Weight += dr.Weight;
            }
        }
        public bool CreateDanceRatings(IEnumerable<DanceRating> ratings, DanceMusicService dms)
        {
            if (ratings == null)
            {
                return false;
            }

            foreach (var dr in ratings)
            {
                dms.CreateDanceRating(this, dr.DanceId, dr.Weight);
                CreateProperty(
                    DanceRatingField,
                    new DanceRatingDelta { DanceId = dr.DanceId, Delta = dr.Weight }.ToString(),
                    CurrentLog,
                    dms
                );
            }

            return true;
        }

        //  TODO: Ought to be able to refactor both of these into one that calls the other
        public bool EditDanceRatings(IEnumerable<DanceRatingDelta> deltas, DanceMusicService dms)
        {
            var log = CurrentLog;

            foreach (var drd in deltas)
            {
                var valid = true;
                var dro = DanceRatings.FirstOrDefault(r => r.DanceId == drd.DanceId);
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
        public bool EditDanceRatings(IList<string> addIn, int addWeight, IList<string> removeIn, int remWeight, DanceMusicService dms)
        {
            if (addIn == null && removeIn == null)
            {
                return false;
            }
            if (DanceRatings == null)
            {
                DanceRatings = new List<DanceRating>();
            }

            var log = CurrentLog;

            var changed = false;

            List<string> add = null;
            if (addIn != null)
                add = new List<string>(addIn);

            List<string> remove = null;
            if (removeIn != null)
                remove = new List<string>(removeIn);

            var del = new List<DanceRating>();

            // Cleaner way to get old dance ratings?
            foreach (var dr in DanceRatings)
            {
                var added = false;
                var delta = 0;

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
            foreach (var dr in del)
            {
                DanceRatings.Remove(dr);
            }

            // This handles the new ratings
            if (add != null)
            {
                foreach (var ndr in add)
                {
                    var dr = dms.CreateDanceRating(this, ndr, DanceRatingInitial);

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
                        Trace.WriteLine($"Invalid DanceId={ndr}");
                    }

                }
            }

            return changed;
        }

        private bool BaseTagsFromProperties(ApplicationUser user, IEnumerable<SongProperty> properties, DanceMusicService dms, object data, bool dance)
        {
            var modified = false;
            foreach (var p in properties)
            {
                switch (p.BaseName)
                {
                    case UserField:
                        user = dms.FindUser(p.Value);
                        break;
                    case AddedTags:
                        var qual = p.DanceQualifier;
                        if (qual == null && !dance)
                        {
                            modified |= !AddTags(p.Value, user, dms, data).IsEmpty;
                        }
                        else if (qual != null && dance)
                        {
                            var rating = DanceRatings.FirstOrDefault(r => r.DanceId == qual);
                            
                            if (rating != null)
                            {
                                modified = !rating.AddTags(p.Value,user,dms,data).IsEmpty;
                            }
                            else
                            {
                                Debug.Assert(false);
                            }
                        }
                        break;
                    case RemovedTags:
                        qual = p.DanceQualifier;
                        if (qual == null && !dance)
                        {
                            modified |= !RemoveTags(p.Value, user, dms, data).IsEmpty;
                        }
                        else if (qual != null && dance)
                        {
                            var rating = DanceRatings.FirstOrDefault(r => r.DanceId == qual);
                            
                            if (rating != null)
                            {
                                modified |= !rating.RemoveTags(p.Value,user,dms,data).IsEmpty;
                            }
                            else
                            {
                                Debug.Assert(false);
                            }
                        }
                        break;
                }
            }

            return modified;
        }

        private bool TagsFromProperties(ApplicationUser user, IEnumerable<SongProperty> properties, DanceMusicService dms, object data)
        {
            // Clear out cached user tags
            Tags = null;

            return BaseTagsFromProperties(user, properties, dms, data, false);
        }

        private bool DanceTagsFromProperties(ApplicationUser user, IEnumerable<SongProperty> properties, DanceMusicService dms, object data)
        {
            // Clear out cached user tags
            if (DanceRatings != null)
            {
                foreach (var dr in DanceRatings)
                {
                    dr.Tags = null;
                }
            }

            return BaseTagsFromProperties(user, properties, dms, data, true);
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
            foreach (var pi in ScalarProperties)
            {
                var v = pi.GetValue(sd);
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
                foreach (var prop in sd.SongProperties)
                {
                    SongProperties.Add(new SongProperty() { Song = this, SongId = SongId, Name = prop.Name, Value = prop.Value });
                }
            }

            if (DanceRatings == null)
            {
                DanceRatings = new List<DanceRating>();
            }
            Debug.Assert(DanceRatings.Count == 0);
            foreach (var dr in sd.DanceRatings)
            {
                AddDanceRating(dr);
            }

            if (ModifiedBy == null)
            {
                ModifiedBy = new List<ModifiedRecord>();
            }
            Debug.Assert(ModifiedBy.Count == 0);
            foreach (var user in sd.ModifiedBy)
            {
                AddUser(user.UserName, dms);
            }

            ApplicationUser currentUser = null;
            var mr = ModifiedBy.LastOrDefault();
            if (mr != null)
            {
                currentUser = mr.ApplicationUser;
            }
            TagsFromProperties(currentUser, sd.Properties, dms, null);
            DanceTagsFromProperties(currentUser, sd.Properties, dms, null);
            SetTimesFromProperties();
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
                us.SongId = SongId;
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
                    Trace.WriteLineIf(TraceLevels.General.TraceVerbose,
                        $"Duplicate Mapping: Song = {SongId} User = {us.ApplicationUserId}");
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
            var ret = false;
            var hash = string.IsNullOrWhiteSpace(Title) ? 0 : CreateTitleHash(Title);
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
        static string _guidMatch = "d40817d45b68427d86e989fa21773b48";

        public void Load(string s, DanceMusicService dms)
        {
            var sd = new SongDetails(s);

            if (sd.SongId == new Guid(_guidMatch))
            {
                Trace.WriteLine("This is the bad one?");
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