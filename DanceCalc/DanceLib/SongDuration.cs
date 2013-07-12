using System;
using System.Collections.Generic;
using System.Diagnostics;

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
        /// Create a song with any arbitrary tempo
        /// </summary>
        /// <param name="length">minutes, seconds, beats or measures depending on type</param>
        /// <param name="type">any duration type</param>
        /// <param name="tempo">any duration type</param>
        public SongDuration(decimal length, DurationType type, Tempo tempo)
            : this()
        {
            switch (type.DurationKind)
            {
                case DurationKind.Second:
                    Length = length;
                    break;
                case DurationKind.Minute:
                    Length = length * 60;
                    break;
                case DurationKind.Beat:
                    if (tempo == null) throw new ArgumentNullException("tempo");
                    Length = length * tempo.SecondsPerBeat;
                    break;
                case DurationKind.Measure:
                    if (tempo == null) throw new ArgumentNullException("tempo");
                    Length = length * tempo.SecondsPerMeasure;
                    break;
                default: 
                    Debug.Assert(false);
                    break;
            }

            Validate();
        }

        public SongDuration(decimal length, DurationType type)
            : this(length, type, null)
        {
        }

        /// <summary>
        /// Create a song duration from a string (currently only parsing 'short' version of syntax
        /// 
        /// MmSs
        /// </summary>
        public SongDuration(string s)
            : this()
        {
            int imin = s.IndexOf('m');
            int isec = s.IndexOf('s');

            string shour = string.Empty;
            string smin = string.Empty;
            string ssec = string.Empty;

            if (imin < 0 && isec < 0)
            {
                if (s.IndexOf(':') >= 0)
                {
                    string[] parts = s.Split(new char[] { ':' },StringSplitOptions.RemoveEmptyEntries);

                    if (parts.Length >= 3)
                    {
                        shour = parts[0];
                        smin = parts[1];
                        ssec = parts[2];
                    }
                    else if (parts.Length == 2)
                    {
                        smin = parts[0];
                        ssec = parts[1];
                    }
                    else if (parts.Length == 1)
                    {
                        ssec = parts[0];
                    }
                }
                else
                {
                    ssec = s;
                }
            }
            else
            {
                if (imin < 0)
                {
                    ssec = s.Substring(0, isec);
                }
                else if (isec < 0)
                {
                    smin = s.Substring(0, imin);
                }
                else
                {
                    smin = s.Substring(0, imin);
                    ssec = s.Substring(imin + 1, isec);
                }
            }

            decimal seconds = 0;
            decimal dmin = 0m;
            decimal dsec = 0m;
            if (!string.IsNullOrEmpty(smin) && decimal.TryParse(smin, out dmin))
            {
                seconds = 60 * dmin;
            }
            if (!string.IsNullOrEmpty(ssec) && decimal.TryParse(ssec, out dsec))
            {
                seconds += dsec;
            }

            Length = seconds;

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

        public decimal LengthIn(DurationKind dk, Tempo tempo = null)
        {
            switch (dk)
            {
                case DurationKind.Second:
                    return Length;
                case DurationKind.Minute:
                    return Length / 60;
                case DurationKind.Beat:
                    if (tempo == null) throw new ArgumentNullException("tempo");
                    return Length / tempo.SecondsPerBeat;
                case DurationKind.Measure:
                    if (tempo == null) throw new ArgumentNullException("tempo");
                    return Length / tempo.SecondsPerMeasure;
                default:
                    Debug.Assert(false); 
                    return 0M;
            }
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

#endregion
    }
}
