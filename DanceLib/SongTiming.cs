using System.Diagnostics;

namespace DanceLibrary
{
    /// <summary>
    ///     This class encapsulates the information about a song's timing - meter, tempo, length
    /// </summary>
    public class SongTiming
    {
        public Tempo Tempo { get; set; }
        public SongDuration Duration { get; set; }
        public DurationKind DurationKind { get; set; }

        public override string ToString()
        {
            var d = Duration.ToString();
            var dk = ((DurationType)DurationKind).ToString();
            var t = Tempo.ToString();

            return $"{d},{dk},{t}";
        }

        /// <summary>
        ///     Rate in current TempoType units per second
        /// </summary>
        /// <param name="rate"></param>
        public void SetRate(decimal rate)
        {
            var perUnit = rate;

            if (Tempo.TempoType.TempoKind != TempoKind.BPS)
            {
                perUnit *= 60;
            }

            SetDenormalizedRate(perUnit);
        }

        /// <summary>
        ///     Rate in current units per current units
        /// </summary>
        /// <param name="perUnit"></param>
        public void SetDenormalizedRate(decimal perUnit)
        {
            Tempo = new Tempo(perUnit, Tempo.TempoType);
        }

        /// <summary>
        ///     Duration in seconds
        /// </summary>
        /// <param name="length"></param>
        public void SetLength(decimal length)
        {
            Duration = length;
        }

        public decimal GetBiasedLength()
        {
            return Duration.LengthIn(DurationKind, Tempo);
        }

        /// <summary>
        ///     Set tempo in current TempoType at rate of BPS
        /// </summary>
        /// <param name="rate"></param>
        public void SetBPS(decimal rate)
        {
            var tempoBPS = new Tempo(rate, new TempoType(TempoKind.BPS, null));
            Tempo = tempoBPS.Convert(Tempo.TempoType);
        }

        public void Convert(IConversand conversand)
        {
            switch (conversand.Kind)
            {
                case Kind.Duration:
                    Debug.Assert(conversand is DurationType);
                    DurationKind = conversand as DurationType;
                    break;
                case Kind.Tempo:
                    Debug.Assert(conversand is TempoType);
                    Tempo = Tempo.Convert(conversand as TempoType);
                    break;
                default:
                    Debug.Assert(false);
                    break;
            }
        }

        public override bool Equals(object obj)
        {
            var st = obj as SongTiming;
            if (st == null)
            {
                return false;
            }

            return Duration == st.Duration && Tempo == st.Tempo && DurationKind == st.DurationKind;
        }

        public override int GetHashCode()
        {
            return DurationKind.GetHashCode() ^ Duration.GetHashCode() ^ Tempo.GetHashCode();
        }

        #region Constructors

        public SongTiming()
        {
            Tempo = new Tempo(32.0m, new TempoType(TempoKind.MPM, new Meter(4, 4)));
            Duration = new SongDuration(90);
            DurationKind = DurationKind.Measure;
        }

        public SongTiming(Tempo tempo, decimal length, DurationKind dk)
        {
            Tempo = tempo;
            Duration = new SongDuration(length, dk, tempo);
            DurationKind = dk;
        }

        public SongTiming(string s) : this()
        {
            var rgs = s.Split(new[] { ',' });
            if (rgs.Length == 3)
            {
                Duration = new SongDuration(rgs[0].Trim());
                DurationKind = new DurationType(rgs[1]);
                Tempo = new Tempo(rgs[2].Trim());
            }
        }

        #endregion
    }
}
