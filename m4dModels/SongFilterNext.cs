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

        // Let the base build the full filter, then if we have a single dance and tempo
        // constraints, the base will have added top-level Tempo clauses which we must replace
        // with dance-specific ones. Build our own instead.
        if (singleId == null || (!TempoMin.HasValue && !TempoMax.HasValue))
        {
            return base.GetOdataFilter(dms);
        }

        // Build the filter manually, replacing top-level Tempo clauses with per-dance ones.
        var tempoFieldPath = $"dance_{singleId}/{SongIndexNext.DanceTempoSubField}";
        var sort = SongSort;

        // Use per-dance tempo field in the numeric-sort null-exclusion prefilter when applicable.
        var sortField = sort.Id == SongSort.Tempo ? tempoFieldPath : sort.Id;

        string odata = sort.Numeric
            ? $"({sortField} ne null) and ({sortField} ne 0)"
            : null;

        odata = CombineOdataFilter(odata, GetDanceOdataFilter(dms));
        odata = CombineOdataFilter(odata, UserQuery.ODataFilter);

        if (TempoMin.HasValue)
        {
            var tempoMin = TempoMin.Value % 1M < (decimal).0001 ? TempoMin - .5M : TempoMin;
            odata = (odata == null ? "" : odata + " and ") + $"({tempoFieldPath} ge {tempoMin})";
        }

        if (TempoMax.HasValue)
        {
            var tempoMax = TempoMax.Value % 1M < (decimal).0001 ? TempoMax + .5M : TempoMax;
            odata = (odata == null ? "" : odata + " and ") + $"({tempoFieldPath} le {tempoMax})";
        }

        if (LengthMin.HasValue)
        {
            odata = (odata == null ? "" : odata + " and ") + $"(Length ge {LengthMin})";
        }

        if (LengthMax.HasValue)
        {
            odata = (odata == null ? "" : odata + " and ") + $"(Length le {LengthMax})";
        }

        odata = CombineOdataFilter(odata, ODataPurchase);
        odata = CombineOdataFilter(odata, TagQuery.GetODataFilter(dms));
        odata = CombineOdataFilter(odata, GetCommentsFilter());

        return odata;
    }

    private static string CombineOdataFilter(string existing, string newData)
    {
        if (newData == null) return existing;
        return (existing == null ? "" : existing + " and ") + newData;
    }

    private string GetDanceOdataFilter(DanceMusicCoreService dms)
    {
        return IsRaw
            ? RawDanceQuery?.GetODataFilter(dms)
            : DanceQuery?.GetODataFilter(dms);
    }
}

