using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace music4dance.Models
{
    public class Song
    {
        public int SongId { get; set; }
        public int Source { get; set; }
        public string Dances { get; set; }
        public int Tempo { get; set; }
        public string Title { get; set; }
        public string Artist { get; set; }
        public string Album { get; set; }
    }
}