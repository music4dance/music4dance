using Newtonsoft.Json;

using System.ComponentModel.DataAnnotations.Schema;

using FacetResults =
    System.Collections.Generic.IDictionary<string, System.Collections.Generic.IList<
        Azure.Search.Documents.Models.FacetResult>>;


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
                    _ = tags.Add(rg[0]);
                }
            }

            return tags;
        }

        public string Description => string.Join(";", Tags.Where(t => t.TagClass != "Dance").Select(t => t.Description));

        public bool IsEmpty => string.IsNullOrWhiteSpace(Summary);

        #endregion

        #region Constructors

        public TagSummary()
        {
            Summary = "";
        }

        public TagSummary(string serialized, IReadOnlyDictionary<string, TagGroup> tagMap = null)
        {
            // Normalize the tags summary by pushing it through parse/deserialize
            var tags = Parse(serialized);
            if (tagMap != null)
            {
                tags = [.. tags.Select(t => MassageeTag(t, tagMap))];
            }
            Summary = Serialize(tags);
        }

        public TagSummary(TagSummary tagSummary, IReadOnlyDictionary<string, TagGroup> tagMap = null)
            : this(tagSummary.Summary, tagMap)
        {
        }

        public TagSummary(FacetResults facets, IReadOnlyDictionary<string, TagGroup> tagMap)
        {
            Dictionary<string, long> tags = [];

            foreach (var facet in facets)
            {
                if (facet.Value == null || facet.Value.Count == 0)
                {
                    continue;
                }
                foreach (var f in facet.Value)
                {
                    if (f.Value == null || f.Count == null || f.Count == 0)
                    {
                        continue;
                    }
                    var tag = MassageTag(f.Value as string, facet.Key, tagMap);
                    if (tags.TryGetValue(tag, out var count))
                    {
                        tags[tag] = count + f.Count ?? 0;
                    }
                    else
                    {
                        tags[tag] = f.Count ?? 0;
                    }
                }
            }

            Summary = Serialize(tags.Select(kvp => new TagCount(kvp.Key, (int)kvp.Value)).ToList());
        }

        private static string MassageTag(string tvalue, string ttype,
            IReadOnlyDictionary<string, TagGroup> tagMap)
        {
            const string prefix = "dances_ALL/";
            if (ttype.StartsWith(prefix))
            {
                ttype = ttype[prefix.Length..];
            }
            var key =
                $"{tvalue}:{TagQuery.TagFromFacetId(ttype)}";
            if (tagMap.TryGetValue(key.ToLower(), out var tt))
            {
                key = tt.Key;
            }

            return key;
        }

        private static TagCount MassageeTag(TagCount tag, IReadOnlyDictionary<string, TagGroup> tagMap)
        {
            return tagMap.TryGetValue(tag.Value, out var tt)
                ? new TagCount(tt.Key, tag.Count)
                : tag;
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
                        _ = tags.Remove(tc);
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

            _ = tags.Remove(old);
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

            var rg = serialized.Split(['|'], StringSplitOptions.RemoveEmptyEntries);

            tags.AddRange(rg.Select(s => new TagCount(s)));

            tags.Sort((sc1, sc2) => string.Compare(sc1.Value, sc2.Value, StringComparison.Ordinal));
            return tags;
        }

        private static string Serialize(IEnumerable<TagCount> tags)
        {
            var list = tags as List<TagCount> ?? [.. tags];
            list.Sort((sc1, sc2) => string.Compare(sc1.Value, sc2.Value, StringComparison.Ordinal));
            return string.Join("|", list);
        }
        #endregion
    }
}
