using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

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
            List<string> ret = new List<string>();

            IList<string> src = Tags;
            IList<string> trg = other == null ? new List<string>() : other.Tags;

            foreach (string s in src)
            {
                if (!trg.Contains(s))
                {
                    ret.Add(s);
                }
            }

            return new TagList(ret);
        }

        public TagList Add(TagList other)
        {
            List<string> ret = Tags;
            foreach (string tag in other.Tags)
            {
                if (!ret.Contains(tag))
                {
                    ret.Add(tag);
                }
            }

            return new TagList(ret);
        }
        #endregion

        #region Implementation
        static private List<string> Parse(string serialized)
        {
            List<string> tags = new List<string>();

            if (string.IsNullOrWhiteSpace(serialized))
            {
                return tags;
            }

            string[] rg = serialized.Split(new char[] { '|' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string s in rg)
            {
                tags.Add(s);
            }

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
