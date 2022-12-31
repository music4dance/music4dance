using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization.Json;
using System.Text;
using AutoMapper;

namespace m4dModels
{
    public class SongFilterProfile : Profile
    {
        public SongFilterProfile()
        {
            CreateMap<SongFilter, SongFilterSparse>();
            CreateMap<SongFilterSparse, SongFilter>();
        }
    }

    public class SongFilterSparse
    {
        public int Version { get; set; }
        public string Action { get; set; }
        public string SearchString { get; set; }
        public string Dances { get; set; }
        public string SortOrder { get; set; }
        public string Purchase { get; set; }
        public string User { get; set; }
        public decimal? TempoMin { get; set; }
        public decimal? TempoMax { get; set; }
        public int? LengthMin { get; set; }
        public int? LengthMax { get; set; }
        public int? Page { get; set; }
        public string Tags { get; set; }
        public int? Level { get; set; }
    }

    [TypeConverter(typeof(SongFilterConverter))]
    public class SongFilter
    {
        private const char SubChar = '\u001a';
        private const char Separator = '-';

        private const string CommaSeparator = ", ";

        private static readonly Dictionary<string, string> s_tagClasses =
            new()
            {
                { "Music", "Genre" }, { "Style", "Style" }, { "Tempo", "Tempo" },
                { "Other", "Other" }
            };

        private static readonly List<PropertyInfo> PropertyInfo;

        private static readonly Dictionary<string,object>  AltDefaults =
            new() { {"Action", "index" }, {"Dances", "all" },  {"Page", 1 } };

        private readonly string _subStr = new(SubChar, 1);
        private string _action;

        // TODO: Move Field Ids into SongIndex.
        //  Continue to abstract out parts of song index that are specific to the Flat Schema
        //  Create another child class for StructuredIndex
        //  Make the index definition include the type & allow that to be selectable via admin portal
        //  Move to a factory system based on this boolean
        //  This should work for songfitler & its descendents
        public static bool StructuredSchema { get; set; }

        static SongFilter()
        {
            var info =
                typeof(SongFilter).GetProperties(BindingFlags.Public | BindingFlags.Instance);
            PropertyInfo = info.Where(p => p.CanRead && p.CanWrite).ToList();
        }

        public SongFilter()
        {
            Action = "Index";
        }

        // TODO: Should we also enable a length column when filtering/sorting by length???

        // action-dances-sortorder-searchstring-purchase-user-tempomin-tempomax[-lengthmin-lengthmax]-page-tags-level
        public SongFilter(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            var cells = SplitFilter(value);

            int idx = 0;

            var versionString = ReadCell(cells, 0);

            Version = string.Equals(versionString, "v2", StringComparison.OrdinalIgnoreCase) ? 2 : 1;
            if (Version > 1)
            {
                idx += 1;
            }
            Action = ReadCell(cells, idx++);
            Dances = ReadCell(cells, idx++);
            SortOrder = ReadCell(cells, idx++);
            SearchString = ReadCell(cells, idx++);
            Purchase = ReadCell(cells, idx++);
            User = ReadCell(cells, idx++);
            TempoMin = ReadDecimal(cells, idx++);
            TempoMax = ReadDecimal(cells, idx++);
            if (Version > 1)
            {
                LengthMin = ReadInt(cells, idx++);
                LengthMax = ReadInt(cells, idx++);
            }
            Page = ReadInt(cells, idx++);
            Tags = ReadCell(cells, idx++);
            Level = ReadInt(cells, idx++);

            if (!IsRaw && Action.StartsWith("azure", StringComparison.OrdinalIgnoreCase))
            {
                Action = Action.Contains("advanced", StringComparison.OrdinalIgnoreCase)
                    ? "Advanced"
                    : "Index";
            }
        }

        public SongFilter Clone()
        {
            return new SongFilter(ToString());
        }

        private List<string> SplitFilter(string input)
        {
            return input
                .Replace(@"\-", _subStr)
                .Split('-')
                .Select(s => s.Trim().Replace(_subStr, "-"))
                .ToList();
        }

        private static string ReadCell(List<string> cells, int index)
        {
            var ret = cells.Count > index ? cells[index] : null;
            return ret = string.IsNullOrWhiteSpace(ret) || ret == "." ? null : ret;
        }

        private static decimal? ReadDecimal(List<string> cells, int index)
        {
            var s = ReadCell(cells, index);
            if (!string.IsNullOrWhiteSpace(s) && decimal.TryParse(s, out decimal d))
            {
                return d;
            }
            return null;
        }

        private static int? ReadInt(List<string> cells, int index)
        {
            var s = ReadCell(cells, index);
            if (!string.IsNullOrWhiteSpace(s) && int.TryParse(s, out int i))
            {
                return i;
            }
            return null;
        }

        public SongFilter(RawSearch raw)
        {
            Action = "azure+raw+" + (raw.IsLucene ? "lucene" : "");
            SearchString = raw.SearchText;
            Dances = raw.ODataFilter;
            SortOrder = raw.SortFields;
            Purchase = raw.Description;
            User = raw.SearchFields;
            Page = raw.Page;
            Tags = raw.Flags;
            Level = raw.CruftFilter == m4dModels.CruftFilter.NoCruft
                ? null
                : (int?)raw.CruftFilter;
        }

        public SongFilter(string action, RawSearch raw) : this(raw)
        {
            Action = action;
        }

        public int Version { get; set; }
        public string Action
        {
            get => _action ?? "Index";
            set => _action = value;
        }

        public string Dances { get; set; }
        public string SortOrder { get; set; }
        public string SearchString { get; set; }
        public string Purchase { get; set; }
        public string User { get; set; }
        public decimal? TempoMin { get; set; }
        public decimal? TempoMax { get; set; }
        public int? LengthMin { get; set; }
        public int? LengthMax { get; set; }
        public int? Page { get; set; }
        public string Tags { get; set; }
        public int? Level { get; set; }

        public string TargetAction => IsAzure ? "azuresearch" : Action;

        public string VueName
        {
            get
            {
                var action = Action.Equals("Advanced", StringComparison.OrdinalIgnoreCase)
                    || Action.StartsWith("azure+raw", StringComparison.OrdinalIgnoreCase)
                    || Action.Equals("MergeCandidates")
                        ? "index"
                        : Action.ToLowerInvariant();

                switch (action)
                {
                    case "newmusic":
                        return "new-music";
                    case "holidaymusic":
                        return "holiday-music";
                    case "index":
                    default:
                        return "song-index";
                }
            }
        }
        public bool DescriptionOverride => IsRaw && !string.IsNullOrWhiteSpace(Purchase);

        public DanceQuery DanceQuery => new(IsRaw ? null : Dances);
        public UserQuery UserQuery => new(User);
        public SongSort SongSort => new(SortOrder);

        public CruftFilter CruftFilter =>
            !Action.StartsWith("merge", StringComparison.OrdinalIgnoreCase) && Level.HasValue
                ? (CruftFilter)Level.Value
                : CruftFilter.NoCruft;

        public IList<string> ODataSort
        {
            get
            {
                var sort = SongSort;

                switch (sort.Id)
                {
                    case SongSort.Dances:
                        return DanceQuery?.ODataSort(sort.Descending ? "asc" : "desc");
                    case SongSort.Comments:
                        return new List<string> { "Modified " + (sort.Descending ? "asc" : "desc") };
                    default:
                        return sort.OData;
                }
            }
        }

        public bool IsSimple => !IsAdvanced;

        public bool IsAdvanced => string.Equals(
                Action.ToLower().Replace(' ', '+'),
                "azure+advanced", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Action, "advanced", StringComparison.OrdinalIgnoreCase) ||
            IsRaw;

        public bool IsLucene => Action.ToLower().Replace(' ', '+')
            .EndsWith("+lucene", StringComparison.OrdinalIgnoreCase);

        public bool IsRaw
        {
            get
            {
                var action = Action.ToLower().Replace(' ', '+');
                return action.StartsWith("azure+raw", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(action, "holidaymusic", StringComparison.OrdinalIgnoreCase);
            }
        }

        public bool IsAzure =>
            Action.ToLower().StartsWith("azure", StringComparison.OrdinalIgnoreCase);

        public string ODataPurchase
        {
            get
            {
                var purch = Purchase;
                if (string.IsNullOrWhiteSpace(purch))
                {
                    return null;
                }

                var not = "";
                if (purch.StartsWith("!"))
                {
                    not = "not ";
                    purch = purch[1..];
                }

                var services = purch.ToCharArray().Select(c => MusicService.GetService(c).Name);

                var sb = new StringBuilder();
                foreach (var s in services)
                {
                    if (sb.Length > 0)
                    {
                        sb.Append(" or ");
                    }

                    sb.AppendFormat("Purchase/any(t: t eq '{0}')", s);
                }

                return $"{not}({sb})";
            }
        }

        public bool IsEmpty => EmptyExcept(new[] { "SortOrder" });

        public bool IsEmptyPaged => EmptyExcept(new[] { "Page", "Action", "SortOrder" });
        public bool IsEmptyBot => EmptyExcept(new[] { "Page", "Action" });

        public bool IsEmptyDance =>
            EmptyExcept(new[] { "Page", "Action", "SortOrder", "Dances" }) &&
            DanceQuery.Dances.Count() < 2;

        public bool IsEmptyUser(string user) =>
            EmptyExcept(new[] { "Page", "Action", "SortOrder", "Dances", "User" }) &&
            DanceQuery.Dances.Count() < 2 &&
            UserQuery.IsDefault(user);

        public bool IsSingleDance => IsEmptyDance && DanceQuery?.Dances.Count() == 1;
        public bool HasDances => (DanceQuery?.Dances)?.Any() ?? false;

        public bool IsUserOnly =>
            EmptyExcept(new[] { "Page", "Action", "User" });

        public string Description
        {
            get
            {
                // All [dance] songs [including the text "<SearchString>] [Available on [Amazon|ITunes|Spotify]
                //   [Including tags TI] [Excluding tags TX] [between Tempo Range] [between Length]
                //   [(liked|disliked|edited) by user] sorted by [Sort Order] from [High|low] to [low|high]
                // TOOD: If we pass in context, we can have user name + we can do better stuff with the tags...

                if (IsRaw)
                {
                    return string.IsNullOrWhiteSpace(Purchase)
                        ? new RawSearch(this).ToString()
                        : Purchase;
                }

                var separator = " ";

                var danceQuery = DanceQuery;
                var prefix = "All " + danceQuery;

                var sb = new StringBuilder(prefix);

                if (!string.IsNullOrWhiteSpace(SearchString))
                {
                    sb.AppendFormat(" containing the text \"{0}\"", SearchString);
                    separator = CommaSeparator;
                }

                if (!string.IsNullOrWhiteSpace(Purchase))
                {
                    sb.AppendFormat(
                        "{0}available on {1}", separator,
                        MusicService.FormatPurchaseFilter(Purchase, " or "));
                    separator = CommaSeparator;
                }

                var tags = new TagList(Tags);

                sb.Append(DescribeTags(tags.ExtractAdd(), "including tag", "and", ref separator));
                sb.Append(DescribeTags(tags.ExtractRemove(), "excluding tag", "or", ref separator));

                if (TempoMin.HasValue && TempoMax.HasValue)
                {
                    sb.AppendFormat(
                        "{0}having tempo between {1} and {2} beats per minute",
                        separator, TempoMin.Value, TempoMax.Value);
                }
                else if (TempoMin.HasValue)
                {
                    sb.AppendFormat(
                        "{0}having tempo greater than {1} beats per minute", separator,
                        TempoMin.Value);
                }
                else if (TempoMax.HasValue)
                {
                    sb.AppendFormat(
                        "{0}having tempo less than {1} beats per minute", separator,
                        TempoMax.Value);
                }

                if (LengthMin.HasValue && LengthMax.HasValue)
                {
                    sb.AppendFormat(
                        "{0}having length between {1} and {2} seconds",
                        separator, LengthMin.Value, LengthMax.Value);
                }
                else if (LengthMin.HasValue)
                {
                    sb.AppendFormat(
                        "{0}having length greater than {1} seconds", separator,
                        LengthMin.Value);
                }
                else if (LengthMax.HasValue)
                {
                    sb.AppendFormat(
                        "{0}having length less than {1} seconds", separator,
                        LengthMax.Value);
                }

                var noUserFilter = new SongFilter(ToString())
                    { Action = null, User = null, Page = null };
                var trivialUser = noUserFilter.IsEmpty;

                sb.Append(UserQuery.Description(trivialUser));
                sb.Append('.');

                sb.Append(SongSort.Description);

                return sb.ToString().Trim();
            }
        }

        public string ShortDescription
        {
            get
            {
                if (IsRaw)
                {
                    return string.IsNullOrWhiteSpace(Purchase)
                        ? new RawSearch(this).ToString()
                        : Purchase;
                }

                var sb = new StringBuilder();
                var dances = DanceQuery.ShortDescription;
                if (!string.IsNullOrWhiteSpace(dances))
                {
                    sb.AppendFormat("{0}: ", dances);
                }

                if (!string.IsNullOrWhiteSpace(SearchString))
                {
                    sb.AppendFormat("\"{0}\" ", SearchString);
                }

                if (TempoMin.HasValue && TempoMax.HasValue)
                {
                    sb.AppendFormat(
                        "Between {0} and {1} beats per minute", TempoMin.Value,
                        TempoMax.Value);
                }
                else if (TempoMin.HasValue)
                {
                    sb.AppendFormat("Tempo > {0} beats per minute", TempoMin.Value);
                }
                else if (TempoMax.HasValue)
                {
                    sb.AppendFormat("Tempo < {0} beats per minute", TempoMax.Value);
                }

                if (LengthMax.HasValue && LengthMin.HasValue)
                {
                    sb.AppendFormat(
                        "Between {0} and {1} seconds", LengthMin.Value,
                        LengthMax.Value);
                }
                else if (LengthMin.HasValue)
                {
                    sb.AppendFormat("Length > {0} seconds", LengthMin.Value);
                }
                else if (LengthMax.HasValue)
                {
                    sb.AppendFormat("Length < {0} seconds", LengthMax.Value);
                }

                if (SortOrder != null && SortOrder.StartsWith(SongSort.Comments))
                {
                    sb.Append("only including songs with comments");
                }
                if (sb.Length > 0)
                {
                    sb.Append(". ");
                }

                sb.Append(SongSort.Description);

                return sb.ToString().Trim();
            }
        }

        public string Filename => ShortDescription.Replace(".", "").Replace(":", "-");

        public SongFilter Normalize(string userName)
        {
            var clone = Clone();

            if (string.Equals(clone.Action, "index", StringComparison.OrdinalIgnoreCase))
            {
                clone.Action = "Advanced";
            }

            var userQuery = clone.UserQuery;
            if (userQuery.IsDefault(userName))
            {
                clone.User = null;
            }
            else if (userQuery.UserName != null)
            {
                clone.User = userQuery.Query;
            }
            clone.Page = null;

            return clone;
        }

        public static SongFilter GetDefault(string userName)
        {
            return userName == null
                ? new SongFilter()
                : new SongFilter($"Index-----\\-{userName}|h");
        }

        public static SongFilter CreateHolidayFilter(string dance = null, int page = 1)
        {
            const string holidayFilter =
                "((OtherTags/any(t: t eq 'Holiday') or GenreTags/any(t: t eq 'Christmas' or t eq 'Holiday')) and OtherTags/all(t: t ne 'Halloween'))";

            string danceFilter = null;
            if (!string.IsNullOrWhiteSpace(dance))
            {
                var d = DanceLibrary.Dances.Instance.DanceFromName(dance);
                if (d != null)
                {
                    danceFilter = $"(DanceTags/any(t: t eq '{dance}'))";
                }
            }

            var odata = string.IsNullOrWhiteSpace(dance)
                ? holidayFilter
                : $"{danceFilter} and {holidayFilter}";

            return new SongFilter(
                "holidaymusic",
                new RawSearch
                {
                    ODataFilter = odata, Page = page,
                    Flags = danceFilter == null ? "" : "singleDance"
                }
            );
        }

        public string GetTagFilter(DanceMusicCoreService dms)
        {
            var tags = new TagList(Tags);

            if (tags.IsEmpty)
            {
                return null;
            }

            var tlInclude = new TagList(Tags);
            var tlExclude = new TagList();

            if (tlInclude.IsQualified)
            {
                var temp = tlInclude;
                tlInclude = temp.ExtractAdd();
                tlExclude = temp.ExtractRemove();
            }

            // We're accepting either a straight include list of tags or a qualified list (+/- for include/exlude)
            // TODO: For now this is going to be explicit (i&i&!e*!e) - do we need a stronger expression syntax at this level
            //  or can we do some kind of top level OR of queries?

            var rInclude = new TagList(dms.GetTagRings(tlInclude).Select(tt => tt.Key));
            var rExclude = new TagList(dms.GetTagRings(tlExclude).Select(tt => tt.Key));

            var sb = new StringBuilder();

            foreach (var tp in s_tagClasses)
            {
                HandleFilterClass(sb, rInclude, tp.Key, tp.Value, "{0}Tags/any(t: t eq '{1}')");
                HandleFilterClass(sb, rExclude, tp.Key, tp.Value, "{0}Tags/all(t: t ne '{1}')");
            }

            return sb.ToString();
        }

        public string GetCommentsFilter()
        {
            return new SongSort(SortOrder).Id == SongIndex.CommentsField ? "Comments/any()" : null;
        }

        public static IEnumerable<string> GetTagClasses()
        {
            return s_tagClasses.Keys;
        }

        public static string TagClassFromName(string tagClass)
        {
            return string.Equals(tagClass, "Genre", StringComparison.OrdinalIgnoreCase)
                ? "Music"
                : tagClass;
        }

        private static void HandleFilterClass(StringBuilder sb, TagList tags, string tagClass,
            string tagName, string format)
        {
            var filtered = tags.Filter(tagClass);
            if (filtered.IsEmpty)
            {
                return;
            }

            foreach (var t in filtered.StripType())
            {
                if (sb.Length > 0)
                {
                    sb.Append(" and ");
                }

                var tt = t.Replace(@"'", @"''");

                sb.AppendFormat(format, tagName, tt);
            }
        }

        //public bool Advanced => !string.IsNullOrWhiteSpace(Purchase) ||
        //                        TempoMin.HasValue || TempoMax.HasValue || DanceQuery.Advanced;

        public bool Anonymize(string user)
        {
            return SwapUser(UserQuery.IdentityUser, user);
        }

        public bool Deanonymize(string user)
        {
            return SwapUser(user, UserQuery.IdentityUser);
        }

        public string GetOdataFilter(DanceMusicCoreService dms)
        {
            var odata = SongSort.Numeric
                ? $"({SongSort.Id} ne null) and ({SongSort.Id} ne 0)"
                : null;

            odata = CombineFilter(odata, DanceQuery.ODataFilter);
            odata = CombineFilter(odata, UserQuery.ODataFilter);

            if (TempoMin.HasValue)
            {
                var tempoMin = TempoMin.Value % 1M < (decimal).0001 ? TempoMin - .5M : TempoMin;
                odata = (odata == null ? "" : odata + " and ") + $"(Tempo ge {tempoMin})";
            }

            if (TempoMax.HasValue)
            {
                var tempoMax = TempoMax.Value % 1M < (decimal).0001 ? TempoMax + .5M : TempoMax;
                odata = (odata == null ? "" : odata + " and ") + $"(Tempo le {tempoMax})";
            }

            if (LengthMin.HasValue)
            {
                odata = (odata == null ? "" : odata + " and ") + $"(Length ge {LengthMin})";
            };

            if (LengthMax.HasValue)
            {
                odata = (odata == null ? "" : odata + " and ") + $"(Length le {LengthMax})";
            };

            odata = CombineFilter(odata, ODataPurchase);
            odata = CombineFilter(odata, GetTagFilter(dms));
            odata = CombineFilter(odata, GetCommentsFilter());


            Trace.WriteLine($"ODataFilter: {odata}");
            return odata;
        }

        private string CombineFilter(string odata, string newData)
        {
            if (newData == null) return odata;

            return (odata == null ? "" : odata + " and ") + newData;
        }

        public override string ToString()
        {
            var version = Version == 2 ? "v2-" : "";
            var length = Version == 2 ? $"{Format(LengthMin.ToString())}-{Format(LengthMax.ToString())}-" : "";
            var ret = $"{version}{Action}-{Dances}-{Format(SortOrder)}-{Format(SearchString)}-{Format(Purchase)}-{Format(User)}-" +
                $"{Format(TempoMin.ToString())}-{Format(TempoMax.ToString())}-{length}{Format(Page.ToString())}-{Format(Tags)}-{Format(Level.ToString())}";
            var clean = ret.TrimEnd(new char [] { '.', '-' });
            return string.Equals(clean, "index", StringComparison.OrdinalIgnoreCase) ? "" : clean;
        }

        private string Format(string s)
        {
            return string.IsNullOrWhiteSpace(s)
                ? "."
                : s.Contains('-') ? s.Replace("-", @"\-") : s;
        }

        public string ToJson()
        {
            var stream = new MemoryStream();
            var serializer = new DataContractJsonSerializer(typeof(SongFilter));
            serializer.WriteObject(stream, this);
            return Encoding.UTF8.GetString(stream.ToArray());
        }

        private bool EmptyExcept(IEnumerable<string> properties)
        {
            var props = new List<string>(properties);
            props.Add("Version");
            return !PropertyInfo
                .Where(pi => !props.Contains(pi.Name)).Select(t => new { n = t.Name, v =  t.GetValue(this)})
                .Any(o => o.v != null && !IsAltDefault(o.v, o.n));
        }

        private static string DescribeTags(TagList tags, string prefix, string connector,
            ref string separator)
        {
            return FormatList(tags.Strip(), prefix, connector, ref separator);
        }

        private static string FormatList(IList<string> list, string prefix, string connector,
            ref string separator)
        {
            var count = list.Count;

            if (count == 0)
            {
                return string.Empty;
            }

            var ret = new StringBuilder();
            if (count < 3)
            {
                ret.AppendFormat(
                    "{0}{1}{2} {3}", separator, prefix, count > 1 ? "s" : "",
                    string.Join($" {connector} ", list));
                separator = CommaSeparator;
            }
            else
            {
                var last = list[count - 1];
                list.RemoveAt(count - 1);
                ret.AppendFormat(
                    "{0}{1}s {2} {3} {4}", separator, prefix, string.Join(", ", list),
                    connector, last);
                separator = CommaSeparator;
            }

            return ret.ToString();
        }

        private bool SwapUser(string newUser, string oldUser)
        {
            if (string.IsNullOrWhiteSpace(User) || !User.Contains(oldUser))
            {
                return false;
            }

            User = User.Replace(oldUser, newUser);
            return true;
        }

        private static bool IsAltDefault(object o, string name)
        {

            return AltDefaults.TryGetValue(name, out var value) && (value is string s
                ? string.Equals(o as string, s, StringComparison.InvariantCultureIgnoreCase)
                : Equals(o, value));
        }
    }
}
