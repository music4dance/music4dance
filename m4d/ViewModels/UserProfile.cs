namespace m4d.ViewModels
{
    public class UserProfile
    {
        public string UserName { get; set; }
        public bool IsPublic { get; set; }
        public bool IsPseudo { get; set; }
        public string SpotifyId { get; set; }
        public int favoriteCount { get; set; }
        public int blockedCount { get; set; }
        public int editCount { get; set; }
    }
}
