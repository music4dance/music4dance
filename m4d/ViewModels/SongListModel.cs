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

public class PlaylistViewerModel
{
    public string Id { get; set; }
    public List<SongHistory> Histories { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public string OwnerId { get; set; }
    public string OwnerName { get; set; }
    public int TotalCount { get; set; }

    /// <summary>How many playlist tracks (in playlist order) were actually checked against the
    /// catalog, bounded by the viewer's subscription-tier limit.</summary>
    public int CheckedCount { get; set; }

    /// <summary>Catalog matches found among the <see cref="CheckedCount"/> tracks checked.</summary>
    public int MatchedCount { get; set; }

    /// <summary>Whether the viewer is signed in and can add unmatched tracks to the catalog.</summary>
    public bool CanAddSongs { get; set; }

    /// <summary>Playlist tracks with no catalog match, populated only when <see cref="CanAddSongs"/>.</summary>
    public List<UnmatchedTrack> Unmatched { get; set; }
}

public class UnmatchedTrack
{
    public string Title { get; set; }
    public string Artist { get; set; }
    public string TrackId { get; set; }
}
