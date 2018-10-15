using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
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
        public string Name { get; set; }
        public string Description { get; set; }
        public string Data1 { get; set; }
        public string Data2 { get; set; }
        public DateTime Created { get; set; }
        public DateTime? Updated { get; set; }
        public bool Deleted { get; set; }
    }

    [NotMapped]
    public class SongsFromSpotify : PlayList {
        public string Tags {
            get => Data1;
            set => Data1 = value;
        }

        public string SongIds
        {
            get => Data2;
            set => Data2 = value;
        }
        public IEnumerable<string> SongIdList => string.IsNullOrEmpty(SongIds)
            ? null
            : SongIds.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);

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

    public class GenericPlaylist
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public IList<ServiceTrack> Tracks { get; set; }

    }
}