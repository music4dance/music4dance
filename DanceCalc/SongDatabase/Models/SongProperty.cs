using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics;
using System.Threading;


namespace SongDatabase.Models
{
    public class SongProperty : DbObject
    {
        public Int64 Id { get; set; }
        public int SongId { get; set; }
        public virtual Song Song { get; set; }
        public string Name { get; set; }
        public string Value { get; set; }

        public override void Dump()
        {
            base.Dump();

            string output = string.Format("Id={0},SongId={1},Name={2},Value={3}", Id, SongId, Name, Value);
            Debug.WriteLine(output);
        }
    }

}
