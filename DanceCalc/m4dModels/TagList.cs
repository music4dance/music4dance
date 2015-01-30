using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace m4dModels
{
    [ComplexType]
    public class TagList
    {
        #region Properties
        public String Summary { get; set; }
        public List<string> Tags
        {
            get
            {
                return Parse(Summary);
            }
        }
        #endregion

        #region Constructors

        public TagList()
        {
        }

        public TagList(string serialized)
        {
            // Normalize the tags list by pushing it through parse/deserialize
            Summary = Serialize(Parse(serialized));
        }

        public TagList(List<string> tags)
        {
            tags.Sort();
            Summary = Serialize(tags);
        }

        #endregion

        #region Operators
        public override string ToString()
        {
            return Summary;
        }

        // Subtract 'other' from this list - get's the tags in this list that aren't in delta
        public TagList Subtract(TagList other)
        {
            var trg = other == null ? new List<string>() : other.Tags;

            return new TagList(Tags.Where(s => !trg.Contains(s)).ToList());
        }

        public TagList Add(TagList other)
        {
            var ret = Tags;
            foreach (var tag in other.Tags.Where(tag => !ret.Contains(tag)))
            {
                ret.Add(tag);
            }

            return new TagList(ret);
        }

        public TagList Filter(string tagType)
        {
            var filtered = Tags.Where(tag => tag.EndsWith(":" + tagType)).ToList();
            return new TagList(filtered);
        }

        public IList<string> StripType()
        {
            var tags = Tags.Select(tag => tag.Substring(0, tag.IndexOf(':'))).ToList();
            return tags;
        }

        #endregion

        #region Implementation
        static private List<string> Parse(string serialized)
        {
            var tags = new List<string>();

            if (string.IsNullOrWhiteSpace(serialized))
            {
                return tags;
            }

            tags.AddRange(serialized.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries));

            tags.Sort();

            return tags;
        }

        static private string Serialize(List<string> tags)
        {
            return string.Join("|", tags);
        }
        #endregion
    }
}
