using System;
using System.Collections.Generic;
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

    public class SongFilter
    {
        internal const string CommaSeparator = ", ";

        protected static readonly List<PropertyInfo> PropertyInfo;

        private static readonly Dictionary<string, object> AltDefaults =
            new() { { "Action", "index" }, { "Dances", "all" }, { "Page", 1 } };

        // Changed from '\u001a' to '~' to avoid corrupt marker byte issues
        private readonly string _subStr = "~";
        private string _action;

        static SongFilter()
        {
            var info =
                typeof(SongFilter).GetProperties(BindingFlags.Public | BindingFlags.Instance);
            PropertyInfo = [.. info.Where(p => p.CanRead && p.CanWrite)];
        }

        public static SongFilter Create(bool nextVersion, string value = null)
        {
            return nextVersion ? new SongFilterNext(value) : new SongFilter(value);
        }

        public static SongFilter Create(bool nextVersion, RawSearch raw, string action = null)
        {
            return nextVersion ? new SongFilterNext(raw, action) : new SongFilter(raw, action);
        }

        // action-dances-sortorder-searchstring-purchase-user-tempomin-tempomax[-lengthmin-lengthmax]-page-tags-level
        protected SongFilter(string value = null)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                Action = "Index";
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
            {
                var min = ReadDecimal(cells, idx++);
                var max = ReadDecimal(cells, idx++);
                if (min.HasValue && max.HasValue && min > max)
                {
                    TempoMin = max;
                    TempoMax = min;
                }
                else
                {
                    TempoMin = min;
                    TempoMax = max;
                }
            }

            if (Version > 1)
            {
                var min = ReadInt(cells, idx++);
                var max = ReadInt(cells, idx++);
                if (min.HasValue && max.HasValue && min > max)
                {
                    LengthMin = max;
                    LengthMax = min;
                }
                else
                {
                    LengthMin = min;
                    LengthMax = max;
                }
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

        public virtual SongFilter Clone()
        {
            return new SongFilter(ToString());
        }

        private List<string> SplitFilter(string input)
        {
            return [.. input
                .Replace(@"\-", _subStr)
                .Split('-')
                .Select(s => s.Trim().Replace(_subStr, "-"))];
        }

        private static string ReadCell(List<string> cells, int index)
        {
            var ret = cells.Count > index ? cells[index] : null;
            return string.IsNullOrWhiteSpace(ret) || ret == "." ? null : ret;
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

        protected SongFilter(RawSearch raw, string action = null)
        {
            Action = action ?? "azure+raw+" + (raw.IsLucene ? "lucene" : "");
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

        public bool TextSearch => !string.IsNullOrEmpty(SearchString);

        public string VueName
        {
            get
            {
                var action = Action.Equals("Advanced", StringComparison.OrdinalIgnoreCase)
                    || Action.StartsWith("azure+raw", StringComparison.OrdinalIgnoreCase)
                    || Action.Equals("MergeCandidates")
                        ? "index"
                        : Action.ToLowerInvariant();

                return action switch
                {
                    "newmusic" => "new-music",
                    "holidaymusic" => "holiday-music",
                    _ => "song-index",
                };
            }
        }
        public bool DescriptionOverride => IsRaw && !string.IsNullOrWhiteSpace(Purchase);

        public virtual KeywordQuery KeywordQuery => new(SearchString);
        public virtual DanceQuery DanceQuery => new(IsRaw ? null : Dances);
        public virtual RawDanceQuery RawDanceQuery => new(Dances, Tags);
        public virtual UserQuery UserQuery => new(User);
        public virtual TagQuery TagQuery => new(Tags);
        public virtual SongSort SongSort => new(SortOrder, TextSearch);

        public CruftFilter CruftFilter =>
            !Action.StartsWith("merge", StringComparison.OrdinalIgnoreCase) && Level.HasValue
                ? (CruftFilter)Level.Value
                : CruftFilter.NoCruft;

        public IList<string> ODataSort
        {
            get
            {
                if (IsRaw)
                {
                    return string.IsNullOrEmpty(SortOrder) ? new List<string>() : SortOrder.Split(',', StringSplitOptions.RemoveEmptyEntries);
                }
                var sort = SongSort;

                return sort.Id switch
                {
                    SongSort.Dances => GetDanceSort(sort.Descending ? "asc" : "desc"),
                    SongSort.Comments => ["Modified " + (sort.Descending ? "asc" : "desc")],
                    _ => sort.OData,
                };
            }
        }

        private IList<string> GetDanceSort(string order)
        {
            return IsRaw
                ? RawDanceQuery?.ODataSort(order) ?? new List<string>()
                : DanceQuery?.ODataSort(order) ?? new List<string>();
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
                    string.Equals(action, "customsearch", StringComparison.OrdinalIgnoreCase) ||
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
                if (purch.StartsWith('!'))
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

        public bool IsEmpty => EmptyExcept(["SortOrder"]);

        public bool IsEmptyPaged => EmptyExcept(["Page", "Action", "SortOrder"]);
        public bool IsEmptyBot => EmptyExcept(["Page", "Action"]);

        public bool IsEmptyDance =>
            EmptyExcept(["Page", "Action", "SortOrder", "Dances"]) &&
            GetDanceCount() < 2;

        public bool IsEmptyUser(string user) =>
            EmptyExcept(["Page", "Action", "SortOrder", "Dances", "User"]) &&
            !GetDanceIsComplex() &&
            UserQuery.IsDefault(user);

        public bool IsSingleDance => GetDanceCount() == 1;
        public bool HasDances => GetDanceCount() > 0;

        private int GetDanceCount()
        {
            return IsRaw
                ? RawDanceQuery?.Dances.Count() ?? 0
                : DanceQuery?.Dances.Count() ?? 0;
        }

        private bool GetDanceIsComplex()
        {
            return IsRaw
                ? RawDanceQuery?.IsComplex ?? false
                : DanceQuery?.IsComplex ?? false;
        }

        public bool IsUserOnly =>
            EmptyExcept(["Page", "Action", "User"]);

        public bool IsDefault => EmptyExcept(["User"]);

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
                    sb.AppendFormat(" {0}", KeywordQuery.Description);
                    separator = CommaSeparator;
                }

                if (!string.IsNullOrWhiteSpace(Purchase))
                {
                    sb.AppendFormat(
                        "{0}available on {1}", separator,
                        MusicService.FormatPurchaseFilter(Purchase, " or "));
                    separator = CommaSeparator;
                }

                sb.Append(TagQuery.Description(ref separator));

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

                var noUserFilter = Clone();
                noUserFilter.Action = null;
                noUserFilter.User = null;
                noUserFilter.Dances = null;
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
                var dances = GetDanceShortDescription();
                if (!string.IsNullOrWhiteSpace(dances))
                {
                    sb.AppendFormat("{0}: ", dances);
                }

                if (TextSearch)
                {
                    sb.AppendFormat("{0} ", KeywordQuery.ShortDescription);
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

        private string GetDanceShortDescription()
        {
            return IsRaw
                ? RawDanceQuery?.ShortDescription ?? string.Empty
                : DanceQuery?.ShortDescription ?? string.Empty;
        }

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

        public virtual SongFilter CreateCustomSearchFilter(string name = "holiday", string dance = null, int page = 1)
        {
            var holidayFilter = name.ToLowerInvariant() switch
            {
                "halloween" => "OtherTags/any(t: t eq 'Halloween')",
                "holiday" or "christmas" => "(OtherTags/any(t: t eq 'Holiday') or GenreTags/any(t: t eq 'Christmas' or t eq 'Holiday')) and OtherTags/all(t: t ne 'Halloween')",
                "broadway" => "GenreTags/any(t: t eq 'Broadway') or GenreTags/any(t: t eq 'Show Tunes') or GenreTags/any(t: t eq 'Musicals') or GenreTags/any(t: t eq 'Broadway And Vocal')",
                _ => throw new Exception($"Unknown holiday: {name}"),
            };
            string danceFilter = null;
            string danceSort = null;
            if (string.IsNullOrWhiteSpace(dance))
            {
                danceSort = "dance_ALL/Votes desc";
            }
            else
            {
                var d = DanceLibrary.Dances.Instance.DanceFromName(dance);
                if (d != null)
                {
                    danceFilter = $"DanceTags/any(t: t eq '{dance}')";
                    danceSort = $"dance_{d.Id}/Votes desc";
                }
            }

            var odata = string.IsNullOrWhiteSpace(dance)
                ? holidayFilter
                : $"{danceFilter} and ({holidayFilter})";

            return new SongFilter(
                new RawSearch
                {
                    ODataFilter = odata,
                    SortFields = danceSort,
                    Page = page,
                    Flags = danceFilter == null ? "" : "singleDance"
                },
                "customsearch"
            );
        }

        public string GetCommentsFilter()
        {
            return SongSort.Id == SongIndex.CommentsField ? "Comments/any()" : null;
        }

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

            odata = CombineFilter(odata, GetDanceODataFilter(dms));
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
            }

            if (LengthMax.HasValue)
            {
                odata = (odata == null ? "" : odata + " and ") + $"(Length le {LengthMax})";
            }

            odata = CombineFilter(odata, ODataPurchase);
            odata = CombineFilter(odata, TagQuery.GetODataFilter(dms));
            odata = CombineFilter(odata, GetCommentsFilter());

            return odata;
        }

        private string GetDanceODataFilter(DanceMusicCoreService dms)
        {
            return IsRaw
                ? RawDanceQuery?.GetODataFilter(dms)
                : DanceQuery?.GetODataFilter(dms);
        }

        private static string CombineFilter(string existing, string newData)
        {
            if (newData == null) return existing;

            return (existing == null ? "" : existing + " and ") + newData;
        }

        public override string ToString()
        {
            var version = Version == 2 ? "v2-" : "";
            var length = Version == 2 ? $"{Format(LengthMin.ToString())}-{Format(LengthMax.ToString())}-" : "";
            var ret = $"{version}{Action}-{Format(Dances)}-{Format(SortOrder)}-{Format(SearchString)}-{Format(Purchase)}-{Format(User)}-" +
                $"{Format(TempoMin.ToString())}-{Format(TempoMax.ToString())}-{length}{Format(Page.ToString())}-{Format(Tags)}-{Format(Level.ToString())}";
            var clean = ret.TrimEnd(['.', '-']);
            return string.Equals(clean, "index", StringComparison.OrdinalIgnoreCase) ? "" : clean;
        }

        private string Format(string s) => string.IsNullOrWhiteSpace(s)
                ? "."
                : s.Contains('-') ? s.Replace("-", _subStr) : s;

        public string ToJson()
        {
            var stream = new MemoryStream();
            var serializer = new DataContractJsonSerializer(typeof(SongFilter));
            serializer.WriteObject(stream, this);
            return Encoding.UTF8.GetString(stream.ToArray());
        }

        private bool EmptyExcept(IEnumerable<string> properties)
        {
            var props = new List<string>(properties)
            {
                "Version"
            };
            return !PropertyInfo
                .Where(pi => !props.Contains(pi.Name)).Select(t => new { n = t.Name, v = t.GetValue(this) })
                .Any(o => o.v != null && !IsAltDefault(o.v, o.n));
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
