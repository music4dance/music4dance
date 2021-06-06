using System;
using System.Text;

namespace m4dModels
{
    // TrackNumber is an extended [work][volume]track index
    //  all three are 1 based with max value of 999
    //  work and volume can be null (zero represnets null)
    // String format is [www:][aaa:][ttt]
    public class TrackNumber : IComparable
    {
        #region Constructors

        public TrackNumber(int num)
        {
            _val = num;
        }

        public TrackNumber(int track, int? volume, int? work)
        {
            Initialize(track, volume, work);
        }

        public TrackNumber(string s)
        {
            var track = 0;
            var volume = 0;
            var work = 0;

            var cells = new string[] { };
            if (s != null) cells = s.Split(':');
            if (cells.Length > 0 && int.TryParse(cells[cells.Length - 1], out track))
                if (cells.Length > 1 && int.TryParse(cells[cells.Length - 2], out volume))
                    if (cells.Length > 2)
                        int.TryParse(cells[cells.Length - 3], out work);

            Initialize(track, volume, work);
        }

        private void Initialize(int? track, int? volume, int? work)
        {
            var t = 0;
            var a = 0;
            var w = 0;

            if (track.HasValue) t = track.Value;

            if (volume.HasValue) a = volume.Value;

            if (work.HasValue) w = work.Value;

            _val = t + a * 1000 + w * 1000000;
        }

        #endregion

        #region Properties

        public int? Track
        {
            get
            {
                int? track = _val % 1000;
                if (track.Value == 0) track = null;
                return track;
            }
            set => Initialize(value, Volume, Work);
        }

        public int? Volume
        {
            get
            {
                int? volume = _val / 1000 % 1000;
                if (volume.Value == 0) volume = null;
                return volume;
            }
            set => Initialize(Track, value, Work);
        }

        public int? Work
        {
            get
            {
                int? work = _val / 1000000 % 1000000;
                if (work.Value == 0) work = null;
                return work;
            }
            set => Initialize(Track, value, Work);
        }

        #endregion

        #region Operators

        public static implicit operator int(TrackNumber track)
        {
            return track._val;
        }

        public static implicit operator TrackNumber(int val)
        {
            return new TrackNumber(val);
        }

        public int CompareTo(object other)
        {
            if (other is TrackNumber)
                return _val.CompareTo(((TrackNumber) other)._val);
            else
                return -1;
        }

        public override bool Equals(object obj)
        {
            return CompareTo(obj) == 0;
        }

        public override int GetHashCode()
        {
            return _val.GetHashCode();
        }

        public static bool operator ==(TrackNumber a, TrackNumber b)
        {
            if ((object) a == null || (object) b == null)
                return (object) a == (object) b;
            else
                return a.Equals(b);
        }

        public static bool operator !=(TrackNumber a, TrackNumber b)
        {
            return !(a == b);
        }

        public override string ToString()
        {
            return Format();
        }

        private static readonly string[] s_invariantFormat = new string[]
            {"{0:D3}:", "{0:D3}:", "{0:D3}"};

        private static readonly string[] s_friendlyFormat = new string[]
            {"Work {0}, ", "Disk {0}, ", "Track {0}"};

        public string Format(string specifier = null)
        {
            var sb = new StringBuilder();
            var work = Work;
            var volume = Volume;
            var track = Track;

            var format = string.IsNullOrWhiteSpace(specifier)
                ? s_invariantFormat
                : s_friendlyFormat;

            if (work.HasValue) sb.AppendFormat(format[0], work.Value);
            if (volume.HasValue) sb.AppendFormat(format[1], volume.Value);
            if (track.HasValue) sb.AppendFormat(format[2], track.Value);

            return sb.ToString();
        }

        #endregion

        private int _val;
    }
}