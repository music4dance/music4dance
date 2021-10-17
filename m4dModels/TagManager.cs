using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Azure.Search.Documents.Models;

namespace m4dModels
{
    public class TagManager
    {
        private readonly Dictionary<string, TagGroup> _queuedTags =
            new Dictionary<string, TagGroup>();

        public TagManager(IEnumerable<TagGroup> tagGroups)
        {
            TagGroups = (tagGroups as List<TagGroup>)?.ToList();
            FixupTags();
        }

        public List<TagGroup> TagGroups { get; set; }

        public Dictionary<string, TagGroup> TagMap { get; private set; }

        public static async Task<TagManager> BuildTagManager(DanceMusicCoreService dms,
            string source = "default")
        {
            var tagManager = new TagManager(dms.TagGroups.OrderBy(t => t.Key).ToList());

            tagManager.TagMap = tagManager.TagGroups.ToDictionary(tt => tt.Key.ToLower());

            var facets = await dms.GetTagFacets(
                "GenreTags,StyleTags,TempoTags,OtherTags", 10000, source);

            foreach (var facet in facets)
            {
                var id = SongFilter.TagClassFromName(facet.Key.Substring(0, facet.Key.Length - 4))
                    .ToLower();
                tagManager.IndexFacet(facet.Value, id);
            }

            return tagManager;
        }

        public void FixupTags()
        {
            TagMap = TagGroups.ToDictionary(tt => tt.Key.ToLower());

            foreach (var tt in TagGroups.Where(tt => !string.IsNullOrEmpty(tt.PrimaryId)))
            {
                tt.Primary = TagMap[tt.PrimaryId.ToLower()];
                if (tt.Primary.Children == null)
                {
                    tt.Primary.Children = new List<TagGroup>();
                }

                tt.Primary.Children.Add(tt);
            }
        }

        public TagGroup FindOrCreateTagGroup(string tag)
        {
            lock (_queuedTags)
            {
                var tt = TagMap.GetValueOrDefault(tag.ToLower()) ??
                    _queuedTags.GetValueOrDefault(tag.ToLower());
                if (tt != null)
                {
                    return tt;
                }

                var t = TagList.NormalizeTag(tag);

                tt = new TagGroup(t);
                _queuedTags[tt.Key] = tt;

                AddTagGroup(tt);
                return tt;
            }
        }

        public void AddTagGroup(TagGroup tt)
        {
            lock (_queuedTags)
            {
                TagMap[tt.Key.ToLower()] = tt;
                var index = TagGroups.BinarySearch(
                    tt,
                    Comparer<TagGroup>.Create(
                        (a, b) =>
                            string.Compare(a.Key, b.Key, StringComparison.OrdinalIgnoreCase)));
                if (index < 0)
                {
                    index = ~index;
                }

                TagGroups.Insert(index, tt);
            }
        }

        public void DeleteTagGroup(string key)
        {
            lock (_queuedTags)
            {
                key = key.ToLower();
                var tt = TagMap.GetValueOrDefault(key);
                if (tt == null)
                {
                    return;
                }

                if (!TagMap.Remove(key))
                {
                    return;
                }

                tt.Primary?.Children.Remove(tt);
                var index = TagGroups.BinarySearch(
                    tt,
                    Comparer<TagGroup>.Create(
                        (a, b) =>
                            string.Compare(a.Key, b.Key, StringComparison.OrdinalIgnoreCase)));
                if (index < 0)
                {
                    return;
                }

                TagGroups.RemoveAt(index);
            }
        }

        public void UpdateTagRing(string key, string primaryId)
        {
            lock (_queuedTags)
            {
                var tagGroup = TagMap.GetValueOrDefault(key.ToLower());
                if (tagGroup == null)
                {
                    return;
                }

                if (string.IsNullOrEmpty(primaryId))
                {
                    tagGroup.Primary?.Children?.Remove(tagGroup);
                    tagGroup.PrimaryId = null;
                    tagGroup.Primary = null;
                }
                else
                {
                    tagGroup.PrimaryId = primaryId;
                    tagGroup.Primary = TagMap.GetValueOrDefault(primaryId.ToLower());
                    tagGroup.Primary.AddChild(tagGroup);
                }
            }
        }

        public void ChangeTagName(string oldKey, string newKey)
        {
            lock (_queuedTags)
            {
                var tag = TagMap.GetValueOrDefault(oldKey.ToLower());
                if (tag == null)
                {
                    return;
                }

                DeleteTagGroup(oldKey);

                tag.Key = newKey;
                AddTagGroup(tag);
            }
        }

        public IEnumerable<TagGroup> DequeueTagGroups()
        {
            lock (_queuedTags)
            {
                var ret = _queuedTags.Values.ToList();
                _queuedTags.Clear();
                return ret;
            }
        }

        private void IndexFacet(IEnumerable<FacetResult> facets, string category)
        {
            foreach (var facet in facets)
            {
                if (!facet.Count.HasValue)
                {
                    continue;
                }

                var key = $"{facet.Value}:{category}";
                var tt = FindOrCreateTagGroup(key);

                tt.Count = (int)facet.Count.Value;
            }
        }
    }
}
