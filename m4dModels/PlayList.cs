using System;
using System.Collections.Generic;
using System.Linq;

namespace m4dModels
{
    public enum PlayListType
    {
        Undefined,
        Music4Dance,
        SongsFromSpotify,
        SpotifyFromSearch
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
        public string Name { get; set; }
        public string Description { get; set; }
        public IEnumerable<string> SongIdList => string.IsNullOrEmpty(SongIds)
            ? null
            : SongIds.Split(new[] {'|'}, StringSplitOptions.RemoveEmptyEntries);

        public bool AddSongs(IEnumerable<string> songIds)
        {
            var existing = string.IsNullOrEmpty(SongIds)
            ? new HashSet<string>()
            : new HashSet<string>(SongIdList);

            var initial = existing.Count;
            foreach (var id in songIds.Where(id => !existing.Contains(id)))
            {
                existing.Add(id);
            }

            if (initial == existing.Count) return false;

            SongIds = string.Join("|", existing);
            Updated = DateTime.Now;
            return true;
        }
    }
}