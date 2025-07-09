namespace m4dModels;

internal class SongFilter2 : SongFilter
{
    internal SongFilter2(string filter = null) : base(filter)
    {
    }

    internal SongFilter2(RawSearch rawSearch, string action) : base(rawSearch, action)
    {
    }

    public override SongFilter Clone()
    {
        return new SongFilter2(ToString());
    }
}

