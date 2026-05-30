namespace m4d.ViewModels;

/// <summary>
/// Safe-to-serialize projection of a single Search entry for the admin searches index page.
/// URLs are pre-built server-side since they require SongFilter serialization.
/// </summary>
public class SearchSummary
{
    public long Id { get; set; }
    public string UserName { get; set; }
    public string Query { get; set; }
    public string Description { get; set; }
    public string SearchUrl { get; set; }
    public string SearchPageUrl { get; set; }
    public int? MostRecentPage { get; set; }
    public int Count { get; set; }
    public DateTime Created { get; set; }
    public DateTime Modified { get; set; }
    public string Spotify { get; set; }
    public string DeleteUrl { get; set; }
}

/// <summary>
/// Top-level model for the searches Vue page.
/// Server handles paging; Vue renders the current page and builds navigation URLs.
/// </summary>
public class SearchesPageModel
{
    public List<SearchSummary> Searches { get; set; }
    public int Page { get; set; }
    public int TotalPages { get; set; }
    public string Sort { get; set; }
    public bool ShowDetails { get; set; }
    public bool SpotifyOnly { get; set; }
    public string User { get; set; }
    public bool IsAdmin { get; set; }
    public bool CanDeleteAll { get; set; }
    public string BasicSearchUrl { get; set; }
    public string AdvancedSearchUrl { get; set; }
    public string DeleteAllUrl { get; set; }
}
