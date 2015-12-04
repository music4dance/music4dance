using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;

namespace m4dModels
{
    public class SongProperty
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

        public SongProperty(SongProperty prop)
        {
            SongId = prop.SongId;
            Name = prop.Name;
            Value = prop.Value;
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
                if (string.Equals(name, SongBase.TempoField))
                {
                    value = FormatTempo(value);
                }
            }

            Value = value;
        }
        public SongProperty(Guid songId, string baseName, string value = null, int index = -1, string qual = null)
        {
            SongId = songId;

            var name = baseName;

            if (index >= 0)
            {
                name = $"{name}:{index:D2}";
            }

            if (qual != null)
            {
                name = name + ":" + qual;
            }

            Name = name;
            Value = value;
        }

        public static SongProperty Create(string baseName, string value = null, int index = -1, string qual = null)
        {
            return new SongProperty(Guid.Empty,baseName,value,index,qual);
        }

        public SongProperty CopyTo(Song song)
        {
            var n = new SongProperty
            {
                SongId = song.SongId,
                Song = song,
                Name = Name,
                Value = Value
            };
            return n;
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
                    case SongBase.TempoField:
                        // decimal
                        if (Value != null)
                        {
                            decimal v;
                            decimal.TryParse(Value, out v);
                            ret = v;
                        }
                        break;
                    case SongBase.LengthField:
                    case SongBase.TrackField:
                    case SongBase.DanceRatingField:
                        //int
                        if (Value != null)
                        {
                            int v;
                            int.TryParse(Value, out v);
                            ret = v;
                        }
                        break;
                    case SongBase.TimeField:
                        {
                            DateTime v;
                            DateTime.TryParse(Value, out v);
                            ret = v;
                        }
                        break;
                    case SongBase.OwnerHash:
                        {
                            int hash;
                            if (int.TryParse(Value, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out hash))
                            {
                                ret = hash;
                            }
                        }
                        break;
                    case SongBase.LikeTag:
                        {
                            bool like;
                            if (bool.TryParse(Value, out like))
                            {
                                ret = like;
                            }
                        }
                        break;
                    default:
                        ret = Value;
                        break;
                }

                return ret;
            }
        }
        public bool IsComplex => IsComplexName(Name);
        public bool IsAction => IsActionName(Name);

        public static bool IsComplexName(string name)
        {
            return name.Contains(":");
        }
        public static bool IsActionName(string name)
        {
            return name.StartsWith(".");
        }

        public string BaseName => ParseBaseName(Name);

        public int? Index => ParseIndex(Name);

        public string Qualifier => ParseQualifier(Name);

        public string DanceQualifier => ParsePart(Name,1);

        #endregion

        #region Overrides
        public override string ToString()
        {
            var value = Value;
            if (string.IsNullOrWhiteSpace(value))
            {
                value = string.Empty;
            }
            else if (string.Equals(BaseName, SongBase.TempoField))
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

            return $"{Name}={value}";
        }
        
        #endregion

        #region Serialization
        public static string Serialize(IEnumerable<SongProperty> properties, string[] actions)
        {
            var sb = new StringBuilder();

            var sep = string.Empty;
            foreach (var sp in properties)
            {
                if (actions == null || !actions.Contains(sp.Name))
                {
                    var p = sp.ToString();

                    sb.AppendFormat("{0}{1}", sep, p);

                    sep = "\t";
                }
            }

            return sb.ToString();
        }

        public static void Load(Guid songId, string props, ICollection<SongProperty> properties)
        {
            var cells = props.Split('\t');

            foreach (var cell in cells)
            {
                var values = cell.Split('=');

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

        public static string ParsePart(string name, int idx)
        {
            var parts = name.Split(':');
            return (parts.Length > idx) ? parts[idx] : null;
        }

        public static string ParseBaseName(string name)
        {
            return ParsePart(name, 0);
        }


        public static string ParseQualifier(string name)
        {
            return ParsePart(name, 2);
        }

        public static int? ParseIndex(string name)
        {
            int? idx = null;

            var part = ParsePart(name, 1);
            if (part != null)
            {
                int val;
                if (int.TryParse(part, out val))
                {
                    idx = val;
                }                
            }

            return idx;
        }

        public static string FormatName(string baseName, int? idx = null, string qualifier = null)
        {
            var name = baseName;

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
    }
}
