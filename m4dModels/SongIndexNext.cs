namespace m4dModels;

// SongIndexNext is retained as a placeholder for the next breaking change.
public class SongIndexNext : SongIndex
{
    public SongIndexNext()
    {
    }

    internal SongIndexNext(DanceMusicCoreService dms, string id) : base(dms, id)
    {
    }

    public override bool IsNext => true;
}
