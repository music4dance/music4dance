using Newtonsoft.Json;

namespace DanceLibrary
{
    /// <summary>
    ///     Encapsulates the idea of a tempo range in which a dance can be performed
    ///     This is an immutable class
    ///     The idea is that this is a range between two MPM measurements for the meter that the dance
    ///     is danced to.
    /// </summary>
    public class TempoRange
    {
        public static readonly string PositiveDecimal =
            "must be a positive decimal number less than 250";

        public static readonly string RangeOrder = "Min must be less than or equal to Max";

        /// <summary>
        ///     Copy constructor
        /// </summary>
        /// <param name="other">Any valid tempo object</param>
        public TempoRange(TempoRange other)
        {
            Min = other.Min;
            Max = other.Max;

            Validate();
        }


        [JsonConstructor]
        public TempoRange(decimal min, decimal max)
        {
            Min = min;
            Max = max;

            Validate();
        }

        public decimal Min { get; }

        public decimal Max { get; }

        [JsonIgnore]
        public decimal Average => Min + (Max - Min) / 2;

        // Formatted values are shown to two decimal places except
        //  when the value is with .01 of an integer, in which case
        //  only the integer is displayed

        [JsonIgnore]
        public string MinString => Format(Min);

        [JsonIgnore]
        public string MaxString => Format(Max);

        private void Validate()
        {
            if (Min <= 0M || Min > 1000)
            // ReSharper disable once NotResolvedInText
            {
                throw new ArgumentOutOfRangeException("_minTempo", PositiveDecimal);
            }

            if (Max <= 0M || Max > 1000)
            // ReSharper disable once NotResolvedInText
            {
                throw new ArgumentOutOfRangeException("_maxTempo", PositiveDecimal);
            }

            if (Max < Min)
            // ReSharper disable once NotResolvedInText
            // ReSharper disable once LocalizableElement
            {
                throw new ArgumentException("_minTempo", RangeOrder);
            }
        }

        public override bool Equals(object obj)
        {
            return obj is TempoRange other && (other.Min == Min && other.Max == Max);
        }

        public override int GetHashCode()
        {
            return Min.GetHashCode() * (Max + 1009).GetHashCode();
        }

        public decimal CalculateDelta(decimal tempo)
        {
            return tempo > Max
                ? tempo - Max
                : tempo < Min
                    ? tempo - Min
                    : 0;
        }

        public decimal CalculateDeltaPercent(decimal tempo)
        {
            return CalculateDelta(tempo) * 100 / (tempo >= Max ? Max : Min);
        }

        public TempoRange Include(TempoRange other)
        {
            return other == null
                ? new TempoRange(this)
                : new TempoRange(
                    Math.Min(Min, other.Min),
                    Math.Max(Max, other.Max));
        }

        public override string ToString()
        {
            return Min == Max ? MinString : $"{MinString}-{MaxString}";
        }

        public bool Contains(decimal tempo)
        {
            return tempo >= Min && tempo <= Max;
        }

        private string Format(decimal d)
        {
            var i = Math.Round(d);
            return Math.Abs(i - d) < .01M ? i.ToString("F0") : d.ToString("F2");
        }
    }
}
