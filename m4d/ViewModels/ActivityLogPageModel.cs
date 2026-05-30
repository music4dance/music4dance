namespace m4d.ViewModels;

/// <summary>
/// Safe-to-serialize projection of a single ActivityLog entry.
/// </summary>
public class ActivityLogEntry
{
    public int Id { get; set; }
    public DateTimeOffset Date { get; set; }
    public string UserName { get; set; }
    public string Action { get; set; }
    public string Details { get; set; }
}

/// <summary>
/// Top-level model for the activity log Vue page.
/// Server handles paging; Vue renders the current page and builds navigation URLs.
/// </summary>
public class ActivityLogPageModel
{
    public List<ActivityLogEntry> Entries { get; set; }
    public int Page { get; set; }
    public int TotalPages { get; set; }
}
