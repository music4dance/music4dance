using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace m4dModels.Tests
{
    class MockFactories : IFactories
    {
        public SongProperty CreateSongProperty(Song song, string name, object value, SongLog log)
        {
            var ret = new SongProperty()
            {
                Id = s_nextId++,
                SongId = song.SongId,
                Song = song,
                Name = name,
                Value = SongProperty.SerializeValue(value)
            };

            if (song.SongProperties == null)
            {
                song.SongProperties = new List<SongProperty>();
            }

            song.SongProperties.Add(ret);

            return ret;
        }

        public DanceRating CreateDanceRating(Song song, string danceId, int weight)
        {
            DanceRating dr = new DanceRating() { DanceId = danceId, Weight = weight };

            song.AddDanceRating(dr);

            return dr;
        }

        private static Int64 s_nextId=1;
    }
}
