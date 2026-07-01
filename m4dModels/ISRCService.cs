namespace m4dModels;

/// <summary>
/// Stores ISRC (International Standard Recording Code) values per album entry,
/// keyed as "RS" in the AlbumDetails purchase dictionary.
///
/// IsSearchable = true causes GetExtendedPurchaseIds() to include ISRC codes in
/// the Azure Search ServiceIds field as "R:{isrc}" (e.g. "R:USRC17607839"),
/// making songs findable by a specific ISRC code. ISRCs are populated from data
/// returned by the Spotify API — there is no ISRC search API to call.
/// ParseSearchResults is overridden to return empty rather than throw, since the
/// enrichment loop iterates all searchable services and ISRC has no search URL.
///
/// ShowInProfile = false because there is no ISRC storefront to link to.
/// </summary>
public class ISRCService() : MusicService(ServiceType.ISRC, 'R', "ISRC", null, null, null, null)
{
    public override bool IsSearchable => true;

    public override bool ShowInProfile => false;

    public override Task<IList<ServiceTrack>> ParseSearchResults(
        dynamic results, Func<string, Task<dynamic>> getResult,
        IEnumerable<string> excludeTracks)
        => Task.FromResult<IList<ServiceTrack>>([]);
}
