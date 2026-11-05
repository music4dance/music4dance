namespace m4dApi;

/// <summary>
/// The search index fields the API reads, copied from m4dModels/SongIndex.cs rather than
/// referenced so the function doesn't depend on m4dModels (and with it EF and most of the app).
/// m4dModels.Tests links this file into PartnerApiIndexContractTests, which fails CI-SERVER if
/// the index stops providing any of them.
/// </summary>
public static class IndexFields
{
    public const string SongId = "SongId";
    public const string Title = "Title";
    public const string Artist = "Artist";
    public const string Length = "Length";
    public const string Tempo = "Tempo";
    public const string TempoTags = "TempoTags";
    public const string ServiceIds = "ServiceIds";
    public const string DanceTags = "DanceTags";

    // Subfields of each dance_{ID} complex field
    public const string Votes = "Votes";
    public const string DanceTempo = "Tempo";
    public const string StyleTags = "StyleTags";

    // dance_ALL holds the total of confirmed votes across dances
    public const string AllDances = "ALL";

    public static readonly string[] Selected = [SongId, Title, Artist, Length, Tempo, TempoTags, ServiceIds];
    public static readonly string[] Searched = [Title, Artist];
    public static readonly string[] Filtered = [ServiceIds, DanceTags];
    public static readonly string[] DanceSubfields = [Votes, DanceTempo, StyleTags];

    // Same result as SongIndex.AddCruftInfo's confirmed-content filter: at least one dance, and
    // at least one confirmed vote (unconfirmed-only songs have no dance_ALL/Votes)
    public static readonly string ConfirmedOnly = $"{DanceTags}/any() and {Dance(AllDances)}/{Votes} ne null";

    public static string Dance(string danceId) => $"dance_{danceId}";
}
