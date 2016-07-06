using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using DanceLibrary;

namespace m4dModels
{
    public class Song : SongBase
    {

        #region Properties
        public int TitleHash { get; set; }

        #endregion

        #region Actions

        public void Create(ApplicationUser user, string command, string value, bool addUser, DanceMusicService dms)
        {
            var time = DateTime.Now;
            
            if (!string.IsNullOrWhiteSpace(command))
            {
                CreateProperty(command, value, null, dms);
            }

            Created = time;
            Modified = time;

            if (!addUser || user == null) return;

            AddUser(user, dms);
            CreateProperty(UserField, user.UserName, null, dms);
            CreateProperty(TimeField, time.ToString(CultureInfo.InvariantCulture), null, dms);
        }

        public void Create(SongDetails sd, IEnumerable<UserTag> tags, ApplicationUser user, string command, string value, DanceMusicService dms)
        {
            var addUser = !(sd.ModifiedBy != null && sd.ModifiedList.Count > 0 && AddUser(sd.ModifiedList[0].UserName, dms));

            Create(user, command, value, addUser, dms);

            // Handle User association
            if (!addUser)
            {
                // This is the Modified record created when we computed the addUser condition above
                var mr = ModifiedBy.First();
                mr.Owned = sd.ModifiedList[0].Owned;

                CreateProperty(UserField, mr.UserName, null, dms);
                CreateProperty(TimeField, Created.ToString(CultureInfo.InvariantCulture), null, dms);
                if (mr.Owned.HasValue)
                    CreateProperty(OwnerHash, mr.Owned, null, dms);
            }

            Debug.Assert(!string.IsNullOrWhiteSpace(sd.Title));
            foreach (var pi in ScalarProperties)
            {
                var prop = pi.GetValue(sd);
                if (prop == null) continue;

                pi.SetValue(this, prop);
                CreateProperty(pi.Name, prop, null, dms);
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
                    modified |= album.ModifyInfo(dms, this, old);
                    oldAlbums.Remove(old);
                }
                else
                {
                    // We're in new territory only do something if the name field is non-empty
                    if (string.IsNullOrWhiteSpace(album.Name)) continue;

                    album.CreateProperties(dms, this);
                    modified = true;
                }
            }

            // Handle deleted albums
            foreach (var album in oldAlbums)
            {
                modified = true;
                album.Remove(dms, this);
            }

            // Now check order and insert a re-order record if they aren't line up...
            // TODO: Linq???
            var needReorder = false;
            var reorder = new List<int>();
            var prev = -1;

            foreach (var t in edit.Albums.Select(album => album.Index))
            {
                if (prev > t)
                {
                    needReorder = true;
                }
                prev = t;
                reorder.Add(t);
            }

            if (!needReorder) return modified;

            var temp = string.Join(",", reorder.Select(x => x.ToString()));
            var order = LastProperty(AlbumOrder);
            if (order?.Value == temp)
            {
                return modified;
            }
            CreateProperty(AlbumOrder, temp, null, dms);

            return true;
        }

        internal bool AdminEdit(string properties, DanceMusicService dms)
        {
            CleanUserTags(dms);

            dms.DanceRatings.RemoveRange(DanceRatings.ToList());
            DanceRatings.Clear();

            dms.Modified.RemoveRange(ModifiedBy.ToList());
            ModifiedBy.Clear();

            dms.SongProperties.RemoveRange(SongProperties.ToList());
            SongProperties.Clear();

            ClearValues();
            TitleHash = 0;
            Album = null;

            Load(properties,dms);

            Modified = DateTime.Now;

            return true;
        }

        // Edit 'this' based on songdetails + extras
        public bool Edit(ApplicationUser user, SongDetails edit, IEnumerable<UserTag> tags, DanceMusicService dms)
        {
            var modified = EditCore(user,edit,dms);

            modified |= UpdatePurchaseInfo(edit);
            modified |= UpdateModified(user, edit, dms, true);

            if (tags != null)
            {
                modified |= InternalEditTags(user, tags, dms);
            }

            if (modified)
            {
                InferDances(user);
                Modified = DateTime.Now;
                return true;
            }

            RemoveEditProperties(user,EditCommand,dms);

            return false;
        }

        public bool EditLike(ApplicationUser user, bool? like, DanceMusicService dms)
        {
            var modified = AddUser(user, dms);
            var modrec = FindModified(user.UserName);
            modified |= EditLike(modrec, like, dms);
            return modified;
        }

        public bool EditLike(ModifiedRecord modrec, bool? like, DanceMusicService dms)
        {
            if (modrec.Like == like) return false;

            CreateEditProperties(modrec.ApplicationUser,EditCommand,dms);
            var lt = modrec.LikeString;
            modrec.Like = like;
            CreateProperty(LikeTag, modrec.LikeString, lt, dms);
            return true;
        }

        public bool EditDanceLike(ApplicationUser user, bool? like, string danceId, DanceMusicService dms)
        {
            var r = UserDanceRating(user.UserName, danceId);

            // If the existing like value is in line with the current rating, do nothing
            if ((like.HasValue && (like.Value && r > 0 || !like.Value && r < 0)) || (!like.HasValue && r == 0))
            {
                return false;
            }

            CreateEditProperties(user, EditCommand, dms);

            // First, neutralize existing rating
            var delta = -r;
            var tagDelta = TagsFromDances(new[] { danceId });
            var tagNeg = "!" + tagDelta;
            if (like.HasValue)
            {
                // Then, update the value for our current nudge factor in the appropriate direction
                if (like.Value)
                {
                    delta += DanceRatingIncrement;
                    AddTags(tagDelta, user, dms, this);
                    RemoveTags(tagNeg, user, dms, this);
                }
                else
                {
                    delta += DanceRatingDecrement;
                    AddTags(tagNeg, user, dms, this);
                    RemoveTags(tagDelta, user, dms, this);
                }
            }
            else
            {
                RemoveTags(tagDelta + "|" + tagNeg, user, dms, this);
            }

            UpdateDanceRating(new DanceRatingDelta {DanceId = danceId, Delta = delta}, true);
            return true;
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

            UpdateProperties(mrg,dms.DanceStats);
            UpdateFromService(dms);

            UpdatePurchaseInfo(update);
            Album = null;
            if (update.Albums != null && update.Albums.Count > 0)
            {
                Album = update.Albums[0].AlbumTrack;
            }

            return true;
        }

        public void UpdateProperties(ICollection<SongProperty> properties, DanceStatsInstance stats, string[] excluded = null)
        {
            LoadProperties(properties, stats);

            foreach (var prop in properties.Where(prop => excluded == null || !excluded.Contains(prop.BaseName)))
            {
                SongProperties.Add(prop.CopyTo(this));
            }
        }

        public bool UpdateTagSummaries(DanceMusicService dms)
        {
            var changed = false;
            var delta = new SongDetails(SongId,SongProperties,dms.DanceStats);
            if (!Equals(TagSummary.Summary, delta.TagSummary.Summary))
            {
                changed = UpdateTagSummary(delta.TagSummary);
            }

            foreach (var dr in DanceRatings)
            {
                var drDelta = delta.DanceRatings.FirstOrDefault(drd => drd.DanceId == dr.DanceId);
                if (drDelta == null)
                {
                    Trace.WriteLine($"Bad Comparison: {SongId}:{dr.DanceId}");
                    continue;
                }

                changed |= dr.UpdateTagSummary(drDelta.TagSummary);
            }
            return changed;
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
                    modified |= album.UpdateInfo(dms, this, old);
                }
                else
                {
                    // We're in new territory only do something if the name field is non-empty
                    if (string.IsNullOrWhiteSpace(album.Name)) continue;

                    album.CreateProperties(dms, this);
                    modified = true;
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

            // Possibly a bit of a kludge, but we're going to handle vote (Like/Hate) as a top level tag up to this point
            // So:  null:Like, true:Like, false:Like converts to the appropriate nullable boolean on the modified record.
            var modified = false;
            var songTags = new TagList(hash[""].Summary);
            var likeTags = songTags.Filter("Like");
            if (!likeTags.IsEmpty)
            {
                songTags = songTags.Subtract(likeTags);
                var lt = likeTags.StripType()[0];
                var like = ModifiedRecord.ParseLike(lt);

                // TODO: See if we can easily add this into the full editor
                //  Fix songfilter text to include user
                //  Fix move to advanced form to include user
                //  Make sure that login loop isn't broken
                var mr = ModifiedBy.FirstOrDefault(m => m.ApplicationUserId == user.Id);
                if (mr != null && mr.Like != like)
                {
                    CreateProperty(LikeTag, lt, mr.LikeString, dms);
                    mr.Like = like;
                    modified = true;
                }
            }

            // First strip out all of the deleted dances and save them for later
            var deleted = songTags.ExtractPrefixed('^');
            songTags = songTags.ExtractNotPrefixed('^');

            // Next handle the top-level tags, this will incidently add any new danceratings
            //  implied by those tags
            modified |= ChangeTags(songTags, user, dms, "Dances");

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

            // Finally do the full removal of all danceratings/tags associated with the removed tags
            modified |= DeleteDanceRatings(user,deleted,dms);

            return modified;
        }

        public void LoadTags(DanceMusicService dms)
        {
            var id = TagIdBase;
            var tags = dms.Tags.Where(t => t.Id.Contains(id)).ToList();
            Tags = tags;
        }

        private bool DeleteDanceRatings(ApplicationUser user, TagList deleted, DanceMusicService dms)
        {
            var ratings = new List<DanceRating>();

            // For each entry find the actual dance rating
            // ReSharper disable once LoopCanBeConvertedToQuery
            foreach (var dance in deleted.StripType())
            {
                var d = Dances.Instance.DanceFromName(dance);
                if (d == null) continue;

                var did = d.Id;
                var rating = DanceRatings.FirstOrDefault(dr => dr.DanceId == did);
                if (rating == null) continue;

                ratings.Add(rating);
            }
            if (!ratings.Any())
                return false;

            // For each user that has modified the song, back out anything having to do with the deleted dances
            var lastUser = user;
            var remove = deleted.Add(deleted.AddQualifier('!'));
            foreach (var mr in ModifiedBy)
            {
                var userModified = false;
                var u = mr.ApplicationUser;

                // Add the user property into the SongProperties
                var userProp = CreateProperty(UserProxy, u.UserName, dms);

                // Back out any top-level tags related to the dance styles
                var songTags = FindUserTag(u, dms);
                if (songTags != null)
                {
                    var newSongTags = songTags.Tags.Subtract(remove);
                    if (newSongTags.Summary != songTags.Tags.Summary)
                    {
                        userModified = true;
                        userModified |= ChangeTags(newSongTags, u, dms, this);
                    }
                }

                // Back out the tags directly associated with the dance style
                userModified = ratings.Aggregate(userModified, (current, rating) => current | rating.ChangeTags(string.Empty, user, dms, this));

                // If this user didn't touch anything, back out the user property
                if (userModified)
                    lastUser = u;
                else
                    TruncateProperty(dms,userProp.Name,userProp.Value);
            }

            // If any other user 
            if (lastUser.UserName != user.UserName)
            {
                CreateProperty(UserProxy, user.UserName, dms);
            }

            foreach (var r in ratings.Select(rating => new DanceRatingDelta {DanceId = rating.DanceId, Delta = -rating.Weight}))
            {
                UpdateDanceRating(r, true);
            }

            return true;
        }

        public override void RegisterChangedTags(TagList added, TagList removed, ApplicationUser user, DanceMusicService dms, object data)
        {
            var test = data as string;
            if (string.Equals("Dances", test, StringComparison.OrdinalIgnoreCase))
            {
                var dts = added?.Filter("Dance")??new TagList();
                var dtr = removed?.Filter("Dance") ?? new TagList();

                if (!dts.IsEmpty || !dtr.IsEmpty)
                {
                    var likes = DancesFromTags(dts.ExtractNotPrefixed('!'));
                    var hates = DancesFromTags(dts.ExtractPrefixed('!'));
                    var nulls =
                        DancesFromTags(dtr.ExtractPrefixed('!').Add(dtr.ExtractNotPrefixed('!')))
                            .Where(x => !likes.Contains(x) && !hates.Contains(x));
                    UpdateUserDanceRatings(user.UserName, likes, DanceRatingIncrement);
                    UpdateUserDanceRatings(user.UserName, hates, DanceRatingDecrement);
                    UpdateUserDanceRatings(user.UserName, nulls, 0);

                    // TODO:Dance tags have a property where there may be a "!" version (hate), we need
                    //  to explicity disallow having both the like and hate, but if we do it here we'll remove
                    //  things that don't need to be removed.
                    //removed = removed ?? new TagList();
                    //removed = dts.Tags.Aggregate(removed, 
                    //    (current, tag) => current.Add(tag.StartsWith("!") ? tag.Substring(1) : "!" + tag));
                }
            }

            base.RegisterChangedTags(added, removed, user, dms, data);

            // TODO: We're still removing negative tags need to figure out how to easily
            // refilter removed for no-op tags...

        }

        private void UpdateUserDanceRatings(string userName, IEnumerable<string> danceIds, int rating)
        {
            foreach (var did in danceIds)
            {
                var delta = -UserDanceRating(userName, did) + rating;
                UpdateDanceRating(new DanceRatingDelta { DanceId = did, Delta = delta }, true);
            }
        }

        private bool UpdateModified(ApplicationUser user, SongDetails edit, DanceMusicService dms, bool force)
        {
            var mr = ModifiedBy.FirstOrDefault(m => m.UserName == user.UserName);
            if (mr == null) return false;

            var mrN = edit.ModifiedBy.FirstOrDefault(m => m.UserName == user.UserName);
            if (mrN == null || (!force && !mrN.Owned.HasValue) || (mr.Owned == mrN.Owned)) return false;

            mr.Owned = mrN.Owned;
            CreateProperty(OwnerHash, mr.Owned, dms);
            return true;
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
                        CreateProperty(UserField, us.ApplicationUser.UserName, null, dms);
                    }
                }

                // TAGTEST: Try merging two songs with tags.
                ApplicationUser currentUser = null;
                var userWritten = false;
                foreach (var prop in from.SongProperties)
                {
                    var bn = prop.BaseName;

                    // ReSharper disable once SwitchStatementMissingSomeCases
                    switch (bn)
                    {
                        case UserField:
                        case UserProxy:
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

                CreateProperty(DanceRatingField, value, null, dms);
            }

        }
        private bool UpdateProperty(SongDetails edit, string name, DanceMusicService dms)
        {
            // TODO: This can be optimized
            var eP = edit.GetType().GetProperty(name).GetValue(edit);
            var oP = GetType().GetProperty(name).GetValue(this);

            if (Equals(eP, oP)) return false;

            GetType().GetProperty(name).SetValue(this, eP);

            CreateProperty(name, eP, oP, dms);

            return true;
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
            CreateProperty(name, eP, null, dms);

            return true;
        }

        private static object NullIfWhitespace(object o)
        {
            var s = o as string;
            if (s != null && string.IsNullOrWhiteSpace(s)) o = null;

            return o;
        }
        protected override SongProperty CreateProperty(string name, object value, object old, DanceMusicService dms)
        {
            SongProperty prop = null;
            if (dms != null)
            {
                prop = dms.CreateSongProperty(this, name, value, old);
            }
            else
            {
                base.CreateProperty(name, value, old, null);
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

        public void RemoveEditProperties(ApplicationUser user, string command, DanceMusicService dms)
        {
            TruncateProperty(dms,TimeField);
            TruncateProperty(dms, UserField, user.UserName);
            TruncateProperty(dms, EditCommand);
        }

        private void TruncateProperty(DanceMusicService dms, string name, string value = null)
        {
            var prop = SongProperties.Last();
            if (prop.Name != name || (value != null && prop.Value != value)) return;

            SongProperties.Remove(prop);
            dms.Context.SongProperties.Remove(prop);
        }

        private bool UpdatePurchaseInfo(SongDetails edit, bool additive = false)
        {
            var pi = additive ? edit.MergePurchaseTags(Purchase) : edit.GetPurchaseTags();

            if ((Purchase ?? string.Empty) == (pi ?? string.Empty)) return false;
            Purchase = pi;
            return true;
        }

        public bool AddUser(ApplicationUser user, DanceMusicService dms, bool? like=null)
        {
            ModifiedRecord us;
            if (dms != null)
            {
                us = dms.CreateModified(SongId, user.Id);
                us.ApplicationUser = user;
                us.Like = like;
            }
            else
            {
                us = new ModifiedRecord { ApplicationUser = user, Song = this, SongId = SongId, Like = like};
            }
            return AddModifiedBy(us) == us;
        }

        public bool AddUser(string name, DanceMusicService dms, bool? like=null)
        {
            var u = dms.FindUser(name);
            return AddUser(u, dms, like);
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

                ad.CreateProperties(dms, this);
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
                    dms
                );
            }

            return true;
        }

        //  TODO: Ought to be able to refactor both of these into one that calls the other
        public bool EditDanceRatings(IEnumerable<DanceRatingDelta> deltas, DanceMusicService dms)
        {
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
                    dms.CreateSongProperty(this, DanceRatingField, drd.ToString());
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

                if (delta == 0) continue;

                dr.Weight += delta;

                CreateProperty(DanceRatingField, new DanceRatingDelta { DanceId = dr.DanceId, Delta = delta }.ToString(), dms);

                changed = true;
            }

            // This handles the deleted weights
            foreach (var dr in del)
            {
                DanceRatings.Remove(dr);
            }

            // This handles the new ratings
            if (add == null) return changed;

            foreach (var ndr in add)
            {
                var dr = dms.CreateDanceRating(this, ndr, DanceRatingInitial);

                if (dr != null)
                {
                    CreateProperty(
                        DanceRatingField,
                        new DanceRatingDelta { DanceId = ndr, Delta = DanceRatingInitial }.ToString(),
                        dms);

                    changed = true;
                }
                else
                {
                    Trace.WriteLine($"Invalid DanceId={ndr}");
                }

            }

            return changed;
        }

        private bool BaseTagsFromProperties(ApplicationUser user, IEnumerable<SongProperty> properties, DanceMusicService dms, object data, bool dance)
        {
            var modified = false;
            foreach (var p in properties)
            {
                // ReSharper disable once SwitchStatementMissingSomeCases
                switch (p.BaseName)
                {
                    case UserField:
                    case UserProxy:
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
                            // Else case is where the dancerating has been fully removed, we
                            //  can safely drop this on the floor
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
                            // Else case is where the dancerating has been fully removed, we
                            //  can safely drop this on the floor
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
            if (DanceRatings == null) return BaseTagsFromProperties(user, properties, dms, data, true);

            foreach (var dr in DanceRatings)
            {
                dr.Tags = null;
            }

            return BaseTagsFromProperties(user, properties, dms, data, true);
        }

        public void Delete(ApplicationUser user, DanceMusicService dms)
        {
            if (user != null)
                CreateEditProperties(user, DeleteCommand, dms);

            CleanUserTags(dms);

            ClearValues();
            TitleHash = 0;
            Album = null;

            if (user != null)
                Modified = DateTime.Now;
        }

        public bool CleanUserTags(DanceMusicService dms)
        {
            var tags = dms.Tags.Where(t => t.Id.EndsWith(TagIdBase)).ToList();
            if (!tags.Any()) return false;

            dms.Tags.RemoveRange(tags);
            return true;
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
            TagSummary = sd.TagSummary;
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
            foreach (var user in sd.ModifiedBy.Where(user => ModifiedBy.All(u => u.UserName != user.UserName)))
            {
                AddUser(user.UserName, dms, user.Like);
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

        protected override ModifiedRecord AddModifiedBy(ModifiedRecord mr)
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
            var hash = string.IsNullOrWhiteSpace(Title) ? 0 : CreateTitleHash(Title);
            if (hash == TitleHash) return false;

            TitleHash = hash;
            return true;
        }

        public bool RemoveEmptyEdits(DanceMusicService dms)
        {
            // Cleanup null edits
            var buffer = new List<SongProperty>();

            var users = new Dictionary<string,List<SongProperty>>();
            var activeUsers = new HashSet<string>();

            var inEmpty = false;
            string currentUser = null;

            foreach (var prop in OrderedProperties)
            {
                // Run through the properties and add all clusters of empties
                if (prop.IsAction)
                {
                    if (inEmpty)
                    {
                        if (currentUser != null)
                        {
                            List<SongProperty> r;
                            if (!users.TryGetValue(currentUser, out r))
                            {
                                r = new List<SongProperty>();
                                users[currentUser] = r;
                            }
                            r.AddRange(buffer);
                        }
                        buffer.Clear();
                    }
                    if (prop.Name != EditCommand) continue;

                    inEmpty = true;
                    buffer.Add(prop);
                }
                else if (prop.Name == UserField || prop.Name == TimeField)
                {
                    if (prop.Name == UserField)
                    {
                        // Count == 1 case is where the .Edit command is the only thing there
                        if (inEmpty && buffer.Count > 1)
                        {
                            if (currentUser != null)
                            {
                                List<SongProperty> r;
                                if (!users.TryGetValue(currentUser, out r))
                                {
                                    r = new List<SongProperty>();
                                    users[currentUser] = r;
                                }
                                r.AddRange(buffer);
                            }
                            buffer.Clear();
                        }
                        else if (currentUser != null)
                        {
                            activeUsers.Add(currentUser);
                        }

                        currentUser = prop.Value;
                        inEmpty = true;
                    }

                    if (inEmpty)
                    {
                        buffer.Add(prop);
                    }
                }
                else
                {
                    inEmpty = false;
                    buffer.Clear();
                }
            }

            var remove = new List<SongProperty>();
            foreach (var user in users)
            {
                if (activeUsers.Contains(user.Key))
                {
                    remove.AddRange(user.Value);
                }
                else
                {
                    var props = user.Value;
                    var u = props.FirstOrDefault(p => p.Name == UserField);
                    if (u == null) continue;

                    props.Remove(u);
                    remove.AddRange(props);
                }
            }

            if (remove.Count == 0) return false;

            foreach (var prop in remove)
            {
                SongProperties.Remove(prop);
                dms.Context.SongProperties.Remove(prop);
            }

            return true;
        }

        public bool RemoveDuplicateDurations(DanceMusicService dms)
        {
            // Cleanup durations that are within 20 seconds of an average

            var count = 0;
            var outliers = 0;
            var avg = 0;
            SongProperty first = null;
            var remove = new List<SongProperty>();

            foreach (var prop in OrderedProperties)
            {
                if (prop.Name != LengthField) continue;

                var val = prop.ObjectValue;
                if (!(val is int)) continue;

                var current = (int)val;

                if (count == 0)
                {
                    avg = current;
                    first = prop;
                    count = 1;
                }
                else if (Math.Abs(avg - current) > 20)
                {
                    outliers += 1;
                    remove.Add(prop);
                }
                else
                {
                    avg = ((avg*count) + current)/(count + 1);
                    count += 1;
                    remove.Add(prop);
                }
            }

            if (remove.Count == 0 || first == null || outliers > count / 2) return false;

            first.Value = avg.ToString();

            foreach (var prop in remove)
            {
                SongProperties.Remove(prop);
                dms.Context.SongProperties.Remove(prop);
            }

            return true;
        }

        public bool CleanupAlbums(DanceMusicService dms)
        {
            // Remove the properties for album info that has been 'deleted'
            // and if any have been removed, also get rid of promote and order

            var albums = new Dictionary<int, List<SongProperty>>();
            var remove = new List<SongProperty>();
            var deleted = new HashSet<int>();

            var changed = false;
            foreach (var prop in OrderedProperties)
            {
                var bn = prop.BaseName;
                var index = prop.Index ?? -1;

                switch (bn)
                {
                    case AlbumOrder:
                    case AlbumPromote:
                        remove.Add(prop);
                        break;
                    case AlbumField:
                    case TrackField:
                    case PublisherField:
                    case PurchaseField:
                        if (prop.IsNull)
                        {
                            if (bn == AlbumField)
                            {
                                deleted.Add(index);
                                // pull the previous properties and add this to removed
                                List<SongProperty> old;
                                if (albums.TryGetValue(index, out old))
                                {
                                    remove.AddRange(old);
                                    albums.Remove(index);
                                }
                                remove.Add(prop);
                                changed = true;
                            }
                            else if (deleted.Contains(index))
                            {
                                remove.Add(prop);
                                changed = true;
                            }
                        }
                        else
                        {
                            List<SongProperty> old;
                            if (!albums.TryGetValue(index, out old))
                            {
                                old = new List<SongProperty>();
                                albums[index] = old;
                            }
                            old.Add(prop);
                            if (deleted.Contains(index))
                            {
                                deleted.Remove(index);
                            }
                        }
                        break;
                }
            }

            if (remove.Count == 0 || !changed) return false;

            foreach (var prop in remove)
            {
                SongProperties.Remove(prop);
                dms.Context.SongProperties.Remove(prop);
            }

            return true;
        }

        private class TagTracker
        {
            public TagTracker()
            {
                Tags = new TagList();
            }
            public TagList Tags { get; set; }
            public SongProperty Property { get; set; }
        }

        private class RatingTracker
        {
            public int Rating { get; set; }
            public SongProperty Property { get; set; }
        }

        private class UserEdits
        {
            public UserEdits()
            {
                UserTags = new Dictionary<string, TagTracker>();
                Ratings = new Dictionary<string, RatingTracker>();
            }
            public  Dictionary<string,TagTracker> UserTags { get; }

            public Dictionary<string, RatingTracker> Ratings { get; } 
        }

        public bool NormalizeRatings(DanceMusicService dms, int max = 2, int min =-1)
        {
            // This function should not semantically change the tags, but it will potentially
            //  reduce the danceratings where there were redundant entries previously and normalize based
            // on max/min

            // For each user, keep a list of the tags and danceratings that they have applied
            var users = new Dictionary<string,UserEdits>();
            var remove = new List<SongProperty>();

            string currentUser = null;
            UserEdits currentEdits = null;
            var changed = false;

            foreach (var prop in OrderedProperties)
            {
                var bn = prop.BaseName;
                switch (prop.BaseName)
                {
                    case UserField:
                    case UserProxy:
                        if (!string.Equals(currentUser, prop.Value, StringComparison.OrdinalIgnoreCase))
                        {
                            currentUser = prop.Value;
                            if (!users.TryGetValue(currentUser, out currentEdits))
                            {
                                currentEdits = new UserEdits();
                                users[currentUser] = currentEdits;
                            }
                        }
                        break;
                    case AddedTags:
                    case RemovedTags:
                        var qual = prop.DanceQualifier ?? string.Empty;
                        if (currentEdits == null)
                        {
                            Trace.WriteLine($"Tag property {prop} comes before user.");
                            break;
                        }
                        TagTracker acc;
                        if (!currentEdits.UserTags.TryGetValue(qual, out acc))
                        {
                            acc = new TagTracker {Property = prop};
                            currentEdits.UserTags[qual] = acc;
                        }
                        else
                        {
                            remove.Add(prop);
                        }
                        acc.Tags = (bn == AddedTags) ? 
                            acc.Tags.Add(new TagList(prop.Value)) : 
                            acc.Tags.Subtract(new TagList(prop.Value));
                        break;
                    case DanceRatingField:
                        if (currentEdits == null)
                        {
                            Trace.WriteLine($"DanceRating property {prop} comes before user.");
                            break;
                        }
                        var drd = new DanceRatingDelta(prop.Value);
                        RatingTracker rating;
                        var delta = drd.Delta;

                        // Enforce normalization of max/min values
                        if (delta > max) delta = max;
                        else if (delta < min) delta = min;

                        if (drd.Delta != delta)
                        {
                            changed = true;
                            drd.Delta = delta;
                            prop.Value = drd.ToString();
                        }

                        if (!currentEdits.Ratings.TryGetValue(drd.DanceId, out rating))
                        {
                            currentEdits.Ratings[drd.DanceId] = new RatingTracker {Rating = delta, Property = prop};
                        }
                        else 
                        {
                            // Keep the vote that is in the direction that is most recent for this user, then the largest value
                            if ((Math.Sign(rating.Rating) != Math.Sign(delta)) || (Math.Abs(rating.Rating) <= Math.Abs(delta)))
                            {
                                changed = true;
                                rating.Rating = delta;
                                rating.Property.Value = drd.ToString();
                            }
                            remove.Add(prop);
                        }
                        break;
                }
            }

            foreach (var prop in remove)
            {
                SongProperties.Remove(prop);
                dms.Context.SongProperties.Remove(prop);
                changed = true;
            }

            foreach (var edit in users.Values)
            {
                foreach (var tracker in edit.UserTags.Values)
                {
                    var tags = tracker.Tags.ToString();
                    if (string.Equals(tags, tracker.Property.Value))
                        continue;
                    tracker.Property.Value = tags;
                    changed = true;
                }
            }

            if (changed) SetRatingsFromProperties();

            return changed;
        }

        public bool CleanupProperties(DanceMusicService dms)
        {
            var changed = RemoveDuplicateDurations(dms);
            changed |= CleanupAlbums(dms);
            changed |= NormalizeRatings(dms);
            changed |= RemoveEmptyEdits(dms);

            return changed;
        }
        #endregion

        #region Serialization
        // ReSharper disable once ConvertToConstant.Local
        // ReSharper disable once FieldCanBeMadeReadOnly.Local
        //static string _guidMatch = "d40817d45b68427d86e989fa21773b48";

        public void Load(string s, DanceMusicService dms)
        {
            var sd = new SongDetails(s,dms?.DanceStats,null,false);

            //if (sd.SongId == new Guid(_guidMatch))
            //{
            //    Trace.WriteLine("This is the bad one?");
            //}

            Restore(sd, dms);
            UpdateUsers(dms);
        }
        #endregion

        protected override void ClearValues()
        {
            base.ClearValues();
            Purchase = null;
        }

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