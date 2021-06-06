using System.Collections.Generic;
using m4dModels;

namespace m4d.ViewModels
{
    public class WeddingDanceHeaderModel
    {
        public IEnumerable<string> Columns { get; set; }
        public bool ShowStyle { get; set; }
    }

    public class WeddingDanceRowModel
    {
        public DanceStats Stats { get; set; }

        public IEnumerable<string> Columns { get; set; }
        public bool IsGroup { get; set; }
    }
}