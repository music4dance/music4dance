namespace m4d.ViewModels;

public class SearchRequestDiagnosticsModel
{
    public string SearchText { get; set; }
    public string QueryType { get; set; }
    public string SearchMode { get; set; }
    public string Filter { get; set; }
    public List<string> OrderBy { get; set; }
    public int? Skip { get; set; }
    public int? Size { get; set; }
    public bool IncludeTotalCount { get; set; }
    public string CruftFilter { get; set; }
}
