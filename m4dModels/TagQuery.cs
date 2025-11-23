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

        // Indicates if dance_ALL tags should be excluded (default: false). '^' prefix means exclude dance_ALL tags.
        public bool ExcludeDanceTags { get; }

        public TagQuery(string tagString)
        {
            _tagString = tagString ?? "";
            if (_tagString.StartsWith("^"))
            {
                ExcludeDanceTags = true;
                _tagString = _tagString[1..];
            }
            else
            {
                ExcludeDanceTags = false;
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
            var filteredTagList = TagList.FilterCategories(["Dances"]);
            // If excluding dance tags, use 'song tag', else just 'tag'
            var inc = FormatList(filteredTagList.ExtractAdd().Strip(), ExcludeDanceTags ? "including song tag" : "including tag", "and", ref separator);
            var exc = FormatList(filteredTagList.ExtractRemove().Strip(), ExcludeDanceTags ? "excluding song tag" : "excluding tag", "or", ref separator);
            return string.Concat(inc, exc).TrimEnd();
        }

        public string ShortDescription(ref string separator)
        {
            var filteredTagList = TagList.FilterCategories(["Dances"]);
            // If excluding dance tags, use 'song' prefix, else no prefix
            var inc = FormatList(filteredTagList.ExtractAdd().Strip(), ExcludeDanceTags ? "song inc" : "inc", "and", ref separator);
            var exc = FormatList(filteredTagList.ExtractRemove().Strip(), ExcludeDanceTags ? "song excl" : "excl", "or", ref separator);
            return string.Concat(inc, exc).TrimEnd();
        }

        private static string FormatList(IList<string> list, string prefix, string connector, ref string separator)
        {
            var count = list.Count;
            if (count == 0)
                return string.Empty;

            var ret = new StringBuilder();
            if (count < 3)
            {
                _ = ret.AppendFormat(
                    "{0}{1}{2} {3}", separator, prefix, count > 1 ? "s" : "",
                    string.Join($" {connector} ", list));
                separator = SongFilter.CommaSeparator;
            }
            else
            {
                var last = list[count - 1];
                list.RemoveAt(count - 1);
                _ = ret.AppendFormat(
                    "{0}{1}s {2} {3} {4}", separator, prefix, string.Join(", ", list),
                    connector, last);
                separator = SongFilter.CommaSeparator;
            }
            return ret.ToString();
        }

        /// <summary>
        /// Returns an OData filter for tags, targeting a specific dance field (e.g., "dance_{DanceId}").
        /// If danceField is null, uses the global/dance_ALL field.
        /// The excludeDanceTags parameter controls whether dance_ALL tags are excluded (true) or included (false, default).
        /// </summary>
        public string GetODataFilterForDanceField(string danceField = null, DanceMusicCoreService dms = null)
        {
            return BuildODataFilter(
                tagString: _tagString,
                expandTagRings: dms != null,
                dms: dms,
                danceField: danceField,
                excludeDanceTags: ExcludeDanceTags
            );
        }

        // Returns an OData filter for tags, using the global/dance_ALL field and tag ring expansion.
        // The excludeDanceTags parameter controls whether dance_ALL tags are excluded (true) or included (false, default).
        public string GetODataFilter(DanceMusicCoreService dms)
        {
            return BuildODataFilter(
                tagString: _tagString,
                expandTagRings: true,
                dms: dms,
                danceField: null,
                excludeDanceTags: ExcludeDanceTags
            );
        }

        // For song-tag queries, if excludeDanceTags is true, danceFormat and danceFormatExclude are null (so only song tags are used).
        // If excludeDanceTags is false (default), both song tags and dance_ALL tags are included in the filter logic.
        private string BuildODataFilter(
            string tagString,
            bool expandTagRings,
            DanceMusicCoreService dms,
            string danceField,
            bool excludeDanceTags = false)
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
                    if (tagClass.IsDanceTag && !excludeDanceTags)
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
                    _ = sb.Append(" and ");

                var tt = t.Replace(@"'", @"''");

                if (songFormat != null && danceFormat != null)
                {
                    _ = sb.Append("(");
                    _ = sb.AppendFormat(songFormat, tagName, tt);
                    _ = sb.Append(" or ");
                    _ = sb.AppendFormat(danceFormat, tagName, tt);
                    _ = sb.Append(")");
                }
                else if (songFormat != null)
                {
                    _ = sb.AppendFormat(songFormat, tagName, tt);
                }
                else if (danceFormat != null)
                {
                    _ = sb.AppendFormat(danceFormat, tagName, tt);
                }
            }
        }
    }
}
