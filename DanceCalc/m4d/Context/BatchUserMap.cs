using m4dModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace m4d.Context
{
    public class BatchUserMap : IDanceMusicContext
    {
        private BatchUserMap()
        {

        }
        public BatchUserMap(DanceMusicContext dmc)
        {
            _db = dmc;
        }

        public ApplicationUser FindUser(string name)
        {
            return _db.FindUser(name);
        }

        public ModifiedRecord CreateMapping(Guid songId, string applicationId)
        {
            string key = songId.ToString() + applicationId;

            ModifiedRecord r = null;
            if (!_map.TryGetValue(key,out r))
            {
                r = new ModifiedRecord() { SongId = songId, ApplicationUserId = applicationId };
                _map[key] = r;
            }

            return r;
        }

        public IEnumerable<ModifiedRecord> GetMappings()
        {
            return _map.Values;
        }

        private DanceMusicContext _db;
        private Dictionary<string, ModifiedRecord> _map = new Dictionary<string,ModifiedRecord>();

        public SongProperty CreateSongProperty(Song song, string name, object value, SongLog log)
        {
            return _db.CreateSongProperty(song, name, value, log);
        }

        public DanceRating CreateDanceRating(Song song, string danceId, int weight)
        {
            return _db.CreateDanceRating(song, danceId, weight);
        }

        public Tag CreateTag(Song song, string value, int count)
        {
            return _db.CreateTag(song, value, count);
        }
    }

}