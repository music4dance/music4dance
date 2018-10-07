using Newtonsoft.Json;
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
    [JsonObject(MemberSerialization.OptIn)]
    public class TempoRange
    {
        /// <summary>
        /// Copy constructor
        /// </summary>
        /// <param name="other">Any valid tempo object</param>
        public TempoRange(TempoRange other)
        {
            _minTempo = other._minTempo;
            _maxTempo = other._maxTempo;

            Validate();
        }


        [JsonConstructor]
        public TempoRange(decimal min, decimal max)
        {
            _minTempo = min;
            _maxTempo = max;

            Validate();
        }

        public static readonly string PositiveDecimal = "must be a positive decimal number less than 250";
        public static readonly string RangeOrder = "Min must be less than or equal to Max";

        private void Validate()
        {
            if (_minTempo <= 0M || _minTempo > 1000)
                // ReSharper disable once NotResolvedInText
                throw new ArgumentOutOfRangeException("_minTempo", PositiveDecimal);

            if (_maxTempo <= 0M || _maxTempo > 1000)
                // ReSharper disable once NotResolvedInText
                throw new ArgumentOutOfRangeException("_maxTempo", PositiveDecimal);

            if (_maxTempo < _minTempo)
                // ReSharper disable once NotResolvedInText
                // ReSharper disable once LocalizableElement
                throw new ArgumentException("_minTempo", RangeOrder);
        }

        [JsonProperty]
        public decimal Min => _minTempo;

        [JsonProperty]
        public decimal Max => _maxTempo;

        public decimal Average => _minTempo + (_maxTempo - _minTempo) / 2;

        public override bool Equals(object obj)
        {
            if (!(obj is TempoRange other))
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

        public TempoRange Include(TempoRange other)
        {
            return other == null ? 
                new TempoRange(this) : 
                new TempoRange(Math.Min(_minTempo, other._minTempo), Math.Max(_maxTempo, other._maxTempo));
        }

        public TempoRange ToBpm(Meter meter)
        {
            return new TempoRange(_minTempo * meter.Numerator, _maxTempo * meter.Numerator);
        }

        // Formatted values are shown to two decimal places except
        //  when the value is with .01 of an integer, in which case
        //  only the integer is displayed

        public string MinString => Format(_minTempo);

        public string MaxString => Format(_maxTempo);

        public string AverageString => Format(Average);

        public override string ToString()
        {
            return _minTempo == _maxTempo ? MinString : $"{MinString}-{MaxString}";
        }

        public bool Contains(decimal tempo)
        {
            return tempo >= _minTempo && tempo <= _maxTempo;
        }

        private string Format(decimal d)
        {
            var i = Math.Round(d);
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
