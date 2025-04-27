using System;

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
            ArgumentNullException.ThrowIfNull(value);

            var parts = value.Split('+', '-');

            var sign = value.Contains('-') ? -1 : 1;
            var offset = 1;

            DanceId = parts[0];
            if (parts.Length > 1)
            {
                _ = int.TryParse(parts[1], out offset);
            }

            Delta = sign * offset;
        }

        public override string ToString()
        {
            return $"{DanceId}{(Delta < 0 ? "-" : "+")}{Math.Abs(Delta)}";
        }

        public string DanceId { get; set; }
        public int Delta { get; set; }
    }
}
