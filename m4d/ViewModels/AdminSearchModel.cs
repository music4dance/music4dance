namespace m4d.ViewModels;

/// <summary>
/// View model for the Admin Search page.
/// Holds parameters and results for each search type.
/// New search types can be added as additional nested models.
/// </summary>
public class AdminSearchModel
{
    public EditedBySearchRequest EditedBy { get; set; } = new();
}

/// <summary>Parameters and results for the "Edited By User in Date Range" search.</summary>
public class EditedBySearchRequest
{
    public string UserName { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }

    /// <summary>Populated by the controller after a search is executed.</summary>
    public List<Song> Results { get; set; }

    public bool HasResults => Results != null;
    public bool HasSearch => !string.IsNullOrWhiteSpace(UserName) && From.HasValue && To.HasValue;

    /// <summary>
    /// Returns the ready-to-use SongModifier JSON for re-attributing edits via
    /// the Bulk Admin Modify form.
    /// </summary>
    public string SuggestedModifierJson =>
        HasSearch
            ? $$$"""
               {{
                 "fromDate": "{{{From:yyyy-MM-ddTHH:mm:ss}}}",
                 "toDate": "{{{To:yyyy-MM-ddTHH:mm:ss}}}",
                 "properties": [
                   {{
                     "action": "ReplaceValue",
                     "name": "User",
                     "value": "{{{UserName}}}",
                     "replace": "tempo-bot"
                   }}
                 ]
               }}
               """
            : null;
}
