using System;
using Newtonsoft.Json;

namespace DanceLibrary
{
    public enum TempoKind
    {
        Bpm,
        Mpm,
        Bps
    }

    /// <summary>
    ///     Represents a rate with a labe (MPM, BPM, BPS), in other word a tempo
    ///     This is an immutable class
    /// </summary>
    public class TempoType
    {
        public static readonly string TempoSyntaxError = "Sytax error in tempo";

        private TempoType()
        {
        }

        [JsonConstructor]
        public TempoType(TempoKind kind, Meter meter = null)
        {
            TempoKind = kind;
            Meter = meter;
        }

        /// <summary>
        ///     Create a TempoType from a string of format "[BPS|BPM|([MPM ]{positive int}/{positive int})]"
        /// </summary>
        /// <param name="s"></param>
        public TempoType(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                TempoKind = TempoKind.Bpm;
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
                TempoKind = TempoKind.Bps;
                if (fields.Length > 1)
                {
                    throw new ArgumentOutOfRangeException(TempoSyntaxError);
                }
            }
            else if (string.Equals(fields[0], "BPM", StringComparison.OrdinalIgnoreCase))
            {
                TempoKind = TempoKind.Bpm;
                if (fields.Length > 1)
                {
                    throw new ArgumentOutOfRangeException(TempoSyntaxError);
                }
            }
            else
            {
                TempoKind = TempoKind.Mpm;
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
        public TempoKind TempoKind { get; }

        /// <summary>
        ///     Meter for MPM kinds
        /// </summary>
        public Meter Meter { get; }

        [JsonIgnore]
        public static string TypeName => "Tempo";

        public override string ToString()
        {
            switch (TempoKind)
            {
                case TempoKind.Bpm: return "BPM";
                case TempoKind.Bps: return "BPS";
                case TempoKind.Mpm: return $"MPM {Meter}";
                default:
                    System.Diagnostics.Debug.Assert(false);
                    return "#ERROR#";
            }
        }

        public override bool Equals(object obj)
        {
            var tempo = obj as TempoType;
            return tempo != null && (TempoKind == tempo.TempoKind && Meter == tempo.Meter);
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
            return a?.Equals(b) ?? false;
        }

        public static bool operator !=(TempoType a, TempoType b)
        {
            return !(a == b);
        }
    }
}
