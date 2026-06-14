namespace m4dModels;

// SongFilterNext is retained as a placeholder for the next breaking change.
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
}

