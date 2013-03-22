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
        public static readonly string PositiveIntegerNumerator = "Numerator must be a positive integer";
        public static readonly string PositiveIntegerDenominator = "Denominator must be a positive integer";
        public static readonly string DenominatorLimit = "We are currently only supporting x/4 type signatures";

        private Meter()
        {
        }

        public Meter(int numerator, int denominator)
        {
            _numerator = numerator;
            _denominator = denominator;

            Validate();
        }

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
            if (_numerator <= 0)
                throw new ArgumentOutOfRangeException("numerator", PositiveIntegerNumerator);

            if (_denominator <= 0)
                throw new ArgumentOutOfRangeException("denominator", PositiveIntegerDenominator);

            if (_denominator != 4)
                throw new ArgumentOutOfRangeException("denominator", DenominatorLimit);
        }

        public int Numerator
        {
            get { return _numerator; }
        }

        public int Denominator
        {
            get { return _denominator; }
        }

        public override string ToString()
        {
            return string.Format("MPM {0}/{1}", _numerator, _denominator);
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
            return this._numerator * 17 + this._denominator;
        }

        //public static bool operator== (Meter m1, Meter m2)
        //{
        //    return (m1._numerator == m2._numerator) && (m1._denominator == m2._denominator);
        //}

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
