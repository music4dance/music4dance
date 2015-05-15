using System;
using System.Data.Entity;
using System.Linq;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;
using m4dModels;

namespace m4dModels.Tests
{
    class MockContext : IDanceMusicContext
    {
        public static DanceMusicService CreateService(bool seedUsers)
        {
            var context = new MockContext(seedUsers);
            var umanager = new UserManager<ApplicationUser>(new MockUserStore(context));
            var service = new DanceMusicService(context, umanager);
            service.SeedDances();
            return service;
        }
        class DanceSet : TestDbSet<Dance>
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

        class SongSet : TestDbSet<Song>
        {
            public override Song Find(params object[] keyValues)
            {
                var id = keyValues.Single() as Guid?;
                return id == null ? null : this.SingleOrDefault(s => s.SongId == id);
            }
        }

        class ApplicationUserSet : TestDbSet<ApplicationUser>
        {
            public override ApplicationUser Find(params object[] keyValues)
            {
                var id = keyValues.Single() as string;
                if (id == null)
                {
                    return null;
                }
                else
                {
                    return this.SingleOrDefault(u => u.Id == id);
                }
            }
        }

        public MockContext(bool seedUsers=true)
        {
            Songs = new SongSet();
            SongProperties = new TestDbSet<SongProperty>();
            Dances = new DanceSet();
            DanceRatings = new TestDbSet<DanceRating>();
            Tags = new TestDbSet<Tag>();
            TagTypes = new TagTypeSet();
            Log = new TestDbSet<SongLog>();
            Modified = new TestDbSet<ModifiedRecord>();
            Users = new ApplicationUserSet();
            Roles = new TestDbSet<IdentityRole>();

            if (seedUsers)
            {
                Users.Add(new ApplicationUser() {UserName="dwgray", Id="05849D25-0292-44CF-A3E6-74D07D94855C"});
                Users.Add(new ApplicationUser() {UserName="batch", Id="DE3752CA-42CD-46FB-BEE9-F7163CFB091B"});
            }
        }

        #region Events
        //protected override void OnModelCreating(System.Data.Entity.DbModelBuilder modelBuilder)
        //{
        //    DanceMusicContextHelpers.ModelCreating(modelBuilder);
        //}
        #endregion

        public DbSet<Song> Songs { get; set; }

        public DbSet<SongProperty> SongProperties { get; set; }

        public DbSet<Dance> Dances { get; set; }

        public DbSet<DanceRating> DanceRatings { get; set; }

        public DbSet<Tag> Tags { get; set; }

        public DbSet<TagType> TagTypes { get; set; }

        public DbSet<SongLog> Log { get; set; }

        public DbSet<ModifiedRecord> Modified { get; set; }

        public IDbSet<ApplicationUser> Users { get; set; }

        public IDbSet<IdentityRole> Roles { get; set; }

        public int SaveChangesCount { get; private set; } 

        public int SaveChanges()
        {
            SaveChangesCount++;
            return 1;
        }
        public void TrackChanges(bool track)
        {
            // NOOP?
        }

        public void CheckpointSongs() { }

        public void LoadDances() { }

        public void Dispose()
        {
        }
    }
}
