using System.Collections.Generic;
using DanceLibrary;
using m4dModels;

namespace m4d.ViewModels
{
    public class CategoryTableModel
    {
        public string Title { get; set; }
        public DanceStatsInstance DanceStats { get; set; }
        public IList<DanceInstance> Dances { get; set; }
        public DanceCategoryType DanceCategoryType { get; set; }
    }
}
