using System.Collections.Generic;
using DanceLibrary;

namespace m4dModels
{
    public class TagMatrix
    {
        public List<TagColumn> Columns { get; set; }
        public List<TagRowGroup> Groups { get; set; }
    }

    public class TagColumn
    {
        public string Title { get; set; }
        public string Tag { get; set; }
    }

    public class TagRow
    {
        public string Dance { get; set; }
        public List<int> Counts { get; set; }
    }

    public class TagRowGroup : TagRow
    {
        public List<TagRow> Children { get; set; }
    }
}
