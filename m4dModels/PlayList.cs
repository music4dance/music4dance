using System;
using System.Collections.Generic;
using System.Linq;

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

        public bool AddSongs(IEnumerable<string> songIds)
        {
            var existing = string.IsNullOrEmpty(SongIds)
            ? new HashSet<string>()
            : new HashSet<string>(SongIds.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries));

            var initial = existing.Count;
            foreach (var id in songIds.Where(id => !existing.Contains(id)))
            {
                existing.Add(id);
            }

            return initial < existing.Count;
        }
    }
}