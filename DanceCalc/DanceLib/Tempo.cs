using System;

namespace DanceLibrary
{
    /// <summary>
    /// Encapsulates the idea of a tempo range in which a dance can be performed
    /// 
    /// This is an immutable class
    ///
    /// The idea is that this is a range between two MPM measurements for the meter that the dance 
    /// is danced to.
    /// 
    /// </summary>
    public class Tempo
    {
        /// <summary>
        /// Construct a tempo from serialized XML: Note this should be part of the JSON conversion
        /// </summary>
        /// <param name="d"></param>
        /// <param name="name"></param>
        public Tempo(DanceObject d, string name)
        {
            decimal t = 0;
            _minTempo = 0;
            _maxTempo = 0;

            if (d.TryGetDecimalAttribute(name, out t))
            {
                _maxTempo = t;
                _minTempo = t;
            }
            else if (d.TryGetDecimalAttribute("Min" + name, out t))
            {
                decimal t2 = 0;
                if (d.TryGetDecimalAttribute("Max" + name, out t2))
                {
                    _minTempo = t;
                    _maxTempo = t2;
                }
            }
        }

        /// <summary>
        /// Copy constructor
        /// </summary>
        /// <param name="other">Any valid tempo object</param>
        public Tempo(Tempo other)
        {
            _minTempo = other._minTempo;
            _maxTempo = other._maxTempo;

            Validate();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="minTempo"></param>
        /// <param name="maxTempo"></param>
        public Tempo(decimal minTempo, decimal maxTempo)
        {
            _minTempo = minTempo;
            _maxTempo = maxTempo;

            Validate();
        }

        public static readonly string PositiveDecimal = "must be a positive decimal number less than 250";
        public static readonly string RangeOrder = "Min must be less than or equal to Max";

        private void Validate()
        {
            if (_minTempo <= 0M || _minTempo > 250)
                throw new ArgumentOutOfRangeException("min", PositiveDecimal);

            if (_maxTempo <= 0M || _maxTempo > 250)
                throw new ArgumentOutOfRangeException("max", PositiveDecimal);

            if (_maxTempo < _minTempo)
                throw new ArgumentException("min", RangeOrder);
        }

        public decimal Min
        {
            get { return _minTempo; }
        }

        public decimal Max
        {
            get { return _maxTempo; }
        }

        public decimal Average
        {
            get { return _minTempo + (_maxTempo - _minTempo) / 2; }
        }

        public override bool Equals(object obj)
        {
            Tempo other = obj as Tempo;

            if (other == null)
                return false;

            return other.Min == Min && other.Max == Max;
        }

        public override int GetHashCode()
        {
            return Min.GetHashCode() * (Max + 1009).GetHashCode();
        }

        public decimal CalculateDelta(decimal tempo)
        {
            decimal delta = 0;
            if (tempo > Max)
            {
                delta = tempo - Max;
            }
            else if (tempo < Min)
            {
                delta = tempo - Min;
            }
            return delta;
        }

        public Tempo Include(Tempo other)
        {
            if (other == null)
                return new Tempo(this);
            else
                return new Tempo(Math.Min(_minTempo, other._minTempo), Math.Max(_maxTempo, other._maxTempo));
        }

        // Formatted values are shown to two decimal places except
        //  when the value is with .01 of an integer, in which case
        //  only the integer is displayed

        public string MinString
        {
            get { return Format(_minTempo); }
        }

        public string MaxString
        {
            get { return Format(_maxTempo); }
        }

        public string AverageString
        {
            get { return Format(Average); }
        }

        public override string ToString()
        {
            if (_minTempo == _maxTempo)
                return MinString;
            else
                return string.Format("{0}-{1}", MinString, MaxString);
        }

        private string Format(decimal d)
        {
            decimal i = Math.Round(d);
            if (Math.Abs(i - d) < .01M)
            {
                return i.ToString("F0");
            }
            else
            {
                return d.ToString("F2");
            }
        }

        readonly decimal _minTempo;
        readonly decimal _maxTempo;
    }
}
