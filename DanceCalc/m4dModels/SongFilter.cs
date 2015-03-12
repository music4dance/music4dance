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
        private static readonly string SSubString = new string(SubChar, 1);
        private const char Separator = '-';
        private static readonly string SSepString = new string(Separator, 1);
 
        static public SongFilter Default
        {
            get
            {
                return new SongFilter();
            }
        }

        static SongFilter()
        {
            var info = typeof(SongFilter).GetProperties(BindingFlags.Public | BindingFlags.Instance);
            SPropertyInfo = info.Where(p => p.CanRead && p.CanWrite).ToList();            
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
                value = value.Replace(@"\-", SSubString);
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

                var pi = SPropertyInfo[i];

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

        public bool Advanced
        {
            get 
            {
                return !string.IsNullOrWhiteSpace(Purchase) ||
                    TempoMin.HasValue || TempoMax.HasValue;
            }
        }
        public override string ToString()
        {
            var ret = new StringBuilder();
            var nullBuff = new StringBuilder();

            var sep = string.Empty;
            foreach (var v in SPropertyInfo.Select(p => p.GetValue(this)))
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
                sep = SSepString;
            }

            return ret.ToString();
        }

        private static string Format(string s)
        {
            return s.Contains("-") ? s.Replace("-", @"\-") : s;
        }

        private static readonly List<PropertyInfo> SPropertyInfo;
    }
}