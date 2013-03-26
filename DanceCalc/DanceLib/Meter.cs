using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace DanceLibrary
{
    public class Meter : IConversand
    {
        static Meter()
        {
            s_commonMeters = new List<Meter>(3);
            s_commonMeters.Add(new Meter(2,4));
            s_commonMeters.Add(new Meter(3,4));
            s_commonMeters.Add(new Meter(4,4));
        }

        public static readonly string MeterSyntaxError = "Meter must contain exaclty one '/' with an optional \"MPM\" prefix";
        public static readonly string IntegerNumerator = "Numerator must be an integer";
        public static readonly string IntegerDenominator = "Denominator must be an integer";
        public static readonly string PositiveIntegerNumerator = "Numerator must be a positive integer less than 1000";
        public static readonly string PositiveIntegerDenominator = "Denominator must be a positive integer less than 1000";
        public static readonly string DenominatorLimit = "We are currently only supporting x/4 type signatures";

        private Meter()
        {
        }

        /// <summary>
        /// Create a Meter from a positive integer numerator and denominator
        /// </summary>
        /// <param name="numerator"></param>
        /// <param name="denominator"></param>
        public Meter(int numerator, int denominator)
        {
            _numerator = numerator;
            _denominator = denominator;

            Validate();
        }

        /// <summary>
        /// Create a from a string of format "[MPM ]{positive int}/{positive int}"
        /// </summary>
        /// <param name="s"></param>
        public Meter(string s)
        {
            if (string.IsNullOrEmpty(s)) throw new ArgumentNullException();

            string [] strings = s.Split(new char[]{'/',' '});

            int offset = 0;

            if (strings.Length == 3 && strings[0].Equals("MPM"))
            {
                offset = 1;
            }
            else if (strings.Length != 2)
            {
                throw new ArgumentOutOfRangeException(MeterSyntaxError);
            }

            if (!int.TryParse(strings[0+offset],out _numerator))
                throw new ArgumentOutOfRangeException(IntegerNumerator);

            if (!int.TryParse(strings[1+offset],out _denominator))
                throw new ArgumentOutOfRangeException(IntegerDenominator);

            Validate();
        }

        private void Validate()
        {
            if (_numerator <= 0 || _numerator >= 1000)
                throw new ArgumentOutOfRangeException("numerator", PositiveIntegerNumerator);

            if (_denominator <= 0 || _denominator >= 1000)
                throw new ArgumentOutOfRangeException("denominator", PositiveIntegerDenominator);

            if (_denominator != 4)
                throw new ArgumentOutOfRangeException("denominator", DenominatorLimit);

            
        }

        /// <summary>
        /// Return the numerator of the Meter (the top number)
        /// </summary>
        public int Numerator
        {
            get { return _numerator; }
        }

        /// <summary>
        /// Return the denominator of the Meter
        /// </summary>
        public int Denominator
        {
            get { return _denominator; }
        }

        // Return a string of the form "MPM {numerator}/{denominator}"
        public override string ToString()
        {
            return ToString(null);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="format"></param>
        /// <returns></returns>
        public string ToString(string format)
        {
            string prefix = "MPM ";
            if (format != null && format.Equals("C"))
            {
                prefix = "";
            }

            return string.Format("{0}{1}/{2}", prefix, _numerator, _denominator);
        }

        public override bool Equals(object obj)
        {
            Meter m = obj as Meter;
            if (m == null)
                return false;
            else
                return (this._numerator == m._numerator) && (this._denominator == m._denominator);
        }

        public override int GetHashCode()
        {
            return this._numerator * 1009 + this._denominator;
        }

        //public static bool operator== (Meter m1, Meter m2)
        //{
        //    return (m1._numerator == m2._numerator) && (m1._denominator == m2._denominator);
        //}

        /// <summary>
        /// Return a collection of the common meters to be used for populating a drop-down
        /// </summary>
        public static ReadOnlyCollection<Meter> CommonMeters
        {
            get
            {
                return new ReadOnlyCollection<Meter>(s_commonMeters);
            }
        }

        private int _numerator;
        private int _denominator;
        
        private static List<Meter> s_commonMeters;

        // Implementation of IConversand
        public Kind Kind
        {
            get { return Kind.Rate; }
        }

        public string Name
        {
            get { return this.ToString(); }
        }
    }
}
