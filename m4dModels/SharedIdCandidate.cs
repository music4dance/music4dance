namespace m4dModels;

public enum SharedIdStatus
{
    Pending = 0,
    Merged = 1,
    Rejected = 2,

    // One of the songs was deleted without being merged
    Deleted = 3
}

/// <summary>
/// Two catalog songs that hold, or would have held, the same Spotify, iTunes or ISRC id, queued
/// for an admin to merge or to mark as different songs (see SharedIdLog). One row per pair of
/// songs, with SongA the lower of the two ids, so the same pair found again adds its ids to the
/// existing row.
/// </summary>
public class SharedIdCandidate
{
    public int Id { get; set; }

    public Guid SongA { get; set; }
    public Guid SongB { get; set; }

    // The shared ids as the ServiceIds index field spells them ("I:123,R:USRC17607839")
    public string ServiceIds { get; set; }

    // What found the pair: the seed scan, or the enrichment step that was about to attach the id
    public string Source { get; set; }

    // The service track that carried the id, for the reviewer
    public string Detail { get; set; }

    public DateTimeOffset Created { get; set; }

    public SharedIdStatus Status { get; set; }
    public string ResolvedBy { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
}
