using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Runtime.Serialization;

namespace m4dModels
{
    // Inherit from this and define IdModifier (single char) and TagIdBase (string version of your classes ID field(S)
    // Assumption:  We'll only maintain one tag entry per user, per object (undo provided only on songs where we've got properties).
    // Do we really need a timestamp on these?

    // TODONEXT: If we want users to be taggable we'll need to rethink base class vs. interface (possibly both?)
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
                return string.Format("{0}:{1}", IdModifier, TagIdBase);
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
        virtual public TagList AddTags(string tags, ApplicationUser user, DanceMusicService dms = null, object data = null)
        {
            TagList added = new TagList(tags);
            Tag ut = FindOrCreateUserTags(user, dms);

            TagList newTags = added.Subtract(ut.Tags);
            TagList allTags = ut.Tags.Add(newTags);

            ut.Modified = DateTime.Now;
            ut.Tags = allTags;

            DoUpdate(newTags, null, user, dms, data);

            return newTags;
        }

        // Remove any tags from tags that have previously been added by the user and return a list
        //  of the actually removed tags in canonical form
        public TagList RemoveTags(string tags, ApplicationUser user, DanceMusicService dms = null, object data = null)
        {
            TagList removed = new TagList(tags);
            Tag ut = FindOrCreateUserTags(user, dms);

            TagList badTags = removed.Subtract(ut.Tags);
            TagList oldTags = removed.Subtract(badTags);
            TagList newTags = ut.Tags.Subtract(oldTags);

            ut.Modified = DateTime.Now;
            ut.Tags = newTags;

            DoUpdate(null, oldTags, user, dms, data);

            return oldTags;
        }

        // Change the user's set of tags for this object to reflect the tags parameter
        //  return true if tags have actually changed
        public bool ChangeTags(string tags, ApplicationUser user, DanceMusicService dms = null, object data = null)
        {
            bool changed = false;
            TagList newTags = new TagList(tags);
            Tag ut = FindOrCreateUserTags(user, dms);
            TagList userTags = ut.Tags;

            TagList added = newTags.Subtract(userTags);
            TagList removed = userTags == null ? new TagList() : userTags.Subtract(newTags);

            if (added.Tags.Count > 0 || removed.Tags.Count > 0)
            {
                ut.Modified = DateTime.Now;
                ut.Tags = newTags;
                DoUpdate(added, removed, user, dms, data);
                changed = true;
            }

            return changed;
        }

        private void DoUpdate(TagList added, TagList removed, ApplicationUser user, DanceMusicService dms, object data)
        {
            if (TagSummary == null)
            {
                TagSummary = new TagSummary();
            }

            TagList addRing = ConvertToRing(added,dms);
            TagList delRing = ConvertToRing(removed, dms);
            TagSummary.ChangeTags(addRing, delRing);
            UpdateTagTypes(added, removed, dms);
            RegisterChangedTags(added, removed, user, dms, data);
        }

        // TODO: Think about if we need both implementations (dms and stand-alone);
        private void UpdateTagTypes(TagList added, TagList removed, DanceMusicService dms)
        {
            if (dms == null)
                return;

            if (added != null)
            {
                foreach (string tag in added.Tags)
                {
                    // Create a transitory tag type to parse the tag string
                    TagType tt = dms.FindOrCreateTagType(tag);
                    tt.Count += 1;
                }
            }

            if (removed != null)
            {
                foreach (string tag in removed.Tags)
                {
                    // Create a transitory tag type to parse the tag string
                    TagType tt = dms.FindOrCreateTagType(tag);
                    tt.Count -= 1;
                    if (tt.Count <= 0)
                    {
                        dms.TagTypes.Remove(tt);
                    }
                }
            }
        }
        public virtual TagList UserTags(ApplicationUser user, DanceMusicService dms=null)
        {
            var tag = FindUserTag(user, dms);
            if (tag == null)
                return new TagList();
            else
                return tag.Tags;
        }
        public Tag FindUserTag(ApplicationUser user, DanceMusicService dms=null)
        {
            Tag tag = null;
            if (Tags != null)
            {
                tag = Tags.FirstOrDefault(t => (t.User == user) && (t.Id == TagId));
            }

            if (tag == null && dms != null)
            {
                tag = dms.Tags.FirstOrDefault(t => t.UserId == user.Id && t.Id == TagId);
                if (tag != null)
                {
                    if (Tags == null)
                    {
                        Tags = new List<Tag>();
                    }
                    Tags.Add(tag);
                }
            }
            return tag;
        }

        private Tag FindOrCreateUserTags(ApplicationUser user, DanceMusicService dms)
        {
            var ut = FindUserTag(user, dms);

            if (ut == null)
            {
                ut = dms != null ? dms.Tags.Create() : new Tag();
                ut.UserId = user.Id;
                ut.User = user;
                ut.Id = TagId;
                ut.Tags = new TagList();

                if (dms != null)
                {
                    dms.Tags.Add(ut);
                }

                if (Tags == null)
                {
                    Tags = new List<Tag>();
                }
                Tags.Add(ut);
            }

            return ut;
        }


        public void UpdateTagSummary(DanceMusicService dms)
        {
            string tagId = TagId;
            var tags = from t in dms.Context.Tags where t.Id == tagId select t;

            TagSummary = new TagSummary();
            foreach (var tag in tags)
            {
                TagSummary.ChangeTags(ConvertToRing(tag.Tags, dms), null);
            }
        }

        private void UpdateUserTag(Tag tag, DanceMusicService dms)
        {
            var ut = FindOrCreateUserTags(tag.User, dms);
            if (ut.Tags.Summary != tag.Tags.Summary)
            {
                ut.Tags = tag.Tags;
                ut.Modified = DateTime.Now;
            }
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
            if (dms == null)
                return tags;

            return new TagList(tags.Tags.Select(t => (dms.TagTypes.Find(t)??new TagType{Key=t}).GetPrimary()).Select(tt => tt.Key).ToList());
        }
    }
}
