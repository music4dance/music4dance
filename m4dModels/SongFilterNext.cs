namespace m4dModels;

// SongFilterNext: v3 filter that uses per-dance Tempo sub-field when a single dance is selected.
public class SongFilterNext : SongFilter
{
    internal SongFilterNext(string filter = null) : base(filter)
    {
    }

    internal SongFilterNext(RawSearch rawSearch, string action) : base(rawSearch, action)
    {
    }

    public override SongFilter Clone()
    {
        return new SongFilterNext(ToString());
    }

    public override DanceQuery DanceQuery => new DanceQueryNext(IsRaw ? null : Dances);

    // Returns the single dance ID when IsSingleDance, otherwise null.
    private string SingleDanceId => IsSingleDance
        ? DanceQuery?.DanceIds.FirstOrDefault()
        : null;

    // Override ODataSort to use dance_{id}/Tempo when sorting by tempo on a single dance.
    public override IList<string> ODataSort
    {
        get
        {
            var sort = SongSort;
            var singleId = SingleDanceId;

            if (sort.Id == SongSort.Tempo && singleId != null)
            {
                var order = sort.Descending ? "desc" : "asc";
                return [$"dance_{singleId}/{SongIndexNext.DanceTempoSubField} {order}"];
            }

            return base.ODataSort;
        }
    }

    // Override GetOdataFilter to use dance_{id}/Tempo for tempo range when single dance.
    public override string GetOdataFilter(DanceMusicCoreService dms)
    {
        var singleId = SingleDanceId;

        if (singleId == null || (!TempoMin.HasValue && !TempoMax.HasValue))
        {
            return base.GetOdataFilter(dms);
        }

        var tempoFieldPath = $"dance_{singleId}/{SongIndexNext.DanceTempoSubField}";
        return BuildOdataFilter(dms, tempoFieldPath);
    }
}

