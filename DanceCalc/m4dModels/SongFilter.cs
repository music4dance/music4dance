using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;

namespace m4dModels
{
    [TypeConverter(typeof(SongFilterConverter))]
    public class SongFilter
    {
        private const string Empty = ".";
        private const char SubChar = '\u001a';
        private readonly string _subStr = new string(SubChar, 1);
        private const char Separator = '-';
        private readonly string _sepStr = new string(Separator, 1);
 
        static public SongFilter Default => new SongFilter();

        static SongFilter()
        {
            var info = typeof(SongFilter).GetProperties(BindingFlags.Public | BindingFlags.Instance);
            s_propertyInfo = info.Where(p => p.CanRead && p.CanWrite).ToList();
        }

        public SongFilter()
        {
            Action = "Index";
        }

        public SongFilter(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;

            var fancy = false;
            if (value.Contains(@"\-"))
            {
                fancy = true;
                value = value.Replace(@"\-", _subStr);
            }

            var cells = value.Split(Separator);

            for (var i = 0; i < cells.Length; i++)
            {
                if (string.Equals(cells[i], Empty))
                {
                    cells[i] = string.Empty;
                }
                
                if (fancy)
                {
                    cells[i] = cells[i].Replace(SubChar, Separator);
                }

                var pi = s_propertyInfo[i];

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
                            v = ut.InvokeMember("Parse", BindingFlags.Static | BindingFlags.Public | BindingFlags.InvokeMethod, null, null, new object[] {cells[i]});
                        }
                        catch (Exception e)
                        {
                            Trace.WriteLine(e.Message);
                        }
                    }
                }

                pi.SetValue(this,v);
            }
        }
        public string Action { get; set; }
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

        public DanceQuery DanceQuery => new DanceQuery(Dances);
        public UserQuery UserQuery => new UserQuery(User);
        public SongSort SongSort => new SongSort(SortOrder);

        public bool Advanced => !string.IsNullOrWhiteSpace(Purchase) ||
                                TempoMin.HasValue || TempoMax.HasValue || DanceQuery.Advanced;

        public override string ToString()
        {
            var ret = new StringBuilder();
            var nullBuff = new StringBuilder();

            var sep = string.Empty;
            foreach (var v in s_propertyInfo.Select(p => p.GetValue(this)))
            {
                if (v == null)
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
            }

            return ret.ToString();
        }

        public bool IsEmpty
        {
            get
            {
                return !s_propertyInfo.Where(pi => pi.Name != "SortOrder").Select(t => t.GetValue(this)).Where((o, i) => o != null && !IsAltDefault(o, i)).Any();
            }
        }

        public bool IsEmptyPaged
        {
            get
            {
                return !s_propertyInfo.Where(pi => pi.Name != "Page").Select(t => t.GetValue(this)).Where((o, i) => o != null && !IsAltDefault(o, i)).Any();
            }
        }

        public string Description
        {
            get
            {
                // All [dance] songs [including the text "<SearchString>] [Available on [Groove|Amazon|ITunes|Spotify] [Including tags TI] [Excluding tags TX] [Tempo Range] [(liked|disliked|edited) by user] sorted by [Sort Order] from [High|low] to [low|high]
                // TOOD: If we pass in context, we can have user name + we can do better stuff with the tags...

                var separator = " ";

                var danceQuery = DanceQuery;
                var prefix = danceQuery.IsExclusive ? FormatDanceList(danceQuery, "all", "and") : FormatDanceList(danceQuery, "any", "or");

                var sb = new StringBuilder(prefix);

                if (!string.IsNullOrWhiteSpace(SearchString))
                {
                    sb.AppendFormat(" containing the text \"{0}\"",SearchString);
                    separator = CommaSeparator;
                }

                if (!string.IsNullOrWhiteSpace(Purchase))
                {
                    sb.AppendFormat("{0}available on {1}",separator,MusicService.FormatPurchaseFilter(Purchase, " or "));
                    separator = CommaSeparator;
                }

                var tags = new TagList(Tags);

                sb.Append(DescribeTags(tags.ExtractAdd(), "including tag", "and", ref separator));
                sb.Append(DescribeTags(tags.ExtractRemove(), "excluding tag", "or", ref separator));

                if (TempoMin.HasValue && TempoMax.HasValue)
                {
                    sb.AppendFormat("{0}having tempo between {1} and {2} beats per minute", separator, TempoMin.Value, TempoMax.Value);
                }
                else if (TempoMin.HasValue)
                {
                    sb.AppendFormat("{0}having tempo greater than {1} beats per minute", separator, TempoMin.Value);
                }
                else if (TempoMax.HasValue)
                {
                    sb.AppendFormat("{0}having tempo less than {1} beats per minute", separator, TempoMax.Value);
                }

                var noUserFilter = new SongFilter(ToString()) {Action="Index", User = null, Page = null};
                var trivialUser = noUserFilter.IsEmpty;

                sb.Append(UserQuery.Description(trivialUser));
                sb.Append(".");

                sb.Append(SongSort.Description);

                return sb.ToString().Trim();
            }
        }

        private const string CommaSeparator = ", ";

        private static string DescribeTags(TagList tags, string prefix, string connector, ref string separator)
        {
            return FormatList(tags.Strip(),prefix,connector,ref separator);
        }

        private static string FormatList(IList<string> list, string prefix, string connector, ref string separator)
        {
            var count = list.Count;

            if (count == 0)
            {
                return string.Empty;
            }

            var ret = new StringBuilder();
            if (count < 3)
            {
                ret.AppendFormat("{0}{1}{2} {3}", separator, prefix, count > 1 ? "s" : "", string.Join($" {connector} ", list));
                separator = CommaSeparator;
            }
            else
            {
                var last = list[count - 1];
                list.RemoveAt(count - 1);
                ret.AppendFormat("{0}{1}s {2} {3} {4}", separator, prefix, string.Join(", ", list), connector, last);
                separator = CommaSeparator;
            }
            return ret.ToString();
        }

        private static string FormatDanceList(DanceQuery list, string prefix, string connector)
        {
            var dances = list.Dances.Select(n => n.Name).ToList();
            var count = dances.Count;

            switch (count)
            {
                case 0:
                    return "All songs";
                case 1:
                    return $"All {dances[0]} songs";
                case 2:
                    return $"All songs dancable to {prefix} of {dances[0]} {connector} {dances[1]}";
                default:
                    var last = dances[count - 1];
                    dances.RemoveAt(count - 1);
                    return $"All songs danceable to {prefix} of {string.Join(", ", dances)} {connector} {last}";
            }
        }

        private static string Format(string s)
        {
            return s.Contains("-") ? s.Replace("-", @"\-") : s;
        }

        private static bool IsAltDefault(object o, int index)
        {
            if (index > s_altDefaults.Length -1 || s_altDefaults[index] == null) return false;

            var s = o as string;
            return s != null ? string.Equals(s, s_altDefaults[index] as string, StringComparison.InvariantCultureIgnoreCase) : Equals(o, s_altDefaults[index]);
        }

        private static readonly List<PropertyInfo> s_propertyInfo;
        private static readonly object[] s_altDefaults = {"index","all","modified",null,null,null,null,1};
    }
}