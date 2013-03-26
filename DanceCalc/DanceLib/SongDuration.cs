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
    /// 
    /// This is an immutable structure
    /// 
    /// TODO: Figure out what I was thinking about with durations of beats and measures, did
    ///     this get solved in another way (in which case I should clean up this class)
    /// </summary>
    public struct SongDuration : IComparable<SongDuration>
    {

#region Constructors
        /// <summary>
        /// Create a song duration with a number of seconds
        /// </summary>
        /// <param name="length">number of seconds</param>
        public SongDuration(decimal length) : this()
        {
            this.Length = length;
            Validate();
        }

        /// <summary>
        /// Create a song with minutes or seconds
        /// </summary>
        /// <param name="length">minutes or seconds depending on type</param>
        /// <param name="type">only valid as Seconds or Minutes</param>
        public SongDuration(decimal length, DurationType type) : this()
        {
            if (type.DurationKind == DurationKind.Beat || type.DurationKind == DurationKind.Measure)
                throw new ArgumentOutOfRangeException("type","only Seconds or Minutes are allowed");

            Length = length;

            Validate();
        }

        void Validate()
        {
            if (Length < 0)
                throw new ArgumentOutOfRangeException("length", "length must be non-negative");
        }
#endregion

        #region Properties

        // This is a read only property
        public decimal Length { get; private set; }

        public int Minutes
        {
            get
            {
                return (int) (Length / 60);
            }
        }

        public int Seconds
        {
            get
            {
                return (int) (Length - 60 * Minutes);
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

        #endregion

        #region operators
        static public implicit operator decimal(SongDuration sd)
        {
            return sd.Length;
        }

        static public implicit operator SongDuration(decimal d)
        {
            return new SongDuration(d);
        }

        public int CompareTo(SongDuration other)
        {
            return Length.CompareTo(other.Length);
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
            return Length.GetHashCode();
        }

        public static bool operator ==(SongDuration a, SongDuration b)
        {
            return a.Length == b.Length;
        }

        public static bool operator !=(SongDuration a, SongDuration b)
        {
            return a.Length != b.Length;
        }

        #endregion

        #region formatting

        public override string ToString()
        {
            string ret = Format(DurationFormat.Short);
            return ret;
        }

        public string Format(DurationFormat f)
        {
            string[] rgs = { "{0:N0}s", "{0}m", "{0}m{1}s" };
            string[] rgl = { "{0:N0} second(s)", "{0} minute(s)", "{0} minutes, {1} seconds" };
            string[] rg = rgl;

            bool exact = false;
            if (Length / 60 == Minutes)
            {
                exact = true;
            }

            if (f == DurationFormat.Short)
            {
                rg = rgs;
            }

            if (Length < 100)
            {
                return string.Format(rg[0], Length);
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
        #endregion

        #region Standard Durations
        public static IEnumerable<SongDuration> GetStandarDurations()
        {
            return _durations;
        }


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

        #endregion
    }
}
