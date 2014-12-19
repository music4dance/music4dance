using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;


namespace m4dModels
{
    public class SongProperty : DbObject
    {
        // Name Syntax: BaseName[:idx[:qual]]
        // idx is zeros based indes for multi-value fields (only album at this point?)
        // qual is a qualifier for purchase type (may generalize?)
        //
        // Not implementing this yet, but for artist might allow artist type after the colon

        #region Constructors
        public SongProperty()
        {
        }

        public SongProperty(Guid songId, string name, string value)
        {
            SongId = songId;
            Name = name;

            if (!string.IsNullOrWhiteSpace(value))
            {
                if (value.Contains("\\t"))
                {
                    value = value.Replace("\\t", "\t");
                }
                if (value.Contains("\\<EQ>\\"))
                {
                    value.Replace("\\<EQ>\\", "=");
                }
                if (string.Equals(name, Song.TempoField))
                {
                    value = FormatTempo(value);
                }
            }

            Value = value;
        }
        public SongProperty(Guid songId, string baseName, string value = null, int index = -1, string qual = null)
        {
            SongId = songId;

            string name = baseName;

            if (index >= 0)
            {
                name = string.Format("{0}:{1:D2}", name, index);
            }

            if (qual != null)
            {
                name = name + ":" + qual;
            }

            Name = name;
            Value = value;
        }
        
        #endregion

        #region Properties
        public Int64 Id { get; set; }
        public Guid SongId { get; set; }
        public virtual Song Song { get; set; }
        public string Name { get; set; }
        public string Value { get; set; }
        public object ObjectValue
        {
            get
            {
                object ret = null;
                switch (BaseName)
                {
                    case Song.TempoField:
                        // decimal
                        if (Value != null)
                        {
                            decimal v;
                            decimal.TryParse(Value, out v);
                            ret = v;
                        }
                        break;
                    case Song.LengthField:
                    case Song.TrackField:
                    case Song.DanceRatingField:
                        //int
                        if (Value != null)
                        {
                            int v;
                            int.TryParse(Value, out v);
                            ret = v;
                        }
                        break;
                    case Song.TimeField:
                        {
                            DateTime v;
                            DateTime.TryParse(Value, out v);
                            ret = v;
                        }
                        break;

                    default:
                        ret = Value;
                        break;
                }

                return ret;
            }
        }
        public bool IsComplex
        {
            get { return IsComplexName(Name); }
        }
        public bool IsAction
        {
            get { return IsActionName(Name); }
        }

        public static bool IsComplexName(string name)
        {
            return name.Contains(":");
        }
        public static bool IsActionName(string name)
        {
            return name.StartsWith(".");
        }

        public string BaseName
        {
            get
            {
                return ParseBaseName(Name);
            }
        }

        public int? Index
        {
            get
            {
                return ParseIndex(Name);
            }
        }

        public string Qualifier
        {
            get
            {
                return ParseQualifier(Name);
            }
        }
        
        #endregion

        #region Overrides
        public override string ToString()
        {
            string value = Value;
            if (string.IsNullOrWhiteSpace(value))
            {
                value = string.Empty;
            }
            else if (string.Equals(BaseName, Song.TempoField))
            {
                value = FormatTempo(value);
            }
            else
            {
                if (value.Contains('='))
                {
                    value = value.Replace("=", "\\<EQ>\\");
                }

                if (value.Contains('\t'))
                {
                    value = value.Replace("\t", "\\t");
                }
            }

            return string.Format("{0}={1}", Name, value);
        }
        
        #endregion

        #region Serialization
        public static string Serialize(IEnumerable<SongProperty> properties, string[] actions)
        {
            StringBuilder sb = new StringBuilder();

            string sep = string.Empty;
            foreach (SongProperty sp in properties)
            {
                if (actions == null || !actions.Contains(sp.Name))
                {
                    string p = sp.ToString();

                    sb.AppendFormat("{0}{1}", sep, p);

                    sep = "\t";
                }
            }

            return sb.ToString();
        }

        public static void Load(Guid songId, string props, ICollection<SongProperty> properties)
        {
            string[] cells = props.Split(new char[] { '\t' });

            foreach (string cell in cells)
            {
                string[] values = cell.Split(new char[] { '=' });

                if (values.Length == 2)
                {
                    properties.Add(new SongProperty(songId, values[0], values[1]));
                }
                else
                {
                    Trace.WriteLine("Bad SongProperty: {0}", cell);
                }
            }
        }

        public static string SerializeValue(object o)
        {
            if (o == null)
            {
                return null;
            }
            else
            {
                return o.ToString();
            }
        }
        
        #endregion

        #region Static Helpers

        private static string FormatTempo(string value)
        {
            decimal v;
            if (decimal.TryParse(value, out v))
            {
                value = v.ToString("F1");
            }
            return value;
        }
        public static string ParseBaseName(string name)
        {
            int i = name.IndexOf(':');

            if (i >= 0)
            {
                name = name.Substring(0, i);
            }

            return name;
        }

        public static string ParseQualifier(string name)
        {
            string qual = null;

            if (name.Contains(":"))
            {
                string[] parts = name.Split(new char[] { ':' });

                if (parts.Length > 2)
                {
                    qual = parts[2];
                }
            }

            return qual;
        }

        public static int? ParseIndex(string name)
        {
            int? idx = null;

            if (name.Contains(":"))
            {
                string[] parts = name.Split(new char[] { ':' });

                if (parts.Length > 1)
                {
                    int val = 0;
                    if (int.TryParse(parts[1], out val))
                    {
                        idx = val;
                    }
                }
            }

            return idx;
        }

        public static string FormatName(string baseName, int? idx = null, string qualifier = null)
        {
            string name = baseName;

            if (idx.HasValue)
            {
                name += ":" + idx.Value.ToString("D2");
            }

            if (qualifier != null)
            {
                if (!idx.HasValue)
                {
                    name += ":";
                }
                name += ":" + qualifier;
            }

            return name;
        }
        
        #endregion

        #region Diagnostics
        public override void Dump()
        {
            base.Dump();

            string output = string.Format("Id={0},SongId={1},Name={2},Value={3}", Id, SongId, Name, Value);
            Trace.WriteLine(output);
        }
        #endregion
    }
}
