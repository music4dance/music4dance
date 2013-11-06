using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics;
using System.Threading;


namespace SongDatabase.Models
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
            Debug.WriteLine(output);
        }
    }
}
