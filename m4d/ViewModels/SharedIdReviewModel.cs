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

    public string Message { get; init; }

    public IReadOnlyList<SharedIdReviewCluster> Clusters { get; init; } = [];

    public static SharedIdReviewModel Build(SharedIdPage page, DateTimeOffset? sortedBy = null,
        string message = null) =>
        new()
        {
            Page = page.Page,
            PageCount = page.PageCount,
            PendingCandidates = page.PendingCandidates,
            ClusterCount = page.ClusterCount,
            SortedBy = sortedBy,
            WithoutFeaturingCount = page.RankCounts.GetValueOrDefault(SharedIdPreviewStore.WithoutFeaturingRank),
            CloseCount = page.RankCounts.GetValueOrDefault(SharedIdPreviewStore.CloseRank),
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

    // The song each name of a merged song comes from, or null for the proposal
    public Guid? TitleFrom { get; set; }
    public Guid? ArtistFrom { get; set; }
    public Guid? ArtistsFrom { get; set; }

    public List<SharedIdRemoval> Removals { get; set; } = [];

    public SharedIdChoice ToChoice()
    {
        var songs = (Songs ?? []).Where(s => s != null).ToList();
        return new SharedIdChoice(Action, [.. songs.Select(s => s.SongId)], CandidateIds ?? [],
            [.. songs.Select(s => s.Group ?? "")],
            new SharedIdNamePicks(TitleFrom, ArtistFrom, ArtistsFrom),
            [.. (Removals ?? []).Where(r => r != null)]);
    }
}

public class SharedIdSongGroup
{
    public Guid SongId { get; set; }
    public string Group { get; set; }
}

/// <summary>What an Apply did: the SharedIdBatchResult counts and its summary.</summary>
public record SharedIdApplyResponse(string Message, int Merged, int Rejected, int Left, int Edited,
    int UnsharedPairs, int Stale)
{
    public static SharedIdApplyResponse Build(SharedIdBatchResult result) =>
        new(result.Summary(), result.Merged, result.Rejected, result.Left, result.Edited,
            result.UnsharedPairs, result.Stale);
}
