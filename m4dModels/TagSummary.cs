using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using Newtonsoft.Json;
using FacetResults =
    System.Collections.Generic.IDictionary<string, System.Collections.Generic.IList<
        Microsoft.Azure.Search.Models.FacetResult>>;


namespace m4dModels
{
    // TagSummary is serialized as a string of the form
    //  Tag0[:Count0]|Tag1[:Count1]...TagN[:CountN]
    //  where Tags are in alphabetical order
    [ComplexType]
    [JsonConverter(typeof(ToStringJsonConverter))]
    public class TagSummary
    {
        #region Properties

        public string Summary { get; set; }
        public IList<TagCount> Tags => Parse(Summary);

        public int TagCount(string name)
        {
            var tc = Tags.FirstOrDefault(
                t =>
                    string.Equals(t.Value, name, StringComparison.InvariantCultureIgnoreCase));
            return tc?.Count ?? 0;
        }

        public HashSet<string> GetTagSet(string type)
        {
            var tags = new HashSet<string>();

            foreach (var tag in Tags)
            {
                var rg = tag.Value.Split(':');

                if (rg[1] == type)
                {
                    tags.Add(rg[0].ToLower());
                }
            }

            return tags;
        }

        public bool IsEmpty => string.IsNullOrWhiteSpace(Summary);

        #endregion

        #region Constructors

        public TagSummary()
        {
            Summary = "";
        }

        public TagSummary(string serialized)
        {
            // Normalize the tags summary by pushing it through parse/deserialize
            Summary = Serialize(Parse(serialized));
        }

        public TagSummary(FacetResults facets, IReadOnlyDictionary<string, TagGroup> tagMap)
        {
            Summary = Serialize(
                Parse(
                    string.Join(
                        "|",
                        facets.Keys.Select(
                            key => string.Join(
                                "|",
                                facets[key].Select(
                                        f => MassageTag(f.Value as string, key, f.Count, tagMap))
                                    .ToList())))));
        }

        private static string MassageTag(string tvalue, string ttype, long? count,
            IReadOnlyDictionary<string, TagGroup> tagMap)
        {
            var key =
                $"{tvalue}:{SongFilter.TagClassFromName(ttype.Substring(0, ttype.Length - 4))}";
            TagGroup tt;
            if (tagMap.TryGetValue(key.ToLower(), out tt))
            {
                key = tt.Key;
            }

            return $"{key}:{count}";
        }

        public TagSummary(IEnumerable<TagCount> tags)
        {
            Summary = Serialize(tags);
        }

        #endregion

        #region Operators

        public override string ToString()
        {
            return Summary;
        }

        public void Clean()
        {
            Summary = string.Empty;
        }

        public bool HasTag(string tag)
        {
            var idx = Summary.IndexOf(tag, StringComparison.OrdinalIgnoreCase);
            if (idx == -1)
            {
                return false;
            }

            var tl = tag.Length;
            return (idx == 0 || Summary[idx - 1] == '|') &&
                (Summary[idx + tl] == ':' || idx == Summary.Length - tl ||
                    Summary[idx + tl] == '|');
        }

        public void ChangeTags(TagList added, TagList removed)
        {
            var tags = Tags;

            if (added != null)
            {
                foreach (var s in added.Tags)
                {
                    var tc = tags.FirstOrDefault(
                        t =>
                            string.Equals(t.Value, s, StringComparison.InvariantCultureIgnoreCase));
                    if (tc == null)
                    {
                        tc = new TagCount(s, 0);

                        tags.Add(tc);
                    }

                    tc.Count += 1;
                }
            }

            if (removed != null)
            {
                foreach (var tc in removed.Tags.Select(
                        s => tags.FirstOrDefault(
                            t => string.Equals(
                                t.Value, s,
                                StringComparison.InvariantCultureIgnoreCase)))
                    .Where(tc => tc != null))
                {
                    tc.Count -= 1;
                    if (tc.Count <= 0)
                    {
                        tags.Remove(tc);
                    }
                }
            }

            Summary = Serialize(tags);
        }

        public void DeleteTag(TagCount tag)
        {
            var tags = Tags;
            var old = Tags.FirstOrDefault(t => t.Value == tag.Value);
            if (old == null)
            {
                return;
            }

            tags.Remove(old);
            Summary = Serialize(tags);
        }

        #endregion

        #region Implementation

        private static List<TagCount> Parse(string serialized)
        {
            var tags = new List<TagCount>();

            if (string.IsNullOrWhiteSpace(serialized))
            {
                return tags;
            }

            var rg = serialized.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);

            tags.AddRange(rg.Select(s => new TagCount(s)));

            tags.Sort((sc1, sc2) => string.Compare(sc1.Value, sc2.Value, StringComparison.Ordinal));
            return tags;
        }

        private static string Serialize(IEnumerable<TagCount> tags)
        {
            var list = tags as List<TagCount> ?? tags.ToList();
            list.Sort((sc1, sc2) => string.Compare(sc1.Value, sc2.Value, StringComparison.Ordinal));
            return string.Join("|", list);
        }

        #endregion
    }
}
