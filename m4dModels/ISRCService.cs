namespace m4dModels;

/// <summary>
/// Pseudo-service that stores ISRC codes alongside the Spotify/iTunes IDs that
/// produced them.  Not searchable and not shown in user profiles — it exists
/// only to give ISRCs a home in the purchase-ID dictionary (key "RS").
/// </summary>
public class ISRCService() : MusicService(ServiceType.ISRC, 'R', "ISRC", null, null, null, null)
{
    public override bool IsSearchable => false;

    public override bool ShowInProfile => false;
}
