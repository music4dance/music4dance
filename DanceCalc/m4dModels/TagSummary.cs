using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity.Core.Common.CommandTrees;
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
            var tc = Tags.FirstOrDefault(t => string.Equals(t.Value, name, StringComparison.InvariantCultureIgnoreCase));
            return tc == null ? 0 : tc.Count;
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

        public bool IsEmpty
        {
            get { return string.IsNullOrWhiteSpace(Summary); }
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
            Summary = String.Empty;
        }

        public void ChangeTags(TagList added, TagList removed)
        {
            var tags = Tags;

            if (added != null)
            {
                foreach (string s in added.Tags)
                {
                    var tc = tags.FirstOrDefault(t => string.Equals(t.Value, s, StringComparison.InvariantCultureIgnoreCase));
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
                    var tc = tags.FirstOrDefault(t => string.Equals(t.Value, s, StringComparison.InvariantCultureIgnoreCase));
                    if (tc == null) continue;

                    tc.Count -= 1;
                    if (tc.Count <= 0)
                    {
                        tags.Remove(tc);
                    }
                }
            }

            Summary = Serialize(tags);
        }

        #endregion

        #region Implementation
        static private List<TagCount> Parse(string serialized)
        {
            var tags = new List<TagCount>();

            if (string.IsNullOrWhiteSpace(serialized))
            {
                return tags;
            }

            var rg = serialized.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);

            tags.AddRange(rg.Select(s => new TagCount(s)));

            tags.Sort((sc1,sc2)=>String.Compare(sc1.Value, sc2.Value, StringComparison.Ordinal));
            return tags;
        }

        static private string Serialize(IEnumerable<TagCount> tags)
        {
            var list = tags as List<TagCount> ?? tags.ToList();
            list.Sort((sc1, sc2) => String.Compare(sc1.Value, sc2.Value, StringComparison.Ordinal));
            return string.Join("|", list);
        }
        #endregion
    }
}
