namespace m4dModels;

// SongFilterNext is now merged into SongFilter.
// Retain as a stub for future breaking changes.
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
}

