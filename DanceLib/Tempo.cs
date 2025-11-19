using System.Diagnostics;

namespace DanceLibrary
{
    public class Tempo
    {
        public static readonly string PositiveDecimalRate =
            "Tempo must start with a positive integer";

        private static readonly TempoType s_bps = new(TempoKind.Bps, null);

        public Tempo(decimal rate, TempoType tempoType)
        {
            Rate = rate;
            TempoType = tempoType;
        }

        public Tempo(decimal bpm) : this(bpm, new TempoType(TempoKind.Bpm, null))
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
                rateString = s[..ispace];
                typeString = s[(ispace + 1)..];
            }

            if (!decimal.TryParse(rateString, out var rate))
            {
                throw new ArgumentOutOfRangeException(PositiveDecimalRate);
            }

            Rate = rate;
            TempoType = new TempoType(typeString);
        }

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
                return TempoType.TempoKind == TempoKind.Mpm ? spb * TempoType.Meter.Numerator : spb;
            }
        }

        public decimal BeatsPerMinute => Convert(new TempoType(TempoKind.Bpm)).Rate;

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
                case TempoKind.Bps: return normalized;
                case TempoKind.Bpm: return new Tempo(normalized.Rate * 60, tempoType);
                case TempoKind.Mpm:
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
                case TempoKind.Bps: return this;
                case TempoKind.Bpm: return new Tempo(Rate / 60, s_bps);
                case TempoKind.Mpm: return new Tempo(Rate * TempoType.Meter.Numerator / 60, s_bps);
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
            return tempo != null && (TempoType == tempo.TempoType && Rate == tempo.Rate);
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
            return a?.Equals(b) ?? false;
        }

        public static bool operator !=(Tempo a, Tempo b)
        {
            return !(a == b);
        }
    }
}
