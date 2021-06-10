using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Newtonsoft.Json;

namespace DanceLibrary
{
    public enum TempoKind
    {
        BPM,
        MPM,
        BPS
    }

    /// <summary>
    ///     Represents a rate with a labe (MPM, BPM, BPS), in other word a tempo
    ///     This is an immutable class
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class TempoType : IConversand
    {
        public static readonly string TempoSyntaxError = "Sytax error in tempo";


        private static List<TempoType> s_commonTempi;

        static TempoType()
        {
            s_commonTempi = new List<TempoType>(5);
            s_commonTempi.Add(new TempoType(TempoKind.BPM));
            s_commonTempi.Add(new TempoType(TempoKind.BPS));
            s_commonTempi.Add(new TempoType(TempoKind.MPM, new Meter(2, 4)));
            s_commonTempi.Add(new TempoType(TempoKind.MPM, new Meter(3, 4)));
            s_commonTempi.Add(new TempoType(TempoKind.MPM, new Meter(4, 4)));
        }

        private TempoType()
        {
        }

        [JsonConstructor]
        public TempoType(TempoKind kind, Meter meter)
        {
            TempoKind = kind;
            Meter = meter;
        }

        public TempoType(TempoKind kind) : this(kind, null)
        {
        }

        /// <summary>
        ///     Create a TempoType from a string of format "[BPS|BPM|([MPM ]{positive int}/{positive int})]"
        /// </summary>
        /// <param name="s"></param>
        public TempoType(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                TempoKind = TempoKind.BPM;
                return;
            }

            s = s.Trim();
            var fields = s.Split(new[] { ' ' });
            if (fields == null || fields.Length < 1)
            {
                throw new ArgumentOutOfRangeException(TempoSyntaxError);
            }

            if (string.Equals(fields[0], "BPS", StringComparison.OrdinalIgnoreCase))
            {
                TempoKind = TempoKind.BPS;
                if (fields.Length > 1)
                {
                    throw new ArgumentOutOfRangeException(TempoSyntaxError);
                }
            }
            else if (string.Equals(fields[0], "BPM", StringComparison.OrdinalIgnoreCase))
            {
                TempoKind = TempoKind.BPM;
                if (fields.Length > 1)
                {
                    throw new ArgumentOutOfRangeException(TempoSyntaxError);
                }
            }
            else
            {
                TempoKind = TempoKind.MPM;
                if (string.Equals(fields[0], "MPM", StringComparison.OrdinalIgnoreCase))
                {
                    if (fields.Length != 2)
                    {
                        throw new ArgumentOutOfRangeException(TempoSyntaxError);
                    }

                    Meter = new Meter(fields[1]);
                }
                else
                {
                    Meter = new Meter(fields[0]);
                    if (fields.Length > 1)
                    {
                        throw new ArgumentOutOfRangeException(TempoSyntaxError);
                    }
                }
            }
        }

        /// <summary>
        ///     Kind of tempo (MPM, BPM, BPS)
        /// </summary>
        [JsonProperty]
        public TempoKind TempoKind { get; private set; }

        /// <summary>
        ///     Meter for MPM kinds
        /// </summary>
        [JsonProperty]
        public Meter Meter { get; private set; }

        public static ReadOnlyCollection<TempoType> CommonTempi =>
            new ReadOnlyCollection<TempoType>(s_commonTempi);

        public static string TypeName => "Tempo";

        public override string ToString()
        {
            switch (TempoKind)
            {
                case TempoKind.BPM: return "BPM";
                case TempoKind.BPS: return "BPS";
                case TempoKind.MPM: return $"MPM {Meter}";
                default:
                    System.Diagnostics.Debug.Assert(false);
                    return "#ERROR#";
            }
        }

        public override bool Equals(object obj)
        {
            var tempo = obj as TempoType;
            if (tempo == null)
            {
                return false;
            }

            return TempoKind == tempo.TempoKind && Meter == tempo.Meter;
        }

        public override int GetHashCode()
        {
            var hash = TempoKind.GetHashCode();
            if (Meter != null)
            {
                hash += 7 * Meter.GetHashCode();
            }

            return hash;
        }

        public static bool operator ==(TempoType a, TempoType b)
        {
            // If both are null, or both are same instance, return true.
            if (ReferenceEquals(a, b))
            {
                return true;
            }

            // Handle a is null case☺.
            if ((object)a == null)
            {
                return (object)b == null;
            }

            return a.Equals(b);
        }

        public static bool operator !=(TempoType a, TempoType b)
        {
            return !(a == b);
        }


        #region IConversand Implementation

        public Kind Kind => Kind.Tempo;

        public string Name => ToString();

        public string Label => TypeName;

        #endregion
    }
}
