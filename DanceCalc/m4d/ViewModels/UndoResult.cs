using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using m4d.Models;

namespace m4d.ViewModels
{
    public class UndoResult
    {
        public SongLog Original {get; set;}
        public SongLog Result { get; set; }
    }
}
