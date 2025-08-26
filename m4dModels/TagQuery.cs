using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace m4dModels
{
    /// <summary>
    /// Encapsulates tag management logic for filters.
    /// </summary>
    public class TagQuery
    {
        private class TagClass(string name, bool isSongTag = true, bool isDanceTag = true)
        {
            public string Name { get; } = name;
            public bool IsSongTag { get; } = isSongTag;
            public bool IsDanceTag { get; } = isDanceTag;
        }

        private static readonly Dictionary<string, TagClass> s_tagClasses =
            new()
            {
                { "Music", new TagClass("Genre", isDanceTag: false ) },
                { "Style", new TagClass("Style", isSongTag: false) },
                { "Tempo", new TagClass("Tempo" )},
                { "Other", new TagClass("Other") }
            };

        private readonly string _tagString;
        public TagList TagList { get; }

        public TagQuery(string tagString)
        {
            _tagString = tagString ?? "";
            TagList = new TagList(_tagString);
        }

        public static string TagFromFacetId(string facetId)
        {
            if (string.IsNullOrWhiteSpace(facetId))
                return null;

            var lastPart = facetId.Contains('/') ? facetId[(facetId.LastIndexOf('/') + 1)..] : facetId;
            return TagFromClassName(lastPart.EndsWith("Tags") ? lastPart[..^4] : null);
        }

        public static string TagFromClassName(string tagClass)
        {
            return string.Equals(tagClass, "Genre", StringComparison.OrdinalIgnoreCase)
                ? "Music"
                : tagClass;
        }

        public string DescribeTags(ref string separator)
        {
            return FormatList(TagList.ExtractAdd().Strip(), "including tag", "and", ref separator) +
                     FormatList(TagList.ExtractRemove().Strip(), "excluding tag", "or", ref separator);
        }

        private static string FormatList(IList<string> list, string prefix, string connector, ref string separator)
        {
            var count = list.Count;
            if (count == 0)
                return string.Empty;

            var ret = new StringBuilder();
            if (count < 3)
            {
                ret.AppendFormat(
                    "{0}{1}{2} {3}", separator, prefix, count > 1 ? "s" : "",
                    string.Join($" {connector} ", list));
                separator = SongFilter.CommaSeparator;
            }
            else
            {
                var last = list[count - 1];
                list.RemoveAt(count - 1);
                ret.AppendFormat(
                    "{0}{1}s {2} {3} {4}", separator, prefix, string.Join(", ", list),
                    connector, last);
                separator = SongFilter.CommaSeparator;
            }
            return ret.ToString();
        }

        public string GetODataFilter(DanceMusicCoreService dms)
        {
            if (TagList.IsEmpty)
                return null;

            var tlInclude = new TagList(_tagString);
            var tlExclude = new TagList();

            if (tlInclude.IsQualified)
            {
                var temp = tlInclude;
                tlInclude = temp.ExtractAdd();
                tlExclude = temp.ExtractRemove();
            }

            var rInclude = new TagList(dms.GetTagRings(tlInclude).Select(tt => tt.Key));
            var rExclude = new TagList(dms.GetTagRings(tlExclude).Select(tt => tt.Key));

            var sb = new StringBuilder();

            foreach (var tp in s_tagClasses)
            {
                var tagClass = tp.Value;
                HandleFilterClass(sb, rInclude, tp.Key, tagClass.Name,
                    tagClass.IsSongTag ? "{0}Tags/any(t: t eq '{1}')" : null,
                    tagClass.IsDanceTag ? "dance_ALL/{0}Tags/any(t: t eq '{1}')" : null);
                HandleFilterClass(sb, rExclude, tp.Key, tagClass.Name,
                    tagClass.IsSongTag ? "{0}Tags/all(t: t ne '{1}')" : null,
                    tagClass.IsDanceTag ? "dance_ALL/{0}Tags/all(t: t ne '{1}')" : null);
            }

            return sb.ToString();
        }

        private static void HandleFilterClass(
            StringBuilder sb, TagList tags, string tagClass, string tagName, string songFormat, string danceFormat)
        {
            var filtered = tags.Filter(tagClass);
            if (filtered.IsEmpty)
                return;

            foreach (var t in filtered.StripType())
            {
                if (sb.Length > 0)
                    sb.Append(" and ");

                var tt = t.Replace(@"'", @"''");

                if (songFormat != null && danceFormat != null)
                {
                    sb.Append("(");
                    sb.AppendFormat(songFormat, tagName, tt);
                    sb.Append(" or ");
                    sb.AppendFormat(danceFormat, tagName, tt);
                    sb.Append(")");
                }
                else if (songFormat != null)
                {
                    sb.AppendFormat(songFormat, tagName, tt);
                }
                else if (danceFormat != null)
                {
                    sb.AppendFormat(danceFormat, tagName, tt);
                }
            }
        }
    }
}
