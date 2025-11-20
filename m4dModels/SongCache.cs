using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace m4dModels
{
    public class SongCache
    {
        private readonly Dictionary<Guid, Song> _queuedSongs = [];
        private readonly Dictionary<Guid, Song> _songs = [];

        public async Task LoadSongs(IEnumerable<string> songs, DanceMusicCoreService dms)
        {
            var loaded = await dms.SongIndex.CreateSongs(songs);
            AddSongs(loaded);
        }

        public void AddSongs(IEnumerable<Song> songs)
        {
            foreach (var s in songs)
            {
                _songs[s.SongId] = s;
            }
        }

        public void UpdateSong(Song song)
        {
            lock (_queuedSongs)
            {
                _queuedSongs[song.SongId] = song;


                if (song.IsNull)
                {
                    _songs.Remove(song.SongId);
                }
                else
                {
                    _songs[song.SongId] = song;
                }
            }
        }

        public IEnumerable<Song> DequeueSongs()
        {
            lock (_queuedSongs)
            {
                var ret = _queuedSongs.Values.ToList();
                _queuedSongs.Clear();
                return ret;
            }
        }

        public Song FindSongDetails(Guid songId)
        {
            return _songs.GetValueOrDefault(songId);
        }

        public List<string> Serialize()
        {
            return [.. _songs.Select(s => s.Value.ToString())];
        }
    }
}
