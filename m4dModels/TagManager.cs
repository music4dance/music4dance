using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

using Azure.Search.Documents.Models;

namespace m4dModels
{
    public class TagManager
    {
        public static List<string> Duplicates { get; } = [];

        public TagManager(IEnumerable<TagGroup> tagGroups)
        {
            SetTagMap(CleanTagGroups([.. tagGroups]));
        }

        public ConcurrentDictionary<string, TagGroup> TagMap { get; private set; }

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

                Trace.WriteLine($"{dups.Count} duplicate tags");
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

        public static async Task<TagManager> BuildTagManager(
            DanceMusicCoreService dms,
            IEnumerable<string> facetIds,
            string source = "default")
        {
            var tagManager =
                new TagManager(
                    [.. dms.TagGroups.OrderBy(t => t.Key)]);

            var facets = await dms.GetSongIndex(source).GetTagFacets(
                string.Join(',', facetIds), 10000);

            foreach (var facet in facets)
            {
                var id = SongFilter.TagFromFacetId(facet.Key);
                tagManager.IndexFacet(facet.Value, id);
            }

            return tagManager;
        }

        public void SetTagMap(IList<TagGroup> tagGroups)
        {
            TagMap = new ConcurrentDictionary<string, TagGroup>(
                tagGroups.Select(t => KeyValuePair.Create(t.Key, t)),
                StringComparer.OrdinalIgnoreCase);

            foreach (var tt in tagGroups.Where(tt => !string.IsNullOrEmpty(tt.PrimaryId)))
            {
                tt.Primary = TagMap[tt.PrimaryId];
                tt.Primary.Children ??= [];

                tt.Primary.Children.Add(tt);
            }
        }

        public TagGroup FindOrCreateTagGroup(string tag)
        {
            var t = TagList.NormalizeTag(tag);
            var tt = TagMap.GetValueOrDefault(t);

            if (tt != null)
            {
                return tt;
            }

            tt = new TagGroup(t);
            AddTagGroup(tt);
            return tt;
        }

        public void AddTagGroup(TagGroup tt)
        {
            if (TagMap.ContainsKey(tt.Key))
            {
                return;
            }

            TagMap[tt.Key] = tt;
        }

        public void DeleteTagGroup(string key)
        {
            TagMap.TryRemove(key, out var _);
        }

        public void ChangeTagName(string oldKey, string newKey)
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

        public TagGroup SetPrimary(string key, string primaryKey)
        {
            var tag = TagMap.GetValueOrDefault(key);
            var primary = TagMap.GetValueOrDefault(primaryKey);
            if (tag == null || primary == null)
            {
                return null;
            }
            tag.PrimaryId = primaryKey;
            tag.Primary = primary;

            primary.AddChild(tag);
            primary.Count += tag.Count;
            tag.Count = 0;
            return tag;
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
