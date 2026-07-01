namespace m4dModels;

/// <summary>
/// Stores ISRC (International Standard Recording Code) values per album entry,
/// keyed as "RS" in the AlbumDetails purchase dictionary.
///
/// IsIndexed = true (inherited default) causes GetExtendedPurchaseIds() to include
/// ISRC codes in the Azure Search ServiceIds field as "R:{isrc}" (e.g. "R:USRC17607839"),
/// making songs findable by a specific ISRC code. ISRCs are populated from Spotify
/// track metadata via GetISRCData — there is no standalone ISRC search API.
///
/// CanSearchExternally = false excludes ISRC from the enrichment loop in
/// UpdateSongAndServices / ConditionalUpdateSongAndServices, preventing RecordFail
/// from writing 'R' into FailedLookup on every song.
///
/// ShowInProfile = false because there is no ISRC storefront to link to.
/// </summary>
public class ISRCService() : MusicService(ServiceType.ISRC, 'R', "ISRC", null, null, null, null)
{
    public override bool CanSearchExternally => false;

    public override bool ShowInProfile => false;

    public override Task<IList<ServiceTrack>> ParseSearchResults(
        dynamic results, Func<string, Task<dynamic>> getResult,
        IEnumerable<string> excludeTracks)
        => Task.FromResult<IList<ServiceTrack>>([]);
}
