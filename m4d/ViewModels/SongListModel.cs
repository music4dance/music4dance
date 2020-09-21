using System.Collections.Generic;
using m4dModels;

namespace m4d.ViewModels
{
    public class SongListModel
    {
        public List<SongSparse> Songs { get; set; }
        public SongFilterSparse Filter { get; set; }
        public string UserName { get; set; }
    }
}
