using System;
using System.Net;
using System.Runtime.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Diagnostics;

namespace DanceLibrary
{
    public class SongTiming
    {
        public SongTiming()
        {
            Meter = new Meter(4, 4);
            DurationType = new DurationType(DurationKind.Second);
            NormalizedLength = new SongDuration(0);
            NormalizedTempo = 1;
        }

        public SongTiming(Meter meter, DurationType dt, decimal tempo, decimal length)
        {
            Meter = meter;
            DurationType = dt;

            NormalizedTempo = NormalizeTempo(tempo, meter);
            NormalizedLength = NormalizeLength(length, NormalizedTempo, dt, meter);
        }

        public SongTiming(string s)
        {
            string[] rgs = s.Split(new char[] { ',' });
            Debug.Assert(rgs.Length == 4);

            Meter = new Meter(rgs[0]);
            DurationType = new DurationType(rgs[1]);
            NormalizedTempo = Math.Min(decimal.Parse(rgs[2]),1000M); //NormalizeTempo(, Meter);
            NormalizedLength = Math.Min(decimal.Parse(rgs[3]),10000M); //NormalizeLength(decimal.Parse(rgs[3]), NormalizedTempo, DurationType, Meter);
        }

        public override string ToString()
        {
            string m = Meter.ToString();
            string d = DurationType.ToString();
            string t = NormalizedTempo.ToString();
            string l = ((decimal)NormalizedLength).ToString();

            return string.Format("{0},{1},{2},{3}", m, d, t, l);
        }

        public Meter Meter {get;set;}
        public DurationType DurationType {get;set;}

        /// <summary>
        /// Tempo in M/BPM based on the current meter
        /// </summary>
        public decimal Tempo
        {
            get {return ComputeTempo(NormalizedTempo,Meter);}
            set { NormalizedTempo = NormalizeTempo(value, Meter); }
        }

        public decimal Length
        {
            get { return ComputeLength(NormalizedLength.Length, DurationType, NormalizedTempo, Meter); }
            set { NormalizedLength = NormalizeLength(value, NormalizedTempo, DurationType, Meter); }
        }

        public decimal Rate
        {
            get { return ComputeRate(NormalizedTempo); }
            set { NormalizedTempo = NormalizeRate(value, Meter); }
        }


        /// <summary>
        /// Convert length into seconds
        /// </summary>
        /// <param name="length">length in duration type/meter units</param>
        /// <param name="tempo">tempo in meter units</param>
        /// <param name="dt">duration type</param>
        /// 
        /// <returns></returns>
        static private SongDuration NormalizeLength(decimal length, decimal tempo, DurationType dt, Meter meter)
        {
            decimal ret = -1;
            switch (dt.DurationKind)
            {
                case DurationKind.Second:
                    ret = length;
                    break;
                case DurationKind.Minute:
                    ret = length * 60;
                    break;
                case DurationKind.Beat:
                    ret = (length * 60) / (60 * meter.Numerator);
                    break;
                case DurationKind.Measure:
                    ret = (length * meter.Numerator) / tempo;
                    break;
                default:
                    Debug.Assert(false);
                    break;
            }
            return new SongDuration(ret);
        }

        static private decimal NormalizeTempo(decimal tempo, Meter meter)
        {
            return (tempo * meter.Numerator) / 60;
        }

        /// <summary>
        /// Take the rate in number of seconds per measure/beat and convert that to tempo
        /// </summary>
        /// <param name="rate"></param>
        static decimal NormalizeRate(decimal rate, Meter meter)
        {
            decimal spb = rate / meter.Numerator; // Seconds Per Beat
            decimal bps = 1 / spb;

            System.Diagnostics.Debug.WriteLine("rate = {0}, spb = {1}, bps = {2}", rate,spb,bps);

            return bps;
        }

        /// <summary>
        /// Compute the Length of a song taking into account duration type, tempo, and meter
        /// </summary>
        /// <param name="length">length in seconds</param>
        /// <param name="dt">duration type</param>
        /// <param name="tempo">tempo in bps</param>
        /// <param name="meter">meter</param>
        /// <returns></returns>
        static private decimal ComputeLength(decimal length, DurationType dt, decimal tempo, Meter meter)
        {
            switch (dt.DurationKind)
            {
                case DurationKind.Second:
                    return length;
                case DurationKind.Minute:
                    return length/60;
                case DurationKind.Beat:
                    return length * tempo;
                case DurationKind.Measure:
                    return (length * tempo) / meter.Numerator;
            }
            Debug.Assert(false);
            return -1;
        }

        static private decimal ComputeTempo(decimal tempo, Meter meter)
        {
            return (tempo / meter.Numerator) * 60;
        }

        private decimal ComputeRate(decimal NormalizedTempo)
        {
            throw new NotImplementedException();
        }

        // Let's make these public but with the warning that they're normalized values that are generally
        //  only used for serialization

        public decimal NormalizedTempo { get; set; } // tempo in bps
        public SongDuration NormalizedLength { get; set; } // length in seconds
    }
}
