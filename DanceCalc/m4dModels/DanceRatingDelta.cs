using System;
using System.Linq;

namespace m4dModels
{
    public class DanceRatingDelta
    {
        public DanceRatingDelta()
        {

        }

        public DanceRatingDelta(string id, int delta)
        {
            DanceId = id;
            Delta = delta;
        }

        public DanceRatingDelta(string value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(@"value");
            }
            string[] parts = value.Split('+', '-');

            int sign = value.Contains('-') ? -1 : 1;
            int offset = 1;

            DanceId = parts[0];
            if (parts.Length > 1)
            {
                int.TryParse(parts[1], out offset);
            }

            Delta = sign * offset;
        }

        public override string ToString()
        {
            return string.Format("{0}{1}{2}", DanceId, Delta < 0 ? "-" : "+", Math.Abs(Delta));
        }

        public string DanceId { get; set; }
        public int Delta { get; set; }
    }
}