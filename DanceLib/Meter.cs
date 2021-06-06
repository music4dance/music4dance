using System;
using Newtonsoft.Json;

namespace DanceLibrary
{
    /// <summary>
    ///     Represents a musical meter with an integral numerator and denominator
    ///     This is an immutable class
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class Meter
    {
        public static readonly string MeterSyntaxError =
            "Meter must be in the format {positive integer}/{positive integer}";

        public static readonly string IntegerNumerator = "Numerator must be an integer";
        public static readonly string IntegerDenominator = "Denominator must be an integer";

        public static readonly string PositiveIntegerNumerator =
            "Numerator must be a positive integer less than 1000";

        public static readonly string PositiveIntegerDenominator =
            "Denominator must be a positive integer less than 1000";

        private int _denominator;

        //public static bool operator== (Meter m1, Meter m2)
        //{
        //    return (m1._numerator == m2._numerator) && (m1._denominator == m2._denominator);
        //}

        private int _numerator;

        private Meter()
        {
        }

        /// <summary>
        ///     Create a Meter from a positive integer numerator and denominator
        /// </summary>
        /// <param name="numerator"></param>
        /// <param name="denominator"></param>
        [JsonConstructor]
        public Meter(int numerator, int denominator)
        {
            _numerator = numerator;
            _denominator = denominator;

            Validate();
        }

        /// <summary>
        ///     Create a Meter from a string of format "{positive int}/{positive int}"
        /// </summary>
        /// <param name="s"></param>
        public Meter(string s)
        {
            if (string.IsNullOrEmpty(s)) throw new ArgumentNullException();

            var strings = s.Split('/', ' ');

            if (strings.Length != 2) throw new ArgumentOutOfRangeException(MeterSyntaxError);

            if (!int.TryParse(strings[0], out _numerator))
                throw new ArgumentOutOfRangeException(IntegerNumerator);

            if (!int.TryParse(strings[1], out _denominator))
                throw new ArgumentOutOfRangeException(IntegerDenominator);

            Validate();
        }

        /// <summary>
        ///     Return the numerator of the Meter (the top number)
        /// </summary>
        [JsonProperty]
        public int Numerator => _numerator;

        /// <summary>
        ///     Return the denominator of the Meter
        /// </summary>
        [JsonProperty]
        public int Denominator => _denominator;

        private void Validate()
        {
            if (_numerator <= 0 || _numerator >= 1000)
                throw new ArgumentOutOfRangeException("numerator", PositiveIntegerNumerator);

            if (_denominator <= 0 || _denominator >= 1000)
                throw new ArgumentOutOfRangeException("denominator", PositiveIntegerDenominator);

            //if (_denominator != 4)
            //    throw new ArgumentOutOfRangeException("denominator", DenominatorLimit);
        }

        // Return a string of the form "{numerator}/{denominator}"
        public override string ToString()
        {
            return $"{_numerator}/{_denominator}";
        }

        public override bool Equals(object obj)
        {
            var m = obj as Meter;
            if (m == null)
                return false;
            return _numerator == m._numerator && _denominator == m._denominator;
        }

        public static bool operator ==(Meter a, Meter b)
        {
            // If both are null, or both are same instance, return true.
            if (ReferenceEquals(a, b)) return true;

            // Handle a is null case☺.
            if ((object) a == null) return (object) b == null;

            return a.Equals(b);
        }

        public static bool operator !=(Meter a, Meter b)
        {
            return !(a == b);
        }

        public override int GetHashCode()
        {
            return _numerator * 1009 + _denominator;
        }
    }
}