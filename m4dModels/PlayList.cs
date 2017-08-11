using System;

namespace m4dModels
{
    public enum PlayListType
    {
        Undefined,
        Music4Dance,
        Spotify
    }
    public class PlayList
    {
        public string User { get; set; }
        public PlayListType Type { get; set; }
        public string Id { get; set; }
        public string Tags { get; set; }
        public DateTime Created { get; set; }
        public DateTime? Updated { get; set; }
        public bool Deleted { get; set; }
        public string SongIds { get; set; }
    }
}