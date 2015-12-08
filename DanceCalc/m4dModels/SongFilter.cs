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
        public string Description
        {
            get
            {
                // All [dance] songs [including the text "<SearchString>] [Available on [Groove|Amazon|ITunes|Spotify] [Including tags TI] [Excluding tag TX] [Tempo Range] 
                // TODO: Later? [Edited by User] [(Page n)]
                // TOOD: If we pass in context, we can have user name + we can do better stuff with the tags...

                var name = "All songs";
                var separator = " ";
                const string addSep = ", ";

                var danceQuery = DanceQuery;
                if (!danceQuery.All)
                {
                    var dances = danceQuery.Dances.ToList();
                    if (dances.Count == 1)
                    {
                        name = $"All {dances[0].Name} songs";
                    }
                    else
                    {
                        var x = danceQuery.IsExclusive ? "all" : "any";
                        name += $" danceable to {x} of {string.Join(", ", dances.Select(n => n.Name))}";
                    }
                }

                var dance = DanceLibrary.Dances.Instance.DanceFromId(Dances);
                if (dance != null)
                {
                    name = string.Format($"All {dance.Name} songs");
                }

                var sb = new StringBuilder(name);

                if (!string.IsNullOrWhiteSpace(SearchString))
                {
                    sb.AppendFormat(" containing the text \"{0}\"",SearchString);
                    separator = addSep;
                }

                if (!string.IsNullOrWhiteSpace(Purchase))
                {
                    sb.AppendFormat("{0}available on {1}",separator,MusicService.FormatPurchaseFilter(Purchase, " or "));
                    separator = addSep;
                }

                var tags = new TagList(Tags);
                var inc = tags.ExtractAdd();
                var exc = tags.ExtractRemove();

                if (inc.Tags.Count > 0)
                {
                    sb.AppendFormat("{0}including tag{1} {2}", separator, inc.Tags.Count > 1 ? "s" : "",string.Join(" and ",inc.Strip()));
                    separator = addSep;
                }
                if (exc.Tags.Count > 0)
                {
                    sb.AppendFormat("{0}excluding tag{1} {2}", separator, exc.Tags.Count > 1 ? "s" : "", string.Join(" or ", exc.Strip()));
                    separator = addSep;
                }

                if (TempoMin.HasValue && TempoMax.HasValue)
                {
                    sb.AppendFormat("{0}having tempo between {1} and {2} beats per measure", separator, TempoMin.Value, TempoMax.Value);
                }
                else if (TempoMin.HasValue)
                {
                    sb.AppendFormat("{0}having tempo greater than {1} beats per measure", separator, TempoMin.Value);
                }
                else if (TempoMax.HasValue)
                {
                    sb.AppendFormat("{0}having tempo less than {1} beats per measure", separator, TempoMax.Value);
                }

                var noUserFilter = new SongFilter(ToString()) {Action="Index", User = null, Page = null};
                var trivialUser = noUserFilter.IsEmpty;

                sb.Append(UserQuery.Description(trivialUser));

                return sb.ToString().Trim();
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
        private static readonly object[] s_altDefaults = {"index","all",null,null,null,null,null,1};
    }
}