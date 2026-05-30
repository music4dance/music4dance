namespace m4d.ViewModels;

/// <summary>
/// Safe-to-serialise projection of a PlayList entry for the admin playlist index page.
/// Data1/Data2 are truncated to 50 chars for display.
/// </summary>
public class PlayListSummary
{
    public string Id { get; set; }
    public string User { get; set; }
    public int Type { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public string Data1 { get; set; }
    public string Data2 { get; set; }
    public DateTime Created { get; set; }
    public DateTime? Updated { get; set; }
    public bool Deleted { get; set; }
}

/// <summary>
/// Top-level model for the admin playlist Vue page.
/// Includes both active and deleted playlists; client toggles visibility.
/// </summary>
public class PlayListPageModel
{
    public List<PlayListSummary> PlayLists { get; set; }
    public int Type { get; set; }
    public string FilteredUser { get; set; }
    public string Data1Name { get; set; }
    public string Data2Name { get; set; }
}
