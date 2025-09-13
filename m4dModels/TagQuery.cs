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

        // Indicates if song-tag queries should also include dances_ALL tags (via ^ prefix)
        public bool IncludeDancesAllInSongTags { get; }

        public TagQuery(string tagString)
        {
            _tagString = tagString ?? "";
            if (_tagString.StartsWith("^"))
            {
                IncludeDancesAllInSongTags = true;
                _tagString = _tagString[1..];
            }
            else
            {
                IncludeDancesAllInSongTags = false;
            }
            TagList = new TagList(_tagString);
        }

        public bool IsEmpty => TagList.IsEmpty;

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

        public string Description(ref string separator)
        {
            var filteredTagList = TagList.FilterCategories(new[] { "Dances" });
            var inc = FormatList(filteredTagList.ExtractAdd().Strip(), "including tag", "and", ref separator);
            var exc = FormatList(filteredTagList.ExtractRemove().Strip(), "excluding tag", "or", ref separator);

            // When includeDancesAllInSongTags is true, we're searching both song and dance tags
            if (IncludeDancesAllInSongTags && (!string.IsNullOrEmpty(inc) || !string.IsNullOrEmpty(exc)))
            {
                // Replace "including tag" with "including song or dance tag"
                var modifiedInc = !string.IsNullOrEmpty(inc) ? inc.Replace("including tag", "including song or dance tag") : "";
                var modifiedExc = !string.IsNullOrEmpty(exc) ? exc.Replace("excluding tag", "excluding song or dance tag") : "";
                return string.Concat(modifiedInc, modifiedExc).Trim();
            }

            return string.Concat(inc, exc).Trim();
        }

        public string ShortDescription(ref string separator)
        {
            var filteredTagList = TagList.FilterCategories(new[] { "Dances" });
            var inc = FormatList(filteredTagList.ExtractAdd().Strip(), "inc", "and", ref separator);
            var exc = FormatList(filteredTagList.ExtractRemove().Strip(), "excl", "or", ref separator);

            // When includeDancesAllInSongTags is true, add "song+dance" prefix
            if (IncludeDancesAllInSongTags && (!string.IsNullOrEmpty(inc) || !string.IsNullOrEmpty(exc)))
            {
                var prefix = "song+dance ";
                return string.Concat(prefix, inc, exc).Trim();
            }

            return string.Concat(inc, exc).Trim();
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

        /// <summary>
        /// Returns an OData filter for tags, targeting a specific dance field (e.g., "dance_{DanceId}").
        /// If danceField is null, uses the global/dance_ALL field.
        /// </summary>
        public string GetODataFilterForDanceField(string danceField = null, DanceMusicCoreService dms = null)
        {
            return BuildODataFilter(
                tagString: _tagString,
                expandTagRings: dms != null,
                dms: dms,
                danceField: danceField,
                includeDancesAllInSongTags: IncludeDancesAllInSongTags
            );
        }

        // Returns an OData filter for tags, using the global/dance_ALL field and tag ring expansion.
        public string GetODataFilter(DanceMusicCoreService dms)
        {
            return BuildODataFilter(
                tagString: _tagString,
                expandTagRings: true,
                dms: dms,
                danceField: null,
                includeDancesAllInSongTags: IncludeDancesAllInSongTags
            );
        }

        // Cleaned up logic: for song-tag queries, if includeDancesAllInSongTags is false, danceFormat and danceFormatExclude are null
        private string BuildODataFilter(
            string tagString,
            bool expandTagRings,
            DanceMusicCoreService dms,
            string danceField,
            bool includeDancesAllInSongTags = false)
        {
            var tagList = new TagList(tagString);
            if (tagList.IsEmpty)
                return null;

            var tlInclude = tagList;
            var tlExclude = new TagList();

            if (tlInclude.IsQualified)
            {
                var temp = tlInclude;
                tlInclude = temp.ExtractAdd();
                tlExclude = temp.ExtractRemove();
            }

            if (expandTagRings && dms != null)
            {
                tlInclude = new TagList(dms.GetTagRings(tlInclude).Select(tt => tt.Key));
                tlExclude = new TagList(dms.GetTagRings(tlExclude).Select(tt => tt.Key));
            }

            var sb = new StringBuilder();

            foreach (var tp in s_tagClasses)
            {
                var tagClass = tp.Value;

                string songFormat = null, songFormatExclude = null, danceFormat = null, danceFormatExclude = null;

                if (danceField != null)
                {
                    // Dance-specific query: always use danceField, never song tags
                    danceFormat = $"{danceField}/{{0}}Tags/any(t: t eq '{{1}}')";
                    danceFormatExclude = $"{danceField}/{{0}}Tags/all(t: t ne '{{1}}')";
                }
                else if (tagClass.IsSongTag)
                {
                    // Song-tag query
                    songFormat = "{0}Tags/any(t: t eq '{1}')";
                    songFormatExclude = "{0}Tags/all(t: t ne '{1}')";
                    if (tagClass.IsDanceTag && includeDancesAllInSongTags)
                    {
                        // Also include dances_ALL as an OR for inclusion, AND for exclusion
                        danceFormat = tagClass.IsDanceTag ? "dance_ALL/{0}Tags/any(t: t eq '{1}')" : null;
                        danceFormatExclude = tagClass.IsDanceTag ? "dance_ALL/{0}Tags/all(t: t ne '{1}')" : null;
                    }
                }
                else if (tagClass.IsDanceTag)
                {
                    // Only dance_ALL for dance tags
                    danceFormat = "dance_ALL/{0}Tags/any(t: t eq '{1}')";
                    danceFormatExclude = "dance_ALL/{0}Tags/all(t: t ne '{1}')";
                }

                // Inclusion
                HandleFilterClass(sb, tlInclude, tp.Key, tagClass.Name, songFormat, danceFormat);
                // Exclusion
                HandleFilterClass(sb, tlExclude, tp.Key, tagClass.Name, songFormatExclude, danceFormatExclude);
            }

            return sb.Length == 0 ? null : sb.ToString();
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
