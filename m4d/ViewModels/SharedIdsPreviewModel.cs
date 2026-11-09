using m4dModels;

namespace m4d.ViewModels;

public class SharedIdsPreviewModel
{
    public DateTimeOffset Created { get; init; }
    public IReadOnlyList<SharedIdMergePlan> Plans { get; init; } = [];

    public IEnumerable<SharedIdMergePlan> Merges => Plans.Where(p => p.WillMerge);
    public IEnumerable<IGrouping<string, SharedIdMergePlan>> Skips =>
        Plans.Where(p => !p.WillMerge).GroupBy(p => p.SkipReason).OrderByDescending(g => g.Count());
}
