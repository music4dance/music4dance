using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Serialization;

namespace m4dModels
{
    // Inherit from this and define IdModifier (single char) and TagIdBase (string version of your classes ID field(S)
    // Assumption:  We'll only maintain one tag entry per user, per object (undo provided only on songs where we've got properties).
    // Do we really need a timestamp on these?

    // TODO: If we want users to be taggable we'll need to rethink base class vs. interface (possibly both?)
    //  Test Editing a song with tags, merging a song with tags, log & restore log with tags
    //  Complete:  Saving/reloading the database
    //  Go through and make sure we're hitting TAGTEST
    //  Go through the old tagging code rip TAGDELETE
    //  Implement & Test Tag Rings????
    [DataContract]
    public abstract class TaggableObject
    {
        protected TaggableObject()
        {
            TagSummary = new TagSummary();
        }
        public abstract char IdModifier { get; }
        public abstract string TagIdBase {get;}

        [DataMember]
        public string TagId
        {
            get
            {
                return $"{IdModifier}:{TagIdBase}";
            }
            set
            {
                throw new NotImplementedException(
                    "If we hit this it means we're trying to deserialize a Taggable object - Not sure we want to do this");
            }
        }
        [DataMember]
        public TagSummary TagSummary { get; set; }

        // TODO: Do we actually want this?  Or should this be a dms based operations rather than a property
        [NotMapped]
        public IList<Tag> Tags { get; set; }

        // Override this to register changed tags per user for your class (for instance song would push in song properties)
        public virtual void RegisterChangedTags(TagList added, TagList removed, ApplicationUser user, DanceMusicService dms, object data)
        {
            //Trace.WriteLineIf(TraceLevels.General.TraceVerbose, string.Format("{0}:{1} - added={2};removed={3}", 
            //    user.UserName, TagId, added == null ? "(null)" : added.ToString(), removed == null ? "removed" : removed.ToString()));
        }

        // Add any tags from tags that haven't already been added by the user and return a list of
        // the actually added tags in canonical form
        virtual public TagList AddTags(string tags, ApplicationUser user, DanceMusicService dms = null, object data = null, bool updateTypes=true)
        {
            var added = new TagList(tags);
            var ut = FindOrCreateUserTags(user, dms);

            var newTags = added.Subtract(ut.Tags);
            var allTags = ut.Tags.Add(newTags);

            ut.Modified = DateTime.Now;
            ut.Tags = allTags;

            DoUpdate(newTags, null, user, dms, data, updateTypes);

            return newTags;
        }

        // Remove any tags from tags that have previously been added by the user and return a list
        //  of the actually removed tags in canonical form
        public TagList RemoveTags(string tags, ApplicationUser user, DanceMusicService dms = null, object data = null, bool updateTypes=true)
        {
            var removed = new TagList(tags);
            var ut = FindOrCreateUserTags(user, dms);

            var badTags = removed.Subtract(ut.Tags);
            var oldTags = removed.Subtract(badTags);
            var newTags = ut.Tags.Subtract(oldTags);

            ut.Modified = DateTime.Now;
            ut.Tags = newTags;

            DoUpdate(null, oldTags, user, dms, data, updateTypes);

            if (dms != null && ut.Tags.IsEmpty)
            {
                dms.Tags.Remove(ut);
            }
            return oldTags;
        }

        // Change the user's set of tags for this object to reflect the tags parameter
        //  return true if tags have actually changed
        public bool ChangeTags(string tags, ApplicationUser user, DanceMusicService dms = null, object data = null, bool updateTypes=true)
        {
            var newTags = new TagList(tags);
            var ut = FindOrCreateUserTags(user, dms);
            var userTags = ut.Tags;

            var added = newTags.Subtract(userTags);
            var removed = userTags == null ? new TagList() : userTags.Subtract(newTags);

            if (added.Tags.Count <= 0 && removed.Tags.Count <= 0) return false;

            ut.Modified = DateTime.Now;
            ut.Tags = newTags;
            DoUpdate(added, removed, user, dms, data, updateTypes);

            return true;
        }

        private void DoUpdate(TagList added, TagList removed, ApplicationUser user, DanceMusicService dms, object data, bool updateTypes=true)
        {
            if (TagSummary == null)
                TagSummary = new TagSummary();

            var addRing = ConvertToRing(added,dms);
            var delRing = ConvertToRing(removed, dms);
            TagSummary.ChangeTags(addRing, delRing);
            if (updateTypes && dms != null)
                UpdateTagTypes(added, removed, dms);
            RegisterChangedTags(added, removed, user, dms, data);
        }

        // TODO: Think about if we need both implementations (dms and stand-alone);
        private static void UpdateTagTypes(TagList added, TagList removed, DanceMusicService dms)
        {
            if (dms == null)
                return;

            if (added != null)
            {
                foreach (var tag in added.Tags)
                {
                    // Create a transitory tag type to parse the tag string
                    var tt = dms.FindOrCreateTagType(tag);
                    tt.Count += 1;
                }
            }

            if (removed == null) return;

            foreach (var tt in removed.Tags.Select(dms.FindOrCreateTagType))
            {
                tt.Count -= 1;
                if (tt.Count <= 0)
                {
                    dms.TagTypes.Remove(tt);
                }
            }
        }
        public virtual TagList UserTags(ApplicationUser user, DanceMusicService dms=null)
        {
            var tag = FindUserTag(user, dms);
            return tag == null ? new TagList() : tag.Tags;
        }

        public Tag FindUserTag(ApplicationUser user, DanceMusicService dms=null)
        {
            if (user == null)
            {
                Trace.WriteLine("Null User in FindUserTag?");
                return null;
            }

            // TODO: Local tag list is riding the edge of flaky...
            Tag tag = null;
            if (Tags != null)
            {
                tag = Tags.FirstOrDefault(t => (t.UserId == user.Id) && (t.Id == TagId)) ??
                      Tags.FirstOrDefault(t => (t.User != null) && (t.User.UserName == user.UserName) && (t.Id == TagId));
            }

            if (tag != null || dms == null) return tag;

            tag = dms.Tags.FirstOrDefault(t => t.UserId == user.Id && t.Id == TagId);
            if (tag == null) return null;

            if (Tags == null)
                Tags = new List<Tag>();

            Tags.Add(tag);
            return tag;
        }

        private Tag FindOrCreateUserTags(ApplicationUser user, DanceMusicService dms)
        {
            var ut = FindUserTag(user, dms);

            if (ut != null) return ut;

            ut = dms != null ? dms.Tags.Create() : new Tag();
            ut.UserId = user.Id;
            ut.User = user;
            ut.Id = TagId;
            ut.Tags = new TagList();

            if (dms != null)
                dms.Tags.Add(ut);

            if (Tags == null)
            {
                Tags = new List<Tag>();
            }
            Tags.Add(ut);

            return ut;
        }


        public bool UpdateTagSummary(DanceMusicService dms)
        {
            var tagId = TagId;
            var tags = from t in dms.Context.Tags where t.Id == tagId select t;

            var ts = TagSummary;

            TagSummary = new TagSummary();
            foreach (var tag in tags)
            {
                TagSummary.ChangeTags(ConvertToRing(tag.Tags, dms), null);
            }

            var changed = ts.Summary != TagSummary.Summary;
            if (changed && TraceLevels.General.TraceVerbose)
            {
                Trace.WriteLine($"{TagId}: {ts.Summary} - {TagSummary.Summary}");
            }

            return changed;
        }

        private void UpdateUserTag(Tag tag, DanceMusicService dms)
        {
            if (tag.User.IsPlaceholder) 
            {
                tag.User = dms.FindUser(tag.User.UserName);//dms.FindOrAddUser(tag.User.UserName);
                if (tag.User == null)
                {
                    throw new InvalidCastException("tag");
                }
            }
            var ut = FindOrCreateUserTags(tag.User, dms);
            if (ut.Tags.Summary == tag.Tags.Summary) return;

            var removed = ut.Tags.Subtract(tag.Tags);
            var added = tag.Tags.Subtract(ut.Tags);

            foreach (var t in removed.Tags)
            {
                var tt = dms.TagTypes.Find(t);
                if (tt != null) tt.Count -= 1;
            }

            foreach (var t in added.Tags)
            {
                var tt = dms.FindOrCreateTagType(t);
                if (tt != null) tt.Count += 1;
            }

            ut.Tags = tag.Tags;
            ut.Modified = DateTime.Now;
        }
        public void UpdateUserTags(DanceMusicService dms)
        {
            if (Tags == null) return;

            var tags = Tags;
            Tags = null;

            foreach (var tag in tags)
            {
                UpdateUserTag(tag, dms);
                var ring = ConvertToRing(tag.Tags, dms);
                if (ring.Summary != tag.Tags.Summary)
                {
                    TagSummary.ChangeTags(ring, tag.Tags);
                }
            }
        }

        private static TagList ConvertToRing(TagList tags, DanceMusicService dms)
        {
            if (tags == null)
                return null;

            return (dms == null) ? tags : 
                new TagList(tags.Tags.Select(t => (dms.TagTypes.Find(t)??new TagType{Key=t}).GetPrimary()).Select(tt => tt.Key).Distinct().ToList());
        }
    }
}
