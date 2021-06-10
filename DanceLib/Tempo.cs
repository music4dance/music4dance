using System;
using System.Diagnostics;

namespace DanceLibrary
{
    public class Tempo
    {
        public static readonly string PositiveDecimalRate =
            "Tempo must start with a positive integer";

        private static readonly TempoType _bps = new TempoType(TempoKind.BPS, null);

        public Tempo(decimal rate, TempoType tempoType)
        {
            Rate = rate;
            TempoType = tempoType;
        }

        public Tempo(decimal bpm) : this(bpm, new TempoType(TempoKind.BPM, null))
        {
        }

        /// <summary>
        ///     Create a Tempo from a string of format "{positive decimal} [BPS|BPM|([MPM ]{positive int}/{positive int})]"
        /// </summary>
        /// <param name="s"></param>
        public Tempo(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                throw new ArgumentNullException();
            }

            s = s.Trim();

            var rateString = s;
            var typeString = string.Empty;
            var ispace = s.IndexOf(' ');
            if (ispace > 0)
            {
                rateString = s.Substring(0, ispace);
                typeString = s.Substring(ispace + 1);
            }

            decimal rate;
            if (!decimal.TryParse(rateString, out rate))
            {
                throw new ArgumentOutOfRangeException(PositiveDecimalRate);
            }

            Rate = rate;
            TempoType = new TempoType(typeString);
        }

        public static Tempo DefaultTempo { get; } =
            new Tempo(32M, new TempoType(TempoKind.MPM, new Meter(4, 4)));

        public decimal SecondsPerBeat
        {
            get
            {
                var t = Normalize();
                return 1 / t.Rate;
            }
        }

        public decimal SecondsPerMeasure
        {
            get
            {
                var spb = SecondsPerBeat;
                if (TempoType.TempoKind == TempoKind.MPM)
                {
                    return spb * TempoType.Meter.Numerator;
                }

                return spb;
            }
        }

        public decimal Rate { get; }
        public TempoType TempoType { get; }

        public Tempo Convert(TempoType tempoType)
        {
            if (tempoType == TempoType)
            {
                return this;
            }

            var normalized = Normalize();

            switch (tempoType.TempoKind)
            {
                case TempoKind.BPS: return normalized;
                case TempoKind.BPM: return new Tempo(normalized.Rate * 60, tempoType);
                case TempoKind.MPM:
                    return new Tempo(normalized.Rate * 60 / tempoType.Meter.Numerator, tempoType);
                default:
                    Debug.Assert(false);
                    return null;
            }
        }

        public Tempo Normalize()
        {
            switch (TempoType.TempoKind)
            {
                case TempoKind.BPS: return this;
                case TempoKind.BPM: return new Tempo(Rate / 60, _bps);
                case TempoKind.MPM: return new Tempo(Rate * TempoType.Meter.Numerator / 60, _bps);
                default:
                    Debug.Assert(false);
                    return null;
            }
        }

        public override string ToString()
        {
            return $"{Rate} {TempoType}";
        }

        public override bool Equals(object obj)
        {
            var tempo = obj as Tempo;
            if (tempo == null)
            {
                return false;
            }

            return TempoType == tempo.TempoType && Rate == tempo.Rate;
        }

        public override int GetHashCode()
        {
            return (TempoType.GetHashCode() * 1023) ^ Rate.GetHashCode();
        }

        public static bool operator ==(Tempo a, Tempo b)
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

        public static bool operator !=(Tempo a, Tempo b)
        {
            return !(a == b);
        }
    }
}
