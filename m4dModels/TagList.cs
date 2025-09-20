using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace m4dModels
{
    [ComplexType]
    public partial class TagList
    {
        #region Properties

        public string Summary { get; set; }
        public List<string> Tags => Parse(Summary);
        internal string Description => string.Join(";", Tags.Where(t => !t.EndsWith(":Dance")).Select(t => new TagCount(t).Description));
        #endregion

        #region Constructors

        public TagList()
        {
        }

        public TagList(string serialized)
        {
            if (!string.IsNullOrEmpty(serialized) && serialized.StartsWith("^"))
                serialized = serialized[1..];
            // Normalize the tags list by pushing it through parse/deserialize
            Summary = Serialize(Parse(serialized));
        }

        public TagList(IEnumerable<string> tags)
        {
            var list = tags.ToList();
            list.Sort();
            Summary = Serialize(list);
        }

        public bool IsQualified => string.IsNullOrWhiteSpace(Summary) || Summary[0] == '+' ||
            Summary[0] == '-';

        public bool IsEmpty => string.IsNullOrWhiteSpace(Summary);

        #endregion

        #region Operators

        public override string ToString()
        {
            return Summary;
        }

        // Subtract 'other' from this list - get's the tags in this list that aren't in delta
        // This is resilient to qualifiers (subtract 'tag:type' will remove any of 'tag:type', '-tag:type', '+tag:type'
        public TagList Subtract(TagList other)
        {
            IList<string> trg = [];
            if (other != null)
            {
                trg = other.IsQualified ? other.StripQualifier() : other.Tags;
            }

            return new TagList([.. Tags.Where(s => !trg.Contains(TrimQualifier(s)))]);
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

        public TagList Add(string tag)
        {
            var n = new TagList(tag);
            return !n.IsEmpty ? Add(n) : this;
        }

        public TagList Filter(string tagType)
        {
            var filtered = Tags.Where(tag => tag.EndsWith(":" + tagType)).ToList();
            return new TagList(filtered);
        }

        public TagList FilterCategories(string[] categories)
        {
            var cats = categories.Select(cat => cat.ToLowerInvariant()).ToArray();
            var filteredTags = Tags.Where(tag =>
            {
                var parts = tag.Split(':');
                if (parts.Length < 2) return true; // Keep tags without category
                var category = TrimQualifier(parts[1]).ToLowerInvariant();
                return !cats.Contains(category);
            }).ToList();
            return new TagList(filteredTags);
        }

        public TagList ExtractAdd()
        {
            return IsQualified ? Extract('+') : this;
        }

        public TagList ExtractRemove()
        {
            return IsQualified ? Extract('-') : new TagList();
        }

        public TagList ExtractPrefixed(char c)
        {
            return string.IsNullOrWhiteSpace(Summary)
                ? new TagList()
                : new TagList(
                    [.. Tags.Where(tag => tag[0] == c).Select(tag => tag[1..])]);
        }

        public TagList ExtractNotPrefixed(char c)
        {
            return string.IsNullOrWhiteSpace(Summary)
                ? new TagList()
                : new TagList([.. Tags.Where(tag => tag[0] != c)]);
        }

        private TagList Extract(char c)
        {
            return !IsQualified ? throw new InvalidConstraintException() : ExtractPrefixed(c);
        }

        public IList<string> StripType()
        {
            return Summary == null
                ? []
                : Tags.Select(StripTagType).ToList();
        }

        public IList<string> StripQualifier()
        {
            return [.. Tags.Select(TrimQualifier)];
        }

        public IList<string> Strip()
        {
            return Summary != null
                ? Tags.Select(tag => TrimQualifier(StripTagType(tag))).ToList()
                : [];
        }

        private static string StripTagType(string tag)
        {
            return tag.Contains(':') ? tag[..tag.IndexOf(':')] : tag;
        }

        public TagList AddQualifier(char q)
        {
            var qual = new string(q, 1);

            return new TagList([.. Tags.Select(tag => qual + TrimQualifier(tag))]);
        }

        public TagList AddMissingQualifier(char q)
        {
            return IsQualified ? this : AddQualifier(q);
        }

        public TagList Normalize(string category)
        {
            var result = new List<string>();
            foreach (var tag in Tags)
            {
                var fields = tag.Split(':');
                var fullTag = tag;
                if (fields.Length == 1)
                {
                    fullTag = fields[0] + ":" + category;
                }

                result.Add(fullTag);
            }

            return new TagList(result);
        }

        public IList<string> ToStringList()
        {
            return Parse(Summary);
        }

        public TagList RemoveDuplicates(DanceMusicCoreService dms)
        {
            var tags = Tags;
            var seen = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (var tag in tags)
            {
                var tt = dms.GetTagRing(tag);
                if (seen.TryGetValue(tt.Key, out var others))
                {
                    others.Add(tag);
                }
                else
                {
                    seen.Add(tt.Key, [tag]);
                }
            }

            var remove = new List<string>();
            foreach (var pair in seen.Where(p => p.Value.Count > 1))
            {
                var dups = pair.Value;
                var idx = dups.IndexOf(pair.Key);
                dups.RemoveAt(idx == -1 ? 0 : idx);
                remove.AddRange(dups);
            }

            foreach (var tag in remove)
            {
                tags.Remove(tag);
            }

            return new TagList(tags);
        }

        private static readonly HashSet<string> s_validClasses =
            ["dance", "music", "style", "tempo", "other"];

        public TagList FixBadCategory()
        {
            var tags = Parse(Summary, false);

            var list = new TagList();
            foreach (var tag in tags)
            {
                if (string.Equals(tag, "halloween:othe", StringComparison.OrdinalIgnoreCase))
                {
                    list = list.Add("Halloween:Other");
                    continue;
                }

                if (string.Equals(tag, "hoiday:other"))
                {
                    list = list.Add("Holiday:Other");
                    continue;
                }

                var parts = tag.Split(':');
                if (parts.Length != 2)
                {
                    list = list.Add($"{parts[0]}:Other");
                }
                else
                {
                    var value = parts[0].Trim();
                    var category = parts[1].Trim();
                    if (s_validClasses.Contains(category.ToLower()))
                    {
                        list = list.Add($"{value}:{category}");
                    }
                    else
                    {
                        Trace.WriteLine($"Tag: '{tag}'");
                        list = list.Add($"{value}:Other");
                        list = list.Add($"{category}:Music");
                    }
                }
            }

            return list;
        }

        public TagList ConvertToPrimary(DanceMusicCoreService dms)
        {
            return new TagList(
                Tags.Select(
                    t =>
                    {
                        var r = dms.GetTagRing(t);
                        return r == null ? t : r.Key;
                    }));
        }

        public static string NormalizeTag(string tag)
        {
            var fields = tag.Split(':').ToList();

            if (fields.Count < 2)
            {
                fields.Add("Other");
            }
            else if (!char.IsUpper(fields[1][0]))
            {
                fields[1] = fields[1][..1].ToUpper() + fields[1][1..];
            }

            return string.Join(":", fields);
        }

        public static string Concatenate(string tags1, string tags2)
        {
            return string.IsNullOrWhiteSpace(tags1) ? tags2 :
                string.IsNullOrWhiteSpace(tags2) ? tags1 :
                new TagList($"{tags1}|{tags2}").ToString();
        }

        #endregion

        #region Implementation

        public static string Clean(string value)
        {
            return TagChars().Replace(value, "");
        }

        [GeneratedRegex(@"[^\p{L}\d()'& ]", RegexOptions.Multiline | RegexOptions.Compiled)]
        private static partial Regex TagChars();

        private static List<string> Parse(string serialized, bool trim = true)
        {
            var tags = new List<string>();

            if (string.IsNullOrWhiteSpace(serialized))
            {
                return tags;
            }

            tags.AddRange(
                serialized
                    .Split(['|'], StringSplitOptions.RemoveEmptyEntries)
                    .Select(tag => trim ? tag.Trim() : tag));

            tags.Sort();

            return tags;
        }

        private static string Serialize(IEnumerable<string> tags)
        {
            return string.Join("|", tags.Select(t => t.Trim()));
        }

        private static string TrimQualifier(string tag)
        {
            return string.IsNullOrWhiteSpace(tag) ? tag : tag[0] == '+' || tag[0] == '-' ? tag[1..] : tag;
        }

        private static readonly TextInfo s_ti = new CultureInfo("en-US", false).TextInfo;

        #endregion
    }
}
