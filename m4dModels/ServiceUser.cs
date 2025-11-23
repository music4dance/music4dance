namespace m4dModels;

public class SimplePlaylist
{
    public string Id { get; set; }
    public string Name { get; set; }

    public string Description { get; set; }
    public int TrackCount { get; set; }
    public string Owner { get; set; }
    public string Music4danceId { get; set; }
}

public class ServiceUser
{
    public string Id { get; set; }
    public string Name { get; set; }
    public List<SimplePlaylist> Playlists { get; set; }
}
