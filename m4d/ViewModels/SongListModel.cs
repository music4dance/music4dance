﻿using m4dModels;

namespace m4d.ViewModels;

public class SongListModel
{
    public List<SongHistory> Histories { get; set; }
    public SongFilterSparse Filter { get; set; }
    public int Count { get; set; }
    public int RawCount { get; set; }
    public List<string> HiddenColumns { get; set; }
}

public class CustomSearchModel : SongListModel
{
    public string Name { get; set; }
    public string Description { get; set; }
    public string Dance { get; set; }
    public string PlayListId { get; set; }
}
