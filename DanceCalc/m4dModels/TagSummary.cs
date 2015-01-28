using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace m4dModels
{
    // TagSummary is serialized as a string of the form
    //  Tag0[:Count0]|Tag1[:Count1]...TagN[:CountN]
    //  where Tags are in alphabetical order
    [ComplexType]
    public class TagSummary 
    {
        #region Properties
        public String Summary { get; set; }
        public IList<TagCount> Tags 
        {
            get 
            {
                return Parse(Summary);
            }
        }
        public int TagCount(string name)
        {
            TagCount tc = Tags.FirstOrDefault(t => string.Equals(t.Value, name, StringComparison.InvariantCultureIgnoreCase));
            if (tc == null)
                return 0;

            return tc.Count;
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
        #endregion

        #region Operators
        public override string ToString()
        {
            return Summary;
        }

        public void Clean()
        {
            Summary = String.Empty;
        }

        public void ChangeTags(TagList added, TagList removed)
        {
            IList<TagCount> tags = Tags;

            if (added != null)
            {
                foreach (string s in added.Tags)
                {
                    TagCount tc = tags.FirstOrDefault(t => string.Equals(t.Value, s, StringComparison.InvariantCultureIgnoreCase));
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
                foreach (string s in removed.Tags)
                {
                    TagCount tc = tags.FirstOrDefault(t => string.Equals(t.Value, s, StringComparison.InvariantCultureIgnoreCase));
                    if (tc != null)
                    {
                        tc.Count -= 1;
                        if (tc.Count <= 0)
                        {
                            tags.Remove(tc);
                        }
                    }
                }


            }

            Summary = Serialize(tags);
        }

        #endregion

        #region Implementation
        static private List<TagCount> Parse(string serialized)
        {
            List<TagCount> tags = new List<TagCount>();

            if (string.IsNullOrWhiteSpace(serialized))
            {
                return tags;
            }

            string[] rg = serialized.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string s in rg)
            {
                tags.Add(new TagCount(s));
            }

            tags.Sort((sc1,sc2)=>sc1.Value.CompareTo(sc2.Value));
            return tags;
        }

        static private string Serialize(IEnumerable<TagCount> tags)
        {
            List<TagCount> list = tags.ToList();
            list.Sort((sc1, sc2) => sc1.Value.CompareTo(sc2.Value));
            return string.Join("|", list);
        }
        #endregion
    }
}
