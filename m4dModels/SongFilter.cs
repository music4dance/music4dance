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
        public string Action { get; set; }
        public string SearchString { get; set; }
        public string Dances { get; set; }
        public string SortOrder { get; set; }
        public string Purchase { get; set; }
        public string User { get; set; }
        public decimal? TempoMin { get; set; }
        public decimal? TempoMax { get; set; }
        public int? Page { get; set; }
        public string Tags { get; set; }
        public int? Level { get; set; }
    }

    [TypeConverter(typeof(SongFilterConverter))]
    public class SongFilter
    {
        private const string Empty = ".";
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

        private static readonly object[] AltDefaults =
            { "index", "all", null, null, null, null, null, 1 };

        private readonly string _sepStr = new(Separator, 1);
        private readonly string _subStr = new(SubChar, 1);

        private string _action;

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

        // action-dances-sortorder-searchstring-purchase-user-tempomin-tempomax-page-tags-level

        public SongFilter(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            var fancy = false;
            if (value.Contains(@"\-"))
            {
                fancy = true;
                value = value.Replace(@"\-", _subStr);
            }

            var cells = value.Split(Separator).ToList();

            // Special case where the first field is dance
            //  Need to see if we're still generating these
            //  as this is way kludgier than I'd like
            if (cells.Count > 0)
            {
                var danceQuery = new DanceQuery(cells[0]);
                if (danceQuery.Dances.Any())
                {
                    cells.Insert(0, "index");
                    if (cells.Count > 2)
                    {
                        cells.RemoveAt(2);
                    }
                }
            }

            for (var i = 0; i < cells.Count; i++)
            {
                if (string.Equals(cells[i], Empty))
                {
                    cells[i] = string.Empty;
                }

                if (fancy)
                {
                    cells[i] = cells[i].Replace(SubChar, Separator);
                }

                var pi = PropertyInfo[i];

                object v = null;
                if (!string.IsNullOrWhiteSpace(cells[i]))
                {
                    var type = pi.PropertyType;
                    if (type == typeof(string))
                    {
                        v = cells[i];
                    }
                    else
                    {
                        // This should get the underlying type for a nullable type or just the type otherwise
                        var ut = Nullable.GetUnderlyingType(pi.PropertyType) ?? pi.PropertyType;
                        try
                        {
                            v = ut.InvokeMember(
                                "Parse",
                                BindingFlags.Static | BindingFlags.Public |
                                BindingFlags.InvokeMethod, null, null, new object[] { cells[i] });
                        }
                        catch (Exception e)
                        {
                            Trace.WriteLineIf(
                                TraceLevels.General.TraceError,
                                $"SongFilter: {e.Message}");
                        }
                    }
                }

                pi.SetValue(this, v);
            }

            if (Action.StartsWith("azure", StringComparison.OrdinalIgnoreCase))
            {
                Action = Action.Contains("advanced", StringComparison.OrdinalIgnoreCase)
                    ? "Advanced"
                    : "Index";
            }
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
            Level = raw.CruftFilter == DanceMusicCoreService.CruftFilter.NoCruft
                ? null
                : (int?)raw.CruftFilter;
        }

        public SongFilter(string action, RawSearch raw) : this(raw)
        {
            Action = action;
        }

        public string Action
        {
            get => _action ?? "index";
            set => _action = value;
        }

        public string Dances { get; set; }
        public string SortOrder { get; set; }
        public string SearchString { get; set; }
        public string Purchase { get; set; }
        public string User { get; set; }
        public decimal? TempoMin { get; set; }
        public decimal? TempoMax { get; set; }
        public int? Page { get; set; }
        public string Tags { get; set; }
        public int? Level { get; set; }

        public string TargetAction => IsAzure ? "azuresearch" : Action;

        public bool DescriptionOverride => IsRaw && !string.IsNullOrWhiteSpace(Purchase);

        public DanceQuery DanceQuery => new(IsRaw ? null : Dances);
        public UserQuery UserQuery => new(User);
        public SongSort SongSort => new(SortOrder);

        public DanceMusicCoreService.CruftFilter CruftFilter =>
            !Action.StartsWith("merge", StringComparison.OrdinalIgnoreCase) && Level.HasValue
                ? (DanceMusicCoreService.CruftFilter)Level.Value
                : DanceMusicCoreService.CruftFilter.NoCruft;

        public IList<string> ODataSort
        {
            get
            {
                var sort = SongSort;

                if (sort.Id != "Dances")
                {
                    return sort.OData;
                }

                var dq = DanceQuery;
                var dids = dq.DanceIds.ToList();

                if (dids.Count == 0)
                {
                    return null;
                }

                var order = sort.Descending ? "asc" : "desc";
                return dids.Select(did => $"dance_{did} {order}").ToList();
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

        public bool IsSingDance => IsEmptyDance && DanceQuery.Dances.Count() == 1;

        public string Description
        {
            get
            {
                // All [dance] songs [including the text "<SearchString>] [Available on [Amazon|ITunes|Spotify] [Including tags TI] [Excluding tags TX] [between Tempo Range] [(liked|disliked|edited) by user] sorted by [Sort Order] from [High|low] to [low|high]
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

                var noUserFilter = new SongFilter(ToString())
                    { Action = "Index", User = null, Page = null };
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

                if (sb.Length > 0)
                {
                    sb.Append(". ");
                }

                sb.Append(SongSort.Description);

                return sb.ToString().Trim();
            }
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
                    danceFilter = $"(DanceTags/any(t: t eq '{TagManager.CanonicalKey(dance)}'))";
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

            foreach (var t in filtered.StripType().Select(TagManager.CanonicalKey))
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
            var danceFilter = DanceQuery.ODataFilter;
            if (danceFilter != null)
            {
                odata = (odata == null ? "" : odata + " and ") + danceFilter;
            }

            var userFilter = UserQuery.ODataFilter;
            if (userFilter != null)
            {
                odata = (odata == null ? "" : odata + " and ") + userFilter;
            }

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

            var purchaseFilter = ODataPurchase;
            if (purchaseFilter != null)
            {
                odata = (odata == null ? "" : odata + " and ") + purchaseFilter;
            }

            var tagFilter = GetTagFilter(dms);
            if (tagFilter != null)
            {
                odata = (odata == null ? "" : odata + " and ") + tagFilter;
            }

            return odata;
        }

        public override string ToString()
        {
            var ret = new StringBuilder();
            var nullBuff = new StringBuilder();

            var sep = string.Empty;
            var i = 0;
            foreach (var v in PropertyInfo.Select(p => p.GetValue(this)))
            {
                if (v == null || IsAltDefault(v, i))
                {
                    nullBuff.Append(sep);
                    nullBuff.Append(Empty);
                }
                else
                {
                    ret.Append(nullBuff);
                    nullBuff.Clear();
                    ret.Append(sep);
                    ret.Append(Format(v.ToString()));
                }

                sep = _sepStr;
                i += 1;
            }

            return ret.ToString();
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
            return !PropertyInfo.Where(pi => !properties.Contains(pi.Name))
                .Select(t => t.GetValue(this)).Where((o, i) => o != null && !IsAltDefault(o, i))
                .Any();
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

        private static string Format(string s)
        {
            return s.Contains('-') ? s.Replace("-", @"\-") : s;
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

        private static bool IsAltDefault(object o, int index)
        {
            return index > AltDefaults.Length - 1 || AltDefaults[index] == null
                ? false
                : o is string s
                ? string.Equals(
                    s, AltDefaults[index] as string,
                    StringComparison.InvariantCultureIgnoreCase)
                : Equals(o, AltDefaults[index]);
        }
    }
}
