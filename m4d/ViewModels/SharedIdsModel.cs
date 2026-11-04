using m4dModels;

namespace m4d.ViewModels;

public class SharedIdsModel
{
    public const int PageSize = 20;

    public int PendingCandidates { get; init; }
    public int ClusterCount { get; init; }
    public int Page { get; init; }
    public IReadOnlyList<SharedIdCluster> Clusters { get; init; } = [];
    public string Message { get; init; }

    public int PageCount => Math.Max(1, (ClusterCount + PageSize - 1) / PageSize);
}
