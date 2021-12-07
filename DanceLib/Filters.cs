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
            {
                LongName = Name;
            }
            else
            {
                LongName = longName;
            }

            Value = true;
        }

        public string Name { get; }
        public string LongName { get; }

        public bool Value { get; set; }
    }

    public class FilterObject
    {
        private static readonly Dictionary<string, FilterObject> _filters =
            new();

        private readonly List<FilterItem> _sortedValues = new();

        private readonly string _type;

        private readonly Dictionary<string, FilterItem> _values =
            new();

        static FilterObject()
        {
            _filters[Tags.Style] = new FilterObject(
                Tags.Style,
                new[]
                {
                    "International Standard", "International Latin", "American Smooth",
                    "American Rhythm", "Social", "Performance"
                }, new string[] { null, null, null, null, null, null });
            _filters[Tags.Organization] = new FilterObject(
                Tags.Organization,
                new[] { "NDCA", "DanceSport", "Ad-Hoc" },
                new[]
                {
                    "National Dance Council of America (NDCA)",
                    "International DanceSport Federation (IDSF)", "No Offical Organization"
                });
            _filters[Tags.Competitor] = new FilterObject(
                Tags.Competitor,
                new[] { "Professional", "Amateur", "ProAm" },
                new[] { null, null, "Pro/Am" });
            _filters[Tags.Level] = new FilterObject(
                Tags.Level,
                new[] { "Bronze", "Silver", "Gold" }, new string[] { null, null, null });
        }

        public FilterObject(string type, string[] names, string[] longNames)
        {
            _type = type;

            for (var i = 0; i < names.Length; i++)
            {
                var fi = new FilterItem(names[i], longNames[i]);
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
            foreach (var fi in _sortedValues)
            {
                fi.Value = value;
            }
        }

        public static void SetValue(string type, string name, bool value)
        {
            var fo = _filters[type];
            fo.SetValue(name, value);
        }

        public static bool GetValue(string type, string name)
        {
            var fo = _filters[type];
            if (name == Tags.All)
            {
                var ret = false;
                foreach (var item in fo._sortedValues)
                {
                    ret |= item.Value;
                }

                return ret;
            }
            else
            {
                var ret = false;
                var a = name.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var n in a)
                {
                    ret |= fo.GetValue(n);
                }

                return ret;
            }
        }

        /// <summary>
        ///     Return true if all of the true filters in filter type "name" is covered by the values in "values"
        /// </summary>
        /// <param name="name"></param>
        /// <param name="values"></param>
        /// <returns></returns>
        public static bool IsCovered(string name, string values)
        {
            if (values == Tags.All)
            {
                return true;
            }

            var fo = _filters[name];
            foreach (var fi in fo._sortedValues)
            {
                if (fi.Value && !values.Contains(fi.Name))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        ///     This is a bit more hard-coded than I'd like, in a future pass see if there
        ///     is a reasonable way to build this as an arbitrary hierarchy
        /// </summary>
        /// <param name="orgs"></param>
        /// <param name="competitors"></param>
        /// <param name="levels"></param>
        /// <returns></returns>
        public static bool IsCovered(string orgs, string competitors, string levels)
        {
            var ret = IsCovered(Tags.Organization, orgs);

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
            foreach (var fo in _filters.Values)
            {
                var s = t.ReadLine();
                fo.TryParse(s);
            }
        }

        public static void SetState(string s)
        {
            var sr = new StringReader(s);
            ReadState(sr);
        }

        /// <summary>
        ///     This just resets all of the values to a uniform value
        /// </summary>
        /// <param name="value">The direction to set all of the values</param>
        public static void SetAll(bool value)
        {
            foreach (var fo in _filters.Values)
            {
                fo.SetTypeValues(value);
            }
        }

        /// <summary>
        ///     This just resets all of the values for a particular type to a uniform value
        /// </summary>
        /// <param name="value">The direction to set all of the values</param>
        public static void SetAll(string type, bool value)
        {
            var fo = _filters[type];
            fo.SetTypeValues(value);
        }

        public void TryParse(string s)
        {
            var valid = true;
            var a = s.Split(new[] { ',', ':' }, StringSplitOptions.RemoveEmptyEntries);

            if (!a[0].Equals(_type) || a.Length - 1 != _sortedValues.Count)
            {
                // If something funky goes on here, just set the valid state to false which will turn everything (back) on
                Debug.WriteLine($"FilterObject: Unable to parse '{s}'");
                valid = false;
            }

            for (var i = 0; i < _sortedValues.Count; i++)
            {
                if (valid && bool.TryParse(a[i + 1], out var temp))
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
            foreach (var fo in _filters.Values)
            {
                var s = fo.ToString();
                t.WriteLine(s);
            }
        }

        public static string GetState()
        {
            var sw = new StringWriter();
            WriteState(sw);
            var s = sw.GetStringBuilder().ToString();
            return s;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append(_type + ": ");
            foreach (var fi in _sortedValues)
            {
                sb.Append(fi.Value.ToString());
                sb.Append(',');
            }

            return sb.ToString();
        }
    }
}
