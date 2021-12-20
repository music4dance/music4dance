using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Azure.Search.Documents.Models;

namespace m4dModels
{
    // TODONEXT: Split the tag database into just the DB tag rings and the 
    //  tags pulled from the facets (there will be overlap, which we can validate)
    //  Then look at trimming the DB to remove any non-ring tags...
    public class TagManager
    {
        private readonly Dictionary<string, TagGroup> _queuedTags =
            new();

        public static List<string> Duplicates { get; } = new();

        public TagManager(IEnumerable<TagGroup> tagGroups)
        {
            TagGroups = CleanTagGroups(tagGroups.ToList());
            FixupTags();
        }

        public List<TagGroup> TagGroups { get; set; }

        public Dictionary<string, TagGroup> TagMap { get; private set; }

        private static List<TagGroup> CleanTagGroups(List<TagGroup> tagGroups)
        {
            var groups = tagGroups
                .DistinctBy(g => g.Key.ToLower())
                .OrderBy(g => g.Key.ToLower())
                .ToList();

            if (groups.Count != tagGroups.Count)
            {
                //if (Debugger.IsAttached)
                //{
                //    Debugger.Break();
                //}

                var dups = tagGroups
                    .GroupBy(g => g.Key.ToLower())
                    .Where(g => g.Count() > 1).ToList();

                Duplicates.Clear();

                Trace.WriteLine($"{dups.Count()} duplicate tags");
                foreach (var group in dups)
                {
                    foreach (var tag in group)
                    {
                        Duplicates.Add(tag.Key);
                        Trace.WriteLine(tag.Key);
                    }
                }
            }

            return groups;
        }

        public static async Task<TagManager> BuildTagManager(DanceMusicCoreService dms,
            string source = "default")
        {
            var tagManager =
                new TagManager(
                    dms.TagGroups.OrderBy(t => t.Key).ToList());

            tagManager.TagMap = tagManager.TagGroups.ToDictionary(tt => tt.Key);

            var facets = await dms.GetTagFacets(
                "GenreTags,StyleTags,TempoTags,OtherTags", 10000, source);

            foreach (var facet in facets)
            {
                var id = SongFilter.TagClassFromName(facet.Key[0..^4]);
                tagManager.IndexFacet(facet.Value, id);
            }

            return tagManager;
        }

        public void FixupTags()
        {
            TagMap = TagGroups.ToDictionary(
                tt => tt.Key, StringComparer.OrdinalIgnoreCase);

            foreach (var tt in TagGroups.Where(tt => !string.IsNullOrEmpty(tt.PrimaryId)))
            {
                tt.Primary = TagMap[tt.PrimaryId];
                tt.Primary.Children ??= new List<TagGroup>();

                tt.Primary.Children.Add(tt);
            }
        }

        public TagGroup FindOrCreateTagGroup(string tag)
        {
            lock (_queuedTags)
            {
                var t = TagList.NormalizeTag(tag);
                var tt = TagMap.GetValueOrDefault(t) ??
                    _queuedTags.GetValueOrDefault(t);
                if (tt != null)
                {
                    return tt;
                }

                tt = new TagGroup(t);
                _queuedTags[tt.Key] = tt;

                AddTagGroup(tt);
                return tt;
            }
        }

        public void AddTagGroup(TagGroup tt)
        {
            if (TagMap.ContainsKey(tt.Key))
            {
                return;
            }

            lock (_queuedTags)
            {
                TagMap[tt.Key] = tt;
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
                var tagGroup = TagMap.GetValueOrDefault(key);
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
                    tagGroup.Primary = TagMap.GetValueOrDefault(primaryId);
                    tagGroup.Primary.AddChild(tagGroup);
                }
            }
        }

        public void ChangeTagName(string oldKey, string newKey)
        {
            lock (_queuedTags)
            {
                var tag = TagMap.GetValueOrDefault(oldKey);
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
