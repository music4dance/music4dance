using m4d.Services;

using m4dModels;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace m4d.ViewModels;

/// <summary>
/// The shared-id review page as the shared-id-review Vue app gets it (model_): a page of the
/// queue, flattened so the client only renders and collects choices. Everything the page works
/// out from songs (effective artists, extended ids, album indexes, track links, badges, default
/// actions) is worked out here, the same way SharedIdLog.Apply reads them back.
/// </summary>
public class SharedIdReviewModel
{
    public int Page { get; init; }
    public int PageCount { get; init; }
    public int PendingCandidates { get; init; }
    public int ClusterCount { get; init; }

    // When the page is sorted by the last merge preview: when it ran, and how many of the
    // clusters still pending differed only by feat. (they come first) and are close matches
    public DateTimeOffset? SortedBy { get; init; }
    public int WithoutFeaturingCount { get; init; }
    public int CloseCount { get; init; }

    // What that preview found the Merge all tasks would merge (as of when it ran): clusters with
    // matching titles and artists, clusters that differ only by feat., and clusters whose
    // artists are credited differently
    public int WouldMergeCount { get; init; }
    public int WouldMergeFeaturingCount { get; init; }
    public int WouldMergeCreditsCount { get; init; }

    // Clusters left for later, which come after the rest
    public int DeferredCount { get; init; }

    public string Message { get; init; }

    public IReadOnlyList<SharedIdReviewCluster> Clusters { get; init; } = [];

    public static SharedIdReviewModel Build(SharedIdPage page, SharedIdPreview preview = null,
        string message = null) =>
        new()
        {
            Page = page.Page,
            PageCount = page.PageCount,
            PendingCandidates = page.PendingCandidates,
            ClusterCount = page.ClusterCount,
            SortedBy = preview?.Created,
            WouldMergeCount = preview?.Merges.Count() ?? 0,
            WouldMergeFeaturingCount = preview?.FeaturingMerges.Count() ?? 0,
            WouldMergeCreditsCount = preview?.CreditMerges.Count() ?? 0,
            WithoutFeaturingCount = page.RankCounts.GetValueOrDefault(SharedIdPreviewStore.WithoutFeaturingRank),
            CloseCount = page.RankCounts.GetValueOrDefault(SharedIdPreviewStore.CloseRank),
            DeferredCount = page.DeferredCount,
            Message = message,
            Clusters = [.. page.Clusters.Select(SharedIdReviewCluster.Build)]
        };
}

public class SharedIdReviewCluster
{
    // The cluster's songs ids, sorted: identifies it whatever order its songs are listed in
    public string Key { get; init; }
    public IReadOnlyList<string> ServiceIds { get; init; } = [];

    [JsonConverter(typeof(StringEnumConverter))]
    public SharedIdMatch Match { get; init; }

    public bool HasRejectedPair { get; init; }
    public bool HasNoMergeSong { get; init; }
    public bool CleansFeaturing { get; init; }

    // The longest song minus the shortest, in seconds, and whether that's beyond the tolerance
    public int LengthSpread { get; init; }
    public bool LengthsDiffer { get; init; }

    [JsonConverter(typeof(StringEnumConverter))]
    public SharedIdAction DefaultAction { get; init; }

    public IReadOnlyList<SharedIdReviewCandidate> Candidates { get; init; } = [];

    // Empty when the songs changed while the page loaded
    public IReadOnlyList<SharedIdReviewSong> Songs { get; init; } = [];

    // What merging all of the songs would make, and its feat. cleanup; null when the merge
    // couldn't be previewed
    public SharedIdReviewSong Merged { get; init; }
    public SharedIdReviewNames Proposal { get; init; }

    public static SharedIdReviewCluster Build(SharedIdPageCluster entry)
    {
        var cluster = entry.Cluster;
        var shared = cluster.ServiceIds.ToHashSet();
        return new SharedIdReviewCluster
        {
            Key = cluster.Key,
            ServiceIds = cluster.ServiceIds,
            Match = cluster.Match,
            HasRejectedPair = cluster.HasRejectedPair,
            HasNoMergeSong = cluster.HasNoMergeSong,
            CleansFeaturing = entry.CleansFeaturing,
            LengthSpread = cluster.LengthSpread,
            LengthsDiffer = cluster.LengthsDiffer,
            DefaultAction = entry.DefaultAction,
            Candidates = [.. cluster.Candidates.Select(c =>
                new SharedIdReviewCandidate(c.Id, c.Source, c.Created, c.ServiceIds, c.Detail))],
            Songs = [.. cluster.Songs.Select(s => SharedIdReviewSong.Build(s, shared))],
            Merged = entry.Preview == null ? null : SharedIdReviewSong.Build(entry.Preview, shared),
            Proposal = entry.Proposal == null ? null
                : new SharedIdReviewNames(entry.Proposal.Title, entry.Proposal.Artist, entry.Proposal.Artists ?? [])
        };
    }
}

public record SharedIdReviewCandidate(int Id, string Source, DateTimeOffset Created, string ServiceIds,
    string Detail);

public record SharedIdReviewNames(string Title, string Artist, IReadOnlyList<string> Artists);

public record SharedIdReviewDance(string DanceId, int Weight);

public class SharedIdReviewSong
{
    public Guid SongId { get; init; }
    public string Title { get; init; }
    public string Artist { get; init; }

    // The individual artists (Song.EffectiveArtists)
    public IReadOnlyList<string> Artists { get; init; } = [];

    public int? Length { get; init; }
    public decimal? Tempo { get; init; }
    public IReadOnlyList<SharedIdReviewDance> Dances { get; init; } = [];
    public DateTime Created { get; init; }
    public IReadOnlyList<SharedIdReviewAlbum> Albums { get; init; } = [];

    public static SharedIdReviewSong Build(Song song, ISet<string> shared) =>
        new()
        {
            SongId = song.SongId,
            Title = song.Title,
            Artist = song.Artist,
            Artists = song.EffectiveArtists ?? [],
            Length = song.Length,
            Tempo = song.Tempo,
            Dances = [.. song.DanceRatings
                .OrderByDescending(r => r.Weight)
                .Select(r => new SharedIdReviewDance(r.DanceId, r.Weight))],
            Created = song.Created,
            Albums = [.. song.Albums.Select(a => SharedIdReviewAlbum.Build(a, shared))]
        };
}

/// <summary>
/// An album of a song and its song-level ids. Index is the AlbumDetails.Index a removal names,
/// and each id is spelled as AlbumDetails.GetExtendedPurchaseIds spells it ("S:…").
/// </summary>
public record SharedIdReviewAlbum(int Index, string Name, int? Track, IReadOnlyList<SharedIdReviewTrackId> Ids)
{
    public static SharedIdReviewAlbum Build(AlbumDetails album, ISet<string> shared) =>
        new(album.Index, album.Name, album.Track,
            [.. album.GetExtendedPurchaseIds(PurchaseType.Song)
                .Select(id => new SharedIdReviewTrackId(id, TrackLink(album, id), shared.Contains(id)))]);

    // The service's page for a track id on this album ("S:…" or "I:…"), to check by hand which
    // song an id belongs to. ISRCs have no page; the Spotify and iTunes ids beside one on the same
    // album are the tracks that reported it.
    internal static string TrackLink(AlbumDetails album, string id)
    {
        var service = MusicService.GetService(id[0]);
        return service == null || id.Length < 3 ? null
            : service.GetPurchaseLink(PurchaseType.Song,
                album.GetPurchaseIdentifier(service.Id, PurchaseType.Album), id[2..])?.Link;
    }
}

// Shared: one of the ids the cluster's songs share
public record SharedIdReviewTrackId(string Id, string Link, bool Shared);

/// <summary>POST /api/sharedids/apply: the choice made for each cluster on a page.</summary>
public class SharedIdApplyRequest
{
    public int Page { get; set; } = 1;
    public List<SharedIdChoiceRequest> Choices { get; set; } = [];
}

public class SharedIdChoiceRequest
{
    public SharedIdAction Action { get; set; }
    public List<int> CandidateIds { get; set; } = [];

    // Every song in the cluster and the group it's merged in (empty leaves it out)
    public List<SharedIdSongGroup> Songs { get; set; } = [];

    // The song each name (and the tempo) of a merged song comes from, or null for the proposal
    public Guid? TitleFrom { get; set; }
    public Guid? ArtistFrom { get; set; }
    public Guid? ArtistsFrom { get; set; }
    public Guid? TempoFrom { get; set; }

    // What the reviewer typed over the proposal's names and tempo, or null to leave each alone;
    // they win over the picks above when the songs are merged in one group
    public string Title { get; set; }
    public string Artist { get; set; }
    public List<string> Artists { get; set; }
    public decimal? Tempo { get; set; }

    public List<SharedIdRemoval> Removals { get; set; } = [];

    // Songs to delete, whatever the action
    public List<Guid> Deletions { get; set; } = [];

    public SharedIdChoice ToChoice()
    {
        var songs = (Songs ?? []).Where(s => s != null).ToList();
        return new SharedIdChoice(Action, [.. songs.Select(s => s.SongId)], CandidateIds ?? [],
            [.. songs.Select(s => s.Group ?? "")],
            new SharedIdNamePicks(TitleFrom, ArtistFrom, ArtistsFrom,
                Title == null && Artist == null && Artists == null && Tempo == null ? null
                    : new SongNames(Title, Artist, Artists, Tempo),
                TempoFrom),
            [.. (Removals ?? []).Where(r => r != null)],
            [.. Deletions ?? []]);
    }
}

public class SharedIdSongGroup
{
    public Guid SongId { get; set; }
    public string Group { get; set; }
}

/// <summary>What an Apply did: the SharedIdBatchResult counts and its summary.</summary>
public record SharedIdApplyResponse(string Message, int Merged, int Rejected, int Left, int Edited,
    int UnsharedPairs, int Stale, int Deleted)
{
    public static SharedIdApplyResponse Build(SharedIdBatchResult result) =>
        new(result.Summary(), result.Merged, result.Rejected, result.Left, result.Edited,
            result.UnsharedPairs, result.Stale, result.Deleted);
}

/// <summary>POST /api/sharedids/owners: a cluster's songs.</summary>
public class SharedIdOwnersRequest
{
    // A bound on the songs, and so on the lookups, one request makes
    public const int MaxSongs = 20;

    public List<Guid> SongIds { get; set; } = [];
}

/// <summary>
/// What POST /api/sharedids/owners proposes for a cluster (SharedIdOwnership): each shared id's
/// owner or why there's none, the removals that carry it out, and the tracks looked up, as
/// /api/sharedids/tracks gives them. Empty when the songs changed so that fewer than two are left.
/// </summary>
public record SharedIdOwnersResponse(IReadOnlyList<SharedIdOwnerView> Ids, IReadOnlyList<SharedIdRemoval> Removals,
    IReadOnlyList<SharedIdServiceTrack> Tracks)
{
    public static SharedIdOwnersResponse Build(SharedIdOwnership ownership) =>
        ownership == null ? new SharedIdOwnersResponse([], [], [])
            : new SharedIdOwnersResponse(
                [.. ownership.Ids.Select(p => new SharedIdOwnerView(p.Id, p.Status, p.Reason, p.Song, p.Via))],
                ownership.Removals,
                [.. ownership.Tracks.Select(t => SharedIdServiceTrack.From(t.Key, t.Value))]);
}

// A SharedIdOwnerProposal, with its status spelled out
public record SharedIdOwnerView(string Id,
    [property: JsonConverter(typeof(StringEnumConverter))] SharedIdOwnerStatus Status,
    string Reason, Guid? Song, string Via);

/// <summary>POST /api/sharedids/tracks: extended ids ("S:…", "I:…") to look up.</summary>
public class SharedIdTracksRequest
{
    public List<string> Ids { get; set; } = [];
}

/// <summary>
/// What a music service says one of a cluster's ids is, for telling apart songs that share an id.
/// Found is false when the service didn't return the track.
/// </summary>
public record SharedIdServiceTrack(string Id, bool Found, string Title = null, string Artist = null,
    string Album = null, int? Length = null, string Isrc = null, string ReleaseDate = null)
{
    // The services with a track lookup
    public static readonly ServiceType[] Services = [ServiceType.Spotify, ServiceType.ITunes];

    // Enough for any cluster, and a bound on the calls one request makes
    public const int MaxIds = 50;

    /// <summary>
    /// Looks up each Spotify and iTunes id, once each, in order; other ids are skipped. The
    /// lookups run one at a time: MusicServiceManager's track cache isn't thread safe.
    /// </summary>
    public static async Task<IReadOnlyList<SharedIdServiceTrack>> Lookup(IEnumerable<string> ids,
        Func<string, MusicService, Task<ServiceTrack>> fetch)
    {
        var tracks = new List<SharedIdServiceTrack>();
        foreach (var id in (ids ?? []).Where(Lookable).Distinct().Take(MaxIds))
        {
            ServiceTrack track = null;
            try
            {
                track = await fetch(id[2..], MusicService.GetService(id[0]));
            }
            catch (Exception)
            {
                // Reported as not found
            }

            tracks.Add(From(id, track));
        }

        return tracks;
    }

    public static SharedIdServiceTrack From(string id, ServiceTrack track) =>
        track == null ? new SharedIdServiceTrack(id, false)
            : new SharedIdServiceTrack(id, true, track.Name,
                track.Artists is { Length: > 1 } ? string.Join(", ", track.Artists) : track.Artist, track.Album,
                track.Duration, track.ISRC, track.ReleaseDate);

    private static bool Lookable(string id) =>
        id is { Length: > 2 } && id[1] == ':' && MusicService.GetService(id[0]) is { } service &&
        Services.Contains(service.Id);
}
