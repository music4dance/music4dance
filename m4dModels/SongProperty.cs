using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;

namespace m4dModels
{
    public class SongProperty : IEquatable<SongProperty>
    {
        #region Overrides
        public override bool Equals(object obj)
        {
            return Equals(obj as SongProperty);
        }

        public bool Equals(SongProperty other)
        {
            return other is not null &&
                   Name == other.Name &&
                   (Value == other.Value || TagsEqual(other));
        }

        private bool TagsEqual(SongProperty other)
        {
            if (!(IsTag && other.IsTag))
            {
                return false;
            }

            return new TagList(Value).ToString() == new TagList(other.Value).ToString();
        }

        private bool IsTag => BaseName == Song.AddedTags || BaseName == Song.RemovedTags;

        public override int GetHashCode()
        {
            return HashCode.Combine(Name, Value);
        }

        public static bool operator ==(SongProperty left, SongProperty right)
        {
            return EqualityComparer<SongProperty>.Default.Equals(left, right);
        }

        public static bool operator !=(SongProperty left, SongProperty right)
        {
            return !(left == right);
        }


        public override string ToString()
        {
            var value = Value;
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

            return $"{Name}={value}";
        }

        #endregion

        // Name Syntax: BaseName[:idx[:qual]]|[:danceId]
        // idx is zeros based index for multi-value fields (only album at this point?)
        // qual is a qualifier for purchase type (may generalize?)
        //
        // Not implementing this yet, but for artist might allow artist type after the colon

        #region Constructors

        public SongProperty()
        {
        }

        public SongProperty(SongProperty prop)
        {
            Name = prop.Name;
            Value = prop.Value;
        }

        public SongProperty(string name, string value)
        {
            Name = name;

            if (!string.IsNullOrWhiteSpace(value))
            {
                if (value.Contains("\\t"))
                {
                    value = value.Replace("\\t", "\t");
                }

                if (value.Contains("\\<EQ>\\"))
                {
                    value = value.Replace("\\<EQ>\\", "=");
                }

                if (string.Equals(name, Song.TempoField))
                {
                    value = FormatTempo(value);
                }
            }

            Value = value;
        }

        public SongProperty(string baseName, string value = null, int index = -1,
            string qual = null)
        {
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

        public static SongProperty Create(string baseName, string value = null, int index = -1,
            string qual = null)
        {
            return new SongProperty(baseName, value, index, qual);
        }

        #endregion

        #region Properties

        public string Name { get; set; }
        public string Value { get; set; }

        public object ObjectValue
        {
            get
            {
                object ret = null;
                switch (BaseName)
                {
                    case SongIndex.SongIdField:
                        if (!string.IsNullOrEmpty(Value))
                        {
                            if (Guid.TryParse(Value, out var id))
                            {
                                ret = id;
                            }
                        }

                        break;
                    case Song.TempoField:
                        // decimal
                        if (!string.IsNullOrEmpty(Value))
                        {
                            _ = decimal.TryParse(Value, out var v);
                            ret = v;
                        }

                        break;
                    case Song.DanceabilityField:
                    case Song.ValenceField:
                    case Song.EnergyField:
                        // float
                        if (!string.IsNullOrEmpty(Value))
                        {
                            _ = float.TryParse(Value, out var v);
                            ret = v;
                        }

                        break;
                    case Song.LengthField:
                    case Song.TrackField:
                    case Song.DanceRatingField:
                        //int
                        if (!string.IsNullOrEmpty(Value))
                        {
                            _ = int.TryParse(Value, out var v);
                            ret = v;
                        }

                        break;
                    case Song.TimeField:
                        {
                            _ = DateTime.TryParse(Value, out var v);
                            ret = v;
                        }
                        break;
                    case Song.OwnerHash:
                        {
                            if (int.TryParse(
                                Value, NumberStyles.HexNumber, CultureInfo.CurrentCulture,
                                out var hash))
                            {
                                ret = hash;
                            }
                        }
                        break;
                    case Song.LikeTag:
                        {
                            if (bool.TryParse(Value, out var like))
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
        public bool IsEdit => BaseName == Song.EditCommand || BaseName == Song.CreateCommand;

        public bool IsNull => string.IsNullOrWhiteSpace(Value);

        public static bool IsComplexName(string name)
        {
            return name.Contains(':');
        }

        public static bool IsActionName(string name)
        {
            return name.StartsWith('.');
        }

        public string BaseName => ParseBaseName(Name);

        public int? Index => ParseIndex(Name);

        public string Qualifier => ParseQualifier(Name);

        public string DanceQualifier => ParsePart(Name, 1);

        #endregion

        #region Serialization

        // During serialize replace tabs and newlines with unicode sub + T and R respectively
        //  do the opposite during deserialization.
        public static string Serialize(IEnumerable<SongProperty> properties, string[] actions = null, string options = null)
        {
            var sb = new StringBuilder();

            var sep = string.Empty;
            foreach (var sp in properties)
            {
                if (actions != null && actions.Contains(sp.Name))
                {
                    continue;
                }

                var p = sp.ToString().Replace("\t", "\u001aT").Replace("\r\n", "\u001aR");

                _ = sb.AppendFormat("{0}{1}", sep, p);

                sep = SerializationSeparator(options);
            }

            return sb.ToString();
        }

        public static string SerializationSeparator(string options)
        {
            return options == null || !options.Contains('R') ? "\t" : "\r\n";
        }

        public static IEnumerable<SongProperty> Load(string props)
        {
            var properties = new List<SongProperty>();

            Load(props, properties);

            return properties;
        }

        public static void Load(string props, ICollection<SongProperty> properties)
        {
            var cells = props.Split('\t');

            foreach (var cell in cells)
            {
                var idx = cell.IndexOf('=');

                if (idx != -1)
                {
                    var p = cell[(idx + 1)..];
                    if (p.Contains('\u001a'))
                    {
                        p = p.Replace("\u001aT", "\t").Replace("\u001aR", "\r\n");
                    }

                    var name = cell[..idx];
                    properties.Add(new SongProperty(name, p));
                }
                else if (cell.StartsWith('.'))
                {
                    properties.Add(new SongProperty(cell));
                }
                else
                {
                    Trace.WriteLineIf(
                        TraceLevels.General.TraceError, $"Bad SongProperty: {cell}");
                }
            }
        }

        public static string SerializeValue(object o)
        {
            return o?.ToString();
        }

        #endregion

        #region Static Helpers

        private static string FormatTempo(string value)
        {
            if (decimal.TryParse(value, out var v))
            {
                value = v.ToString("F1");
            }

            return value;
        }

        public static string ParsePart(string name, int idx)
        {
            var parts = name.Split(':');
            return parts.Length > idx ? parts[idx] : null;
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
                if (int.TryParse(part, out var val))
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
