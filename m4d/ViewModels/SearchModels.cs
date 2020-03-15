using System;
using System.Collections.Generic;
using m4dModels;

namespace m4d.ViewModels
{
    public class SearchHeaderModel
    {
        public SongFilter Filter { get; set; }
        public IList<DanceStats> Dances { get; set; }
        public string UserName { get; set; }
        public string Description { get; set; }
    }

    public class SearchFooterModel
    {
        public X.PagedList.IPagedList<Song> Songs { get; set; }
        public SongFilter Filter { get; set; }
        public bool HideInferred { get; set; }
        public bool HideSpotify { get; set; }
        public bool ShowExport { get; set; }
    }
}
