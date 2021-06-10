using System.Collections.Generic;

namespace m4dModels
{
    public class SongPropertyMerge
    {
        public string Name { get; set; }
        public int Selection { get; set; }
        public bool AllowAlternates { get; set; }
        public List<object> Values { get; set; }
    }
}
