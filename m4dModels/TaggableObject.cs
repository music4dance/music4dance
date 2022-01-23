using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Serialization;

namespace m4dModels
{
    // Inherit from this and define IdModifier (single char) and TagIdBase (string version of your classes ID field(S)
    // Assumption:  We'll only maintain one tag entry per user, per object (undo provided only on songs where we've got properties).
    // Do we really need a timestamp on these?

    [DataContract]
    public abstract class TaggableObject
    {
        private static readonly HashSet<string> s_validClasses = new() { "other" };

        protected TaggableObject()
        {
            TagSummary = new TagSummary();
            Comments = new List<UserComment> ();
        }

        [DataMember]
        public TagSummary TagSummary { get; set; }

        public List<UserComment> Comments { get; set; }


        protected virtual HashSet<string> ValidClasses => s_validClasses;

        // Override this to register changed tags per user for your class (for instance song would push in song properties)
        public virtual void RegisterChangedTags(TagList added, TagList removed, string user,
            object data)
        {
            //Trace.WriteLineIf(TraceLevels.General.TraceVerbose, string.Format("{0}:{1} - added={2};removed={3}", 
            //    user.UserName, TagId, added == null ? "(null)" : added.ToString(), removed == null ? "removed" : removed.ToString()));
        }

        // Add any tags from tags that haven't already been added by the user and return a list of
        // the actually added tags in canonical form
        public virtual TagList AddTags(string tags, string user, DanceStatsInstance stats,
            object data = null, bool updateTypes = true)
        {
            var added = VerifyTags(tags);
            return added == null ? null : AddTags(added, user, stats, data, updateTypes);
        }

        public TagList AddTags(string tags, DanceStatsInstance stats)
        {
            return AddTags(VerifyTags(tags), stats);
        }

        public TagList AddTags(TagList tags, DanceStatsInstance stats)
        {
            var ring = stats == null ? tags : ConvertToRing(tags, stats);
            TagSummary.ChangeTags(ring, null);
            return ring;
        }

        public TagList RemoveTags(string tags, DanceStatsInstance stats)
        {
            return RemoveTags(VerifyTags(tags), stats);
        }

        public TagList RemoveTags(TagList tags, DanceStatsInstance stats)
        {
            var ring = stats == null ? tags : ConvertToRing(tags, stats);
            TagSummary.ChangeTags(null, ring);
            return ring;
        }

        public void DeleteTag(TagCount tag, DanceStatsInstance stats)
        {
            TagSummary.DeleteTag(tag);
        }

        public virtual TagList AddTags(TagList tags, string user, DanceStatsInstance stats,
            object data = null, bool updateTypes = true)
        {
            if (user == null)
            {
                return null;
            }

            var ut = GetUserTags(user, data as Song);

            var newTags = tags.Subtract(ut);

            DoUpdate(newTags, null, user, stats, data, updateTypes);

            return newTags;
        }

        // Add any tags from tags that haven't already been added by the user and return a list of
        // the actually added tags in canonical form
        public TagList VerifyTags(string tags, bool fix = true)
        {
            var validClasses = ValidClasses;

            var list = new TagList(tags);
            var result = new List<string>();

            foreach (var tag in list.Tags)
            {
                string one;
                var i = tag.LastIndexOf(':');
                if (i == -1)
                {
                    if (!fix)
                    {
                        return null;
                    }

                    one = $"{tag}:Other";
                }
                else
                {
                    var cls = tag[(i + 1)..].ToLower();
                    var val = tag[..i];
                    if (cls.Length < 2 || !validClasses.Contains(cls))
                    {
                        if (!fix)
                        {
                            return null;
                        }

                        cls = "other";
                    }

                    one = $"{val}:{char.ToUpper(cls[0])}{cls[1..].ToLower()}";
                }

                result.Add(one);
            }

            return new TagList(result);
        }

        // Remove any tags from tags that have previously been added by the user and return a list
        //  of the actually removed tags in canonical form
        // TODO: Currently if removeTags gets rid of everything, an add tags in the same session with be a no-op,
        //  working around this by always doing an add before a remove, but that sucks long term
        public TagList RemoveTags(string tags, string user, DanceStatsInstance stats,
            object data = null, bool updateTypes = true)
        {
            // If there were no pre-existing user tags, removal is a no-op
            var ut = GetUserTags(user);
            if (ut == null || ut.IsEmpty)
            {
                return new TagList();
            }

            var removed = new TagList(tags);

            var badTags = removed.Subtract(ut);
            var oldTags = removed.Subtract(badTags);

            if (oldTags.IsEmpty)
            {
                return oldTags;
            }

            DoUpdate(null, oldTags, user, stats, data, updateTypes);

            return oldTags;
        }

        // Change the user's set of tags for this object to reflect the tags parameter
        //  return true if tags have actually changed
        public bool ChangeTags(string tags, string user, DanceStatsInstance stats,
            object data = null, bool updateTypes = true)
        {
            return ChangeTags(new TagList(tags), user, stats, data, updateTypes);
        }

        public bool ChangeTags(TagList newTags, string user, DanceStatsInstance stats,
            object data = null, bool updateTypes = true)
        {
            // Short-circuit if both old and new are empty
            var ut = GetUserTags(user, data as Song);
            if (newTags.IsEmpty && ut.IsEmpty)
            {
                return false;
            }

            var added = newTags.Subtract(ut);
            var removed = ut.Subtract(newTags);

            if (added.Tags.Count <= 0 && removed.Tags.Count <= 0)
            {
                return false;
            }

            DoUpdate(added, removed, user, stats, data, updateTypes);

            return true;
        }

        private void DoUpdate(TagList added, TagList removed, string user, DanceStatsInstance stats,
            object data, bool updateTypes = true)
        {
            TagSummary ??= new TagSummary();

            var addRing = ConvertToRing(added, stats);
            var delRing = ConvertToRing(removed, stats);
            TagSummary.ChangeTags(addRing, delRing);
            if (updateTypes && stats != null)
            {
                UpdateTagGroups(added, removed, stats);
            }

            RegisterChangedTags(added, removed, user, data);
        }

        private static void UpdateTagGroups(TagList added, TagList removed,
            DanceStatsInstance stats)
        {
            if (stats == null)
            {
                return;
            }

            if (added != null)
            {
                foreach (var tag in added.Tags)
                {
                    // Create a transitory tag type to parse the tag string
                    var tt = stats.TagManager.FindOrCreateTagGroup(tag);
                    tt.Count += 1;
                }
            }

            if (removed == null)
            {
                return;
            }

            foreach (var tt in removed.Tags.Select(stats.TagManager.FindOrCreateTagGroup))
            {
                tt.Count -= 1;
            }
            // TODO: We should consider a service that occasionally sweeps TagGroups and removes the ones that
            //  aren't used, but we can't proactively delete them this way since when we're doing a full load
            //  of the database this causes inconsistencies.
            //if (tt.Count <= 0)
            //{
            //    dms.TagGroups.Remove(tt);
            //}
        }

        public bool UpdateTagSummary(TagSummary newSummary)
        {
            if (newSummary.Summary == TagSummary.Summary)
            {
                return false;
            }

            TagSummary = newSummary;

            return true;
        }

        public TagList GetUserTags(
            string userName, Song song = null, bool recent = false,
            DanceStatsInstance stats = null)
        {
            song ??= this as Song;
            var danceId = (this as DanceRating)?.DanceId;
            Debug.Assert(song != null);

            // Build the tags from the properties
            var acc = new TagList();
            if (string.IsNullOrEmpty(userName))
            {
                return acc;
            }

            string cu = null;
            foreach (var prop in song.SongProperties)
                // ReSharper disable once SwitchStatementMissingSomeCases
            {
                switch (prop.BaseName)
                {
                    case Song.UserField:
                    case Song.UserProxy:
                        if (recent)
                        {
                            acc = new TagList();
                        }

                        cu = new ModifiedRecord(prop.Value).UserName;
                        break;
                    case Song.AddedTags:
                    case Song.RemovedTags:
                        var tags = new TagList(prop.Value);
                        var ring = stats == null ? tags : ConvertToRing(tags, stats);

                        if (userName.Equals(cu) && prop.DanceQualifier == danceId)
                        {
                            acc = prop.BaseName == Song.AddedTags
                                ? acc.Add(ring)
                                : acc.Subtract(ring);
                        }

                        break;
                }
            }

            return acc;
        }

        private static TagList ConvertToRing(TagList tags, DanceStatsInstance stats)
        {
            var tagMap = stats.TagManager.TagMap;
            var manager = stats.TagManager;
            return tags == null
                ? null
                : new TagList(
                    tags.Tags
                        .Select(
                            t =>
                                manager.FindOrCreateTagGroup(t)
                                    .GetPrimary()).Select(tt => tt.Key)
                                    .Distinct().ToList());
        }

        public void AddComment(string comment, string userName)
        {
            RemoveComment(userName);
            Comments.Add(new UserComment { Comment = comment, UserName = userName });
        }

        public void RemoveComment(string userName)
        {
            Comments = Comments.Where(c => c.UserName != userName).ToList();
        }
    }
}
