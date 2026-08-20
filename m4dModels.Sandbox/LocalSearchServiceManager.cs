using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;

using Microsoft.Extensions.Azure;

namespace m4dModels.Sandbox;

/// <summary>
/// Client factories for LocalSearchServiceManager's SearchServiceInfo - metadata reads (Id,
/// Abbr, IndexName; e.g. _head.cshtml's environment badge on every page) are pure string
/// computation and never touch these, so only an actual attempt to build a real Azure
/// SearchClient/SearchIndexClient throws.
/// </summary>
internal class ThrowingSearchClientFactory : IAzureClientFactory<SearchClient>
{
    public SearchClient CreateClient(string name) =>
        throw new InvalidOperationException(
            $"The sandbox has no real Azure Search service - cannot create a SearchClient for '{name}'.");
}

internal class ThrowingSearchIndexClientFactory : IAzureClientFactory<SearchIndexClient>
{
    public SearchIndexClient CreateClient(string name) =>
        throw new InvalidOperationException(
            $"The sandbox has no real Azure Search service - cannot create a SearchIndexClient for '{name}'.");
}

/// <summary>
/// Minimal ISearchServiceManager for the no-external-service sandbox host. There is no real
/// Azure Search service behind this - the real m4d controllers construct a fresh
/// DanceMusicService per request without an explicit SongIndex (see
/// DanceMusicController.Database), so SongIndex.Create's lazy self-construction is the only
/// place a SongIndexLocal can be handed back. This class implements ISongIndexFactory (SongIndex
/// Create's opt-in escape hatch) to always return the same in-memory SongIndexLocal instance,
/// re-attached to whichever DanceMusicCoreService asked - the shared song store persists across
/// requests, and .DanceMusicService is only ever used to reach the shared IDanceStatsManager
/// singleton, so which specific per-request service instance it's attached to doesn't matter.
/// GetInfo() itself returns a real (if inert) SearchServiceInfo rather than throwing - shared
/// layout views (e.g. _head.cshtml's environment badge) read it on every page.
/// </summary>
public class LocalSearchServiceManager : ISearchServiceManager, ISongIndexFactory
{
    private const string LocalIndexName = "local-sandbox";

    private readonly SongIndexLocal _index = new();
    private readonly SearchServiceInfo _info;

    public LocalSearchServiceManager()
    {
        _info = new SearchServiceInfo(
            "local", ConfigVersion, LocalIndexName,
            new ThrowingSearchClientFactory(), new ThrowingSearchIndexClientFactory(), this);
    }

    public SongIndex CreateSongIndex(DanceMusicCoreService dms, string id, bool isNext)
    {
        _index.AttachToService(dms);
        return _index;
    }

    public string DefaultId { get; set; } = "local";

    public int CodeVersion => 1;

    public int ConfigVersion { get; set; } = 1;

    public bool NextVersion => false;

    public bool HasNextVersion => false;

    public bool HasNextIndex => false;

    public string RawEnvironment => "local (sandbox - not connected to any external service)";

    public string CurrentIndexName => LocalIndexName;

    public string NextIndexName => LocalIndexName;

    public IEnumerable<string> GetAvailableIds()
    {
        return [DefaultId];
    }

    public bool HasId(string id)
    {
        return id == null || id == DefaultId;
    }

    // Reached constantly (e.g. _head.cshtml's environment badge on every page reads
    // GetInfo().Abbr), so this must actually work, not throw. Returns a real SearchServiceInfo -
    // metadata reads (Id, Abbr, IndexName) are pure string computation; only an actual attempt to
    // build a SearchClient/SearchIndexClient (ThrowingSearchClientFactory above) fails.
    public SearchServiceInfo GetInfo(string id = null)
    {
        return _info;
    }

    // Pure string parsing, not a service call - mirrors SearchServiceManager's implementation.
    public SongFilter GetSongFilter(string filter = null)
    {
        return SongFilter.Create(NextVersion, filter);
    }

    public SongFilter GetSongFilter(RawSearch raw, string action = null)
    {
        return SongFilter.Create(NextVersion, raw, action);
    }

    public void RedirectToUpdate()
    {
        // No-op: the sandbox has a single, in-memory index version.
    }
}
