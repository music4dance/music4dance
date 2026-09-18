namespace m4d.ViewModels;

/// <summary>
/// Type-ahead answer for the artist index search box. Deliberately not the Azure suggester shape
/// (<c>SuggestionList</c>): these come from the in-memory <see cref="ArtistIndex"/> snapshot, so
/// they carry song counts and are already deduplicated by artist key - see
/// architecture/individual-artists.md §9.4.
/// </summary>
public record ArtistSuggestions(string Query, IReadOnlyList<ArtistIndexEntry> Artists)
{
    public static ArtistSuggestions Empty(string query) => new(query, []);
}
