using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace m4dModels.Tests
{
    class MockContext : IDanceMusicContext
    {
        class TestDanceSet : TestDbSet<Dance>
        {
            public override Dance Find(params object[] keyValues)
            {
                var id = keyValues.Single() as string;
                if (id == null)
                {
                    return null;
                }
                else
                {
                    return this.SingleOrDefault(d => d.Id == id);
                }
            }
        }

        class TagTypeSet : TestDbSet<TagType>
        {
            public override TagType Find(params object[] keyValues)
            {
                var id = keyValues.Single() as string;
                if (id == null)
                {
                    return null;
                }
                else
                {
                    return this.SingleOrDefault(t => t.Value == id);
                }
            }
        }

        public MockContext()
        {
            this.Songs = new TestDbSet<Song>();
            this.SongProperties = new TestDbSet<SongProperty>();
            this.Dances = new TestDanceSet();
            this.DanceRatings = new TestDbSet<DanceRating>();
            this.Tags = new TestDbSet<Tag>();
            this.TagTypes = new TagTypeSet();
            this.Log = new TestDbSet<SongLog>();
            this.Modified = new TestDbSet<ModifiedRecord>();

        }
        public DbSet<Song> Songs { get; set; }

        public DbSet<SongProperty> SongProperties { get; set; }

        public DbSet<Dance> Dances { get; set; }

        public DbSet<DanceRating> DanceRatings { get; set; }

        public DbSet<Tag> Tags { get; set; }

        public DbSet<TagType> TagTypes { get; set; }

        public DbSet<SongLog> Log { get; set; }

        public DbSet<ModifiedRecord> Modified { get; set; }

        public int SaveChangesCount { get; private set; } 

        public int SaveChanges()
        {
            this.SaveChangesCount++;
            return 1;
        }

        public ApplicationUser FindUser(string name)
        {
            return s_users.FindUser(name);
        }
        public ApplicationUser FindOrAddUser(string name, string role)
        {
            return s_users.FindOrAddUser(name, role);
        }
        public ModifiedRecord CreateMapping(Guid songId, string applicationId)
        {
            return s_users.CreateMapping(songId, applicationId);
        }

        public void Dispose()
        {

        }
        static IUserMap s_users = new MockUserMap();
    }
}
