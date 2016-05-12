using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace DanceLibrary
{
    public class Tempo
    {
        public static readonly string PositiveDecimalRate = "Tempo must start with a positive integer";

        public Tempo(decimal rate, TempoType tempoType)
        {
            Rate = rate;
            TempoType = tempoType;
        }

        public Tempo(decimal bpm) : this(bpm, new TempoType(TempoKind.BPM, null))
        {
        }

        /// <summary>
        /// Create a Tempo from a string of format "{positive decimal} [BPS|BPM|([MPM ]{positive int}/{positive int})]"
        /// </summary>
        /// <param name="s"></param>
        public Tempo(string s)
        {
            if (string.IsNullOrEmpty(s)) throw new ArgumentNullException();

            s = s.Trim();

            string rateString = s;
            string typeString = string.Empty;
            int ispace = s.IndexOf(' ');
            if (ispace > 0)
            {
                rateString = s.Substring(0,ispace);
                typeString = s.Substring(ispace+1);
            }

            decimal rate;
            if (!decimal.TryParse(rateString, out rate))
                throw new ArgumentOutOfRangeException(PositiveDecimalRate);

            Rate = rate;
            TempoType = new TempoType(typeString);
        }

        public static Tempo DefaultTempo
        {
            get { return _defaulTempo; }
        }
        private static Tempo _defaulTempo = new Tempo(32M, new TempoType(TempoKind.MPM, new Meter(4, 4)));

        public Tempo Convert(TempoType tempoType)
        {
            if (tempoType == TempoType)
                return this;

            Tempo normalized = this.Normalize();

            switch (tempoType.TempoKind)
            {
                case TempoKind.BPS: return normalized;
                case TempoKind.BPM: return new Tempo(normalized.Rate * 60, tempoType);
                case TempoKind.MPM: return new Tempo((normalized.Rate * 60) / tempoType.Meter.Numerator, tempoType);
                default: Debug.Assert(false); return null;
            }
        }

        public Tempo Normalize()
        {
            switch (TempoType.TempoKind)
            {
                case TempoKind.BPS: return this;
                case TempoKind.BPM: return new Tempo(Rate/60,_bps);
                case TempoKind.MPM: return new Tempo((Rate * TempoType.Meter.Numerator)/ 60, _bps);
                default: Debug.Assert(false); return null;
            }
        }

        public decimal SecondsPerBeat
        {
            get
            {
                Tempo t = Normalize();
                return 1 / t.Rate;
            }
        }

        public decimal SecondsPerMeasure
        {
            get
            {
                decimal spb = SecondsPerBeat;
                if (TempoType.TempoKind == TempoKind.MPM)
                {
                    return spb * TempoType.Meter.Numerator;
                }
                else
                {
                    return spb;
                }
            }
        }
        public override string ToString()
        {
            return string.Format("{0} {1}", Rate, TempoType);
        }

        public override bool Equals(object obj)
        {
            Tempo tempo = obj as Tempo;
            if (tempo == null)
                return false;
            else
                return (TempoType == tempo.TempoType) && (Rate == tempo.Rate);
        }

        public override int GetHashCode()
        {
            return TempoType.GetHashCode() * 1023 ^ Rate.GetHashCode();
        }

        public static bool operator ==(Tempo a, Tempo b)
        {
            // If both are null, or both are same instance, return true.
            if (System.Object.ReferenceEquals(a, b))
            {
                return true;
            }

            // Handle a is null case☺.
            if (((object)a == null))
            {
                return ((object)b == null);
            }

            return a.Equals(b);
        }

        public static bool operator !=(Tempo  a, Tempo b)
        {
            return !(a == b);
        }

        public decimal Rate {get; private set;}
        public TempoType TempoType {get; private set;}

        private static readonly TempoType _bps = new TempoType(TempoKind.BPS, null);
    }
}
