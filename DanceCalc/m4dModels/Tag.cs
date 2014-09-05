using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace m4dModels
{
    public class Tag :DbObject
    {
        public Guid SongId { get; set; }
        public virtual Song Song { get; set; }
        public string Value { get; set; }
        public virtual TagType Type {get; set;}
        public int Count { get; set; }
    }
}
