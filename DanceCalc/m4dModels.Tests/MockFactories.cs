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

        public Tag CreateTag(Song song, string value)
        {
            TagType type = _tagTypes.FirstOrDefault(t => string.Equals(t.Value, value, StringComparison.OrdinalIgnoreCase));
            if (type == null)
            {
                type = new TagType() { Value = value };
                _tagTypes.Add(type);
            }

            return new Tag { Song = song, Value = value, Type = type };
        }

        private static List<TagType> _tagTypes = new List<TagType>() 
        {
            new TagType() {Value="Rock",Categories="Genre"},
            new TagType() {Value="Blues",Categories="Genre"},
            new TagType() {Value="Pop",Categories="Genre"},
            new TagType() {Value="Swing",Categories="Dance|Genre"},
            new TagType() {Value="Foxtrot",Categories="Dance"},
            new TagType() {Value="Waltz",Categories="Dance"},
            new TagType() {Value="Rumba",Categories="Dance"},
            new TagType() {Value="Latin",Categories="Dance|Genre"},
        };

    }
}
