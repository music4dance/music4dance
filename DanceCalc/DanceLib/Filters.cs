using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace DanceLibrary
{
    // This is a bit funky and could use some cleaning up - it depends
    //  more on singletons than I'd like and is pretty tied to the precise
    //  options that we're currently supporting.  It's probably woth
    //  getting the latter taken care of soon...
    // The general idea is that a filter object has a static list of filter
    //  types that maps to a value array (true/false) for each of the type
    //  Serialization is then just a run of true/false values for each type
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

        private void SetTypeValues(bool value)
        {
            foreach (FilterItem fi in _sortedValues)
            {
                fi.Value = value;
            }
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
                bool ret = false;
                foreach (FilterItem item in fo._sortedValues)
                {
                    ret |= item.Value;
                }
                return ret;
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

        /// <summary>
        /// This is a bit more hard-coded than I'd like, in a future pass see if there
        ///   is a reasonable way to build this as an arbitrary hierarchy
        /// </summary>
        /// <param name="orgs"></param>
        /// <param name="competitors"></param>
        /// <param name="levels"></param>
        /// <returns></returns>
        public static bool IsCovered(string orgs, string competitors, string levels)
        {
            bool ret = IsCovered(Tags.Organization, orgs);

            if (ret && string.Equals(orgs, "NDCA"))
            {
                ret = IsCovered(Tags.Level, levels) && IsCovered(Tags.Competitor, competitors);
            }

            return ret;
        }

        public static ReadOnlyCollection<FilterItem> GetFilter(string type)
        {
            return new ReadOnlyCollection<FilterItem>(_filters[type]._sortedValues);
        }

        public static void ReadState(TextReader t)
        {
            // TODO: Make this robust against old state
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

        /// <summary>
        /// This just resets all of the values to a uniform value
        /// </summary>
        /// <param name="value">The direction to set all of the values</param>
        public static void SetAll(bool value)
        {
            foreach (FilterObject fo in _filters.Values)
            {
                fo.SetTypeValues(value);
            }
        }

        /// <summary>
        /// This just resets all of the values for a particular type to a uniform value
        /// </summary>
        /// <param name="value">The direction to set all of the values</param>
        public static void SetAll(string type, bool value)
        {
            FilterObject fo = _filters[type];
            fo.SetTypeValues(value);
        }

        public void TryParse(string s)
        {
            bool valid = true;
            string[] a = s.Split(new char[] { ',', ':' },StringSplitOptions.RemoveEmptyEntries);

            if (!a[0].Equals(_type) || a.Length - 1 != _sortedValues.Count)
            {
                // If something funky goes on here, just set the valid state to false which will turn everything (back) on
                Debug.WriteLine(string.Format("FilterObject: Unable to parse '{0}'", s));
                valid = false;
            }

            for (int i = 0; i < _sortedValues.Count; i++)
            {
                bool temp = true;
                if (valid && bool.TryParse(a[i + 1], out temp))
                {
                    _sortedValues[i].Value = temp;
                }
                else
                {
                    _sortedValues[i].Value = true;
                }
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
            _filters[Tags.Style] = new FilterObject(Tags.Style, new string[] { "International Standard", "International Latin", "American Smooth", "American Rhythm", "Social", "Performance"}, new string[] { null, null, null, null, null, null });
            _filters[Tags.Organization] = new FilterObject(Tags.Organization, new string[] { "NDCA", "DanceSport", "Ad-Hoc" }, new string[] { "National Dance Council of America (NDCA)", "International DanceSport Federation (IDSF)", "No Offical Organization" });
            _filters[Tags.Competitor] = new FilterObject(Tags.Competitor, new string[] { "Professional","Amateur","ProAm" }, new string[] { null, null,"Pro/Am" });
            _filters[Tags.Level] = new FilterObject(Tags.Level, new string[] { "Bronze", "Silver", "Gold" }, new string[] { null, null, null });
        }

        private string _type;
        private Dictionary<string, FilterItem> _values = new Dictionary<string,FilterItem>();
        private List<FilterItem> _sortedValues = new List<FilterItem>();

        static private Dictionary<string, FilterObject> _filters = new Dictionary<string,FilterObject>();
    }
}
