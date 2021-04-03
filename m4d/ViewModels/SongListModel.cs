using System.Collections.Generic;
using m4dModels;

namespace m4d.ViewModels
{
    public class SongListModel
    {
        public List<SongSparse> Songs { get; set; }
        public List<SongHistory> Histories { get; set; }
        public SongFilterSparse Filter { get; set; }
        public string UserName { get; set; }
        public int Count { get; set; }
        public bool HideSort { get; set; }
        public List<string> HiddenColumns { get; set; }
        public bool Validate { get; set; }
    }

    public class HolidaySongListModel : SongListModel
    {
        public string Dance { get; set; }
        public string PlayListId { get; set; }
    }
}
