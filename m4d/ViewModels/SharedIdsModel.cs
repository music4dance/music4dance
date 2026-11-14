using m4d.Services;

using m4dModels;

namespace m4d.ViewModels;

public class SharedIdsModel
{
    // The groups a cluster's songs can be merged in
    public static readonly string[] GroupNames = ["A", "B", "C", "D"];

    public SharedIdPage Page { get; init; } = new();

    // When the page is sorted by the last merge preview: when it ran. Of the clusters still
    // pending, those that differ only by feat. come first, then the close matches
    public DateTimeOffset? SortedBy { get; init; }

    public int WithoutFeaturingCount => Page.RankCounts.GetValueOrDefault(SharedIdPreviewStore.WithoutFeaturingRank);
    public int CloseCount => Page.RankCounts.GetValueOrDefault(SharedIdPreviewStore.CloseRank);

    public string Message { get; init; }
}

// One cluster's row of the Apply form
public class SharedIdChoiceForm
{
    public SharedIdAction Action { get; set; }

    // Every song in the cluster, and the group each is merged in (parallel; empty leaves it out)
    public Guid[] SongIds { get; set; }
    public string[] Groups { get; set; }
    public int[] CandidateIds { get; set; }

    // The song each name of a merged song comes from, or null (an empty value) for the If merged
    // column's proposal
    public Guid? Title { get; set; }
    public Guid? Artist { get; set; }
    public Guid? Artists { get; set; }

    // The albums and ids checked for removal, as SharedIdRemoval.Format writes them
    public string[] Removals { get; set; }

    public SharedIdChoice ToChoice() => new(Action, SongIds ?? [], CandidateIds ?? [],
        Groups, new SharedIdNamePicks(Title, Artist, Artists),
        [.. (Removals ?? []).Select(SharedIdRemoval.Parse).Where(r => r != null)]);
}
