using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace m4dModels.Tests
{
    class MockContext : IDanceMusicContext
    {
        public SongProperty CreateSongProperty(Song song, string name, object value, SongLog log)
        {
            return s_factories.CreateSongProperty(song, name, value, log);
        }

        public DanceRating CreateDanceRating(Song song, string danceId, int weight)
        {
            return s_factories.CreateDanceRating(song, danceId, weight);
        }

        public Tag CreateTag(Song song, string value, int count)
        {
            return s_factories.CreateTag(song, value, count);
        }

        public ApplicationUser FindUser(string name)
        {
            return s_users.FindUser(name);
        }

        public ModifiedRecord CreateMapping(Guid songId, string applicationId)
        {
            return s_users.CreateMapping(songId, applicationId);
        }

        static IFactories s_factories = new MockFactories();
        static IUserMap s_users = new MockUserMap();
    }
}
