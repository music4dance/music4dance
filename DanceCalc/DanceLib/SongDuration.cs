using System;
using System.Collections.Generic;

namespace DanceLibrary
{
    public enum DurationFormat { Short, Long };

    /// <summary>
    /// Representation of the duration of a song 
    /// 
    /// This is really just a wrapper around a decimal number of seconds
    /// that allows easy formatting and storage
    /// </summary>
    public struct SongDuration : IComparable<SongDuration>
    {
        /// <summary>
        /// Create a song duration with a number of seconds
        /// </summary>
        /// <param name="length">number of seconds</param>
        public SongDuration(decimal length)
        {
            _length = length;
        }

        /// <summary>
        /// Create a song with minutes or seconds
        /// </summary>
        /// <param name="length">minutes or seconds depending on type</param>
        /// <param name="type">only valid as Seconds or Minutes</param>
        public SongDuration(decimal length, DurationType type)
        {
            if (type.DurationKind == DurationKind.Beat || type.DurationKind == DurationKind.Measure)
                throw new ArgumentOutOfRangeException("type","only Seconds or Minutes are allowed");

            _length = length;
        }

        public decimal Length
        {
            get { return _length; }
        }

        public int Minutes
        {
            get
            {
                return (int) (_length / 60);
            }
        }

        public int Seconds
        {
            get
            {
                return (int) (_length - 60 * Minutes);
            }
        }

        public string Format(DurationFormat f)
        {
            string[] rgs = {"{0:N0}s", "{0}m", "{0}m{1}s"};
            string[] rgl = {"{0:N0} second(s)", "{0} minute(s)", "{0} minutes, {1} seconds"};
            string[] rg = rgl;

            bool exact = false;
            if (_length / 60  == Minutes)
            {
                exact = true;
            }
            
            if (f == DurationFormat.Short)
            {
                rg = rgs;
            }

            if (_length < 100)
            {
                return string.Format(rg[0], _length);
            }
            else if (exact)
            {
                return string.Format(rg[1], Minutes);
            }
            else
            {
                return string.Format(rg[2], Minutes, Seconds);
            }
        }

        public string Name
        {
            get { return Format(DurationFormat.Long); }
        }

        public string ShortName
        {
            get { return Format(DurationFormat.Short); }
        }

        public override string ToString()
        {
            string ret = Format(DurationFormat.Short);
            return ret;
        }

        public static IEnumerable<SongDuration> GetStandarDurations()
        {
            return _durations;
        }

        static public implicit operator decimal(SongDuration sd)
        {
            return sd._length;
        }

        static public implicit operator SongDuration(decimal d)
        {
            return new SongDuration(d);
        }

        private decimal _length;
        private static SongDuration[] _durations = { new SongDuration(30M), new SongDuration(60M), new SongDuration(90M), new SongDuration(120M), new SongDuration(150M), new SongDuration(180M), new SongDuration(240M), new SongDuration(300M) };

        ///// <summary>
        ///// Create a new SongDuration keeping the absolute duration equal but with a new type
        ///// </summary>
        ///// <param name="type">New type</param>
        ///// <returns></returns>
        //public SongDuration NewType(DurationType type)
        //{
        //    switch (type.DurationKind)
        //    {
        //        case DurationKind.Beat

        //    return 
        //}

        //private static decimal ConvertToSeconds(decimal length, DurationKind dk, Meter meter)
        //{
        //    switch (dk)
        //    {
        //        case DurationKind.Second:
        //            return length;
        //        case DurationKind.Measure:
        //            return length / 60;
        //        case DurationKind.Beat:
        //            sf

        //}

        public int CompareTo(SongDuration other)
        {
            return _length.CompareTo(other.Length);
        }


        public override bool Equals(object obj)
        {
            if (obj is SongDuration)
            {
                SongDuration other = (SongDuration)obj;
                return CompareTo(other) == 0;
            }
            else
            {
                return false;
            }
        }

        public override int GetHashCode()
        {
            return _length.GetHashCode();
        }

        public static bool operator ==(SongDuration a, SongDuration b)
        {
            return a._length == b._length;
        }

        public static bool operator !=(SongDuration a, SongDuration b)
        {
            return a._length != b._length;
        }

    }
}
