using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Collections.ObjectModel;
using System.Text;

namespace DanceLibrary
{
    public class FilterItem
    {
        public FilterItem(string name, string longName)
        {
            Name = name;
            if (longName == null)
                LongName = Name;
            else
                LongName = longName;
            Value = true;
        }

        public string Name { get; private set; }
        public string LongName { get; private set; }
        public bool Value 
        { 
            get {return _value;} 
            set {_value=value;} 
        }
        private bool _value;
    }

    public class FilterObject
    {

        public FilterObject(string type, string[] names, string[] longNames)
        {
            _type = type;

            for (int i = 0; i < names.Length; i++)
            {
                FilterItem fi = new FilterItem(names[i], longNames[i]);
                _values[names[i]] = fi;
                _sortedValues.Add(fi);
            }
        }

        public void SetValue(string name, bool value)
        {
            _values[name].Value = value;
        }

        public bool GetValue(string name)
        {
            return _values[name].Value;
        }

        public static void SetValue(string type, string name, bool value)
        {
            FilterObject fo = _filters[type];
            fo.SetValue(name, value);
        }

        public static bool GetValue(string type, string name)
        {
            FilterObject fo = _filters[type];
            if (name == Tags.All)
            {
                return true;
            }
            else
            {
                bool ret = false;
                string[] a = name.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string n in a)
                {
                    ret |= fo.GetValue(n);
                }
                return ret;
            }
        }

        /// <summary>
        /// Return true if all of the true filters in filter type "name" is covered by the values in "values"
        /// </summary>
        /// <param name="name"></param>
        /// <param name="values"></param>
        /// <returns></returns>
        public static bool IsCovered(string name, string values)
        {
            if (values == Tags.All)
                return true;

            FilterObject fo = _filters[name];
            foreach (FilterItem fi in fo._sortedValues)
            {
                if (fi.Value == true && !values.Contains(fi.Name))
                {
                    return false;
                }
            }

            return true;
        }

        public static bool IsCovered(string orgs, string competitors, string levels)
        {
            return IsCovered(Tags.Organization, orgs) && IsCovered(Tags.Competitor, competitors) && IsCovered(Tags.Level, levels);
        }

        public static ReadOnlyCollection<FilterItem> GetFilter(string type)
        {
            return _filters[type]._sortedValues.AsReadOnly();
        }

        public static void ReadState(TextReader t)
        {
            foreach (FilterObject fo in _filters.Values)
            {
                string s = t.ReadLine();
                fo.TryParse(s);
            }
        }

        public static void SetState(string s)
        {
            StringReader sr = new StringReader(s);
            ReadState(sr);
        }

        public static void SetAll(bool value)
        {
            foreach (FilterObject fo in _filters.Values)
            {
                foreach (FilterItem fi in fo._sortedValues)
                {
                    fi.Value = value;
                }
            }
        }

        public void TryParse(string s)
        {
            string[] a = s.Split(new char[] { ',', ':' },StringSplitOptions.RemoveEmptyEntries);
            if (!a[0].Equals(_type) || a.Length - 1 != _sortedValues.Count)
                throw new Exception(string.Format("FilterObject: Unable to parse '{0}'",s));

            for (int i = 0; i < _sortedValues.Count; i++)
            {
                _sortedValues[i].Value = bool.Parse(a[i + 1]);
            }
        }

        public static void WriteState(TextWriter t)
        {
            foreach (FilterObject fo in _filters.Values)
            {
                string s = fo.ToString();
                t.WriteLine(s);
            }
        }

        public static string GetState()
        {
            StringWriter sw = new StringWriter();
            WriteState(sw);
            string s = sw.GetStringBuilder().ToString();
            return s;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(_type + ": ");
            foreach (FilterItem fi in _sortedValues)
            {
                sb.Append(fi.Value.ToString());
                sb.Append(",");
            }

            return sb.ToString();
        }


        static FilterObject()
        {
            _filters[Tags.Category] = new FilterObject(Tags.Category, new string[] { "Standard", "Latin", "Smooth", "Rhythm" }, new string[] { "International Standard", "International Latin", "American Smooth", "American Rhythm" });
            _filters[Tags.Organization] = new FilterObject(Tags.Organization, new string[] { "NDCA", "DanceSport" }, new string[] { "National Dance Council of America (NDCA)", "International DanceSport Federation (IDSF)" });
            _filters[Tags.Competitor] = new FilterObject(Tags.Competitor, new string[] { "Professional","Amateur","ProAm" }, new string[] { null, null,"Pro/Am" });
            _filters[Tags.Level] = new FilterObject(Tags.Level, new string[] { "Bronze", "Silver", "Gold" }, new string[] { null, null, null });
        }

        private string _type;
        private Dictionary<string, FilterItem> _values = new Dictionary<string,FilterItem>();
        private List<FilterItem> _sortedValues = new List<FilterItem>();

        static private Dictionary<string, FilterObject> _filters = new Dictionary<string,FilterObject>();
    }
}
