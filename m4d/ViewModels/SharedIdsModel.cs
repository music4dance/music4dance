using m4dModels;

namespace m4d.ViewModels;

public class SharedIdsModel
{
    public const int PageSize = 20;

    public int PendingCandidates { get; init; }
    public int ClusterCount { get; init; }
    public int Page { get; init; }
    public IReadOnlyList<SharedIdCluster> Clusters { get; init; } = [];

    // Parallel to Clusters: the song merging all of a cluster's songs would make (null if none)
    public IReadOnlyList<Song> Previews { get; init; } = [];

    // Parallel to Clusters: the names the merge should land on, with feat. cleaned up (null if
    // there's no preview)
    public IReadOnlyList<SongNames> Proposals { get; init; } = [];

    // When the clusters are sorted by the last merge preview: when it ran, and how many of its
    // clusters differ only by feat. and how many are close matches (those come first)
    public DateTimeOffset? SortedBy { get; init; }
    public int WithoutFeaturingCount { get; init; }
    public int CloseCount { get; init; }

    public string Message { get; init; }

    public int PageCount => Math.Max(1, (ClusterCount + PageSize - 1) / PageSize);

    // A cluster with a rejected pair or a .NoMerge song needs a closer look, so it starts as Leave
    public static SharedIdAction DefaultAction(SharedIdCluster cluster) =>
        cluster.HasRejectedPair || cluster.HasNoMergeSong ? SharedIdAction.Leave : SharedIdAction.Merge;
}

// One cluster's row of the ApplySharedIds form
public class SharedIdChoiceForm
{
    public SharedIdAction Action { get; set; }
    public Guid[] SongIds { get; set; }
    public int[] CandidateIds { get; set; }

    // The title and artist chosen for the merged song, and the individual artists proposed for
    // it (ArtistSplitter.Serialize)
    public string Title { get; set; }
    public string Artist { get; set; }
    public string Artists { get; set; }

    public SharedIdChoice ToChoice() => new(Action, SongIds ?? [], CandidateIds ?? [],
        string.IsNullOrWhiteSpace(Title) && string.IsNullOrWhiteSpace(Artist) && string.IsNullOrWhiteSpace(Artists)
            ? null
            : new SongNames(Title, Artist, ArtistSplitter.Deserialize(Artists)));
}
