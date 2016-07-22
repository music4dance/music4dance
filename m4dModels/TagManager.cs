using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.Search.Models;

namespace m4dModels
{
    public class TagManager
    {
        public TagManager(IEnumerable<TagType> tagTypes)
        {
            TagTypes = (tagTypes as List<TagType>)?.ToList();
            FixupTags();
        }

        public TagManager(DanceMusicService dms, string source = "default")
        {
            TagTypes = dms.TagTypes.OrderBy(t => t.Key).ToList();

            var facets = dms.GetTagFacets("GenreTags,StyleTags,TempoTags,OtherTags", 500, source);

            TagMap = TagTypes.ToDictionary(tt => tt.Key.ToLower());

            foreach (var facet in facets)
            {
                var id = SongFilter.TagClassFromName(facet.Key.Substring(0, facet.Key.Length - 4)).ToLower();
                IndexFacet(facet.Value, id);
            }
        }

        public List<TagType> TagTypes { get; set; }

        public Dictionary<string, TagType> TagMap { get; private set; }

        public void FixupTags()
        {
            TagMap = TagTypes.ToDictionary(tt => tt.Key.ToLower());

            foreach (var tt in TagTypes.Where(tt => !string.IsNullOrEmpty(tt.PrimaryId)))
            {
                tt.Primary = TagMap[tt.PrimaryId.ToLower()];
                if (tt.Primary.Ring == null) tt.Primary.Ring = new List<TagType>();
                tt.Primary.Ring.Add(tt);
            }
        }

        public TagType FindOrCreateTagType(string tag)
        {
            lock (_queuedTags)
            {
                var tt = TagMap.GetValueOrDefault(tag) ?? _queuedTags.GetValueOrDefault(tag);
                if (tt != null) return tt;

                tt = new TagType(tag);
                _queuedTags[tt.Key] = tt;

                AddTagType(tt);
                return tt;
            }
        }

        public void AddTagType(TagType tt, bool queue = false)
        {
            TagMap[tt.Key.ToLower()] = tt;
            var index = TagTypes.BinarySearch(tt, Comparer<TagType>.Create((a, b) => string.Compare(a.Key, b.Key, StringComparison.OrdinalIgnoreCase)));
            if (index < 0) index = ~index;
            TagTypes.Insert(index, tt);
        }

        public IEnumerable<TagType> DequeueTagTypes()
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
                if (!facet.Count.HasValue) continue;

                var key = $"{facet.Value}:{category}";
                var tt = FindOrCreateTagType(key);

                tt.Count = (int)facet.Count.Value;
            }
        }

        private readonly Dictionary<string, TagType> _queuedTags = new Dictionary<string, TagType>();

    }
}
