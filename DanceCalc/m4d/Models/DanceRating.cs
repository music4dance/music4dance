using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;


namespace m4d.Models
{
    public class DanceRating : DbObject
    {
        public int SongId { get; set; }
        public virtual Song Song { get; set; }

        public string DanceId { get; set; }
        public virtual Dance Dance { get; set; }

        public int Weight { get; set; }

        public override void Dump()
        {
            base.Dump();

            string output = string.Format("DanceId={0},SongId={1},Name={2},Value={3}", DanceId, SongId, Weight);
            Trace.WriteLine(output);
        }
    }

    // Transitory object - move to ViewModel?
    public class DanceRatingDelta
    {
        public DanceRatingDelta()
        {

        }

        public DanceRatingDelta(string value)
        {
            string[] parts = value.Split(new char[] { '+', '-' });

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
