namespace m4d.ViewModels;

public class UserProfile
{
    public string UserName { get; set; }
    public bool IsPublic { get; set; }
    public bool IsPseudo { get; set; }
    public string SpotifyId { get; set; }
    public int FavoriteCount { get; set; }
    public int BlockedCount { get; set; }
    public int EditCount { get; set; }
}
