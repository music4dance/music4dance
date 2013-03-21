using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net;
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
    public class Meter : IConversand
    {
        static Meter()
        {
            s_commonMeters = new List<Meter>(3);
            s_commonMeters.Add(new Meter(2,4));
            s_commonMeters.Add(new Meter(3,4));
            s_commonMeters.Add(new Meter(4,4));
        }

        public Meter(int numerator, int denominator)
        {
            Debug.Assert(denominator == 4);
            if (denominator != 4)
                throw new ArgumentOutOfRangeException("denominator", "We are currently only supporting x/4 type signatures");

            _numerator = numerator;
            _denominator = denominator;
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
                throw new ArgumentOutOfRangeException("Meter must contain exaclty one '/' with an optional \"MPM\" prefix");
            }

            if (!int.TryParse(strings[0+offset],out _numerator))
                throw new ArgumentOutOfRangeException("Numerator must be an integer");

            if (!int.TryParse(strings[1+offset],out _denominator))
                throw new ArgumentOutOfRangeException("Denominator must be an integer");

            if (_denominator != 4)
                throw new ArgumentOutOfRangeException("denominator", "We are currently only supporting x/4 type signatures");
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
                return s_commonMeters.AsReadOnly();
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
