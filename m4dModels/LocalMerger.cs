namespace m4dModels;

// Exact = Title, Artist, Album
// Length = Title, Artist, Length
// Weak = Database doesn't already have a 'real' album and tempo so use the new one
public enum MatchType
{
    None,
    Weak,
    Length,
    Exact
};

public class LocalMerger
{
    public Song Left { get; set; }
    public Song Right { get; set; }
    public bool Conflict { get; set; }
    public MatchType MatchType { get; set; }

    /// <summary>
    /// 1-based row number in the original upload file (1 = header, so data starts at 2).
    /// Zero if not set.
    /// </summary>
    public int OriginalRow { get; set; }
}
