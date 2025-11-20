using System.Collections.Generic;
using System.Linq;

namespace m4dModels
{
    public class TagAccumulator
    {
        public static TagSummary MergeSummaries(IEnumerable<TagSummary> summaries)
        {
            var acc = new TagAccumulator();
            foreach (var summary in summaries)
            {
                acc.AddTags(summary);
            }

            return acc.TagSummary();
        }

        public TagAccumulator()
        {
            Tags = [];
        }

        public TagAccumulator(string summary) : this()
        {
            AddTags(summary);
        }

        public TagAccumulator(TagSummary summary) : this()
        {
            AddTags(summary);
        }

        public Dictionary<string, int> Tags { get; set; }

        public void AddTags(TagSummary summary)
        {
            if (summary == null)
            {
                return;
            }

            AddTags(summary.Summary);
        }

        public void AddTags(string tags)
        {
            if (string.IsNullOrWhiteSpace(tags))
            {
                return;
            }

            var ts = new TagSummary(tags);
            foreach (var tc in ts.Tags)
            {
                if (Tags.TryGetValue(tc.Value, out _))
                {
                    Tags[tc.Value] += tc.Count;
                }
                else
                {
                    Tags[tc.Value] = tc.Count;
                }
            }
        }

        public TagSummary TagSummary()
        {
            var tags = Tags.Select(tc => new TagCount(tc.Key, tc.Value)).ToList();
            return new TagSummary(tags);
        }

        public override string ToString()
        {
            return TagSummary().Summary;
        }
    }
}
